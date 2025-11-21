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
        private static Rect WindowDesign = new Rect(300, 100, 700, 500);
        private static Vector2 scroll;
        private static bool onRequestsPage = false;
        private static readonly List<FriendEntry> friends = new List<FriendEntry>();
        private static readonly List<string> friendRequests = new List<string>();
        private static readonly List<InviteData> ActiveInvites = new();
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

        private class ChatMessage
        {
            public string Sender;
            public string Text;
            public Color TextColor;
            public float TimeAdded;

            public ChatMessage(string s, string t, Color color = default)
            {
                Sender = s;
                Text = t;
                TextColor = color == default ? Color.white : color;
                TimeAdded = Time.time;
            }
        }

        private static Dictionary<string, List<ChatMessage>> FriendConversations = new();
        private static FriendEntry CurrentChatOpen = null;
        private static Vector2 chatScroll;
        private static string ChatInput = "";

        private static void AddMessage(string friendCode, string sender, string message, Color color = default)
        {
            if (!FriendConversations.ContainsKey(friendCode))
                FriendConversations[friendCode] = new List<ChatMessage>();

            FriendConversations[friendCode].Add(new ChatMessage(sender, message, color));
        }

        public static void ReceiveChatMessage(string fromCode, string message, bool SendFailed = false, bool MyMessage = false)
        {
            if (SendFailed && MyMessage)
                return;

            Color color = SendFailed ? Color.red : Color.white;
            AddMessage(fromCode, fromCode, message, color);
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
            WindowDesign = GUI.Window(987654, WindowDesign, (GUI.WindowFunction)DrawWindow, "");
            DrawInvites();
        }

        public static void AddFriend(string name, string status, string code)
        {
            friends.Add(new FriendEntry(name, status, code));
        }

        public static void AddFriends()
        {
            // debug
            AddFriend("Jolyne", "Online", "AF-BCE72C");
            AddFriend("tnx", "Online", "AF-04C8CC");
            AddFriend("Warp", "Online", "AF-568B46");
            AddFriend("Someone", "Offline", "AF-05195E");

            friendRequests.Add("AF-9123AB");
            friendRequests.Add("AF-0019F3");

            NewFriendRequest = true;
        }


        private static void DrawWindow(int id)
        {
            GUI.Box(new Rect(0, 0, WindowDesign.width, WindowDesign.height), $"AIRLOCK FRIENDS: BETA {Settings.Version}");

            /*
            GUI.Label(new Rect(0, 10, WindowDesign.width, 30), $"AIRLOCK FRIENDS: BETA {Settings.Version}", new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.3f, 1f) }
                });
            */

            if (CurrentChatOpen != null)
            {
                DrawChatWindow(CurrentChatOpen);
                GUI.DragWindow(new Rect(0, 0, WindowDesign.width, 30));
                return;
            }

            Color SavedColor = GUI.color;
            if (NewFriendRequest) GUI.color = Color.red;

            if (!onRequestsPage)
            {
                if (GUI.Button(new Rect(10, 10, 150, 30), $"Friend Requests ({friendRequests.Count})"))
                {
                    onRequestsPage = true;
                    NewFriendRequest = false;
                }
            }

            GUI.color = SavedColor;

            if (!onRequestsPage)
            {
                if (GUI.Button(new Rect(WindowDesign.width - 140, 10, 130, 30), "Authenticate"))
                    AirlockFriendsOperations.Initialize();
            }

            if (onRequestsPage)
                DrawRequestsPage();
            else
                DrawFriendsList();

            GUI.DragWindow(new Rect(0, 0, WindowDesign.width, 30));
        }

        private static void DrawFriendsList()
        {
            var listRect = new Rect(10, 50f, WindowDesign.width - 20, WindowDesign.height - 60);
            var viewRect = new Rect(0, 0, listRect.width - 20, friends.Count * 60f);

            GUI.BeginGroup(listRect);
            scroll = GUI.BeginScrollView(new Rect(0, 0, listRect.width, listRect.height), scroll, viewRect);

            float y = 0f;

            for (int i = 0; i < friends.Count; i++)
            {
                var FriendData = friends[i];

                GUI.Box(new Rect(0, y, viewRect.width, 55), "");

                GUI.Label(new Rect(10, y + 15, 150, 25),
                    FriendData.Name,
                    new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

                Color statusColor =
                    FriendData.Status == "Online" ? Color.green :
                    FriendData.Status == "Offline" ? Color.red :
                    Color.yellow;

                GUI.Label(new Rect(170, y + 17, 100, 25),
                    FriendData.Status,
                    new GUIStyle(GUI.skin.label) { normal = { textColor = statusColor } });

                float w = 70;
                Color saved = GUI.color;

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(280, y + 12, w, 30), "Message"))
                {
                    CurrentChatOpen = FriendData;
                    ChatInput = "";

                    if (!FriendConversations.ContainsKey(FriendData.FriendCode))
                        FriendConversations[FriendData.FriendCode] = new List<ChatMessage>();
                }

                GUI.color = Color.red;
                if (GUI.Button(new Rect(280 + w + 10, y + 12, w, 30), "Unfriend"))
                {
                    friends.RemoveAt(i);
                    i--;
                    GUI.color = saved;
                    continue;
                }

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(280 + 2 * (w + 10), y + 12, w, 30), "Invite"))
                {
                    ReceiveInvite(FriendData.FriendCode, "ROOM123");
                }

                if (GUI.Button(new Rect(280 + 3 * (w + 10), y + 12, w, 30), "Join"))
                    MelonLogger.Msg($"Join {FriendData.Name}");

                GUI.color = saved;
                y += 60;
            }

            GUI.EndScrollView();
            GUI.EndGroup();
        }


        private static void DrawRequestsPage()
        {
            if (GUI.Button(new Rect(10, 50, 80, 30), "Back"))
                onRequestsPage = false;

            GUI.Label(new Rect(0, 50, WindowDesign.width, 30),
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
                GUI.Box(new Rect(10, y, WindowDesign.width - 20, 60), "");

                GUI.Label(new Rect(20, y + 17, 300, 25), $"Request: {req}",
                    new GUIStyle(GUI.skin.label) { fontSize = 16 });

                Color saved = GUI.color;

                GUI.color = Color.green;
                if (GUI.Button(new Rect(WindowDesign.width - 220, y + 15, 90, 30), "Accept"))
                {
                    friendRequests.RemoveAt(i);
                    i--;
                    GUI.color = saved;
                    continue;
                }

                GUI.color = Color.red;
                if (GUI.Button(new Rect(WindowDesign.width - 120, y + 15, 90, 30), "Reject"))
                {
                    friendRequests.RemoveAt(i);
                    i--;
                    GUI.color = saved;
                    continue;
                }

                GUI.color = saved;
                y += 70;
            }
        }

        private static void DrawChatWindow(FriendEntry FriendInfo)
        {
            GUI.Label(new Rect(0, 25, WindowDesign.width, 30),
                $"Direct Message With {FriendInfo.Name}",
                new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 17,
                    fontStyle = FontStyle.Bold
                });

            if (GUI.Button(new Rect(10, 10, 80, 30), "Back"))
            {
                CurrentChatOpen = null;
                return;
            }

            var messages = FriendConversations.ContainsKey(FriendInfo.FriendCode) ? FriendConversations[FriendInfo.FriendCode] : new List<ChatMessage>();

            Rect ChatDesign = new Rect(10, 50, WindowDesign.width - 20, WindowDesign.height - 120);

            float totalHeight = 0f;
            GUIStyle HeightFix = new GUIStyle(GUI.skin.label) { richText = true };
            foreach (var msg in messages)
            {
                HeightFix.normal.textColor = msg.TextColor;
                float height = HeightFix.CalcHeight(new GUIContent(msg.Text), ChatDesign.width - 20);
                totalHeight += height + 30;
            }

            Rect ViewingDesign = new Rect(0, 0, ChatDesign.width - 20, totalHeight);

            GUI.BeginGroup(ChatDesign);
            chatScroll = GUI.BeginScrollView(new Rect(0, 0, ChatDesign.width, ChatDesign.height), chatScroll, ViewingDesign);

            float y = 0f;
            foreach (var msg in messages)
            {
                HeightFix.normal.textColor = msg.TextColor;
                float height = HeightFix.CalcHeight(new GUIContent(msg.Text), ChatDesign.width - 20);

                GUI.Box(new Rect(0, y, ViewingDesign.width, height + 25), "");

                GUI.Label(new Rect(10, y + 5, ViewingDesign.width - 20, 20),
                    msg.Sender,
                    new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

                GUI.Label(new Rect(10, y + 25, ViewingDesign.width - 20, height), msg.Text, HeightFix);

                y += height + 30;
            }

            GUI.EndScrollView();
            GUI.EndGroup();

            ChatInput = GUI.TextField(new Rect(10, WindowDesign.height - 60, WindowDesign.width - 140, 40), ChatInput);

            if (GUI.Button(new Rect(WindowDesign.width - 120, WindowDesign.height - 60, 110, 40), "Send"))
            {
                if (!string.IsNullOrWhiteSpace(ChatInput))
                {
                    _ = AirlockFriendsOperations.RPC_SendMessage(FriendInfo.FriendCode, ChatInput);

                    AddMessage(FriendInfo.FriendCode, "You", ChatInput);
                    ChatInput = "";
                }
            }
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
                Rect r = new Rect(WindowDesign.x + 10, WindowDesign.yMax + 10 + (i * 75), 300, 70);

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
                    ActiveInvites.Remove(inv);
                    break;
                }

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Ignore"))
                {
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
