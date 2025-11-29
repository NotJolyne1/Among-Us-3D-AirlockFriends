using System.Net.Mime;
using AirlockFriends.Managers;
using HarmonyLib;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.UI.TitleScreen;
using MelonLoader;
using UnityEngine;

namespace AirlockFriends.Patches
{
    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.OnPopupButton1Pressed))]
    public class OnButton1ClickedPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (Main.AFBanned && !Main.AppealButtonClicked)
            {
                Application.OpenURL("https://discord.gg/S2JzzfF2sr");
                Main.AppealButtonClicked = true;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.OnPopupButton2Pressed))]
    public class OnButton2ClickedPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!Main.AFBanned && !Main.AppealButtonClicked)
            {
                Main.HasShownBanMessage = true;
            }
        }
    }
}
