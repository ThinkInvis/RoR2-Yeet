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
using TILER2;
using Path = System.IO.Path;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;

namespace ThinkInvisible.Yeet {
    [BepInPlugin(ModGuid, ModName, ModVer)]
    [BepInDependency(R2API.R2API.PluginGUID, R2API.R2API.PluginVersion)]
    [BepInDependency(TILER2Plugin.ModGuid, TILER2Plugin.ModVer)]
    [R2APISubmoduleDependency(nameof(CommandHelper), nameof(R2API.Networking.NetworkingAPI))]
    public class YeetPlugin:BaseUnityPlugin {
        public const string ModVer = "2.2.0";
        public const string ModName = "Yeet";
        public const string ModGuid = "com.ThinkInvisible.Yeet";

        public class ServerConfig : AutoConfigContainer {
            [AutoConfig("If true, all equipment cannot be dropped.")]
            [AutoConfigRoOCheckbox()]
            public bool preventEquipment { get; private set; } = false;

            [AutoConfig("If true, Lunar equipment cannot be dropped.")]
            [AutoConfigRoOCheckbox()]
            public bool preventLunarEquipment { get; private set; } = false;

            [AutoConfig("If true, non-Lunar equipment cannot be dropped.")]
            [AutoConfigRoOCheckbox()]
            public bool preventNonLunarEquipment { get; private set; } = false;

            [AutoConfig("If true, all items (except equipment) cannot be dropped.")]
            [AutoConfigRoOCheckbox()]
            public bool preventItems { get; private set; } = false;

            [AutoConfig("If true, hidden items cannot be dropped (highly recommended to leave enabled!).")]
            public bool preventHidden { get; private set; } = true;

            [AutoConfig("If true, items flagged as non-removable cannot be dropped (highly recommended to leave enabled!).")]
            public bool preventCantRemove { get; private set; } = true;

            [AutoConfig("Enter ItemTier names to prevent them from being dropped. Comma-delimited, whitespace is trimmed. Only works on vanilla tiers for now.")]
            public string blacklistTier { get; private set; } = "NoTier, Lunar, VoidTier1, VoidTier2, VoidTier3";

            [AutoConfig("Enter item/equipment name tokens (found in game language files) to prevent them from being dropped. Comma-delimited, whitespace is trimmed.")]
            public string blacklistItem { get; private set; } = "";

            [AutoConfig("If true, dropped items will not work with the Recycler equipment.")]
            [AutoConfigRoOCheckbox()]
            public bool preventRecycling { get; private set; } = false;

            [AutoConfig("If true, dropped items will drop as Command pickers while Artifact of Command is enabled.")]
            [AutoConfigRoOCheckbox()]
            public bool commandExtraCheesyMode { get; private set; } = false;

            [AutoConfig("Minimum speed, in player view direction, to add to droplets for dropped items.",
                AutoConfigFlags.None, 0f, float.MaxValue)]
            [AutoConfigRoOSlider("{0:N1} m/s", 0f, 500f)]
            public float lowThrowForce { get; private set; } = 30f;

            [AutoConfig("Maximum speed, in player view direction, to add to droplets for dropped items.",
                AutoConfigFlags.None, 0f, float.MaxValue)]
            [AutoConfigRoOSlider("{0:N1} m/s", 0f, 500f)]
            public float highThrowForce { get; private set; } = 150f;

            [AutoConfig("Maximum of one type of item to allow dropping at once.",
                AutoConfigFlags.None, 1, int.MaxValue)]
            [AutoConfigRoOSlider("{0:N0}", 1, 100)]
            public int maxThrowCount { get; private set; } = 1;

            [AutoConfig("If greater than 0, time (sec) of cooldown after dropping an item before the dropper can pick it back up.",
                AutoConfigFlags.None, 0f, float.MaxValue)]
            [AutoConfigRoOSlider("{0:N1} s", 0f, 300f)]
            public float yoinkCooldown { get; private set; } = 5f;

            [AutoConfig("If greater than 0, time (sec) of cooldown after dropping one item before being able to drop another.",
                AutoConfigFlags.None, 0f, float.MaxValue)]
            [AutoConfigRoOSlider("{0:N1} s", 0f, 300f)]
            public float yeetCooldown { get; private set; } = 10f;
        }

