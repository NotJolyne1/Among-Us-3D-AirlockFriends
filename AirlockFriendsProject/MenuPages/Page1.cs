using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Customization;
using Il2CppSG.Airlock.Minigames;
using Il2CppSG.Airlock.Roles;
using Il2CppSG.Airlock.Sabotage;
using Il2CppSG.Airlock.UI;
using Il2CppSG.Airlock.UI.TitleScreen;
using Il2CppSG.GlobalEvents.Variables;
using Il2CppSystem.Net;
using MelonLoader;
using AirlockFriends.Config;
using AirlockFriends.Managers;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace AirlockFriends.MenuPages
{
    public class MenuPage1
    {
        public static void Display()
        {
            float y = 50f;

            Settings.messageContent = GUI.TextField(new Rect(460f, y, 80f, 30f), Settings.messageContent);
            Settings.messageFriend = GUI.TextField(new Rect(540f, y, 80f, 30f), Settings.messageFriend);

            if (GUI.Button(new Rect(460f, y + 35f, 160f, 30f), "Send Message"))
            {
                _ = AirlockFriendsOperations.RPC_SendMessage(Settings.messageFriend, Settings.messageContent);
            }

            y += 70f;


            if (GUI.Button(new Rect(460f, y, 160f, 30f), "Friend Request"))
            {
                if (!string.IsNullOrEmpty(Settings.friendCodeTyped))
                {
                    _ = AirlockFriendsOperations.RPC_FriendshipRequest(Settings.friendCodeTyped);
                }
            }

            Settings.friendCodeTyped = GUI.TextField(new Rect(620f, y, 160f, 30f), Settings.friendCodeTyped);
            y += 30f;

            if (GUI.Button(new Rect(460f, y, 160f, 30f), "Authenticate"))
            {
                AirlockFriendsOperations.Initialize();
            }
            y += 30f;
        }

    }
}
