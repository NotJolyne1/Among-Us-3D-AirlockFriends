using AirlockFriends.Managers;
using HarmonyLib;
using Il2CppSG.Airlock.Network;
using MelonLoader;

namespace AirlockFriends.Patches
{
    [HarmonyPatch(typeof(NetworkedLocomotionPlayer), nameof(NetworkedLocomotionPlayer.RPC_SpawnInitialization))]
    public class SpawnInitializationPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref string name, ref string moderationUsername)
        {
            try
            {
                _ = AirlockFriendsOperations.RPC_NotifyFriendGroup(name, ModName: moderationUsername);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Failed to get friends: {ex}");
            }
        }
    }
}