        public class ClientConfig : AutoConfigContainer {
            [AutoConfig("Click hold time (sec) required to reach HighThrowForce.",
            AutoConfigFlags.None, 0f, float.MaxValue)]
            [AutoConfigRoOSlider("{0:N1} s", 0f, 10f)]
            public float highThrowTime { get; private set; } = 2f;

            [AutoConfig("Items to attempt to drop with a left click. May be limited by server. Negative values are converted to a percentage.",
            AutoConfigFlags.None, -100, int.MaxValue)]
            [AutoConfigRoOSlider("{0:N0}", -100, 100)]
            public int primaryQuantity { get; private set; } = 1;

            [AutoConfig("Items to attempt to drop with a right click. May be limited by server. Negative values are converted to a percentage.",
            AutoConfigFlags.None, -100, int.MaxValue)]
            [AutoConfigRoOSlider("{0:N0}", -100, 100)]
            public int secondaryQuantity { get; private set; } = 1;
        }

        public static readonly ServerConfig serverConfig = new ServerConfig();
        public static readonly ClientConfig clientConfig = new ClientConfig();

        internal static ManualLogSource _logger;
        private static GameObject yeetPickupPrefab;
        private static readonly HashSet<string> _blacklistTier = new HashSet<string>();
        private static readonly HashSet<string> _blacklistItem = new HashSet<string>();

        private static readonly RoR2.ConVar.BoolConVar allowYeet = new RoR2.ConVar.BoolConVar("yeet_on", ConVarFlags.SenderMustBeServer, "1", "Boolean (0/1). If 0, all mod functionality will be temporarily disabled.");

        public void Awake() {
            _logger = this.Logger;
            ConfigFile cfgFile = new ConfigFile(Path.Combine(Paths.ConfigPath, ModGuid + ".cfg"), true);

            serverConfig.ConfigEntryChanged += (sender, args) => {
                if(args.target.boundProperty.Name == nameof(serverConfig.blacklistTier)) {
                    _blacklistTier.Clear();
                    _blacklistTier.UnionWith(((string)args.newValue).Split(',').Select(x => x.Trim()));
                }
                if(args.target.boundProperty.Name == nameof(serverConfig.blacklistItem)) {
                    _blacklistItem.Clear();
                    _blacklistItem.UnionWith(((string)args.newValue).Split(',').Select(x => x.Trim()));
                }
            };

            serverConfig.BindAll(cfgFile, "Yeet", "Server");
            clientConfig.BindAll(cfgFile, "Yeet", "Client");

            On.RoR2.UI.ItemIcon.Awake += ItemIcon_Awake;
            On.RoR2.UI.EquipmentIcon.Update += EquipmentIcon_Update;
            On.RoR2.PickupDropletController.OnCollisionEnter += PickupDropletController_OnCollisionEnter;
            On.RoR2.GenericPickupController.CreatePickup += GenericPickupController_CreatePickup;
            On.RoR2.GenericPickupController.GetInteractability += GenericPickupController_GetInteractability;
            On.RoR2.GenericPickupController.OnTriggerStay += GenericPickupController_OnTriggerStay;

            CommandHelper.AddToConsoleWhenReady();

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
        }

        [ConCommand(commandName = "yeet", flags = ConVarFlags.ExecuteOnServer, helpText = "Requests the server to drop an item from your character. Argument 1: item index or partial name. Argument 2: if true, drop equipment instead. Argument 3: throw force. Argument 4: item count.")]
        private static void ConCmdYeet(ConCommandArgs args) {
            if(!allowYeet.value) {
                if(args.sender)
                    NetUtil.ServerSendChatMsg(args.sender, "Yeet mod has been temporarily disabled by the server host.");
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
                yd.age = serverConfig.yeetCooldown;
            }

            if(yd.age < serverConfig.yeetCooldown) {
                var cdRemaining = serverConfig.yeetCooldown - yd.age;
                NetUtil.ServerSendChatMsg(args.sender, $"You must wait {cdRemaining:0} second{((cdRemaining < 2) ? "" : "s")} before yeeting another item.");
                return;
            }

            bool isEquipment = args.TryGetArgBool(1) ?? false;

            if(isEquipment ? serverConfig.preventEquipment : serverConfig.preventItems) return;

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

            float throwForce = Mathf.Lerp(serverConfig.lowThrowForce, serverConfig.highThrowForce, Mathf.Clamp01(args.TryGetArgFloat(2) ?? 0f));
            int throwCount = 1;

            PickupIndex pickup;
            if(isEquipment) {
                if(args.senderBody.inventory.GetEquipmentIndex() != (EquipmentIndex)rawInd) {
                    _logger.LogWarning("ConCmdYeet: someone's trying to drop an equipment they don't have");
                    return;
                }

                var edef = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)rawInd);

