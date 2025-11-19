using Il2CppSteamworks;
using MelonLoader;
using AirlockFriends.Config;
using AirlockFriends.Managers;
using UnityEngine;
using UnityEngine.InputSystem;
using static AirlockFriends.Config.Settings;
using static AirlockFriends.Config.GameReferences;

[assembly: MelonInfo(typeof(AirlockFriends.Main), "AirlockFriends", Settings.Version, "Shadoww.py", "https://github.com/NotJolyne1/ShadowsAmongUsVRHacks/releases/tag/Release")]
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
        public static bool LockdownActive = false;
        public static bool FullLockdownActive = false;

        public override void OnApplicationQuit()
        {
            MelonLogger.Msg("Loading Airlock Friends");
        }

        [System.Obsolete]
        public override void OnApplicationStart()
        {
            IsVR = UnityEngine.Application.productName.Contains("VR");
        }


        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            InGame = sceneName != "Boot" && sceneName != "Title";
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
                refreshGameRefs();
        }




        public override void OnGUI()
        {
            GUI.color = Settings.GUIColor;
            if (!GUIEnabled || !passed || FullLockdownActive)
                return;

            try
            {
                DrawMainMenu();
            }


            catch (System.Exception e)
            {
                MelonLogger.Warning($"[FAIL] Something went wrong! Failed at ModManager.Update(), error: {e}");
            }
        }


        private void DrawMainMenu()
        {
            GUI.Box(new Rect(460f, 0f, 160f, 20f), "Airlock Friends");


            if (CurrentPage < 1) CurrentPage = 1;
            else if (CurrentPage >= 3) CurrentPage = 1;

            switch (CurrentPage)
            {
                case 1: MenuPages.MenuPage1.Display(); break;
            }
        }
    }
}
