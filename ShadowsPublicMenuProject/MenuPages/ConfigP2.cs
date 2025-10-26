using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.UI;
using Il2CppSG.Airlock.UI.TitleScreen;
using Il2CppSG.Airlock.Util;
using MelonLoader;
using ShadowsPublicMenu.Config;
using ShadowsPublicMenu.Managers;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace ShadowsPublicMenu.MenuPages
{
    public class MenuPage2
    {
        public static void Display()
        {
            float y = 50f;

            Color SavedColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(new Rect(50f, y, 100f, 30f), GUIContent.none);
            GUI.color = SavedColor;

            if (GUI.Button(new Rect(20f, y, 30f, 30f), "◄") && Settings.SpeedBoost > 0f)
                Settings.SpeedBoost--;

            GUI.Label(new Rect(55f, y, 95f, 30f), $"Speed: {Settings.SpeedBoost}",
                      new GUIStyle(GUI.skin.label)
                      {
                          alignment = TextAnchor.MiddleCenter,
                      });

            if (GUI.Button(new Rect(150f, y, 30f, 30f), "►") && Settings.SpeedBoost < 50f)
                Settings.SpeedBoost++;

            y += 30f;

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(new Rect(50f, y, 100f, 30f), GUIContent.none);
            GUI.color = SavedColor;

            if (GUI.Button(new Rect(20f, y, 30f, 30f), "◄") && Settings.NotiDuration > 0f)
                Settings.NotiDuration--;

            GUI.Label(new Rect(55f, y, 95f, 30f), $"Notification Time: {Settings.NotiDuration}",
                      new GUIStyle(GUI.skin.label)
                      {
                          alignment = TextAnchor.MiddleCenter,
                      });

            if (GUI.Button(new Rect(150f, y, 30f, 30f), "►") && Settings.NotiDuration < 10f)
                Settings.NotiDuration++;
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Show Notifications: {Settings.ShowNotifications}"))
            {
                Settings.ShowNotifications = !Settings.ShowNotifications;
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), "Clear Notifications"))
                NotificationLib.ClearNotifications();
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Toggle FPS Bar: {Settings.showFpsBar}"))
            {
                Settings.showFpsBar = !Settings.showFpsBar;
                string Toggled = Settings.showFpsBar ? "<color=green>ENABLED</color>" : "<color=red>DISABLED</color>";
                NotificationLib.SendNotification($"[{Toggled}] Toggles the top middle FPS bar");
            }
            y += 30f;

            if (GUI.Button(new Rect(20f, y, 160f, 30f), $"Menu Theme: {Helpers.GetColorName(Settings.GUIColorInt)}"))
            {
                if (Settings.GUIColorInt < 8)
                    Settings.GUIColorInt++;
                else
                    Settings.GUIColorInt = 0;
                Main.SetMenuTheme();
            }
        }
    }
}
