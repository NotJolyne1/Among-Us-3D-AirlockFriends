using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using AirlockFriends.Config;
using AirlockFriends.Managers;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.XR;
using Il2CppSystem.IO;
using MelonLoader;
using Newtonsoft.Json.Linq;
using ShadowsMenu.Managers;
using UnityEngine;
using UnityEngine.InputSystem;
using static Il2CppSG.Airlock.Holomap.Holomap3D;

namespace AirlockFriends.UI
{
    public class FriendGUI
    {
        private static Rect WindowDesign = new Rect(300, 100, 700, 420);
        private static Vector2 Scroll;
        private static Vector2 ChatScroll;
        private static Dictionary<string, List<DirectMessage>> FriendConversations = new();
        public static readonly List<FriendInfo> friends = new List<FriendInfo>();
        public static readonly List<string> FriendRequests = new List<string>();
        private static readonly List<InviteData> ActiveInvites = new();
        private static readonly List<JoinRequestData> JoinRequests = new();
        public static readonly List<AcceptedJoinRequestData> AcceptedJoinRequests = new();
        public static readonly List<AcceptedInviteRequestData> AcceptedInviteRequests = new();
        private static readonly string[] JoinSettings = new string[] { "Joinable", "Ask Me", "Private" };
        private static readonly string[] UserTagSetting = new string[] { "NoTag", "Everyone" };
        private static int JoinIndex = 0;
        public static bool NewFriendRequest = false;
        private static bool OnSettingsPage = false;
        private static bool InitializedSettings = false;
        private static bool FriendRequestsPage = false;
        private static bool AddFriendPage = false;
        private static bool OnBlockedUsersPage = false;
        private static bool ScrollToBottom = false;
        private static bool _allowFriendRequests = AirlockFriendsOperations.AllowFriendRequests;
        private static bool _allowMessages = AirlockFriendsOperations.AllowMessages;
        private static bool _allowInvites = AirlockFriendsOperations.AllowInvites;
        public static bool NewName;
        private const float JoinRequestHeightFix = 50f;
        private const float AcceptedJoinHeightFix = 75f;
        private static string ChatInput = "";
        private static string AddFriendInput = "";
        private static string NewNameInput = "";
        public static Dictionary<string, string> BlockedUsers = new Dictionary<string, string>();
        private static FriendInfo CurrentChatOpen = null;
        private static FriendInfo SelectedUnfriend = null;
        private static FriendInfo SelectedBlock = null;
        public static FriendInfo GetFriend(string code) => friends.FirstOrDefault(SearchFriend => SearchFriend.FriendCode == code);

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

        public class AcceptedJoinRequestData
        {
            public string FriendCode;
            public float TimeAccepted;
            public string RoomID;
        }

        public class AcceptedInviteRequestData
        {
            public string FriendCode;
            public float TimeAccepted;
            public string RoomID;
        }

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
                Privacy = "AskMe";
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


        private static void AddMessage(string friendCode, string sender, string message, Color color = default)
        {
            if (!FriendConversations.ContainsKey(friendCode))
                FriendConversations[friendCode] = new List<DirectMessage>();

            FriendConversations[friendCode].Add(new DirectMessage(sender, message, color));

            if (CurrentChatOpen != null && CurrentChatOpen.FriendCode == friendCode)
                ScrollToBottom = true;
        }



        public static void ReceiveDirectMessage(string fromCode, string name, string message, bool SendFailed = false, bool MyMessage = false)
        {
            Color color = SendFailed ? Color.red : Color.white;
            string nameToShow = MyMessage && SendFailed ? "System" : name;
            AddMessage(fromCode, nameToShow, message, color);
        }


        public static void ReceiveInvite(string friendCode, string roomID)
        {
            if (ActiveInvites.Any(request => request.FriendCode == friendCode))
                return;

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
                NotificationLib.QueueNotification($"[<color=magenta>JOIN REQUEST</color>] <color=lime>{GetFriend(friendCode).Name}</color> wants to join you!");

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
            if (AcceptedJoinRequests.Count > 0)
                DrawAcceptedJoinRequests();
            if (AcceptedInviteRequests.Count > 0)
                DrawAcceptedInviteRequests();

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
                MelonLogger.Msg($"LOADED: {name}, ONLINE: {online}");
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

                GUI.Label(new Rect(0, 60, WindowDesign.width, 40), "<b>Airlock Friends Disabled</b>", BanTitleDesign);

                GUIStyle BanDesign = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 19,
                    richText = true,
                    normal = { textColor = Color.white }
                };

