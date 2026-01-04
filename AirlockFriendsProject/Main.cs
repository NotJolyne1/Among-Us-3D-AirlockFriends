using System;
using System.Linq.Expressions;
using AirlockFriends.Config;
using AirlockFriends.Managers;
using Il2CppSG.Airlock.UI.TitleScreen;
using Il2CppSteamworks;
using MelonLoader;
using ShadowsMenu.Managers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;
using static AirlockFriends.Config.GameReferences;
using static AirlockFriends.Config.Settings;

[assembly: MelonInfo(typeof(AirlockFriends.Main), "AirlockFriends", Settings.Version, "Shadoww.py", "githublink")]
[assembly: MelonGame("Schell Games", "Among Us 3D")]
[assembly: MelonGame("Schell Games", "Among Us VR")]

namespace AirlockFriends
{
    public class Main : MelonMod
    {
        private float nextFpsUpdateTime = 0f;
        private int frames = 0;
        private int fps = 0;
        public static bool passed = true;
        public static bool PostVersion = false;
        public static CSteamID cachedId;
        public static bool AFBanned = false;
        public static bool HasShownBanNoti = false;
        private static bool NotifyingFriends = false;
        public override void OnApplicationQuit()
        {
			MelonLogger.Msg("Thank you for using Airlock Friends!");
        }

        [System.Obsolete]
        public override void OnApplicationStart()
        {
            MelonLogger.Msg("Loading Airlock Friends..");
            IsVR = UnityEngine.Application.productName.Contains("VR");
        }


        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            InGame = sceneName != "Boot" && sceneName != "Title";
            try
            {
                AirlockFriendsOperations.PrepareAuthentication();
                _ = AirlockFriendsOperations.RPC_NotifyFriendGroup();

                if (!NotifyingFriends)
                {
                    NotifyingFriends = true;
					MelonCoroutines.Start(AirlockFriendsAuth.NotifyFriendGroup());
                }
            }
            catch (Exception ex) 
            {
                MelonLogger.Error($"Failed to get friends: {ex}");
            }
        }


        public override void OnUpdate()
        {
            try
            {
                NotificationLib.Update();
                ModUserVisuals.Update();
                if (Keyboard.current.f1Key.wasPressedThisFrame || Keyboard.current.slashKey.wasPressedThisFrame)
                    GUIEnabled = !GUIEnabled;

                if (!InGame)
                    GameRefsFound = false;

                if (InGame && !GameRefsFound)
                    refreshGameRefs();

            }
            catch { }
        }




        public override void OnGUI()
        {
            var OriginalColor = GUI.color;
            GUI.color = GUIColor;
            if (!GUIEnabled || !passed)
            {
                GUI.color = OriginalColor;
                return;
            }

            try
            {
                UI.FriendGUI.Update();

                if (AFBanned && !HasShownBanNoti)
                {
                    HasShownBanNoti = true;
                    NotificationLib.QueueNotification("[<color=red>DISABLED</color>] You have been blacklisted from AirlockFriends", true);
                }
                GUI.color = OriginalColor;
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning($"[FAIL] Something went wrong! Failed at ModManager.Update(), error: {e}");
                GUI.color = OriginalColor;
            }
            GUI.color = OriginalColor;
        }
    }
}
