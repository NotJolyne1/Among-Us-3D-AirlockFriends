using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using Il2CppPhoton.Realtime;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.Roles;
using MelonLoader;
using ShadowsPublicMenu.Config;
using ShadowsPublicMenu.Managers;
using UnityEngine;

namespace ShadowsPublicMenu.MenuPages
{
    public class PlayerPage1
    {
        public static void Display(PlayerState target)
        {
            float y = 50f;
            bool canWork = Settings.InGame && target != null;

            if (GUI.Button(new Rect(180f, y, 160f, 30f), "Kill Player (H)") && canWork)
            {
                if (Settings.IsHost)
                {
                    RPCManager.RPC_TargetedAction(target, ProximityTargetedAction.Kill);
                    NotificationLib.SendNotification($"[<color=lime>Success</color>] Killed <color={Helpers.GetColorHexFromID(target.ColorId)}>{target.NetworkName.Value}</color>");
                }
                else
                {
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to perform this action");
                }
            }

            y += 30f;


            if (GUI.Button(new Rect(180f, y, 160f, 30f), "Infect Player (H)") && canWork)
            {
                if (Settings.IsHost && GameReferences.GameState.GameModeStateValue.GameMode == GameModes.Infection)
                {
                    RPCManager.RPC_TargetedAction(target, ProximityTargetedAction.Infect);
                    RPCManager.RPC_ForceRole(target, GameRole.Infected);
                    NotificationLib.SendNotification($"[<color=lime>Success</color>] Infected <color={Helpers.GetColorHexFromID(target.ColorId)}>{target.NetworkName.Value}</color>");
                }
                else if (Settings.IsHost)
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to force infect players");
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be in the tag gamemode");

            }
            y += 30f;

            if (GUI.Button(new Rect(180f, y, 160f, 30f), "Force Imposter (H)") && canWork)
            {
                if (Settings.IsHost)
                {
                    RPCManager.RPC_ForceRole(target, GameRole.Impostor);
                    NotificationLib.SendNotification($"[<color=lime>Success</color>] Makes <color={Helpers.GetColorHexFromID(target.ColorId)}>{target.NetworkName.Value}</color> a <color=red>Imposter</color>");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to force players roles");
            }
            y += 30f;

            if (GUI.Button(new Rect(180f, y, 160f, 30f), "Force Vigilante (H)") && canWork)
            {
                if (Settings.IsHost)
                {
                    RPCManager.RPC_ForceRole(target, GameRole.Vigilante);
                    NotificationLib.SendNotification($"[<color=lime>Success</color>] Made <color={Helpers.GetColorHexFromID(target.ColorId)}>{target.NetworkName.Value}</color> a <color=orange>Vigilante</color>");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to force players roles");
            }
            y += 30f;

            if (GUI.Button(new Rect(180f, y, 160f, 30f), "Force Crewmate (H)") && canWork)
            {
                if (Settings.IsHost)
                {
                    RPCManager.RPC_ForceRole(target, GameRole.Crewmember);
                    NotificationLib.SendNotification($"[<color=lime>Success</color>] Made <color={Helpers.GetColorHexFromID(target.ColorId)}>{target.NetworkName.Value}</color> a <color=cyan>Crewmate</color>");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to force players roles");
            }
            y += 30f;

            if (GUI.Button(new Rect(180f, y, 160f, 30f), "Force Wraith (H)") && canWork)
            {
                if (Settings.IsHost)
                {
                    RPCManager.RPC_ForceRole(target, GameRole.Revenger);
                    NotificationLib.SendNotification($"[<color=lime>Success</color>] Made <color={Helpers.GetColorHexFromID(target.ColorId)}>{target.NetworkName.Value}</color> a <color=purple>Wraith</color>");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to force players roles");
            }
            y += 30f;

            if (GUI.Button(new Rect(180f, y, 160f, 30f), $"Spaz Colors (H): {Mods.spazColors}") && canWork)
            {
                if (Settings.IsHost)
                {
                    Mods.spazColors = !Mods.spazColors;
                    MelonCoroutines.Start(ModManager.SpazColors(target));
                    string Toggled = Mods.spazColors ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                    NotificationLib.SendNotification($"[{Toggled}] Makes <color={Helpers.GetColorHexFromID(target.ColorId)}>{target.NetworkName.Value}</color>'s colors go crazy as host");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to change player colors");
            }
            y += 30f;

            if (GUI.Button(new Rect(180f, y, 160f, 30f), "Teleport To Player") && canWork)
            {
                var loco = target?.LocomotionPlayer;
                GameReferences.Rig.Teleport(loco.RigidbodyPosition, loco.RigidbodyRotation, true, true, false);
                NotificationLib.SendNotification($"[<color=lime>Success</color>] Teleported you to <color={Helpers.GetColorHexFromID(target.ColorId)}>{target.NetworkName.Value}</color>");
            }
        }
    }
}
