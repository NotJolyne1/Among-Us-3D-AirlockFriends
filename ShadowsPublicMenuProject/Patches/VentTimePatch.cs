using HarmonyLib;
using Il2CppSG.Airlock.Roles;
using ShadowsPublicMenu.Config;

namespace ShadowsPublicMenu.Patches
{
    [HarmonyPatch(typeof(RoleData), "get_MaxTimeInVents")]
    public class VentTimePatch
    {
        public static bool Prefix(ref int __result)
        {
            if (Mods.NoVentCooldown)
            {
                __result = 9999;
                return false;
            }
            return true;
        }
    }
}
