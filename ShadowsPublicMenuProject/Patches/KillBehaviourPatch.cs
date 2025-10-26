using HarmonyLib;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using ShadowsPublicMenu.Config;
using ShadowsPublicMenu.Managers;

namespace ShadowsPublicMenu.Patches
{
    [HarmonyPatch(typeof(NetworkedKillBehaviour), nameof(NetworkedKillBehaviour.RPC_TargetedAction))]
    public class KillBehaviourPatch
    {
        public static void Prefix(PlayerRef targetedPlayer, PlayerRef perpetrator, int action)
        {
            if (!Mods.KillAlerts) return;

            PlayerState VictimState = Helpers.GetPlayerStateFromRef(targetedPlayer);
            PlayerState KillerState = Helpers.GetPlayerStateFromRef(perpetrator);

            if (action == 0)
            {
                NotificationLib.SendNotification($"[<color=red>KILL</color>] <color={Helpers.GetColorHexFromID(KillerState.ColorId)}>{KillerState.NetworkName.Value}</color> killed <color={Helpers.GetColorHexFromID(VictimState.ColorId)}>{VictimState.NetworkName.Value}</color>");
            }
            else if (action == 1)
            {
                NotificationLib.SendNotification($"[<color=lime>INFECT</color>] <color={Helpers.GetColorHexFromID(KillerState.ColorId)}>{KillerState.NetworkName.Value}</color> infected <color={Helpers.GetColorHexFromID(VictimState.ColorId)}>{VictimState.NetworkName.Value}</color>");
            }
        }
    }
}
