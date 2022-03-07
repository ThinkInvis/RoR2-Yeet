using RoR2;
using BepInEx;
using R2API.Utils;
using UnityEngine;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

namespace ThinkInvisible.Yeet {
    
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin(ModGuid, ModName, ModVer)]
    [BepInDependency("com.ThinkInvisible.TILER2", BepInDependency.DependencyFlags.SoftDependency)]
    [R2APISubmoduleDependency(nameof(CommandHelper))]
    public class YeetPlugin:BaseUnityPlugin {
        public const string ModVer = "1.2.2";
        public const string ModName = "Yeet";
        public const string ModGuid = "com.ThinkInvisible.Yeet";

        //static float regrabCooldown = 5f;
        static float lowThrowForce = 20f;
        static float highThrowForce = 100f;
        static float highThrowTime = 2f;
        static bool preventLunar = true;
        static bool preventEquipment = false;
        static bool preventItems = false;
        static bool commandExtraCheesyMode = false;

        internal static ManualLogSource _logger;
        
        public void Awake() {
            _logger = this.Logger;
            ConfigFile cfgFile = new ConfigFile(Paths.ConfigPath + "\\" + ModGuid + ".cfg", true);

            /*var cfgRegrabCooldown = cfgFile.Bind(new ConfigDefinition("Yeet", "RegrabCooldown"), 5f, new ConfigDescription(
                "Time (in seconds) to prevent picking an item back up after dropping it. Does not apply to other players, only the dropper. Set to <= 0 to disable.",
                new AcceptableValueRange<float>(0f,float.MaxValue)));*/

            var cfgLowThrowForce = cfgFile.Bind(new ConfigDefinition("YeetServer", "LowThrowForce"), 30f, new ConfigDescription(
                "Minimum speed, in player view direction, to add to droplets for dropped items.",
                new AcceptableValueRange<float>(0f,float.MaxValue)));
            var cfgHighThrowForce = cfgFile.Bind(new ConfigDefinition("YeetServer", "HighThrowForce"), 150f, new ConfigDescription(
                "Maximum speed, in player view direction, to add to droplets for dropped items.",
                new AcceptableValueRange<float>(0f,float.MaxValue)));
            var cfgHighThrowTime = cfgFile.Bind(new ConfigDefinition("YeetClient", "HoldTime"), 2f, new ConfigDescription(
                "Click hold time (sec) required to reach HighThrowForce.",
                new AcceptableValueRange<float>(0f,float.MaxValue)));

            var cfgPreventLunar = cfgFile.Bind(new ConfigDefinition("YeetServer", "PreventLunar"), true, new ConfigDescription(
                "If true, lunar items cannot be dropped (to preserve the consequences of picking one up)."));
            var cfgPreventEquipment = cfgFile.Bind(new ConfigDefinition("YeetServer", "PreventEquipment"), false, new ConfigDescription(
                "If true, equipment items cannot be dropped."));
            var cfgPreventItems = cfgFile.Bind(new ConfigDefinition("YeetServer", "PreventItems"), false, new ConfigDescription(
                "If true, all non-equipment items cannot be dropped."));

            var cfgCommandExtraCheesyMode = cfgFile.Bind(new ConfigDefinition("YeetServer", "CommandExtraCheesyMode"), false, new ConfigDescription(
                "If true, dropped items will drop as Command pickers while Artifact of Command is enabled."));

            //regrabCooldown = cfgRegrabCooldown.Value;
            lowThrowForce = cfgLowThrowForce.Value;
            highThrowForce = cfgHighThrowForce.Value;
            highThrowTime = cfgHighThrowTime.Value;

            preventLunar = cfgPreventLunar.Value;
            preventEquipment = cfgPreventEquipment.Value;
            preventItems = cfgPreventItems.Value;
            commandExtraCheesyMode = cfgCommandExtraCheesyMode.Value;

            On.RoR2.UI.ItemIcon.Awake += ItemIcon_Awake;
            On.RoR2.UI.EquipmentIcon.Update += EquipmentIcon_Update;
            On.RoR2.PickupDropletController.OnCollisionEnter += PickupDropletController_OnCollisionEnter;
            
            CommandHelper.AddToConsoleWhenReady();
        }

        private void PickupDropletController_OnCollisionEnter(On.RoR2.PickupDropletController.orig_OnCollisionEnter orig, PickupDropletController self, Collision collision) {
            bool wasCmd = false;
            if(NetworkServer.active && !commandExtraCheesyMode && self.GetComponent<PickupDropletNoCommandFlag>()) {
                wasCmd = RunArtifactManager.enabledArtifactsEnumerable.Contains(RoR2Content.Artifacts.Command);
                if(wasCmd) RunArtifactManager.instance.SetArtifactEnabledServer(RoR2Content.Artifacts.Command, false);
            }
            orig(self, collision);
            if(NetworkServer.active && wasCmd)
                RunArtifactManager.instance.SetArtifactEnabledServer(RoR2Content.Artifacts.Command, true);
        }

        private void ItemIcon_Awake(On.RoR2.UI.ItemIcon.orig_Awake orig, RoR2.UI.ItemIcon self) {
            orig(self);
            self.gameObject.AddComponent<YeetButton>();
        }

        private void EquipmentIcon_Update(On.RoR2.UI.EquipmentIcon.orig_Update orig, RoR2.UI.EquipmentIcon self) {
            orig(self);
            if(self.gameObject.GetComponent<YeetButton>()) return;
            var btn = self.gameObject.AddComponent<YeetButton>();
            btn.isEquipment = true;
        }

        [ConCommand(commandName = "yeet", flags = ConVarFlags.ExecuteOnServer, helpText = "Requests the server to drop an item from your character. Argument 1: item index or partial name. Argument 2: if true, drop equipment instead. Argument 3: throw force.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by UnityEngine")]
        private static void ConCmdYeet(ConCommandArgs args) {
            if(!args.senderBody) {
                _logger.LogError("ConCmdYeet: called by nonexistent player!");
                return;
            }
            if(args.Count < 1) {
                _logger.LogError("ConCmdYeet: not enough arguments! Need at least 1 (item ID), received 0.");
                return;
            }

            bool isEquipment = args.TryGetArgBool(1) ?? false;

            if(isEquipment ? preventEquipment : preventItems) return;

            int rawInd;
            string itemSearch = args.TryGetArgString(0);
            if(itemSearch == null) {
                _logger.LogError("ConCmdYeet: could not read first argument (item ID)!");
                return;
            }
            else if(int.TryParse(itemSearch, out rawInd)) {
                if(isEquipment) {
                    if(!EquipmentCatalog.IsIndexValid((EquipmentIndex)rawInd)) {
                        _logger.LogError("ConCmdYeet: first argument (equipment ID as integer EquipmentIndex) is out of range; no equipment with that ID exists!");
                        return;
                    }
                } else {
                    if(!ItemCatalog.IsIndexValid((ItemIndex)rawInd)) {
                        _logger.LogError("ConCmdYeet: first argument (item ID as integer ItemIndex) is out of range; no item with that ID exists!");
                        return;
                    }
                }
            } else {
                if(isEquipment) {
                    var results = EquipmentCatalog.allEquipment.Where((searchInd)=>{
                        var iNameToken = EquipmentCatalog.GetEquipmentDef(searchInd).nameToken;
                        var iName = Language.GetString(iNameToken);
                        return iName.ToUpper().Contains(itemSearch.ToUpper());
                    });
                    if(results.Count() < 1) {
                        _logger.LogError("ConCmdYeet: first argument (equipment ID as string EquipmentName) not found in EquipmentCatalog; no equipment with a name containing that string exists!");
                        return;
                    } else {
                        if(results.Count() > 1)
                            _logger.LogWarning("ConCmdYeet: first argument (item ID as string EquipmentName) matched multiple equipments; using first.");
                        rawInd = (int)results.First();
                    }
                } else {
                    var results = ItemCatalog.allItems.Where((searchInd)=>{
                        var iNameToken = ItemCatalog.GetItemDef(searchInd).nameToken;
                        var iName = Language.GetString(iNameToken);
                        return iName.ToUpper().Contains(itemSearch.ToUpper());
                    });
                    if(results.Count() < 1) {
                        _logger.LogError("ConCmdYeet: first argument (item ID as string ItemName) not found in ItemCatalog; no item with a name containing that string exists!");
                        return;
                    } else {
                        if(results.Count() > 1)
                            _logger.LogWarning("ConCmdYeet: first argument (item ID as string ItemName) matched multiple items; using first.");
                        rawInd = (int)results.First();
                    }
                }
            }

            float throwForce = Mathf.Lerp(lowThrowForce, highThrowForce, Mathf.Clamp01(args.TryGetArgFloat(2) ?? 0f));

            if(isEquipment) {
                if(args.senderBody.inventory.GetEquipmentIndex() != (EquipmentIndex) rawInd) {
                    _logger.LogWarning("ConCmdYeet: someone's trying to drop an equipment they don't have");
                    return;
                }

                var edef = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)rawInd);
                args.senderBody.inventory.SetEquipmentIndex(EquipmentIndex.None);
                args.senderBody.inventory.RemoveItem((ItemIndex)rawInd);
            
                PickupDropletController.CreatePickupDroplet(
                    PickupCatalog.FindPickupIndex((EquipmentIndex)rawInd),
                    args.senderBody.inputBank.aimOrigin,
                    args.senderBody.inputBank.aimDirection * throwForce);
            } else {
                int count;
                if(Compat_TILER2.enabled)
                    count = Compat_TILER2.GetRealItemCount(args.senderBody.inventory, (ItemIndex)rawInd);
                else
                    count = args.senderBody.inventory.GetItemCount((ItemIndex)rawInd);
                if(count < 1) {
                    _logger.LogWarning("ConCmdYeet: someone's trying to drop an item they don't have any of");
                    return;
                }

                var idef = ItemCatalog.GetItemDef((ItemIndex)rawInd);
                if(idef.hidden || !idef.canRemove || (idef.tier == ItemTier.Lunar && preventLunar) || idef.tier == ItemTier.NoTier) return;
                args.senderBody.inventory.RemoveItem((ItemIndex)rawInd);

                var obj = GameObject.Instantiate(PickupDropletController.pickupDropletPrefab, args.senderBody.inputBank.aimOrigin, Quaternion.identity);
                if(!commandExtraCheesyMode) obj.AddComponent<PickupDropletNoCommandFlag>();
                obj.GetComponent<PickupDropletController>().NetworkpickupIndex = PickupCatalog.FindPickupIndex((ItemIndex)rawInd);
                var rbdy = obj.GetComponent<Rigidbody>();
                rbdy.velocity = args.senderBody.inputBank.aimDirection * throwForce;
                rbdy.AddTorque(Random.Range(150f, 120f) * Random.onUnitSphere);
                NetworkServer.Spawn(obj);
            }
        }
    }

    public class PickupDropletNoCommandFlag : MonoBehaviour {}
    
	public class YeetButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
        float holdTime = 0f;
        public bool isEquipment = false;
		void IPointerDownHandler.OnPointerDown(PointerEventData eventData) {
            holdTime = Time.unscaledTime;
		}
        void IPointerUpHandler.OnPointerUp(PointerEventData eventData) {
            float totalTime = Time.unscaledTime - holdTime;
            var ind = isEquipment
                ? ((int)GetComponent<RoR2.UI.EquipmentIcon>().targetInventory.GetEquipmentIndex()).ToString()
                : ((int)GetComponent<RoR2.UI.ItemIcon>().itemIndex).ToString();
			if(NetworkUser.readOnlyLocalPlayersList.Count > 0) {
                //RoR2.Console.instance.RunClientCmd(NetworkUser.readOnlyLocalPlayersList[0], "yeet", new string[]{((int)ind).ToString(), totalTime.ToString("N3")});
                RoR2.Console.instance.SubmitCmd(NetworkUser.readOnlyLocalPlayersList[0], $"yeet {ind} {(isEquipment ? 1 : 0)} {totalTime.ToString("N4")}");
            } else
                YeetPlugin._logger.LogError("Received inventory click event with no active local players!");
        }
    }
}