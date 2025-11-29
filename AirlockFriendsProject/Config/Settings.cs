using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AirlockFriends.Config
{
    internal class Settings
    {
        public const string Version = "0.7";

        public static bool GUIEnabled = true;
        public static Color GUIColor = Color.blue;
        public static int GUIColorInt = 0;
        public static float RainbowColor = 0f;

        public static bool IsVR = false;
        public static bool InGame = false;
        public static bool GameRefsFound = false;

        public static string messageFriend = "";
        public static string friendCodeTyped = "";
        public static string messageContent = "";
        public static string FriendRequestText = "";

		public static bool CodeRecievced = false;
        public static string CurrentRoom = "Not in a room";

        public static float NotiDuration = 4f;
        public static bool ShowNotifications = true;
    }
}