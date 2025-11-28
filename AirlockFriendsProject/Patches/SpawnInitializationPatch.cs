using AirlockFriends.Managers;
using HarmonyLib;
using Il2CppSG.Airlock.Network;
using MelonLoader;

namespace AirlockFriends.Patches
{
    [HarmonyPatch(typeof(NetworkedLocomotionPlayer), nameof(NetworkedLocomotionPlayer.RPC_SpawnInitialization))]
    public class SpawnInitializationPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref string name)
        {
            try
            {
                _ = AirlockFriendsOperations.RPC_GetFriends(name);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Failed to get friends: {ex}");
            }
        }
    }
}
