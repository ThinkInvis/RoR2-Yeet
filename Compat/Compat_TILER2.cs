using RoR2;
using System.Runtime.CompilerServices;
using TILER2;

namespace ThinkInvisible.Yeet {
    static class Compat_TILER2 {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static int GetRealItemCount(Inventory inv, ItemIndex ind) {
            var fakeInv = inv.GetComponent<FakeInventory>();
            if(fakeInv) return fakeInv.GetRealItemCount(ind);
            return inv.GetItemCount(ind);
        }

        private static bool? _enabled;
        internal static bool enabled {
            get {
                if(_enabled == null) _enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.ThinkInvisible.TILER2");
                return (bool)_enabled;
            }
        }
    }
}
