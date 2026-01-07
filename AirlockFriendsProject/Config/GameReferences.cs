using System;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.XR;

namespace AirlockFriends.Config
{
    public class GameReferences
    {
        public static SpawnManager Spawn;
        public static XRRig Rig;
        public static GameStateManager GameState;
        public static AirlockNetworkRunner Runner;

        public static void refreshGameRefs()
        {
            string reference = "";

            try
            {
                Spawn = null;
                Rig = null;
                GameState = null;
                Runner = null;

                reference = "Spawn Manager";
                Spawn = UnityEngine.Object.FindObjectOfType<SpawnManager>();
                reference = "XRRig";
                Rig = UnityEngine.Object.FindObjectOfType<XRRig>();
                reference = "GameState";
                GameState = UnityEngine.Object.FindObjectOfType<GameStateManager>();
                reference = "Airlock Runner";
                Runner = UnityEngine.Object.FindObjectOfType<AirlockNetworkRunner>();
                Settings.GameRefsFound = true;
            }
            catch (Exception e)
            {
                Managers.Logging.Warning($"Failed to refresh game references! Please report this to me on discord (@NotShadowpy) or github issues tab with this: Failed at: {reference} Error: {e}");
            }
        }
    }
}