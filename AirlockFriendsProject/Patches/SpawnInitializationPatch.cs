using AirlockFriends.Managers;
using HarmonyLib;
using Il2CppSG.Airlock.Network;
using MelonLoader;

namespace FunniMenuLite.Patches
{
    [HarmonyPatch(typeof(NetworkedLocomotionPlayer), nameof(NetworkedLocomotionPlayer.RPC_SpawnInitialization))]
    public class AntiBanPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                _ = AirlockFriendsOperations.RPC_GetFriends();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Failed to get friends: {ex}");
            }
        }
    }
}
