using AirlockFriends.Managers;
using UnityEngine;
using System.Collections.Generic;
using MelonLoader;
using AirlockFriends.Config;
using System.Linq;

namespace AirlockFriends.MenuPages
{
    public class MenuPage1
    {
        private static Rect windowRect = new Rect(300, 100, 700, 500);

        private static Vector2 scroll;
        private static bool showMessagePopup;
        private static FriendEntry currentMessageTarget;
        private static string messageText = "";

        private static bool onRequestsPage = false;

        private static readonly List<FriendEntry> friends = new List<FriendEntry>();
        private static readonly List<string> friendRequests = new List<string>();
        private static readonly List<InviteData> ActiveInvites = new();
        private static string Header => $"AIRLOCK FRIENDS: BETA {Settings.Version}";
        private static bool NewFriendRequest = false;

        private class InviteData
        {
            public string FriendCode;
            public string RoomID;
            public float TimeCreated;
        }

        private class FriendEntry
        {
            public string Name;
            public string Status;
            public string FriendCode;

            public FriendEntry(string n, string s, string c)
            {
                Name = n;
                Status = s;
                FriendCode = c;
            }
        }

        public static void ReceiveInvite(string friendCode, string roomID)
        {
            ActiveInvites.Add(new InviteData
            {
                FriendCode = friendCode,
                RoomID = roomID,
                TimeCreated = Time.time
            });
        }

        public static void Display()
        {
            windowRect = GUI.Window(987654, windowRect, (GUI.WindowFunction)DrawWindow, "");
            DrawInvites();
        }

        public static void AddFriend(string name, string status, string code)
        {
            friends.Add(new FriendEntry(name, status, code));
        }

        public static void AddFriends()
        {
            // debug
            AddFriend("JolyneM", "Online", "AF-BCE72C");
            AddFriend("JolyneA", "Offline", "AF-05295E");
            AddFriend("JolyneT", "Offline", "AF-05195E");

            friendRequests.Add("AF-9123AB");
            friendRequests.Add("AF-0019F3");

            NewFriendRequest = true;
        }

