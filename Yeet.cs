using RoR2;
using BepInEx;
using MonoMod.Cil;
using R2API.Utils;
using UnityEngine;
using Mono.Cecil.Cil;
using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Linq;
using UnityEngine.EventSystems;

namespace ThinkInvisible.Yeet {
    
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin(ModGuid, ModName, ModVer)]
    [R2APISubmoduleDependency(nameof(CommandHelper))]
    public class YeetPlugin:BaseUnityPlugin {
        public const string ModVer = "1.0.0";
        public const string ModName = "Yeet";
        public const string ModGuid = "com.ThinkInvisible.Yeet";

        //static float regrabCooldown = 5f;
        static float throwForce = 30f;
        static bool preventLunar = true;

        internal static ManualLogSource _logger;
        
        public void Awake() {
            _logger = this.Logger;
            ConfigFile cfgFile = new ConfigFile(Paths.ConfigPath + "\\" + ModGuid + ".cfg", true);

            /*var cfgRegrabCooldown = cfgFile.Bind(new ConfigDefinition("Yeet", "RegrabCooldown"), 5f, new ConfigDescription(
                "Time (in seconds) to prevent picking an item back up after dropping it. Does not apply to other players, only the dropper. Set to <= 0 to disable.",
                new AcceptableValueRange<float>(0f,float.MaxValue)));*/

            var cfgThrowForce = cfgFile.Bind(new ConfigDefinition("Yeet", "ThrowForce"), 50f, new ConfigDescription(
                "Initial speed, in player view direction, to add to droplets for dropped items.",
                new AcceptableValueRange<float>(0f,float.MaxValue)));

            var cfgPreventLunar = cfgFile.Bind(new ConfigDefinition("Yeet", "PreventLunar"), true, new ConfigDescription(
                "If true, lunar items cannot be dropped (to preserve the consequences of picking one up)."));
            
            //regrabCooldown = cfgRegrabCooldown.Value;
            throwForce = cfgThrowForce.Value;
            preventLunar = cfgPreventLunar.Value;

            On.RoR2.UI.ItemIcon.Awake += ItemIcon_Awake;
        }

        private void ItemIcon_Awake(On.RoR2.UI.ItemIcon.orig_Awake orig, RoR2.UI.ItemIcon self) {
            orig(self);
            self.gameObject.AddComponent<YeetButton>();
        }

        [ConCommand(commandName = "yeet", flags = ConVarFlags.ExecuteOnServer, helpText = "Requests the server to drop an item from your character. Argument 1: item index or partial name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by UnityEngine")]
        private static void ConCmdYeet(ConCommandArgs args) {
            if(!args.senderBody) {
                _logger.LogError("ConCmdYeet: called by nonexistent player!");
                return;
            }
            ItemIndex ind;
            string itemSearch = args.TryGetArgString(1);
            if(itemSearch == null) {
                _logger.LogError("ConCmdYeet: could not read first argument (item ID)!");
                return;
            }
            else if(int.TryParse(itemSearch, out int rawInd)) {
                ind = (ItemIndex)rawInd;
                if(!ItemCatalog.IsIndexValid(ind)) {
                    _logger.LogError("ConCmdYeet: first argument (item ID as integer ItemIndex) is out of range; no item with that ID exists!");
                    return;
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
                    ind = results.First();
                }
            }

            if(args.senderBody.inventory.GetItemCount(ind) < 1) return;

            var idef = ItemCatalog.GetItemDef(ind);
            if(idef.hidden || (idef.tier == ItemTier.Lunar && preventLunar)) return;
            args.senderBody.inventory.RemoveItem(ind);
            
            PickupDropletController.CreatePickupDroplet(
                PickupCatalog.FindPickupIndex(ind),
                args.senderBody.inputBank.aimOrigin,
                args.senderBody.inputBank.aimDirection * throwForce);
        }
    }
    
	public class YeetButton : MonoBehaviour, IPointerClickHandler {
		void IPointerClickHandler.OnPointerClick(PointerEventData eventData) {
            var icon = this.GetComponentInParent<RoR2.UI.ItemIcon>();
            var ind = icon.GetFieldValue<ItemIndex>("itemIndex");
			if(NetworkUser.readOnlyLocalPlayersList.Count > 0)
                RoR2.Console.instance.SubmitCmd(NetworkUser.readOnlyLocalPlayersList[0], "yeet " + ((int)ind).ToString());
            else
                YeetPlugin._logger.LogError("Received inventory click event with no active local players!");
            
		}
    }
}