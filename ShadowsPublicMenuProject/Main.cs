using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Speech.Synthesis.TtsEngine;
using System.Threading.Tasks;
using Il2CppInternal.Cryptography;
using Il2CppSG.Airlock;
using Il2CppSteamworks;
using MelonLoader;
using ShadowsPublicMenu.Config;
using ShadowsPublicMenu.Managers;
using ShadowsPublicMenu.MenuPages;
using UnityEngine;
using UnityEngine.InputSystem;
using static ShadowsPublicMenu.Config.GameReferences;
using static ShadowsPublicMenu.Config.Settings;

[assembly: MelonInfo(typeof(ShadowsPublicMenu.Main), "Shadows Public Menu", Settings.Version, "Shadoww.py", "https://github.com/NotJolyne1/ShadowsAmongUsVRHacks/releases/tag/Release")]
[assembly: MelonGame("Schell Games", "Among Us 3D")]
[assembly: MelonGame("Schell Games", "Among Us VR")]

namespace ShadowsPublicMenu
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

        public override void OnApplicationQuit()
        {
            MelonLogger.Msg("Thank you for using Shadows Menu!");
        }

        [System.Obsolete]
        public override void OnApplicationStart()
        {
            MelonLogger.Msg($"Initializing Menu...");
            IsVR = Application.productName.Contains("VR");
            VersionCheck();


            MelonLogger.Msg($@"
{"\u001b[35m"}+----------------------------------------------------------------------+
|                                                                      |
|                         SHADOW'S MENU                                |
|                    Developed by Shadoww.py                           |
|                                                                      |
| Thank you for using Shadows Menu! Click Left Ctrl key to toggle it!  |
|                                                                      |
| Join my Discord for early access to updates and to make suggestions! |
| https://discord.com/invite/2FzsKdvjMU                                |
|                                                                      |
+----------------------------------------------------------------------+{"\u001b[0m"}
");

        }


        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            InGame = sceneName != "Boot" && sceneName != "Title";

            if (!InGame)
            {
                Settings.CurrentRoom = "Not in a room";
                Settings.CodeRecievced = false;
            }
            else
            {
                GameObject.Find("BlindboxHeadTrigger")?.SetActive(false);
                GameObject.Find("SightboxHeadTrigger")?.SetActive(false);
                MelonCoroutines.Start(WaitSendTelemetry());
                MelonCoroutines.Start(ContactConsole());
                MelonCoroutines.Start(WaitCheckVersion());
            }
        }


        public override void OnUpdate()
        {
            if (Keyboard.current.leftCtrlKey.wasPressedThisFrame)
                Settings.GUIEnabled = !Settings.GUIEnabled;

            UpdateFps();
            NotificationLib.Update();


            if (SteamAPI.IsSteamRunning() && SteamUser.BLoggedOn() && !cached)
            {
                // Caches ID for telemetry / this replaces the use of moderation IDs for telemetry bc that could be seen as suspicious to some people
                cachedId = SteamUser.GetSteamID();
                cached = true;
                ConsoleManager.AddStm($"Steam_{cachedId}");
            }



            if (GUIColorInt == 8)
            {
                RainbowColor += Time.deltaTime * 0.25f;
                if (RainbowColor > 1f)
                    RainbowColor = 0f;

                GUIColor = Color.HSVToRGB(RainbowColor, 1f, 1f);
            }

            if (!InGame)
            {
                GameRefsFound = false;
                return;
            }

            ModManager.Update();

            if (InGame && !GameRefsFound)
                refreshGameRefs();
        }




        public override void OnGUI()
        {
            GUI.color = Settings.GUIColor;
            if (!GUIEnabled || !passed)
                return;

            try
            {
                DrawMainMenu();
                DrawFPSBar();

                PlayerState player = null;
                if (InGame && Spawn?.PlayerStates != null && PlayerNum >= 0 && PlayerNum < Spawn.PlayerStates.Count)
                    player = Spawn.PlayerStates[PlayerNum];

                DrawPlayerInfo(player);
                DrawPlayerNavigationButtons();

                if (PlayerPageNum <= 0) PlayerPageNum = 1;
                if (PlayerPageNum > 1) PlayerPageNum = 1;

                switch (PlayerPageNum)
                {
                    case 1:
                        PlayerPage1.Display(player);
                        break;
                }
            }


            catch (System.Exception e)
            {
                Settings.ErrorCount += 1;
                MelonLogger.Warning($"[FAIL] Something went wrong! Please report this to me, @Shadoww.py on discord or github issues tab with this: Failed at ModManager.Update(), error: {e}");
            }

            if (!CodeRecievced && InGame)
            {
                CodeRecievced = true;
                Settings.CurrentRoom = Helpers.GetCurrentRoomCode();
            }
        }

        private void UpdateFps()
        {
            frames++;
            float time = Time.unscaledTime;
            if (time >= nextFpsUpdateTime)
            {
                float interval = time - (nextFpsUpdateTime - 1f);
                fps = Mathf.RoundToInt(frames / interval);
                frames = 0;
                nextFpsUpdateTime = time + 1f;
            }
        }

        // Sends telemetry for debugging / analytics
        private static async Task SubmitTelemetry()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Telemetry-ID", $"{cachedId}");
                client.DefaultRequestHeaders.Add("Telemetry-Errors", $"{Settings.ErrorCount}");
                client.DefaultRequestHeaders.Add("Telemetry-Version", $"{Settings.Version}");

                try
                {
                    HttpResponseMessage response = await client.GetAsync("https://shadowsmenu.jolyne108.workers.dev/");

                    if (response.IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"[FAIL] Something went wrong! Please report this to me, @Shadoww.py on discord or github issues tab with this: Failed at Main.SubmitTelemetry(), error: {ex}");
                    Settings.ErrorCount += 1;
                }
            }
        }


        public static IEnumerator WaitSendTelemetry()
        {
            yield return new WaitForSeconds(4f);
            _ = SubmitTelemetry();
        }


        public static IEnumerator WaitCheckVersion()
        {
            yield return new WaitForSeconds(5f);
            VersionCheck();
        }

        public static IEnumerator ContactConsole()
        {
            yield return new WaitForSeconds(6f);

            while (Settings.InGame)
            {
                yield return new WaitForSeconds(2f);

                if (GameReferences.Rig == null || GameReferences.Spawn?.PlayerStates == null)
                    continue;

                yield return new WaitForSeconds(1f);

                foreach (PlayerState player in GameReferences.Spawn.PlayerStates)
                {
                    if (player == null || !player.IsConnected || !player.IsSpawned)
                        continue;

                    var task = ConsoleManager.Identify(player);
                    while (!task.IsCompleted)
                        yield return null;

                    if (task.Result)
                    {
                        if (!PlayerVisualManager.MenuUserTags.ContainsKey(player))
                            PlayerVisualManager.CreateMenuUserTag(player);
                    }
                    else
                    {
                        PlayerVisualManager.RemoveTagText(player);
                    }
                }
            }
        }




        private void DrawPlayerNavigationButtons()
        {
            if (GUI.Button(new Rect(180f, 20f, 80f, 30f), "◄----") && PlayerNum > 0)
                PlayerNum--;

            if (GUI.Button(new Rect(260f, 20f, 80f, 30f), "----►") && PlayerNum < 9)
                PlayerNum++;
        }

        private void DrawPlayerInfo(PlayerState player)
        {
            string name = "Nobody";

            if (player != null && player.IsSpawned && player.IsConnected)
            {
                name = player.NetworkName?.Value ?? "Nobody";

                if (name.Contains("Color##"))
                    name = "Joining..";
            }

            GUI.Box(new Rect(180f, 0f, 160f, 20f), $"{name} ({PlayerNum})");
        }


        private void DrawMainMenu()
        {
            GUI.Box(new Rect(20f, 0f, 160f, 20f), "Shadows Menu" + $" [{CurrentPage}]");

            if (GUI.Button(new Rect(20f, 20f, 80f, 30f), "◄----"))
                CurrentPage--;

            if (GUI.Button(new Rect(100f, 20f, 80f, 30f), "----►"))
                CurrentPage++;

            if (CurrentPage < 1) CurrentPage = 1;
            else if (CurrentPage >= 3) CurrentPage = 1;

            switch (CurrentPage)
            {
                case 1: MenuPages.MenuPage1.Display(); break;
                case 2: MenuPages.MenuPage2.Display(); break;

            }
        }

        private void DrawFPSBar()
        {
            if (!Settings.showFpsBar)
                return;

            string fpsText = $"FPS: {fps}";
            string MenuName = "Shadows Menu";
            string fullText = $"{fpsText} | {MenuName} | {Settings.CurrentRoom}";

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = Color.cyan },
                clipping = TextClipping.Clip,
                padding = new RectOffset(12, 12, 6, 6)
            };

            Vector2 textSize = style.CalcSize(new GUIContent(fullText));
            float barHeight = textSize.y + 8;
            float barWidth = textSize.x + 20;

            float x = (Screen.width - barWidth) / 2;
            float y = 5f;

            if (_roundedRectTexture == null)
                _roundedRectTexture = CreateRoundedTexture(512, 128, 24, new Color(Color.cyan.r, Color.cyan.g, Color.cyan.b, 0.25f));

            _roundedRectTexture.filterMode = FilterMode.Bilinear;

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), _roundedRectTexture);

            GUI.color = Color.cyan;
            GUI.Label(new Rect(x, y, barWidth, barHeight), fullText, style);
            GUI.color = Settings.GUIColor;
        }


        private Texture2D CreateRoundedTexture(int width, int height, float radius, Color color)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false) { filterMode = FilterMode.Bilinear };
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Min(x, width - 1 - x);
                    float dy = Mathf.Min(y, height - 1 - y);
                    float alpha = 1f;

                    if (dx < radius && dy < radius)
                    {
                        float cornerDist = Vector2.Distance(new Vector2(dx, dy), new Vector2(radius, radius));
                        alpha = Mathf.Clamp01(1f - ((cornerDist - (radius - 1f)) / 2f));
                    }

                    Color finalColor = color;
                    finalColor.a *= alpha;
                    tex.SetPixel(x, y, finalColor.a > 0.01f ? finalColor : transparent);
                }
            }

            tex.Apply();
            return tex;
        }


        private static async void VersionCheck()
        {
            string VersionUsing = Settings.Version;
            string NewestVersion = string.Empty;
            bool outdated = false;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    NewestVersion = await client.GetStringAsync("https://shadowsmenu.jolyne108.workers.dev/version.txt");
                    NewestVersion = NewestVersion.Trim();
                }

                if (System.Version.TryParse(VersionUsing, out System.Version usingVersion) && System.Version.TryParse(NewestVersion, out System.Version newestVersion))
                {
                    int compared = newestVersion.CompareTo(usingVersion);

                    if (compared > 0)
                    {
                        MelonLogger.Msg($"[OUTDATED] A newer version of Shadows Menu is available! Please update to version {NewestVersion}");
                        outdated = true;
                    }
                    else if (compared < 0)
                    {
                        MelonLogger.Msg($"[BETA] You are using a beta version of Shadows Menu, beta version {VersionUsing}");
                        MelonCoroutines.Start(VersionNoti(false));
                    }
                    else if (compared == 0 && Settings.betaBuild)
                    {
                        MelonLogger.Msg($"[OUTDATED] You are using a beta build, but the full release of this build has been released. \nPlease update.");
                        outdated = true;
                    }
                    else
                    {
                        MelonLogger.Msg($"[UP TO DATE] Shadows Menu is up to date on Version {VersionUsing}");
                    }

                    if (outdated)
                    {
                        string newestString = newestVersion.ToString();

                        if (!PostVersion)
                        {
                            MelonCoroutines.Start(VersionNoti(true, newestString));
                            Application.OpenURL("https://discord.com/invite/2FzsKdvjMU");
                            PostVersion = true;
                        }
                        else
                        {
                            MelonCoroutines.Start(VersionNoti(true, newestString, true));
                        }
                    }

                }
                else
                {
                    MelonLogger.Warning($"[FAIL] Failed to compare versions. Current: {VersionUsing}, Newest: {NewestVersion}");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[FAIL] Version check failed!: {ex.Message}");
            }
        }

        private static IEnumerator VersionNoti(bool outdated, string newest = null, bool post = false)
        {
            if (!post)
                yield return new WaitForSeconds(10f);
            else
                yield return new WaitForSeconds(3f);

            if (post)
            {
                NotificationLib.SendNotification($"[<color=red>OUTDATED</color>] A new version has just been released!\nPlease update to {newest}");
                MelonLogger.Msg($"[OUTDATED] A newer version of Shadows Menu was just released! Please update to version {newest}");
                yield break;
            }

            if (betaBuild)
            {
                NotificationLib.SendNotification($"[<color=lime>EARLY-ACCESS</color>] You are using early build {Settings.Version},\nPlease report any issues found to the discord!"
                );
            }

            if (outdated)
            {
                NotificationLib.SendNotification($"[<color=red>OUTDATED</color>] You are using an outdated version, {Settings.Version},\nPlease update to {newest}"
                );
            }
        }




        public static Color SetMenuTheme()
        {
            switch (GUIColorInt)
            {
                case 0: return GUIColor = Color.cyan;
                case 1: return GUIColor = Color.blue;
                case 2: return GUIColor = Color.magenta;
                case 3: return GUIColor = Color.red;
                case 4: return GUIColor = Color.yellow;
                case 5: return GUIColor = Color.green;
                case 6: return GUIColor = Color.white;
                case 7: return GUIColor = Color.gray;
                case 8: return GUIColor = Color.HSVToRGB(RainbowColor, 1f, 1f);
            }
            return GUIColor = Color.white;
        }
    }
}