                if(edef.isLunar ? serverConfig.preventLunarEquipment : serverConfig.preventNonLunarEquipment)
                    return;

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
                var attemptThrowCount = args.TryGetArgInt(3) ?? 1;
                if(attemptThrowCount < 0)
                    attemptThrowCount = Mathf.CeilToInt(count / ((-attemptThrowCount) * 100f));
                throwCount = Mathf.Clamp(attemptThrowCount, 1, serverConfig.maxThrowCount);
                var idef = ItemCatalog.GetItemDef((ItemIndex)rawInd);
                Debug.Log(idef._itemTierDef.name);
                if((serverConfig.preventHidden && idef.hidden)
                    || (serverConfig.preventCantRemove && !idef.canRemove)
                    || _blacklistTier.Contains(idef._itemTierDef.name)
                    || _blacklistItem.Contains(idef.nameToken))
                    return;
                args.senderBody.inventory.RemoveItem((ItemIndex)rawInd);
                pickup = PickupCatalog.FindPickupIndex((ItemIndex)rawInd);
            }

            for(var i = 0; i < throwCount; i++) {
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
            }

            yd.age = 0;
        }

        #region Hooks
        private void GenericPickupController_OnTriggerStay(On.RoR2.GenericPickupController.orig_OnTriggerStay orig, GenericPickupController self, Collider other) {
            if(NetworkServer.active) {
                var cb = other.GetComponent<CharacterBody>();
                var yd = self.GetComponent<YeetData>();
                if(cb && yd && yd.yeeter == cb && yd.age < serverConfig.yoinkCooldown) {
                    return;
                }
            }
            orig(self, other);
        }

        private Interactability GenericPickupController_GetInteractability(On.RoR2.GenericPickupController.orig_GetInteractability orig, GenericPickupController self, Interactor activator) {
            var retv = orig(self, activator);
            var yd = self.GetComponent<YeetData>();
            var actBody = activator.GetComponent<CharacterBody>();
            if(yd && actBody && yd.yeeter == actBody && yd.age < serverConfig.yoinkCooldown) {
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
                if(!serverConfig.commandExtraCheesyMode)
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
                            if(serverConfig.preventRecycling)
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
            } else orig(self, collision);
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
        #endregion
    }

    public class YeetData : MonoBehaviour {
        public CharacterBody yeeter;
        public float age = 0f;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used by UnityEngine")]
        void FixedUpdate() {
            age += Time.fixedDeltaTime;
        }
    }
    
	public class YeetButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
        float holdTimeL = 0f;
        float holdTimeR = 0f;
        public bool isEquipment = false;
		void IPointerDownHandler.OnPointerDown(PointerEventData eventData) {
            if(eventData.button == PointerEventData.InputButton.Left)
                holdTimeL = Time.unscaledTime;
            else if(eventData.button == PointerEventData.InputButton.Right)
                holdTimeR = Time.unscaledTime;
		}
        void IPointerUpHandler.OnPointerUp(PointerEventData eventData) {
            var isL = eventData.button == PointerEventData.InputButton.Left;
            var isR = eventData.button == PointerEventData.InputButton.Right;
            if(!isL && !isR) return;
            float totalTime = Time.unscaledTime - (isL ? holdTimeL : holdTimeR);
            var ind = isEquipment
                ? ((int)GetComponent<RoR2.UI.EquipmentIcon>().targetInventory.GetEquipmentIndex()).ToString()
                : ((int)GetComponent<RoR2.UI.ItemIcon>().itemIndex).ToString();
			if(NetworkUser.readOnlyLocalPlayersList.Count > 0) {
                //RoR2.Console.instance.RunClientCmd(NetworkUser.readOnlyLocalPlayersList[0], "yeet", new string[]{((int)ind).ToString(), totalTime.ToString("N3")});
                RoR2.Console.instance.SubmitCmd(NetworkUser.readOnlyLocalPlayersList[0], $"yeet {ind} {(isEquipment ? 1 : 0)} {totalTime:N4} {(isL ? YeetPlugin.clientConfig.primaryQuantity : YeetPlugin.clientConfig.secondaryQuantity)}");
            } else
                YeetPlugin._logger.LogError("Received inventory click event with no active local players!");
        }
    }
}