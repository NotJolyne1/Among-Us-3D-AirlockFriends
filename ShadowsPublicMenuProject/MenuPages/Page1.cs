using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Roles;
using Il2CppSG.Airlock.Sabotage;
using Il2CppSG.Airlock.UI.TitleScreen;
using MelonLoader;
using ShadowsPublicMenu.Config;
using ShadowsPublicMenu.Managers;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace ShadowsPublicMenu.MenuPages
{
    public class MenuPage1
    {
        public static void Display()
        {
            float y = 50f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Join Discord"))
            {
                Application.OpenURL("https://discord.com/invite/2FzsKdvjMU");
                NotificationLib.SendNotification("[<color=lime>Success</color>] Joins the <color=magenta>Shadow's menu</color>. Discord server");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Disconnect"))
            {
                GameReferences.Moderation._peerManager.Disconnect();
                NotificationLib.SendNotification("[<color=lime>Success</color>] Disconnects you from the current room");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Host Game (3D)"))
            {
                Object.FindObjectOfType<HostGameMenu>(true).gameObject.SetActive(true);
                if (Settings.IsVR)
                    NotificationLib.SendNotification("[<color=lime>Success</color>] Hosts a new game if you're on non VR");
                else
                    NotificationLib.SendNotification("[<color=red>Fail</color>] This mod only works on non VR");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Start Game (H)"))
            {
                if (Settings.IsHost)
                {
                    RPCManager.RPC_StartGame();
                    NotificationLib.SendNotification("[<color=lime>Success</color>] Forces the game to start as host");
                }
                else
                    NotificationLib.SendNotification("[<color=red>Fail</color>] You must be host to force start the game");


            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"No Game End (H): {Mods.PreventGameEnd}"))
            {
                Mods.PreventGameEnd = !Mods.PreventGameEnd;
                string Toggled = Mods.PreventGameEnd ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Prevents game from ending for traditional reasons");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Kill Everyone (H)"))
            {
                if (Settings.IsHost)
                {
                    foreach (PlayerState player in GameReferences.Spawn.PlayerStates)
                    {
                        if (player != GameReferences.Rig.PState)
                        {
                            RPCManager.RPC_TargetedAction(player, ProximityTargetedAction.Kill);
                        }
                    }
                    NotificationLib.SendNotification($"[<color=lime>Success</color>] Killed everyone as host");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to force kill players");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Infect Everyone (H)"))
            {
                if (Settings.IsHost && GameReferences.GameState.GameModeStateValue.GameMode == GameModes.Infection)
                {
                    foreach (PlayerState player in GameReferences.Spawn.PlayerStates)
                    {
                        if (player != GameReferences.Rig.PState)
                        {
                            RPCManager.RPC_ForceRole(player, GameRole.Infected);
                            RPCManager.RPC_TargetedAction(player, ProximityTargetedAction.Infect);
                        }
                    }
                    NotificationLib.SendNotification($"[<color=lime>Success</color>] Infected everyone");
                }
                else if (Settings.IsHost)
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to force infect players");
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be in the tag gamemode");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Spaz All Colors (H): {Mods.spazAllColors}"))
            {
                if (Settings.IsHost)
                {
                    Mods.spazAllColors = !Mods.spazAllColors;
                    if (Mods.spazAllColors)
                    {
                        foreach (PlayerState player in GameReferences.Spawn.PlayerStates)
                        {
                            if (player != null && !player.IsSpectating)
                                MelonCoroutines.Start(ModManager.SpazColors(player));
                        }
                    }
                    string Toggled = Mods.spazAllColors ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                    NotificationLib.SendNotification($"[{Toggled}] Changes all players colors fast");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to change player colors");


            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Sabotage Lights (H)"))
            {
                if (Settings.IsHost)
                {
                    RPCManager.RPC_BeginSabotage(Sabotage.SabotageType.Lights);
                    NotificationLib.SendNotification("[<color=lime>Success</color>] Starts the <color=yellow>Lights</color> sabotage as host");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to start sabotages");

            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Sabotage Oxygen (H)"))
            {
                if (Settings.IsHost)
                {
                    RPCManager.RPC_BeginSabotage(Sabotage.SabotageType.Oxygen);
                    NotificationLib.SendNotification("[<color=lime>Success</color>] Starts the <color=cyan>Oxygen</color> sabotage as host");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to start sabotages");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Sabotage Reactor (H)"))
            {
                if (Settings.IsHost)
                {
                    RPCManager.RPC_BeginSabotage(Sabotage.SabotageType.Reactor);
                    NotificationLib.SendNotification("[<color=lime>Success</color>] Starts the <color=red>Reactor</color> sabotage as host");
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be the host to start sabotages");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "End Sabotage"))
            {
                RPCManager.RPC_EndSabotage();
                NotificationLib.SendNotification("[<color=lime>Success</color>] Ends all sabotages instantly");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Noclip: {Mods.Noclip}"))
            {
                Mods.Noclip = !Mods.Noclip;
                string Toggled = Mods.Noclip ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Allows you move freely through all objects");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Speed: {Mods.Speed}"))
            {
                Mods.Speed = !Mods.Speed;
                string Toggled = Mods.Speed ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Increases your walking speed.\nConfigurable on page 2");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"No Kill Cooldown: {Mods.NoKillCooldown}"))
            {
                Mods.NoKillCooldown = !Mods.NoKillCooldown;
                string Toggled = Mods.NoKillCooldown ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Removes your cooldown on all actions");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Hollow Box ESP: {Mods.BoxESP}"))
            {
                Mods.BoxESP = !Mods.BoxESP;
                foreach (var kvp in PlayerVisualManager.playerESPs)
                {
                    if (kvp.Value != null)
                    {
                        foreach (var line in kvp.Value)
                            if (line != null)
                                line.SetActive(Mods.BoxESP);
                    }
                }
                string Toggled = Mods.BoxESP ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Draws ESP boxes around all players");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Cooldown ESP: {Mods.CooldownESP}"))
            {
                Mods.CooldownESP = !Mods.CooldownESP;
                string Toggled = Mods.CooldownESP ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Displays remaining kill cooldowns above players with a cooldown");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Tracers: {Mods.Tracers}"))
            {
                Mods.Tracers = !Mods.Tracers;
                foreach (var kvp in PlayerVisualManager.playerLines)
                {
                    if (kvp.Value != null)
                        kvp.Value.gameObject.SetActive(Mods.Tracers);
                }
                string Toggled = Mods.Tracers ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Draws a tracer from you to every player");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Fullbright: {Mods.Fullbright}"))
            {
                Mods.Fullbright = !Mods.Fullbright;
                string Toggled = Mods.Fullbright ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Makes you able to see everything");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"No Vent Cooldown: {Mods.NoVentCooldown}"))
            {
                Mods.NoVentCooldown = !Mods.NoVentCooldown;
                string Toggled = Mods.NoVentCooldown ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Removes engineer vent cooldown and max vent time");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Kill Alerts (H): {Mods.KillAlerts}"))
            {
                Mods.KillAlerts = !Mods.KillAlerts;
                string Toggled = Mods.KillAlerts ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Sends notifications when players are killed or infected");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "End Game (H)"))
            {
                if (Settings.IsHost)
                {
                    NotificationLib.SendNotification($"[<color=lime>Success</color>] Forces the game to end");
                    GameReferences.GameState.EndGame(GameTeam.Crewmember);
                }
                else
                    NotificationLib.SendNotification($"[<color=red>Fail</color>] You must be host to end the game");
            }
        }
    }
}
