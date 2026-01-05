using AirlockFriends.Managers;
using HarmonyLib;
using Il2CppSG.Airlock.Network;
using Il2CppSystem.IO;

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
                var runner = UnityEngine.Object.FindObjectOfType<AirlockNetworkRunner>();

                foreach (var player in runner.ActivePlayers.ToArray())
                {
                    AirlockFriendsAuth.RPC_SendReliableData(player, "IsUsing");
                }
            }
            catch (System.Exception ex)
            {
                Logging.Error($"Failed to get friends: {ex}");
            }
        }
    }
}
