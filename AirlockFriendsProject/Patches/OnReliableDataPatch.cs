using AirlockFriends.Managers;
using HarmonyLib;
using Il2CppFusion;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace ShadowsMenu.Patches
{
    [HarmonyPatch]
    internal static class ReliableDataPatch
    {
        [HarmonyPatch(typeof(NetworkRunner), nameof(NetworkRunner.Fusion_Simulation_ICallbacks_OnReliableData))]
        [HarmonyPostfix]
        private static void Postfix(PlayerRef player, Il2CppStructArray<byte> dataArray)
        {
            try
            {
                AirlockFriendsAuth.OperationReceived(player, dataArray);
            }
            catch { }
        }
    }
}
