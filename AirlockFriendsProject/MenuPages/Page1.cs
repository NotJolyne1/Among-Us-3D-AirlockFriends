    using AirlockFriends.Managers;
    using UnityEngine;
    using System.Collections.Generic;
    using MelonLoader;
    using AirlockFriends.Config;
    using System.Linq;
    using Il2CppSG.Airlock.Network;
    using Il2CppSG.Airlock.XR;
using System;
using System.Threading.Tasks;

namespace AirlockFriends.UI
    {
        public class FriendGUI
        {
            private static Rect WindowDesign = new Rect(300, 100, 700, 500);
            private static Vector2 scroll;
            private static readonly List<FriendInfo> friends = new List<FriendInfo>();
            public static readonly List<string> FriendRequests = new List<string>();
            private static readonly List<InviteData> ActiveInvites = new();
            private static readonly List<JoinRequestData> JoinRequests = new();
            public static bool NewFriendRequest = false;
            private static bool onSettingsPage = false;
            private static bool FriendRequestsPage = false;
            private static int JoinIndex = 0;
            private static readonly string[] JoinSettings = new string[] { "Joinable", "Ask Me", "Private" };

            private static bool _allowFriendRequests = AirlockFriendsOperations.AllowFriendRequests;
            private static bool _allowMessages = AirlockFriendsOperations.AllowMessages;
            private static bool _allowInvites = AirlockFriendsOperations.AllowInvites;




        private class InviteData
        {
            public string FriendCode;
            public string RoomID;
            public float TimeCreated;
        }

        private class JoinRequestData
        {
            public string FriendCode;
            public float TimeCreated;
        }

        public static FriendInfo GetFriend(string code) => friends.FirstOrDefault(SearchFriend => SearchFriend.FriendCode == code);
        public class FriendInfo
        {
            public string FriendCode { get; private set; }

            public string Name { get; set; }
            public string Status { get; set; }
            public string RoomCode { get; set; }
            public string Privacy { get; set; }

            public bool IsOnline => Status == "Online";

            public FriendInfo(string code)
            {
                FriendCode = code;
                Name = "Unknown";
                Status = "Offline";
                RoomCode = "";
                Privacy = "FriendsOnly";
            }

            public void Update(string name, string status, string room)
            {
                Name = name;
                Status = status;
                RoomCode = room;
            }
        }


        private class DirectMessage
        {
            public string Sender;
            public string Text;
            public Color TextColor;
            public float Timestamp;

            public DirectMessage(string sender, string msg, Color color = default)
            {
                Sender = sender;
                Text = msg;
                TextColor = color == default ? Color.white : color;
                Timestamp = Time.time;
            }
        }

        private static Dictionary<string, List<DirectMessage>> FriendConversations = new();
        private static FriendInfo CurrentChatOpen = null;
        private static Vector2 chatScroll;
        private static string ChatInput = "";

        private static void AddMessage(string friendCode, string sender, string message, Color color = default)
        {
            if (!FriendConversations.ContainsKey(friendCode))
                FriendConversations[friendCode] = new List<DirectMessage>();

            FriendConversations[friendCode].Add(new DirectMessage(sender, message, color));
        }

        public static void ReceiveDirectMessage(string fromCode, string name, string message, bool SendFailed = false, bool MyMessage = false)
        {
            Color color = SendFailed ? Color.red : Color.white;
            string nameToShow = MyMessage && SendFailed ? "System" : name;
            AddMessage(fromCode, nameToShow, message, color);
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

        public static void ReceiveJoinRequest(string friendCode)
        {
            if (!Settings.InGame)
            {
                _ = AirlockFriendsOperations.RPC_RespondToJoin(friendCode, false, false);
                return;
            }

            if (JoinRequests.Any(request => request.FriendCode == friendCode))
                return;

            if (AirlockFriendsOperations.JoinPrivacy == "Private")
                return;

            if (Settings.InGame)
                MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(friendCode, name => { NotificationLib.QueueNotification($"[<color=magenta>JOIN REQUEST</color>] <color=lime>{name}</color> wants to join you!"); }));

            if (AirlockFriendsOperations.JoinPrivacy == "Ask Me")
            {
                JoinRequests.Add(new JoinRequestData
                {
                    FriendCode = friendCode,
                    TimeCreated = Time.time
                });
            }

            else if (AirlockFriendsOperations.JoinPrivacy == "Joinable")
                _ = AirlockFriendsOperations.RPC_RespondToJoin(friendCode, true);
            else
                return;
        }

        public static void Update()
        {
            WindowDesign = GUI.Window(1653, WindowDesign, (GUI.WindowFunction)DrawWindow, "");
            DrawInvites();
            DrawJoinRequests();
        }



        public static void UpdateFriend(string name, string online, string friendCode, string roomID)
        {
            if (name.Length > 16)
                name = name.Substring(0, 16);

            if (roomID.Length > 6)
                return;

            var friend = GetFriend(friendCode);

            if (friend == null)
            {
                friend = new FriendInfo(friendCode);
                friends.Add(friend);
            }

            friend.Update(name, online, roomID);
        }



        private static void DrawWindow(int id)
        {
            GUI.Box(new Rect(0, 0, WindowDesign.width, WindowDesign.height), $"AIRLOCK FRIENDS: BETA {Settings.Version}");

            if (Main.AFBanned)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
                GUI.Box(new Rect(10, 40, WindowDesign.width - 20, WindowDesign.height - 50), "");
                GUI.color = prev;

                GUIStyle BanTitleDesign = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    richText = true,
                    normal = { textColor = Color.red }
                };

                GUI.Label(
                    new Rect(0, 60, WindowDesign.width, 40),
                    "<b>Your Airlock Friends account is blacklisted</b>",
                    BanTitleDesign
                );

                GUIStyle BanDesign = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 19,
                    richText = true,
                    normal = { textColor = Color.white }
                };

                GUI.Label(
                    new Rect(20, 110, WindowDesign.width - 40, 200),
                    "This ban will <b>Never</b> expire\n\n" +
                    "If you believe this is a mistake, you can join the discord and appeal.\n\n" +
                    "You can no longer use Airlock Friends.",
                    BanDesign
                );

                // Join Discord button (centered)

                var savedColor = GUI.color;
                GUI.color = Color.green;
                if (GUI.Button(new Rect((WindowDesign.width - 180f) / 2f, WindowDesign.height / 2f + 5f, 180f, 35f), "Join Discord"))
                {
                    Application.OpenURL("https://discord.gg/S2JzzfF2sr");
                }
                GUI.color = savedColor;


                GUI.DragWindow(new Rect(0, 0, WindowDesign.width, 30));
                return;
            }


            if (onSettingsPage)
            {
                DrawSettingsPage();
                GUI.DragWindow(new Rect(0, 0, WindowDesign.width, 30));
                return;
            }

            if (CurrentChatOpen != null)
            {
                DrawChatWindow(CurrentChatOpen);
                GUI.DragWindow(new Rect(0, 0, WindowDesign.width, 30));
                return;
            }

            Color SavedColor = GUI.color;
            if (NewFriendRequest) GUI.color = Color.red;

            if (!FriendRequestsPage)
            {
                if (GUI.Button(new Rect(10, 10, 150, 30), $"Friend Requests ({FriendRequests.Count})"))
                {
                    FriendRequestsPage = true;
                    NewFriendRequest = false;
                }
            }
            GUI.color = SavedColor;

            if (!FriendRequestsPage)
            {
                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(WindowDesign.width - 140, 10, 130, 30), "Settings"))
                    onSettingsPage = !onSettingsPage;

                GUI.color = SavedColor;
            }

            if (FriendRequestsPage)
                DrawRequestsPage();
            else
                DrawFriendsList();

            GUI.DragWindow(new Rect(0, 0, WindowDesign.width, 30));
        }

        private static bool AddFriendPage = false;
        private static string AddFriendInput = "";

        private static void DrawFriendsList()
        {
            if (AddFriendPage)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.95f);
                GUI.Box(new Rect(0, 0, WindowDesign.width, WindowDesign.height), "");
                GUI.color = Color.white;


                Rect popup = new Rect((WindowDesign.width - 500) / 2, (WindowDesign.height - 270) / 2, 500, 270);

                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.98f);
                GUI.Box(popup, "");
                GUI.color = Color.white;

                GUIStyle BanTitleDesign = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(popup.x + 10, popup.y + 10, 500 - 20, 30), "Add a Friend", BanTitleDesign);

                GUIStyle LabelDesign = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(popup.x + 10, popup.y + 50, 500 - 20, 50), "Enter the Friend Code of the person you want to send a request to:", LabelDesign);

                AddFriendInput = GUI.TextField(new Rect(popup.x + 10, popup.y + 105, 500 - 20, 35), AddFriendInput);

                GUIStyle noteStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    wordWrap = true,
                    normal = { textColor = Color.yellow }
                };
                GUI.Label(new Rect(popup.x + 10, popup.y + 150, 500 - 20, 60), "Note: Friends can send you messages, request to join your room, and invite you if enabled. You can disable all these in your settings.", noteStyle);

                GUIStyle FriendCodeDesign = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(popup.x + 10, popup.y + 190, 500 - 20, 25), $"Your Friend Code: {AirlockFriendsOperations.FriendCode}", FriendCodeDesign);

                if (GUI.Button(new Rect(popup.x + 10, popup.y + 220, 220, 35), "Send Friend Request"))
                {
                    MelonLogger.Msg($"Friend request sent to: {AddFriendInput}");
                    _ = AirlockFriendsOperations.RPC_FriendshipRequest(AddFriendInput);
                    AddFriendPage = false;
                    AddFriendInput = "";
                }

                if (GUI.Button(new Rect(popup.x + 270, popup.y + 220, 220, 35), "Cancel"))
                {
                    AddFriendPage = false;
                    AddFriendInput = "";
                }
                return;
            }
            var listRect = new Rect(10, 50f, WindowDesign.width - 20, WindowDesign.height - 60);
            var viewRect = new Rect(0, 0, listRect.width - 20, friends.Count * 60f + 60);

            GUI.BeginGroup(listRect);
            scroll = GUI.BeginScrollView(new Rect(0, 0, listRect.width, listRect.height), scroll, viewRect);

            float y = 0f;
            for (int i = 0; i < friends.Count; i++)
            {
                var FriendData = friends[i];
                GUI.Box(new Rect(0, y, viewRect.width, 55), "");
                GUI.Label(new Rect(10, y + 15, 150, 25), FriendData.Name, new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

                Color statusColor = FriendData.Status == "Online" ? Color.green : FriendData.Status == "Offline" ? Color.red : Color.yellow;
                GUI.Label(new Rect(170, y + 17, 100, 25), FriendData.Status, new GUIStyle(GUI.skin.label) { normal = { textColor = statusColor } });

                float w = 70;
                Color saved = GUI.color;

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(280, y + 12, w, 30), "Message"))
                {
                    CurrentChatOpen = FriendData;
                    ChatInput = "";
                    if (!FriendConversations.ContainsKey(FriendData.FriendCode))
                        FriendConversations[FriendData.FriendCode] = new List<DirectMessage>();
                }

                GUI.color = Color.red;
                if (GUI.Button(new Rect(280 + w + 10, y + 12, w, 30), "Unfriend"))
                {
                    _ = AirlockFriendsOperations.RPC_FriendRemove(FriendData.FriendCode);
                    friends.RemoveAt(i);
                    i--;
                    GUI.color = saved;
                    continue;
                }

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(280 + 2 * (w + 10), y + 12, w, 30), "Request Join"))
                    _ = AirlockFriendsOperations.RPC_RequestJoin(FriendData.FriendCode);



                if (GUI.Button(new Rect(280 + 3 * (w + 10), y + 12, w, 30), "Update"))
                    _ = AirlockFriendsOperations.RPC_GetFriends();


                GUI.color = saved;
                y += 60;
            }

            if (GUI.Button(new Rect(0, y + 10, 150, 30), "Add Friend"))
                AddFriendPage = true;


            if (GUI.Button(new Rect(10, WindowDesign.height - 40, 150, 30), "Settings"))
            {
                onSettingsPage = true;
            }


            GUI.EndScrollView();
            GUI.EndGroup();
        }



        private static void DrawRequestsPage()
        {
            if (GUI.Button(new Rect(10, 50, 80, 30), "Back"))
                FriendRequestsPage = false;

            GUI.Label(new Rect(0, 50, WindowDesign.width, 30), "Friend Requests", new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold
                });

            float y = 100;
            for (int i = 0; i < FriendRequests.Count; i++)
            {
                int index = i;
                string friendCode = FriendRequests[i];
                MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(friendCode, req =>
                {
                    GUI.Box(new Rect(10, y, WindowDesign.width - 20, 60), "");

                    GUI.Label(new Rect(20, y + 17, 300, 25), $"Request: {req}",
                        new GUIStyle(GUI.skin.label) { fontSize = 16 });

                    Color saved = GUI.color;

                    GUI.color = Color.green;
                    if (GUI.Button(new Rect(WindowDesign.width - 220, y + 15, 90, 30), "Accept"))
                    {
                        _ = AirlockFriendsOperations.RPC_FriendAccept(friendCode);
                        FriendRequests.RemoveAt(index);
                        GUI.color = saved;
                        return;
                    }

                    GUI.color = Color.red;
                    if (GUI.Button(new Rect(WindowDesign.width - 120, y + 15, 90, 30), "Reject"))
                    {
                        _ = AirlockFriendsOperations.RPC_FriendReject(friendCode);
                        FriendRequests.RemoveAt(index);
                        GUI.color = saved;
                        return;
                    }

                    GUI.color = saved;
                }));
                y += 70;
            }
        }

        private static void DrawChatWindow(FriendInfo FriendInfo)
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

            var messages = FriendConversations.ContainsKey(FriendInfo.FriendCode) ? FriendConversations[FriendInfo.FriendCode] : new List<DirectMessage>();

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
                    if (ChatInput.Length > 500)
                    {
                        NotificationLib.QueueNotification("[<color=red>FAIL</color>] Message too long");
                        ReceiveDirectMessage(FriendInfo.FriendCode, "System", "Message too long", true, true);
                        ChatInput = "";
                        return;
                    }

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

        private static void DrawJoinRequests()
        {
            for (int i = JoinRequests.Count - 1; i >= 0; i--)
            {
                if (Time.time - JoinRequests[i].TimeCreated > 30f)
                    JoinRequests.RemoveAt(i);
            }

            for (int i = 0; i < JoinRequests.Count; i++)
            {
                var req = JoinRequests[i];
                Rect bg = new Rect(WindowDesign.x, WindowDesign.yMax + 10 + (i * 75), 300, 50);

                if (!Settings.InGame)
                {
                    _ = AirlockFriendsOperations.RPC_RespondToJoin(req.FriendCode, false, false);
                    JoinRequests.Remove(req);
                    break;
                }

                GUI.color = new Color(0, 0, 0, 0.65f);
                GUI.Box(bg, "");
                GUI.color = Color.white;

                GUILayout.BeginArea(bg);
                GUILayout.Label($"{GetFriend(req.FriendCode).Name} wants to join you!");


                GUILayout.BeginHorizontal();
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Accept"))
                {
                    _ = AirlockFriendsOperations.RPC_RespondToJoin(req.FriendCode, true);
                    JoinRequests.Remove(req);
                    break;
                }

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Ignore"))
                {
                    _ = AirlockFriendsOperations.RPC_RespondToJoin(req.FriendCode, false);
                    JoinRequests.Remove(req);
                    break;
                }
                GUILayout.EndHorizontal();

                GUI.backgroundColor = Color.white;
                GUILayout.EndArea();
            }
        }
        private static bool initializedSettings = false;

        private static void DrawSettingsPage()
        {
            if (!initializedSettings)
            {
                _allowFriendRequests = AirlockFriendsOperations.AllowFriendRequests;
                _allowMessages = AirlockFriendsOperations.AllowMessages;
                _allowInvites = AirlockFriendsOperations.AllowInvites;
                JoinIndex = Array.IndexOf(JoinSettings, AirlockFriendsOperations.JoinPrivacy);
                if (JoinIndex == -1) JoinIndex = 0;

                initializedSettings = true;
            }

            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUI.Button(new Rect(10, 10, 80, 30), "< Back"))
            {
                onSettingsPage = false;
                initializedSettings = false;
            }
            GUI.backgroundColor = Color.white;

            GUI.Label(new Rect(0, 50, WindowDesign.width, 30),
                "Settings",
                new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.cyan }
                });

            float y = 100;
            float labelWidth = 200;
            float controlWidth = 400;
            float spacing = 50;

            GUI.Label(new Rect(50, y, labelWidth, 25), "Join Privacy:", new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            });
            JoinIndex = GUI.SelectionGrid(new Rect(250, y, controlWidth, 30), JoinIndex, JoinSettings, JoinSettings.Length);
            y += spacing;

            _allowFriendRequests = GUI.Toggle(new Rect(50, y, controlWidth, 30), _allowFriendRequests, "Allow Friend Requests");
            y += spacing - 10;

            _allowMessages = GUI.Toggle(new Rect(50, y, controlWidth, 30), _allowMessages, "Allow Messages");
            y += spacing - 10;

            _allowInvites = GUI.Toggle(new Rect(50, y, controlWidth, 30), _allowInvites, "Allow Invites");
            y += spacing;

            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            GUI.contentColor = Color.white;
            if (GUI.Button(new Rect(50, y, 200, 40), "Apply Settings"))
            {
                _ = AirlockFriendsOperations.RPC_UpdateSettings(
                    _allowFriendRequests,
                    JoinSettings[JoinIndex],
                    _allowMessages,
                    _allowInvites
                );

                NotificationLib.QueueNotification("[<color=lime>SUCCESS</color>] Your settings have been updated.");
            }
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            y += 50;

            GUI.Label(new Rect(50, y, 600, 25), $"Debug: Connection Status = {AirlockFriendsAuth.connectionStatus}",
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.yellow }
                });
        }






    }
}
