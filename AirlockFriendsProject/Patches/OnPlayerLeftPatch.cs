using HarmonyLib;
using Il2CppFusion;
using ShadowsMenu.Managers;

namespace AirlockFriends.Patches
{
    [HarmonyPatch(typeof(NetworkRunner), nameof(NetworkRunner.Fusion_Simulation_ICallbacks_PlayerLeft))]
    public class OnPlayerLeftPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerRef player)
        {
            try
            {
                ModUserVisuals.Remove(Helpers.GetPlayerStateFromRef(player));
            }
            catch { }
        }
    }
}
