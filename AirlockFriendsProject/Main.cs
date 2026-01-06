using System;
using AirlockFriends.Config;
using AirlockFriends.Managers;
using Il2CppSteamworks;
using MelonLoader;
using ShadowsMenu.Managers;
using UnityEngine;
using UnityEngine.InputSystem;
using static AirlockFriends.Config.GameReferences;
using static AirlockFriends.Config.Settings;

[assembly: MelonInfo(typeof(AirlockFriends.Main), "AirlockFriends", Settings.Version, "Shadoww.py", "githublink")]
[assembly: MelonGame("Schell Games", "Among Us 3D")]
[assembly: MelonGame("Schell Games", "Among Us VR")]

namespace AirlockFriends
{
    public class Main : MelonMod
    {
        public static bool passed = true;
        public static bool PostVersion = false;
        public static CSteamID cachedId;
        public static bool AFBanned = false;
        public static bool HasShownBanNoti = false;
        private static bool NotifyingFriends = false;
        public static bool outdated = false;
        public static bool UpdateRequired = false;
        public static bool BetaBuild = false;

        public override void OnApplicationQuit()
        {
            Logging.Msg("Thank you for using Airlock Friends!");
        }

        [System.Obsolete]
        public override void OnApplicationStart()
        {
            Logging.Msg("Loading Airlock Friends..");
            IsVR = UnityEngine.Application.productName.Contains("VR");
            FetchServerData();
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
                Logging.Error($"Failed to get friends: {ex}");
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
                {
                    refreshGameRefs();
                    ModUserVisuals.CleanupAll();
                }
            }
            catch { }
        }

        public override void OnGUI()
        {
            Color OriginalColor = GUI.color;
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
                Logging.Warning($"Something went wrong! Failed at ModManager.Update(), error: {e}");
                GUI.color = OriginalColor;
            }
            GUI.color = OriginalColor;
        }

        private static async void FetchServerData()
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    var text = await client.GetStringAsync("https://notjolyne.neocities.org/ServerData.txt");
                    var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    if (lines.Length < 2)
                        return;

                    System.Version ServerVersion = new System.Version(lines[0].Trim());
                    System.Version MinimumVersion = new System.Version(lines[1].Trim());
                    System.Version LocalVersion = new System.Version(Settings.Version);


                    if (LocalVersion < MinimumVersion)
                    {
                        UpdateRequired = true;
                        Logging.Msg("Client is using a unstable version");
                        NotificationLib.QueueNotification("[<color=red>OUTDATED</color>] You are using a outdated version!\nThis version is <b>UNSTABLE</b> and you're <b>required</b> to update in the Discord/GitHub<");
                    }
                    else if (LocalVersion < ServerVersion)
                    {
                        outdated = true;
                        Logging.Msg("Client is outdated");
                        NotificationLib.QueueNotification("[<color=red>OUTDATED</color>] You are using a outdated version!\nPlease update in the <b>Discord/GitHub</b>");
                    }
                    else if (LocalVersion > ServerVersion)
                    {
                        BetaBuild = true;
                        NotificationLib.QueueNotification("[<color=magenta>BETA</color>] You are using a beta build!");
                        Logging.Msg("Client is a beta build");
                    }
                    else
                        Logging.Msg("Client is up to date");
                }
            }
            catch (Exception ex)
            {
                Logging.Error($"ServerData error: {ex.Message}");
            }
        }
    }
}