        private static void DrawWindow(int id)
        {
            GUI.Box(new Rect(0, 0, windowRect.width, windowRect.height), "");

            GUI.Label(new Rect(0, 10, windowRect.width, 30), Header,
                new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.3f, 1f) }
                });

            if (!showMessagePopup)
            {
                Color prev = GUI.color;
                if (NewFriendRequest) GUI.color = Color.red;
                if (!onRequestsPage)
                {
                    if (GUI.Button(new Rect(10, 10, 150, 30), $"Friend Requests ({friendRequests.Count})"))
                    {
                        onRequestsPage = true;
                        NewFriendRequest = false;
                    }
                }
                GUI.color = prev;
            }

            if (!showMessagePopup && !onRequestsPage)
            {
                if (GUI.Button(new Rect(windowRect.width - 140, 10, 130, 30), "Authenticate"))
                    AirlockFriendsOperations.Initialize();
            }

            if (!showMessagePopup)
            {
                if (onRequestsPage)
                    DrawRequestsPage();
                else
                    DrawFriendsList();
            }

            if (showMessagePopup)
                DrawMessagePopup();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, 30));
        }

        private static void DrawFriendsList()
        {
            var listRect = new Rect(10, 50f, windowRect.width - 20, windowRect.height - 60);
            var viewRect = new Rect(0, 0, listRect.width - 20, friends.Count * 60f);

            GUI.BeginGroup(listRect);
            scroll = GUI.BeginScrollView(new Rect(0, 0, listRect.width, listRect.height), scroll, viewRect);

            float y = 0f;

            for (int i = 0; i < friends.Count; i++)
            {
                var f = friends[i];

                GUI.Box(new Rect(0, y, viewRect.width, 55), "");

                GUI.Label(new Rect(10, y + 15, 150, 25),
                    f.Name,
                    new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

                Color statusColor =
                    f.Status == "Online" ? Color.green :
                    f.Status == "Offline" ? Color.red :
                    Color.yellow;

                GUI.Label(new Rect(170, y + 17, 100, 25),
                    f.Status,
                    new GUIStyle(GUI.skin.label) { normal = { textColor = statusColor } });

                float x = 280;
                float w = 70;
                var SavedColor = GUI.color;

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(x, y + 12, w, 30), "Message"))
                {
                    showMessagePopup = true;
                    currentMessageTarget = f;
                    messageText = "";
                }

                GUI.color = Color.red;
                if (GUI.Button(new Rect(x + w + 10, y + 12, w, 30), "Unfriend"))
                {
                    friends.RemoveAt(i);
                    i--;
                    continue;
                }

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(x + 2 * (w + 10), y + 12, w, 30), "Invite"))
                {
                    ReceiveInvite("Elon Musk", "FIEUCD");
                    ReceiveInvite("Mr.Beast", "FIP3UC");
                    ReceiveInvite("SomeMexicanGuy", "F2P6PO");

                }

                if (GUI.Button(new Rect(x + 3 * (w + 10), y + 12, w, 30), "Join"))
                    MelonLogger.Msg($"Join {f.Name}");

                GUI.color = SavedColor;
                y += 60;
            }

            GUI.EndScrollView();
            GUI.EndGroup();
        }

        private static void DrawRequestsPage()
        {
            if (GUI.Button(new Rect(10, 50, 80, 30), "Back"))
                onRequestsPage = false;

            GUI.Label(new Rect(0, 50, windowRect.width, 30),
                "Friend Requests",
                new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                });

            float y = 100;
            for (int i = 0; i < friendRequests.Count; i++)
            {
                string req = friendRequests[i];
                GUI.Box(new Rect(10, y, windowRect.width - 20, 60), "");

                GUI.Label(new Rect(20, y + 17, 300, 25), $"Request: {req}",
                    new GUIStyle(GUI.skin.label) { fontSize = 16 });

                var SavedColor = GUI.color;
                GUI.color = Color.green;
                if (GUI.Button(new Rect(windowRect.width - 220, y + 15, 90, 30), "Accept"))
                {
                    MelonLogger.Msg($"Accepted {req}");
                    friendRequests.RemoveAt(i);
                    i--;
                    continue;
                }

                GUI.color = Color.red;
                if (GUI.Button(new Rect(windowRect.width - 120, y + 15, 90, 30), "Reject"))
                {
                    MelonLogger.Msg($"Rejected {req}");
                    friendRequests.RemoveAt(i);
                    i--;
                    continue;
                }
                GUI.color = SavedColor;

                y += 70;
            }
        }

        private static void DrawMessagePopup()
        {
            GUI.enabled = false;
            GUI.Box(new Rect(0, 0, windowRect.width, windowRect.height), "");
            GUI.enabled = true;

            Rect rect = new Rect(
                windowRect.width / 2 - 150,
                windowRect.height / 2 - 60,
                300,
                120);

            GUI.Box(rect, "");

            GUI.Label(new Rect(rect.x, rect.y + 5, rect.width, 25),
                "Message " + currentMessageTarget.Name,
                new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                });

            messageText = GUI.TextField(new Rect(rect.x + 10, rect.y + 40, 280, 30), messageText);

            var SavedColor = GUI.color;
            GUI.color = Color.green;
            if (GUI.Button(new Rect(rect.x + 10, rect.y + 80, 130, 30), "Send"))
            {
                _ = AirlockFriendsOperations.RPC_SendMessage(currentMessageTarget.FriendCode, messageText);
                showMessagePopup = false;
            }

            GUI.color = Color.red;
            if (GUI.Button(new Rect(rect.x + 160, rect.y + 80, 130, 30), "Cancel"))
                showMessagePopup = false;

            GUI.color = SavedColor;
        }

        private static void DrawInvites()
        {

            for (int i = ActiveInvites.Count - 1; i >= 0; i--)
            {
                if (Time.time - ActiveInvites[i].TimeCreated > 30f)
                    ActiveInvites.RemoveAt(i);
            }

            for (int i = 0; i < ActiveInvites.Count; i++)
            {
                var inv = ActiveInvites[i];
                Rect r = new Rect(windowRect.x + 10, windowRect.yMax + 10 + (i * (70 + 5)), 300, 70);

                GUI.color = new Color(0, 0, 0, 0.65f);
                GUI.Box(r, "");
                GUI.color = Color.white;

                GUILayout.BeginArea(r);
                GUILayout.Label($"{inv.FriendCode} invited you!");
                GUILayout.Label($"Room ID: {inv.RoomID}");

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Accept"))
                {
                    MelonLogger.Msg($"Accepted invite from {inv.FriendCode} for room {inv.RoomID}");
                    ActiveInvites.Remove(inv);
                    break;
                }

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Ignore"))
                {
                    MelonLogger.Msg($"Ignored invite from {inv.FriendCode}");
                    ActiveInvites.Remove(inv);
                    break;
                }
                GUILayout.EndHorizontal();

                GUI.backgroundColor = Color.white;
                GUILayout.EndArea();
            }
        }
    }
}
