using RoR2;
using BepInEx;
using R2API.Utils;
using UnityEngine;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using R2API;
using UnityEngine.AddressableAssets;
using R2API.Networking.Interfaces;

namespace ThinkInvisible.Yeet {
    
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin(ModGuid, ModName, ModVer)]
    [BepInDependency("com.ThinkInvisible.TILER2", BepInDependency.DependencyFlags.SoftDependency)]
    [R2APISubmoduleDependency(nameof(CommandHelper), nameof(R2API.Networking.NetworkingAPI))]
    public class YeetPlugin:BaseUnityPlugin {
        public const string ModVer = "2.0.1";
        public const string ModName = "Yeet";
        public const string ModGuid = "com.ThinkInvisible.Yeet";

        static float lowThrowForce = 20f;
        static float highThrowForce = 100f;
        static float highThrowTime = 2f;
        static float yoinkCooldown = 5f;
        static float yeetCooldown = 10f;
        static bool preventLunar = true;
        static bool preventVoid = true;
        static bool preventEquipment = false;
        static bool preventItems = false;
        static bool preventRecycling = false;
        static bool commandExtraCheesyMode = false;

        internal static ManualLogSource _logger;
        private static GameObject yeetPickupPrefab;

        private static readonly RoR2.ConVar.BoolConVar allowYeet = new RoR2.ConVar.BoolConVar("yeet_on", ConVarFlags.SenderMustBeServer, "true", "If false, all mod functionality will be temporarily disabled.");

        public void Awake() {
            _logger = this.Logger;
            ConfigFile cfgFile = new ConfigFile(Paths.ConfigPath + "\\" + ModGuid + ".cfg", true);

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
            var cfgPreventVoid = cfgFile.Bind(new ConfigDefinition("YeetServer", "PreventVoid"), true, new ConfigDescription(
                "If true, void items cannot be dropped (to preserve the consequences of picking one up)."));
            var cfgPreventEquipment = cfgFile.Bind(new ConfigDefinition("YeetServer", "PreventEquipment"), false, new ConfigDescription(
                "If true, equipment items cannot be dropped."));
            var cfgPreventItems = cfgFile.Bind(new ConfigDefinition("YeetServer", "PreventItems"), false, new ConfigDescription(
                "If true, all non-equipment items cannot be dropped."));

            var cfgYeetCooldown = cfgFile.Bind(new ConfigDefinition("YeetServer", "YeetCooldown"), 10f, new ConfigDescription(
                "If greater than 0, time (sec) of cooldown after dropping one item before being able to drop another.",
                new AcceptableValueRange<float>(0f, float.MaxValue)));
            var cfgYoinkCooldown = cfgFile.Bind(new ConfigDefinition("YeetServer", "YoinkCooldown"), 5f, new ConfigDescription(
                "If greater than 0, time (sec) of cooldown after dropping an item before the dropper can pick it back up.",
                new AcceptableValueRange<float>(0f, float.MaxValue)));

            var cfgPreventRecycling = cfgFile.Bind(new ConfigDefinition("YeetServer", "PreventRecycling"), true, new ConfigDescription(
                "If true, dropped items will not work with the Recycler equipment."));
            var cfgCommandExtraCheesyMode = cfgFile.Bind(new ConfigDefinition("YeetServer", "CommandExtraCheesyMode"), false, new ConfigDescription(
                "If true, dropped items will drop as Command pickers while Artifact of Command is enabled."));

            lowThrowForce = cfgLowThrowForce.Value;
            highThrowForce = cfgHighThrowForce.Value;
            highThrowTime = cfgHighThrowTime.Value;
            yeetCooldown = cfgYeetCooldown.Value;
            yoinkCooldown = cfgYoinkCooldown.Value;
            preventLunar = cfgPreventLunar.Value;
            preventVoid = cfgPreventVoid.Value;
            preventEquipment = cfgPreventEquipment.Value;
            preventItems = cfgPreventItems.Value;
            preventRecycling = cfgPreventRecycling.Value;
            commandExtraCheesyMode = cfgCommandExtraCheesyMode.Value;

            On.RoR2.UI.ItemIcon.Awake += ItemIcon_Awake;
            On.RoR2.UI.EquipmentIcon.Update += EquipmentIcon_Update;
            On.RoR2.PickupDropletController.OnCollisionEnter += PickupDropletController_OnCollisionEnter;
            On.RoR2.GenericPickupController.CreatePickup += GenericPickupController_CreatePickup;
            On.RoR2.GenericPickupController.GetInteractability += GenericPickupController_GetInteractability;
            On.RoR2.GenericPickupController.OnTriggerStay += GenericPickupController_OnTriggerStay;

            CommandHelper.AddToConsoleWhenReady();

            //TODO: add TILER2 dep, netpreventmismatch on configs that need it

            var addrLoad = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/GenericPickup.prefab");
            addrLoad.Completed += (obj) => {
                var res = obj.Result;
                if(!res) {
                    _logger.LogError("Failed to load GenericPickup prefab to copy homework off of. The YoinkCooldown setting will not work (no extra cooldown will be added).");
                    return;
                }
                var yeetPickupPrefabPrefab = res.InstantiateClone("YeetPickupPrefabPrefab");
                yeetPickupPrefabPrefab.AddComponent<YeetData>();
                yeetPickupPrefab = yeetPickupPrefabPrefab.InstantiateClone("YeetPickupPrefab", true);
            };

            R2API.Networking.NetworkingAPI.RegisterCommandType<CmdRemoteChat>();
        }

        private struct CmdRemoteChat : INetCommand {
            private string _content;

            public void Serialize(NetworkWriter writer) {
                writer.Write(_content);
            }

            public void Deserialize(NetworkReader reader) {
                _content = reader.ReadString();
            }

            public void OnReceived() {
                Chat.AddMessage(_content);
            }

            public CmdRemoteChat(string content) {
                _content = content;
            }
        }


        private void GenericPickupController_OnTriggerStay(On.RoR2.GenericPickupController.orig_OnTriggerStay orig, GenericPickupController self, Collider other) {
            if(NetworkServer.active) {
                var cb = other.GetComponent<CharacterBody>();
                var yd = self.GetComponent<YeetData>();
                if(cb && yd && yd.yeeter == cb && yd.age < yoinkCooldown) {
                    return;
                }
            }
            orig(self, other);
        }

        private Interactability GenericPickupController_GetInteractability(On.RoR2.GenericPickupController.orig_GetInteractability orig, GenericPickupController self, Interactor activator) {
            var retv = orig(self, activator);
            var yd = self.GetComponent<YeetData>();
            var actBody = activator.GetComponent<CharacterBody>();
            if(yd && actBody && yd.yeeter == actBody && yd.age < yoinkCooldown) {
                return Interactability.Disabled;
            }
            return retv;
        }

        private void PickupDropletController_OnCollisionEnter(On.RoR2.PickupDropletController.orig_OnCollisionEnter orig, PickupDropletController self, Collision collision) {
            if(!NetworkServer.active || !self.alive) {
                orig(self, collision);
                return;
            }
            bool wasCmd = false;
            var yd = self.GetComponent<YeetData>();
            if(yd) {
                if(!commandExtraCheesyMode)
                    wasCmd = RunArtifactManager.enabledArtifactsEnumerable.Contains(RoR2Content.Artifacts.Command);
                if(wasCmd) RunArtifactManager.instance.SetArtifactEnabledServer(RoR2Content.Artifacts.Command, false);

                if(!yeetPickupPrefab) {
                    orig(self, collision);
                } else {
                    //GenericPickupController.CreatePickup only allows in a struct which makes it very hard to pass in any extra information *fine I'll do it myself*
                    self.alive = false;
                    self.createPickupInfo.position = self.transform.position;

                    bool success = true;
                    //not raised in vanilla because command artifact is the only thing that uses it, but just in case
                    var multicast = (System.MulticastDelegate)typeof(PickupDropletController).GetFieldCached(nameof(PickupDropletController.onDropletHitGroundServer)).GetValue(null);
                    if(multicast != null) {
                        foreach(var del in multicast.GetInvocationList()) {
                            var args = new object[] { self.createPickupInfo, success };
                            del.Method.Invoke(del.Target, args);
                            self.createPickupInfo = (GenericPickupController.CreatePickupInfo)args[0];
                            success = (bool)args[1];
                        }
                    }
                    if(success) {
                        var newPickup = Instantiate(yeetPickupPrefab, self.createPickupInfo.position, self.createPickupInfo.rotation);
                        var pickupController = newPickup.GetComponent<GenericPickupController>();
                        if(pickupController) {
                            pickupController.NetworkpickupIndex = self.createPickupInfo.pickupIndex;
                            if(preventRecycling)
                                pickupController.NetworkRecycled = true;
                        }
                        var pickupIndexNetworker = newPickup.GetComponent<PickupIndexNetworker>();
                        if(pickupIndexNetworker)
                            pickupIndexNetworker.NetworkpickupIndex = self.createPickupInfo.pickupIndex;
                        //no options, should only ever yeet one item
                        var pickupYeetData = newPickup.GetComponent<YeetData>();
                        pickupYeetData.age = yd.age;
                        pickupYeetData.yeeter = yd.yeeter;

                        NetworkServer.Spawn(newPickup);
                    }

                    Destroy(self.gameObject);
                }

                if(wasCmd)
                    RunArtifactManager.instance.SetArtifactEnabledServer(RoR2Content.Artifacts.Command, true);
            }
            else orig(self, collision);
        }

        private GenericPickupController GenericPickupController_CreatePickup(On.RoR2.GenericPickupController.orig_CreatePickup orig, ref GenericPickupController.CreatePickupInfo createPickupInfo) {
            return orig(ref createPickupInfo);
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
            if(!allowYeet.value) {
                if(args.sender)
                    new CmdRemoteChat("Yeet mod has been temporarily disabled by the server host.").Send(args.sender.connectionToClient);
                return;
            }
            if(!args.senderBody) {
                _logger.LogError("ConCmdYeet: called by nonexistent player!");
                return;
            }
            if(args.Count < 1) {
                _logger.LogError("ConCmdYeet: not enough arguments! Need at least 1 (item ID), received 0.");
                return;
            }

            var yd = args.senderBody.GetComponent<YeetData>();
            if(!yd) {
                yd = args.senderBody.gameObject.AddComponent<YeetData>();
                yd.age = yeetCooldown;
            }

            if(yd.age < yeetCooldown) {
                var cdRemaining = yeetCooldown - yd.age;
                new CmdRemoteChat($"You must wait {cdRemaining:0} second{((cdRemaining < 2) ? "" : "s")} before yeeting another item.").Send(args.sender.connectionToClient);
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

            PickupIndex pickup;
            if(isEquipment) {
                if(args.senderBody.inventory.GetEquipmentIndex() != (EquipmentIndex)rawInd) {
                    _logger.LogWarning("ConCmdYeet: someone's trying to drop an equipment they don't have");
                    return;
                }

                var edef = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)rawInd);
                args.senderBody.inventory.SetEquipmentIndex(EquipmentIndex.None);

                pickup = PickupCatalog.FindPickupIndex((EquipmentIndex)rawInd);
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
                if(idef.hidden || !idef.canRemove || idef.tier == ItemTier.NoTier
                    || (idef.tier == ItemTier.Lunar && preventLunar)
                    || (preventVoid && (idef.tier == ItemTier.VoidTier1 || idef.tier == ItemTier.VoidTier2 || idef.tier == ItemTier.VoidTier3)))
                    return;
                args.senderBody.inventory.RemoveItem((ItemIndex)rawInd);
                pickup = PickupCatalog.FindPickupIndex((ItemIndex)rawInd);
            }

            var obj = GameObject.Instantiate(PickupDropletController.pickupDropletPrefab, args.senderBody.inputBank.aimOrigin, Quaternion.identity);
            var pdyd = obj.AddComponent<YeetData>();
            pdyd.yeeter = args.senderBody;
            var pdcComponent = obj.GetComponent<PickupDropletController>();
            if(pdcComponent) {
                pdcComponent.NetworkpickupIndex = pickup;
                pdcComponent.createPickupInfo = new GenericPickupController.CreatePickupInfo {
                    rotation = Quaternion.identity,
                    pickupIndex = pickup
                };
            }
                
            var rbdy = obj.GetComponent<Rigidbody>();
            rbdy.velocity = args.senderBody.inputBank.aimDirection * throwForce;
            rbdy.AddTorque(Random.Range(150f, 120f) * Random.onUnitSphere);
            NetworkServer.Spawn(obj);

            yd.age = 0;
        }
    }

    public class YeetData : MonoBehaviour {
        public CharacterBody yeeter;
        public float age = 0f;

        void FixedUpdate() {
            age += Time.fixedDeltaTime;
        }
    }
    
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