                GUI.Label(new Rect(20, 110, WindowDesign.width - 40, 200), "You cannot use Airlock Friends at this time.\n\nIf you think this is a mistake, join the Discord for help.\n\n", BanDesign);

                var SavedColor2 = GUI.color;
                GUI.color = Color.green;
                if (GUI.Button(new Rect((WindowDesign.width - 180f) / 2f, WindowDesign.height / 2f + 3f, 180f, 35f), "Join Discord"))
                    Application.OpenURL("https://discord.gg/S2JzzfF2sr");

                GUI.color = SavedColor2;

                GUI.DragWindow(new Rect(0, 0, WindowDesign.width, 30));
                return;
            }




            if (NewName)
            {
                Color OldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.95f);
                GUI.Box(new Rect(0, 0, WindowDesign.width, WindowDesign.height), "");
                GUI.color = OldColor;

                Rect NamePopup = new Rect((WindowDesign.width - 500) / 2, (WindowDesign.height - 260) / 2, 500, 260);

                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.98f);
                GUI.Box(NamePopup, "");
                GUI.color = OldColor;

                GUI.Label(new Rect(NamePopup.x + 10, NamePopup.y + 10, 480, 30), "Pick a Display Name", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } });
                GUI.Label(new Rect(NamePopup.x + 20, NamePopup.y + 50, 460, 50), "Your friends will see you as this name", new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } });

                string NonSanitizedName = GUI.TextField(new Rect(NamePopup.x + 20, NamePopup.y + 115, 460, 35), NewNameInput);

                System.Text.StringBuilder SanitizedName = new System.Text.StringBuilder();
                for (int i = 0; i < NonSanitizedName.Length; i++)
                    if (char.IsLetterOrDigit(NonSanitizedName[i]))
                        SanitizedName.Append(NonSanitizedName[i]);

                NewNameInput = SanitizedName.ToString();

                if (NewNameInput.Length > 10)
                    NewNameInput = NewNameInput.Substring(0, 10);

                string[] BlockedNames = { "fag", "nig", "bitch", "slut", "ni6er", "ni66", "ni6a", "hitle", "adolf", "fuck", "negr" };

                bool BlockedText = false;
                for (int i = 0; i < BlockedNames.Length; i++)
                    if (!string.IsNullOrEmpty(NewNameInput) && NewNameInput.IndexOf(BlockedNames[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        BlockedText = true;

                bool EmptyName = string.IsNullOrEmpty(NewNameInput);
                bool tooShort = !EmptyName && NewNameInput.Length < 3;
                bool GoodName = !EmptyName && !tooShort && !BlockedText;

                GUI.Label(new Rect(NamePopup.x + 20, NamePopup.y + 155, 460, 30), EmptyName ? "Choose something people will recognize you by" : GoodName ? "Looks good!" : BlockedText ? "That name contains blacklisted words." : "Minimum 3 characters.", new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = EmptyName ? Color.white : GoodName ? Color.green : BlockedText ? Color.red : Color.white } });

                GUI.color = GoodName ? new Color(0.3f, 1f, 0.3f, 1f) : new Color(0.6f, 1f, 0.6f, 0.5f);

                if (GUI.Button(new Rect(NamePopup.x + 140, NamePopup.y + 190, 220, 35), "Save Name") && GoodName)
                {
                    _ = AirlockFriendsOperations.RPC_NotifyFriendGroup(customName: NewNameInput);
                    NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Display name updated to <b>{NewNameInput}</b>!");
                    NewNameInput = "";
                    NewName = false;
                }

                GUI.color = Color.gray;
                if (!string.IsNullOrEmpty(AirlockFriendsOperations.MyName) && AirlockFriendsOperations.MyName != AirlockFriendsOperations.FriendCode)
                {
                    if (GUI.Button(new Rect(NamePopup.x + 190, NamePopup.y + 235, 120, 28), "Cancel"))
                    {
                        NewNameInput = "";
                        NewName = false;
                    }
                }


                GUI.color = OldColor;
                GUI.DragWindow(new Rect(0, 0, WindowDesign.width, 30));
                return;
            }




            if (OnSettingsPage)
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
            if (NewFriendRequest) GUI.color = Color.green; else GUI.color = Color.cyan;

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
                GUI.color = Color.green;
                if (GUI.Button(new Rect(WindowDesign.width - 140, 10, 130, 30), "Add Friend"))
                    AddFriendPage = !AddFriendPage;

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(WindowDesign.width - 220, 10, 70, 30), "Settings"))
                    OnSettingsPage = !OnSettingsPage;


                GUI.color = SavedColor;
            }



            if (FriendRequestsPage)
                DrawRequestsPage();
            else
                DrawFriendsList();

            GUI.DragWindow(new Rect(0, 0, WindowDesign.width, 30));
        }

        private static void DrawFriendsList()
        {
            if (AddFriendPage)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.95f);
                GUI.Box(new Rect(0, 0, WindowDesign.width, WindowDesign.height), "");
                GUI.color = Color.white;

                Rect popup = new Rect((WindowDesign.width - 500) / 2, (WindowDesign.height - 260) / 2, 500, 260);

                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.98f);
                GUI.Box(popup, "");
                GUI.color = Color.white;

                GUI.Label(new Rect(popup.x + 10, popup.y + 10, popup.width - 20, 30),
                    "Add a Friend", new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 22,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white }
                    });

                GUI.Label(new Rect(popup.x + 10, popup.y + 50, popup.width - 20, 50),
                    "Enter the Friend Code of the person you want to send a request to:", new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 16,
                        wordWrap = true,
                        normal = { textColor = Color.white }
                    });

                AddFriendInput = GUI.TextField(new Rect(popup.x + 10, popup.y + 105, popup.width - 20, 35), AddFriendInput);

                GUI.Label(new Rect(popup.x + 10, popup.y + 145, popup.width - 20, 40),
                    "Note: Friends can send you messages, request to join your room, and invite you if enabled. You can disable all these in your settings.",
                    new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 13,
                        wordWrap = true,
                        normal = { textColor = Color.yellow }
                    });

                GUI.Label(new Rect(popup.x + 10, popup.y + 190, popup.width - 20, 25),
                    $"Your Friend Code: {AirlockFriendsOperations.FriendCode}", new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 12,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    });

                GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
                if (GUI.Button(new Rect(popup.x + 10, popup.y + 220, (popup.width - 30) / 2, 30), "Send Friend Request"))
                {
                    MelonLogger.Msg($"Friend request sent to: {AddFriendInput}");
                    _ = AirlockFriendsOperations.RPC_FriendshipRequest(AddFriendInput);
                    AddFriendPage = false;
                    AddFriendInput = "";
                }

                GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                if (GUI.Button(new Rect(popup.x + 20 + (popup.width - 30) / 2, popup.y + 220, (popup.width - 30) / 2, 30), "Cancel"))
                {
                    AddFriendPage = false;
                    AddFriendInput = "";
                }
                GUI.backgroundColor = Color.white;
                return;
            }

            if (SelectedUnfriend != null || SelectedBlock != null)
            {
                FriendInfo target = SelectedUnfriend ?? SelectedBlock;

                Rect popup = new Rect((WindowDesign.width - 500) / 2, (WindowDesign.height - 260) / 2, 500, 260);

                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.98f);
                GUI.Box(popup, "");
                GUI.color = Color.white;

                GUI.Label(new Rect(popup.x + 10, popup.y + 10, popup.width - 20, 30), "Are you sure?", new GUIStyle() { alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } });

                if (SelectedUnfriend != null)
                    GUI.Label(new Rect(popup.x + 20, popup.y + 50, popup.width - 40, 100), $"You are going to unfriend {target.Name}. They will no longer be your friend, they will still be able to send you a friend request.", new GUIStyle() { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.yellow } });
                else if (SelectedBlock != null)
                    GUI.Label(new Rect(popup.x + 20, popup.y + 50, popup.width - 40, 100), $"You are going to block {target.Name}. They won't be able to interact with you unless you unblock them in settings.", new GUIStyle() { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.yellow } });

                GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
                if (GUI.Button(new Rect(popup.x + 30, popup.y + 160, 200, 40), "Confirm"))
                {
                    if (SelectedUnfriend != null)
                    {
                        _ = AirlockFriendsOperations.RPC_FriendRemove(target.FriendCode);
                        friends.Remove(target);
                        SelectedUnfriend = null;
                    }
                    else if (SelectedBlock != null)
                    {
                        _ = AirlockFriendsOperations.RPC_BlockUser(target.FriendCode);
                        _ = AirlockFriendsOperations.RPC_FriendRemove(target.FriendCode);

                        if (!string.IsNullOrEmpty(target.FriendCode))
                        {
                            MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(target.FriendCode, name =>
                            {
                                BlockedUsers[target.FriendCode] = name;
                            }));
                        }
                        friends.Remove(target);
                        SelectedBlock = null;
                    }
                }

                GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                if (GUI.Button(new Rect(popup.x + 270, popup.y + 160, 200, 40), "Cancel"))
                {
                    SelectedUnfriend = null;
                    SelectedBlock = null;
                }

                GUI.backgroundColor = Color.white;
                return;
            }

            if (friends.Count == 0)
            {
                GUI.Label(new Rect(0, 50, WindowDesign.width, 40),
                    "<size=17>There's no one here, <b>yet</b></size>", new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.UpperCenter,
                        richText = true,
                        fontSize = 14,
                        normal = { textColor = new Color(0.65f, 0.65f, 0.65f, 0.8f) }
                    });
                return;
            }

            Rect ListRect = new Rect(10, 50, WindowDesign.width - 20, WindowDesign.height - 60);
            Rect ViewRect = new Rect(0, 0, ListRect.width - 20, friends.Count * 70f);

            GUI.BeginGroup(ListRect);
            Scroll = GUI.BeginScrollView(new Rect(0, 0, ListRect.width, ListRect.height), Scroll, ViewRect);

            float y = 0f;
            for (int i = 0; i < friends.Count; i++)
            {
                FriendInfo friend = friends[i];
                GUI.Box(new Rect(0, y, ViewRect.width, 60), "");

                GUI.Label(new Rect(10, y + 15, 150, 25), friend.Name, new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

                Color statusColor = friend.Status == "Online" ? Color.green : friend.Status == "Offline" ? Color.red : Color.yellow;
                GUI.Label(new Rect(170, y + 17, 100, 25), friend.Status, new GUIStyle(GUI.skin.label) { normal = { textColor = statusColor } });

                Color SavedColor = GUI.color;

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(280, y + 15, 70, 30), "Message"))
                {
                    CurrentChatOpen = friend;
                    ChatInput = "";
                    if (!FriendConversations.ContainsKey(friend.FriendCode))
                        FriendConversations[friend.FriendCode] = new List<DirectMessage>();
                }

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(360, y + 15, 70, 30), "Join Req"))
                {
                    if (friend.Status == "Online")
                        _ = AirlockFriendsOperations.RPC_RequestJoin(friend.FriendCode);
                    else
                        NotificationLib.QueueNotification($"[<color=red>ERROR</color>] <color=lime>{friend.Name}</color> is offline!");
                }

                GUI.color = Color.cyan;
                if (GUI.Button(new Rect(440, y + 15, 70, 30), "Invite"))
                {
                    if (Settings.InGame)
                    {
                        try
                        {
                            if (friend.Status == "Online")
                                _ = AirlockFriendsOperations.RPC_SendInvite(friend.FriendCode, GameReferences.Runner.SessionInfo.Name);
                            else
                                NotificationLib.QueueNotification($"[<color=red>ERROR</color>] <color=lime>{friend.Name}</color> is offline!");
                        }
                        catch (Exception ex)
                        {
                            NotificationLib.QueueNotification("[<color=red>ERROR</color>] Invite failed! Check console");
                            MelonLogger.Error($"Invite failed: {ex}");
                        }
                    }
                    else
                        NotificationLib.QueueNotification("[<color=red>ERROR</color>] You must be in a room to send invites!");
                }

                GUI.color = Color.red;
                if (GUI.Button(new Rect(520, y + 15, 70, 30), "Unfriend"))
                {
                    SelectedUnfriend = friend;
                }


                GUI.color = new Color(0.8f, 0.3f, 0.3f);
                if (GUI.Button(new Rect(600, y + 15, 50, 30), "Block"))
                {
                    SelectedBlock = friend;
                }

                GUI.color = SavedColor;
                y += 70;
            }

            GUI.EndScrollView();
            GUI.EndGroup();
        }





        private static void DrawRequestsPage()
        {
            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUI.Button(new Rect(10, 10, 90, 40), "Back"))
                FriendRequestsPage = false;

            GUI.Label(new Rect(0, 25, WindowDesign.width, 30), "Friend Requests", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            });

            float y = 75f;
            for (int i = 0; i < FriendRequests.Count; i++)
            {
                int FriendIndex = i;
                string friendCode = FriendRequests[i];

                MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(friendCode, req =>
                {
                    GUI.Box(new Rect(10, y, WindowDesign.width - 20, 60), "");

                    GUI.Label(new Rect(20, y + 17, 300, 25), $"Request: {req}", new GUIStyle(GUI.skin.label) { fontSize = 16 });

                    Color SavedColor = GUI.color;

                    GUI.color = Color.green;
                    if (GUI.Button(new Rect(WindowDesign.width - 270, y + 15, 90, 30), "Accept"))
                    {
                        _ = AirlockFriendsOperations.RPC_FriendAccept(friendCode);
                        FriendRequests.RemoveAt(FriendIndex);
                        GUI.color = SavedColor;
                        return;
                    }

                    GUI.color = Color.red;
                    if (GUI.Button(new Rect(WindowDesign.width - 170, y + 15, 90, 30), "Reject"))
                    {
                        _ = AirlockFriendsOperations.RPC_FriendReject(friendCode);
                        FriendRequests.RemoveAt(FriendIndex);
                        return;
                    }

                    if (GUI.Button(new Rect(WindowDesign.width - 80, y + 20, 60, 20), "Block"))
                    {
                        _ = AirlockFriendsOperations.RPC_BlockUser(friendCode);
                        _ = AirlockFriendsOperations.RPC_FriendReject(friendCode);
                        FriendRequests.RemoveAt(FriendIndex);
                        MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(friendCode, name =>
                        {
                            BlockedUsers[friendCode] = name;
                        })); 
                        GUI.color = SavedColor;
                        return;
                    }

                    GUI.color = SavedColor;
                }));

                y += 70;
            }
        }


        private static void DrawChatWindow(FriendInfo FriendInfo)
        {
            GUI.Label(new Rect(0, 25, WindowDesign.width, 30), $"Direct Message With {FriendInfo.Name}",
                new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 17,
                    fontStyle = FontStyle.Bold
                });

            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUI.Button(new Rect(10, 10, 90, 40), "Back"))
            {
                CurrentChatOpen = null;
                return;
            }

            var messages = FriendConversations.ContainsKey(FriendInfo.FriendCode) ? new List<DirectMessage>(FriendConversations[FriendInfo.FriendCode]) : new List<DirectMessage>();

            Rect ChatDesign = new Rect(10, 50, WindowDesign.width - 20, WindowDesign.height - 120);
            float TotalHeight = 0f;
            GUIStyle HeightFix = new GUIStyle(GUI.skin.label) { richText = true };

            foreach (var msg in messages)
            {
                HeightFix.normal.textColor = msg.TextColor;
                float height = HeightFix.CalcHeight(new GUIContent(msg.Text), ChatDesign.width - 20);
                TotalHeight += height + 30;
            }

            Rect ViewingDesign = new Rect(0, 0, ChatDesign.width - 20, TotalHeight);

            GUI.BeginGroup(ChatDesign);

            ChatScroll = GUI.BeginScrollView(
                new Rect(0, 0, ChatDesign.width, ChatDesign.height),
                ChatScroll,
                ViewingDesign
            );

            float y = 0f;
            foreach (var msg in messages)
            {
                HeightFix.normal.textColor = msg.TextColor;
                float height = HeightFix.CalcHeight(new GUIContent(msg.Text), ChatDesign.width - 20);
                GUI.Box(new Rect(0, y, ViewingDesign.width, height + 25), "");

                GUI.Label(
                    new Rect(10, y + 5, ViewingDesign.width - 20, 20),
                    msg.Sender,
                    new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold }
                );

                GUI.Label(
                    new Rect(10, y + 25, ViewingDesign.width - 20, height),
                    msg.Text,
                    HeightFix
                );

                y += height + 30;
            }

            GUI.EndScrollView();
            GUI.EndGroup();

            if (ScrollToBottom && TotalHeight > ChatDesign.height)
            {
                ChatScroll.y = TotalHeight - ChatDesign.height;
                ScrollToBottom = false;
            }

            if (CurrentChatOpen != null && Event.current.type == EventType.KeyDown)
            {
                if (!Event.current.control && !Event.current.alt && !Event.current.command)
                {
                    GUI.FocusControl("ChatInputField");
                }
            }

            GUI.SetNextControlName("ChatInputField");
            ChatInput = GUI.TextField(
                new Rect(10, WindowDesign.height - 50, WindowDesign.width - 140, 40),
                ChatInput
            );

            if (GUI.Button(new Rect(WindowDesign.width - 120, WindowDesign.height - 50, 110, 40), "Send") || Keyboard.current.enterKey.wasPressedThisFrame)
            {
                if (!string.IsNullOrWhiteSpace(ChatInput))
                {
                    if (ChatInput.Length > 500)
                    {
                        NotificationLib.QueueNotification("[<color=red>ERROR</color>] Message too long");
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
                var invite = ActiveInvites[i];

                GUI.color = new Color(0, 0, 0, 0.65f);
                GUI.Box(new Rect(WindowDesign.x, WindowDesign.yMax + (i * (JoinRequestHeightFix)), 300, JoinRequestHeightFix), "");
                GUI.color = Color.white;

                string inviter = string.Empty;
                GUILayout.BeginArea(new Rect(WindowDesign.x, WindowDesign.yMax + (i * (JoinRequestHeightFix + 6f)), 300, JoinRequestHeightFix));
                MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(invite.FriendCode, name => inviter = name));
                GUILayout.Label($"{inviter} invited you!");
                GUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Accept"))
                {
                    _ = AirlockFriendsOperations.RPC_RespondToInvite(invite.FriendCode, true);

                    AcceptedInviteRequests.Add(new AcceptedInviteRequestData
                    {
                        FriendCode = invite.FriendCode,
                        TimeAccepted = Time.time,
                        RoomID = invite.RoomID
                    });

                    ActiveInvites.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    continue;
                }

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Ignore"))
                {
                    _ = AirlockFriendsOperations.RPC_RespondToInvite(invite.FriendCode, false);
                    ActiveInvites.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    continue;
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

                Rect Background = new Rect(WindowDesign.x, WindowDesign.yMax + (ActiveInvites.Count * (JoinRequestHeightFix + 6f)) + (i * JoinRequestHeightFix), 300, 50
                );

                if (!Settings.InGame)
                {
                    _ = AirlockFriendsOperations.RPC_RespondToJoin(req.FriendCode, false, false);
                    JoinRequests.RemoveAt(i);
                    i--;
                    continue;
                }

                GUI.color = new Color(0, 0, 0, 0.65f);
                GUI.Box(Background, "");
                GUI.color = Color.white;

                GUILayout.BeginArea(Background);
                GUILayout.Label($"{GetFriend(req.FriendCode).Name} wants to join you!");

                GUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Accept"))
                {
                    _ = AirlockFriendsOperations.RPC_RespondToJoin(req.FriendCode, true);
                    JoinRequests.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    continue;
                }

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Ignore"))
                {
                    _ = AirlockFriendsOperations.RPC_RespondToJoin(req.FriendCode, false);
                    JoinRequests.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    continue;
                }

                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
                GUILayout.EndArea();
            }
        }



        private static void DrawAcceptedJoinRequests()
        {
            for (int i = AcceptedJoinRequests.Count - 1; i >= 0; i--)
            {
                if (Time.time - AcceptedJoinRequests[i].TimeAccepted > 30f)
                    AcceptedJoinRequests.RemoveAt(i);
            }

            float baseY = WindowDesign.yMax + (ActiveInvites.Count * (JoinRequestHeightFix + 6f)) + (JoinRequests.Count * (JoinRequestHeightFix + 6f));

            for (int i = 0; i < AcceptedJoinRequests.Count; i++)
            {
                var req = AcceptedJoinRequests[i];

                Rect Background = new Rect(WindowDesign.x, baseY + (i * AcceptedJoinHeightFix), 300, 75);

                GUI.color = new Color(0, 0, 0, 0.65f);
                GUI.Box(Background, "");
                GUI.color = Color.white;

                GUILayout.BeginArea(Background);
                GUILayout.Label($"{GetFriend(req.FriendCode).Name} accepted your join request!");
                GUILayout.Label($"{GetFriend(req.FriendCode).Name} is in room: {req.RoomID}");
                GUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Okay"))
                {
                    AcceptedJoinRequests.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    continue;
                }

                GUI.backgroundColor = Color.blue;
                if (GUILayout.Button("Copy Code"))
                {
                    GUIUtility.systemCopyBuffer = req.RoomID;
                    NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Copied room ID <color=lime>{req.RoomID}</color> to your clipboard!");
                    AcceptedJoinRequests.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    continue;
                }

                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
                GUILayout.EndArea();
            }
        }

        private static void DrawAcceptedInviteRequests()
        {
            for (int i = AcceptedInviteRequests.Count - 1; i >= 0; i--)
            {
                if (Time.time - AcceptedInviteRequests[i].TimeAccepted > 30f)
                    AcceptedInviteRequests.RemoveAt(i);
            }

            float baseY = WindowDesign.yMax + (ActiveInvites.Count * (JoinRequestHeightFix + 6f))
                                       + (JoinRequests.Count * (JoinRequestHeightFix + 6f))
                                       + (AcceptedJoinRequests.Count * AcceptedJoinHeightFix);

            for (int i = 0; i < AcceptedInviteRequests.Count; i++)
            {
                var req = AcceptedInviteRequests[i];

                Rect Background = new Rect(WindowDesign.x, baseY + (i * AcceptedJoinHeightFix), 300, 75);

                GUI.color = new Color(0, 0, 0, 0.65f);
                GUI.Box(Background, "");
                GUI.color = Color.white;

                GUILayout.BeginArea(Background);
                GUILayout.Label($"You accepted {GetFriend(req.FriendCode).Name}'s invite");
                GUILayout.Label($"{GetFriend(req.FriendCode).Name} is in room: {req.RoomID}");

                GUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Okay"))
                {
                    AcceptedInviteRequests.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    continue;
                }

                GUI.backgroundColor = Color.blue;
                if (GUILayout.Button("Copy Code"))
                {
                    GUIUtility.systemCopyBuffer = req.RoomID;
                    NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Copied room ID <color=lime>{req.RoomID}</color> to your clipboard!");
                    AcceptedInviteRequests.RemoveAt(i);
                    i--;
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    continue;
                }

                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;
                GUILayout.EndArea();
            }
        }

        private static void DrawSettingsPage()
        {
            if (!InitializedSettings)
            {
                _allowFriendRequests = AirlockFriendsOperations.AllowFriendRequests;
                _allowMessages = AirlockFriendsOperations.AllowMessages;
                _allowInvites = AirlockFriendsOperations.AllowInvites;
                JoinIndex = Array.IndexOf(JoinSettings, AirlockFriendsOperations.JoinPrivacy);
                if (JoinIndex == -1)
                    JoinIndex = 0;
                InitializedSettings = true;
            }

            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUI.Button(new Rect(20, 20, 90, 40), "Back"))
            {
                if (OnBlockedUsersPage)
                    OnBlockedUsersPage = false;
                else
                {
                    OnSettingsPage = false;
                    InitializedSettings = false;
                }
            }
            GUI.backgroundColor = Color.white;

            if (OnBlockedUsersPage)
            {
                GUIStyle TitleDesign = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.red }
                };
                GUI.Label(new Rect(0, 30, WindowDesign.width, 40), "Blocked Users", TitleDesign);

                float y = 80;
                foreach (var kvp in BlockedUsers.ToList())
                {
                    string friendCode = kvp.Key;
                    string name = kvp.Value;

                    GUI.Box(new Rect(20, y, WindowDesign.width - 40, 40), "");
                    GUI.Label(new Rect(30, y + 10, 200, 20), $"{name} ({friendCode})");

                    GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                    if (GUI.Button(new Rect(WindowDesign.width - 110, y + 5, 80, 30), "Unblock"))
                    {
                        _ = AirlockFriendsOperations.RPC_UnblockUser(friendCode);
                        BlockedUsers.Remove(friendCode);
                        GUI.backgroundColor = Color.white;
                        return;
                    }
                    GUI.backgroundColor = Color.white;

                    y += 50;
                }
                return;
            }

            GUIStyle TitleDesignSettings = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };
            GUI.Label(new Rect(0, 30, WindowDesign.width, 40), "Settings", TitleDesignSettings);

            Rect Panel = new Rect(20, 80, WindowDesign.width - 40, WindowDesign.height - 100);
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            GUI.Box(Panel, "");
            GUI.color = Color.white;

            GUIStyle LabelDesign = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                normal = { textColor = Color.white }
            };

            GUIStyle RadioDesign = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 17,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(Panel.x + 25, Panel.y + 25, 300, 28), "Join Privacy", LabelDesign);
            if (GUI.Toggle(new Rect(Panel.x + 40, Panel.y + 25 + 36, 300, 26), JoinIndex == 0, JoinSettings[0], RadioDesign)) JoinIndex = 0;
            if (GUI.Toggle(new Rect(Panel.x + 40, Panel.y + 25 + 66, 300, 26), JoinIndex == 1, JoinSettings[1], RadioDesign)) JoinIndex = 1;
            if (GUI.Toggle(new Rect(Panel.x + 40, Panel.y + 25 + 96, 300, 26), JoinIndex == 2, JoinSettings[2], RadioDesign)) JoinIndex = 2;

            GUI.DrawTexture(new Rect(Panel.x + 20, Panel.y + 25 + 134, Panel.width - 40, 1), Texture2D.whiteTexture);

            _allowFriendRequests = GUI.Toggle(new Rect(Panel.x + 25, Panel.y + 25 + 160, 300, 30), _allowFriendRequests, "Allow Friend Requests");
            _allowMessages = GUI.Toggle(new Rect(Panel.x + 25, Panel.y + 25 + 194, 300, 30), _allowMessages, "Allow Messages");
            _allowInvites = GUI.Toggle(new Rect(Panel.x + 25, Panel.y + 25 + 228, 300, 30), _allowInvites, "Allow Invites");

            GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
            if (GUI.Button(new Rect(Panel.x + Panel.width - 130, Panel.y + 10, 110, 28), "Change Name"))
            {
                NewName = true;
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUI.Button(new Rect(Panel.x + 25, Panel.y + Panel.height - 42f, 150f, 35f), "Apply Settings"))
            {
                _ = AirlockFriendsOperations.RPC_UpdateSettings(_allowFriendRequests, JoinSettings[JoinIndex], _allowMessages, _allowInvites);
                NotificationLib.QueueNotification("[<color=lime>SUCCESS</color>] Your settings have been updated.");
            }

            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
            if (GUI.Button(new Rect(Panel.x + 190, Panel.y + Panel.height - 42f, 150f, 35f), "Blocked Users"))
            {
                OnBlockedUsersPage = true;
            }
            GUI.backgroundColor = Color.white;

            GUIStyle DebugDesign = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect(30, WindowDesign.height - 22, 600, 25), $"Debug: Connection Status: {AirlockFriendsAuth.connectionStatus} | Internal: {AirlockFriendsOperations.socket.State}", DebugDesign);
        }
    }
}