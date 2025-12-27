using System;
using System.Collections.Generic;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Customization;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.Roles;
using Il2CppSG.Airlock.Sabotage;
using Il2CppSG.Airlock.UI;
using Il2CppSG.Airlock.XR;
using MelonLoader;
using UnityEngine;

namespace AirlockFriends.Config
{
    public class GameReferences
    {
        public static SpawnManager Spawn;
        public static XRRig Rig;
        public static NetworkedLocomotionPlayer LocoPlayer;
        public static PlayerState Player;
        public static NetworkedKillBehaviour Killing;
        public static GameStateManager GameState;
        public static AirlockNetworkRunner Runner;
        public static ModerationManager Moderation;


        public static void refreshGameRefs()
        {
            string reference = "";
            try
            {
                Spawn = null;
                Rig = null;
                LocoPlayer = null;
                Player = null;
                Killing = null;
                GameState = null;
                Runner = null;
                Moderation = null;

                reference = "Spawn Manager";
                Spawn = UnityEngine.Object.FindObjectOfType<SpawnManager>();
                reference = "XRRig";
                Rig = UnityEngine.Object.FindObjectOfType<XRRig>();
                reference = "NetworkedLocomotionPlayer";
                LocoPlayer = UnityEngine.Object.FindObjectOfType<NetworkedLocomotionPlayer>();
                reference = "PlayerState";
                Player = UnityEngine.Object.FindObjectOfType<PlayerState>();
                reference = "NetworkedKillBehavior";
                Killing = UnityEngine.Object.FindObjectOfType<NetworkedKillBehaviour>();
                reference = "GameState";
                GameState = UnityEngine.Object.FindObjectOfType<GameStateManager>();
                reference = "Airlock Runner";
                Runner = UnityEngine.Object.FindObjectOfType<AirlockNetworkRunner>();
                reference = "ModerationManager";
                Moderation = UnityEngine.Object.FindObjectOfType<ModerationManager>();
                Settings.GameRefsFound = true;
                MelonLogger.Msg("Found Game References!");
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[FAIL] Failed to refresh game references! Please report this to me on discord (@Shadoww.py) or github issues tab with this: Failed at: {reference} Error: {e}");
            }
        }
    }
}
