using System;
using System.Linq.Expressions;
using AirlockFriends.Config;
using AirlockFriends.Managers;
using Il2CppSG.Airlock.UI.TitleScreen;
using Il2CppSteamworks;
using MelonLoader;
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
        private Texture2D _roundedRectTexture;

        private float nextFpsUpdateTime = 0f;
        private int frames = 0;
        private int fps = 0;
        public static bool passed = true;
        public static bool PostVersion = false;
        private bool cached = false;
        public static CSteamID cachedId;
        public static bool AFBanned = false;
        public static bool AppealButtonClicked = false;
        public static bool HasShownBanMessage = false;
        public static bool HasShownBanNoti = false;

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
                _ = AirlockFriendsOperations.RPC_GetFriends();

                if (AFBanned && !HasShownBanMessage)
                {
                    try
                    {
                        UnityEngine.Object.FindObjectOfType<MenuManager>().ShowPopup("<color=red>You are currently blacklisted from using Airlock Friends.</color>\n\nThis ban will <b>Never</b> expire.\n\nClick appeal to join the Discord and request a unban.", "APPEAL", "CLOSE");
                        HasShownBanMessage = true;
                    }
                    catch { }
                }
            }
            catch (Exception ex) 
            {
                MelonLogger.Error($"Failed to get friends: {ex}");
            }
        }


        public override void OnUpdate()
        {

            /*
            DateTime expiration = new DateTime(2025, 11, 18, 22, 0, 0);
            if (DateTime.UtcNow > expiration.ToUniversalTime())
            {
                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = "/C msg * \"This testing session has expired and you are unable to use Airlock Friends until a new testing session is assigned or a public build is available.\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);


                Application.Quit();
                return;
            }
            */

            NotificationLib.Update();

            if (Keyboard.current.leftCtrlKey.wasPressedThisFrame)
                Settings.GUIEnabled = !Settings.GUIEnabled;


            if (!InGame)
            {
                GameRefsFound = false;
                return;
            }

            if (InGame && !GameRefsFound)
            {
                refreshGameRefs();
            }
        }




        public override void OnGUI()
        {
            GUI.color = Settings.GUIColor;
            if (!GUIEnabled || !passed)
                return;

            try
            {
                UI.FriendGUI.Update();

                if (AFBanned && !HasShownBanNoti)
                {
                    HasShownBanNoti = true;
                    NotificationLib.QueueNotification("[<color=red>BANNED</color>] You have been blacklisted from AirlockFriends\nThis ban will <b>Never</b> expire.", true);
                }
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning($"[FAIL] Something went wrong! Failed at ModManager.Update(), error: {e}");
            }
        }
    }
}
