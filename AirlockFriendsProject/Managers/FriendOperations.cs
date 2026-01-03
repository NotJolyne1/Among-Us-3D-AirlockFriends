using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AirlockFriends.Config;
using AirlockFriends.UI;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Graphics;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.Roles;
using Il2CppSG.Airlock.Sabotage;
using Il2CppSG.Airlock.UI.TitleScreen;
using Il2CppSG.Airlock.XR;
using Il2CppSteamworks;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static AirlockFriends.Managers.AirlockFriendsAuth;
using static AirlockFriends.UI.FriendGUI;
using static Il2CppSystem.Net.WebSockets.ManagedWebSocket;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.InputSystem.InputRemoting;

namespace AirlockFriends.Managers
{
    public class AirlockFriendsOperations
    {
        protected static string PrivateKey { get; private set; }
        public static string FriendCode { get; private set; }
        public static string MyName { get; private set; } = FriendCode;
        public static bool AllowFriendRequests = true;
        public static string JoinPrivacy = "Joinable";
        public static string ModTags = "Everyone";
        public static bool AllowMessages = true;
        public static bool AllowInvites = true;
        public static bool FriendslistFetched = false;
        public static ClientWebSocket socket;
        private static CancellationTokenSource cts;
        public static readonly string[] UnwantedNames = {
            "Red", "Blue", "Green", "Pink", "Orange",
            "Yellow", "Black", "White", "Purple", "Brown",
            "Cyan", "Lime", "Spectator", "Color###"
        };
        private static Dictionary<string, string> GetNameFromID = new Dictionary<string, string>();
        public static HashSet<string> GettingNames = new();
        public static event Action<string, string> OnUsernameUpdated;

        public static bool IsConnected => socket != null && socket.State == WebSocketState.Open;

        public static bool IsUnwantedName(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    return false;

                var checks = new List<string> { name };
                checks.Add(new string(name.Where(char.IsLetter).ToArray()));

                foreach (var check in checks)
                {
                    foreach (string block in UnwantedNames)
                    {
                        var b = block.ToLowerInvariant();
                        if (check.ToLowerInvariant().Contains(b))
                            return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AirlockFriends] Failed to check name: {ex}");
                return false;
            }
        }



        public static async void PrepareAuthentication()
        {
            if (!IsConnected)
            {
                PrivateKey = AirlockFriendsAuth.PrepareAuthenticationKey();
                MelonLogger.Msg($"[AirlockFriends] [DEBUG] Using PrivateKey: {PrivateKey}");

                await ConnectToServer();
                await AuthenticateWithServer();
            }
        }

        private static async Task ConnectToServer()
        {
            if (!IsConnected)
            {
                connectionStatus = ConnectionStatus.Connecting;
                socket = new ClientWebSocket();
                cts = new CancellationTokenSource();
                socket.Options.SetRequestHeader("User-Agent", $"AirlockFriends/{Settings.Version}");

                try
                {
                    var serverUri = new Uri("wss://lank-lucretia-timocratical.ngrok-free.dev");
                    await socket.ConnectAsync(serverUri, cts.Token);
                    MelonLogger.Msg("[AirlockFriends] Connected to server!");
                    _ = Task.Run(ReceiveLoop);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AirlockFriends] Connection failed: {ex.Message}");
                    MelonCoroutines.Start(AttemptReconnection());
                }
            }
        }

        private static async Task AuthenticateWithServer()
        {
            if (IsConnected)
            {
                connectionStatus = ConnectionStatus.Authenticating;
                var authPayload = new
                {
                    type = "authenticate",
                    privateKey = PrivateKey,
                    steamID = Helpers.GetSelfSteamID(),
                };

                string EventData = System.Text.Json.JsonSerializer.Serialize(authPayload);
                await RaiseEvent(EventData);
                MelonLogger.Msg("[AirlockFriends] Sent authentication request to server.");
            }
        }


        private static async Task ReceiveLoop()
        {
            var buffer = new byte[8192];

            while (IsConnected)
            {
                try
                {
                    var result = await socket.ReceiveAsync(buffer, cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (!msg.Contains("friendsList")) MelonLogger.Msg($"[AirlockFriends] [DEBUG] Server raw: {msg}");
                        if (msg.Contains("This account"))
                            Main.AFBanned = true;

                        try
                        {
                            var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(msg);


                            if (data.TryGetProperty("authenticated", out var Auth))
                            {
                                if (Auth.ValueKind == System.Text.Json.JsonValueKind.True)
                                {
                                    string publicFriendCode = data.GetProperty("friendCode").GetString();
                                    AssignFriendCode(publicFriendCode);

                                    if (data.TryGetProperty("privateKey", out var PrivateToken))
                                    {
                                        string newPrivateKey = PrivateToken.GetString();
                                        AirlockFriendsAuth.SavePrivateKey(newPrivateKey);
                                        MelonLogger.Msg($"[AirlockFriends] [DEBUG] Saved new privateKey from server: {newPrivateKey}");
                                    }

                                    string message = data.TryGetProperty("message", out var m) ? m.GetString() : "Authenticated successfully!";

                                    if (data.TryGetProperty("AllowFriendRequests", out var AllowFriendReq))
                                        AllowFriendRequests = AllowFriendReq.GetBoolean();

                                    if (data.TryGetProperty("JoinPrivacy", out var JoinRequests))
                                        JoinPrivacy = JoinRequests.GetString();

                                    if (data.TryGetProperty("AllowMessages", out var AllowMessaging))
                                        AllowMessages = AllowMessaging.GetBoolean();

                                    if (data.TryGetProperty("AllowInvites", out var AllowInvite))
                                        AllowInvites = AllowInvite.GetBoolean();

                                    if (data.TryGetProperty("Name", out var Name))
                                    {
                                        MyName = Name.GetString();
                                        if (MyName == FriendCode)
                                        {
                                            NewName = true;
                                        }
                                    }

                                    if (data.TryGetProperty("friendsList", out var FriendsList) && FriendsList.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var friend in FriendsList.EnumerateArray())
                                        {
                                            string friendCode = friend.GetProperty("friendCode").GetString();
                                            MelonCoroutines.Start(GetUsername(friendCode, name =>
                                            {
                                                MelonLogger.Msg($"[AirlockFriends] Friend loaded: {name} ({friendCode})");
                                            }));
                                        }
                                    }

                                    if (data.TryGetProperty("blocked", out var BlockedList) && BlockedList.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        BlockedUsers.Clear();
                                        foreach (var blockedFriendCode in BlockedList.EnumerateArray())
                                        {
                                            string targetFriendCode = blockedFriendCode.GetString();
                                            if (!string.IsNullOrEmpty(targetFriendCode))
                                            {
                                                MelonCoroutines.Start(GetUsername(targetFriendCode, name =>
                                                {
                                                    BlockedUsers[targetFriendCode] = name;
                                                }));
                                            }
                                        }
                                    }

                                    if (Main.AFBanned)
                                    {
                                        Main.AFBanned = false;
                                        Main.HasShownBanNoti = false;
                                        NotificationLib.QueueNotification("[<color=lime>UNBANNED</color>] You've been unbanned from Airlock Friends!");
                                    }

                                    connectionStatus = ConnectionStatus.Established;
                                    _ = RPC_NotifyFriendGroup();
                                    if (message.Contains("Welcome back"))
                                    {
                                        // REPLACE BEFORE PROD
                                    }
                                    else if (message.Contains("New user"))
                                        NotificationLib.QueueNotification("[<color=magenta>WELCOME</color>] Welcome to Airlock Friends!");
                                        continue;
                                }

                                if (Auth.ValueKind == System.Text.Json.JsonValueKind.False && data.TryGetProperty("action", out var action) && action.GetString() == "AuthViolation")
                                {
                                    string message = data.TryGetProperty("error", out var error) ? error.GetString() : "AuthViolation";
                                    MelonLogger.Error($"[AirlockFriends] AUTH FAILED: {message}");
                                    NotificationLib.QueueNotification($"[<color=red>Unauthorized</color>] {message}");

                                    if (message.ToLower().Contains("invalid") || message.ToLower().Contains("revoked"))
                                        File.Delete(Path.Combine(Application.persistentDataPath, "AirlockFriendsPrivateKey.txt"));

                                    await Disconnect(rejected: true);
                                    continue;
                                }

                            }

                            if (data.TryGetProperty("type", out var FriendType))
                            {
                                string type = FriendType.GetString();

                                switch (type)
                                {
                                    case "friendRequestReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var fromFR))
                                        {
                                            string Requester = fromFR.GetString();
                                            RPC_OnRequestReceived(Requester);
                                            MelonCoroutines.Start(GetUsername(Requester, name => NotificationLib.QueueNotification($"[<color=magenta>FRIEND REQUEST</color>] <color=lime>{name}</color> wants to friend you!")));
                                        }
                                        break;

                                    case "friendRequest":
                                        if (data.TryGetProperty("status", out var FriendReqStatus) && FriendReqStatus.GetString() == "failed")
                                        {
                                            if (data.TryGetProperty("reason", out var FriendFailReason))
                                                NotificationLib.QueueNotification($"[<color=red>ERROR</color>] {FriendFailReason.GetString()}");
                                            else
                                                NotificationLib.QueueNotification("[<color=red>ERROR</color>] Friend request failed");
                                            break;
                                        }

                                        if (data.TryGetProperty("toFriendCode", out var SentTo))
                                        {
                                            string toPlayer = SentTo.GetString();
                                            MelonCoroutines.Start(GetUsername(toPlayer, name =>
                                            {
                                                MelonLogger.Msg($"[AirlockFriends] Friend request sent to {name}");
                                                NotificationLib.QueueNotification($"[<color=magenta>FRIEND REQUEST</color>] Friend Request Sent to <color=lime>{name}</color>!");
                                            }));
                                        }
                                        break;



                                    case "friendAccepted":
                                        if (data.TryGetProperty("byFriendCode", out var Sender))
                                        {
                                            string AcceptingFriend = Sender.GetString();
                                            MelonCoroutines.Start(GetUsername(AcceptingFriend, friendName =>
                                            {
                                                UpdateFriend(friendName, "Online", AcceptingFriend, "");
                                                MelonLogger.Msg($"[AirlockFriends] Friend accepted: {friendName} ({AcceptingFriend})");
                                                NotificationLib.QueueNotification($"[<color=lime>FRIEND ACCEPTED</color>] <color=lime>{friendName} ({AcceptingFriend})</color> is now your friend!");
                                            }));
                                        }
                                        break;



                                    case "friendRejected":
                                        if (data.TryGetProperty("byFriendCode", out var RejectedBy))
                                        {
                                            string FriendToReject = RejectedBy.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend rejected by {FriendToReject}");
                                            MelonCoroutines.Start(GetUsername(FriendToReject, name => NotificationLib.QueueNotification($"[<color=red>FRIEND REJECTED</color>] <color=lime>{name}</color> rejected your friend request!")));
                                        }
                                        break;

                                    case "friendRemoved":
                                    case "friendRemove":
                                        if (data.TryGetProperty("byFriendCode", out var RemovedBy))
                                        {
                                            string FriendPlayer = RemovedBy.GetString();
                                            MelonCoroutines.Start(GetUsername(FriendPlayer, name => NotificationLib.QueueNotification($"[<color=red>FRIEND REMOVED</color>] <color=lime>{name}</color> is no longer your friend!")));
                                            var f = GetFriend(FriendPlayer);
                                            if (f != null)
                                                friends.Remove(f);
                                        }
                                        break;

                                    case "messageReceived":
                                        {
                                            if (data.TryGetProperty("fromFriendCode", out var Messager) &&
                                                data.TryGetProperty("message", out var MessageText))
                                            {
                                                string MessageSender = Messager.GetString();
                                                string message = MessageText.GetString();
                                                MelonCoroutines.Start(GetUsername(MessageSender, name =>
                                                {
                                                    ReceiveDirectMessage(MessageSender, name, message);
                                                    NotificationLib.QueueNotification($"[<color=magenta>MESSAGE</color>] Message from: <color=lime>{name}</color>: {message}");
                                                }));
                                            }
                                            break;
                                        }


                                    case "sendMessage":
                                        {
                                            if (data.TryGetProperty("status", out var messageStatus) && data.TryGetProperty("toFriendCode", out var SendingTo))
                                            {
                                                string MessageStatus = messageStatus.GetString();

                                                if (MessageStatus == "success")
                                                {
                                                    string target = data.TryGetProperty("toFriendCode", out var toFriendCode) ? toFriendCode.GetString() : "Unknown";
                                                    NotificationLib.QueueNotification($"[<color=lime>SENT</color>] Sent message to {GetFriend(toFriendCode.GetString()).Name}!");
                                                }
                                                else
                                                {
                                                    string reason = data.TryGetProperty("reason", out var MessageFailReason) ? MessageFailReason.GetString() : "Failed to send message";
                                                    NotificationLib.QueueNotification($"[<color=red>ERROR</color>] {reason}");
                                                    ReceiveDirectMessage(SendingTo.GetString(), "System", reason, true, true);
                                                }
                                            }
                                            break;
                                        }





                                    case "friendsList":
                                        if (data.TryGetProperty("friends", out var Friend) && Friend.ValueKind == System.Text.Json.JsonValueKind.Array)
                                        {
                                            if (data.TryGetProperty("myName", out var myName))
                                            {
                                                string updatedName = myName.GetString();
                                                if (!string.IsNullOrEmpty(updatedName))
                                                    MyName = updatedName;
                                            }

                                            foreach (var friend in Friend.EnumerateArray())
                                            {
                                                string code = friend.GetProperty("friendCode").GetString();
                                                string LatestName = friend.GetProperty("name").GetString();
                                                string online = friend.GetProperty("status").GetString();

                                                UpdateFriend(LatestName, online, code, "");
                                            }
                                            if (!FriendslistFetched)
                                            {
                                                int onlineFriends = 0;
                                                foreach (FriendInfo friend in friends)
                                                {
                                                    if (friend.IsOnline)
                                                    {
                                                        onlineFriends++;
                                                    }
                                                }
                                                MelonCoroutines.Start(GetUsername(FriendCode, name => NotificationLib.QueueNotification($"[<color=magenta>SERVER</color>] Welcome back, <color=lime>{name}</color>! You have <color=lime>{onlineFriends}</color> friend(s) online!")));
                                                FriendslistFetched = true;
                                            }
                                        }
                                        break;



                                    case "joinRequestReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var FromReceiving))
                                        {
                                            string senderFriendCode = FromReceiving.GetString();
                                            ReceiveJoinRequest(senderFriendCode);
                                        }
                                        break;


                                    case "joinRequestResponse":
                                        if (data.TryGetProperty("targetFriendCode", out var FromReceivingJoin) &&
                                            data.TryGetProperty("accepted", out var WasAccepted))
                                        {
                                            string JoiningFriend = FromReceivingJoin.GetString();
                                            bool accepted = WasAccepted.GetBoolean();
                                            string roomCode = data.TryGetProperty("roomID", out var InRoomID) ? InRoomID.GetString() : "";
                                            bool inGame = data.TryGetProperty("inGame", out var FriendInGame) && FriendInGame.GetBoolean();

                                            if (accepted)
                                            {
                                                MelonLogger.Msg($"[AirlockFriends] {GetFriend(JoiningFriend).Name} accepted your join request. Room: {roomCode}");
                                                NotificationLib.QueueNotification($"[<color=lime>JOIN ACCEPTED</color>] <color=lime>{GetFriend(JoiningFriend).Name}</color> is in room {roomCode}!");

                                                AcceptedJoinRequests.Add(new AcceptedJoinRequestData
                                                {
                                                    FriendCode = JoiningFriend,
                                                    TimeAccepted = Time.time,
                                                    RoomID = roomCode
                                                });
                                            }
                                            else
                                            {
                                                if (!inGame)
                                                {
                                                    MelonLogger.Msg($"[AirlockFriends] {GetFriend(JoiningFriend).Name} is not in a game.");
                                                    NotificationLib.QueueNotification($"[<color=red>JOIN FAILED</color>] <color=lime>{GetFriend(JoiningFriend).Name}</color> is not in a game.");
                                                }
                                                else
                                                {
                                                    MelonLogger.Msg($"[AirlockFriends] {GetFriend(JoiningFriend).Name} rejected your join request.");
                                                    NotificationLib.QueueNotification($"[<color=red>JOIN REJECTED</color>] <color=lime>{GetFriend(JoiningFriend).Name}</color> did not accept your join request.");
                                                }
                                            }
                                        }
                                        break;




                                    case "inviteResponseResult":
                                        {
                                            if (!data.TryGetProperty("fromFriendCode", out var SenderFriend) || !data.TryGetProperty("accepted", out var InviteAccepted))
                                                break;

                                            string FriendID = SenderFriend.GetString();
                                            bool accepted = InviteAccepted.GetBoolean();

                                            var friend = FriendGUI.GetFriend(FriendID);
                                            string FriendName = friend != null ? friend.Name : FriendID;

                                            if (accepted)
                                            {
                                                MelonLogger.Msg($"[AirlockFriends] {FriendName} accepted your invite!");
                                                NotificationLib.QueueNotification($"[<color=lime>JOIN ACCEPTED</color>] <color=lime>{FriendName}</color> accepted your invite!");
                                            }
                                            else
                                            {
                                                MelonLogger.Msg($"[AirlockFriends] {FriendName} denied your invite!");
                                                NotificationLib.QueueNotification($"[<color=red>Denied</color>] <color=lime>{FriendName}</color> did not accept your invite!");
                                            }
                                            break;
                                        }





                                    case "inviteReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var fromWho) && data.TryGetProperty("roomID", out var RoomID))
                                        {
                                            string FriendID = fromWho.GetString();
                                            string roomCode = RoomID.GetString();

                                            ReceiveInvite(FriendID, roomCode);
                                        }
                                        break;



                                    case "GetPlayerName":
                                        string friendCode = data.TryGetProperty("friendCode", out var friendcode) ? friendcode.GetString() : "UnknownCode";
                                        string name = data.TryGetProperty("name", out var PlayerName) && PlayerName.ValueKind != System.Text.Json.JsonValueKind.Null ? PlayerName.GetString() : "UnknownName";
                                        SaveUsername(friendCode, name);
                                        break;


                                    case "BlockUserFail":
                                        if (data.TryGetProperty("reason", out var blockFailReason))
                                        {
                                            string reason = blockFailReason.GetString();
                                            NotificationLib.QueueNotification($"[<color=red>ERROR</color>] Failed to block user: {reason}");
                                        }
                                        break;

                                    case "UnblockUserFail":
                                        if (data.TryGetProperty("reason", out var UnblockFailReason))
                                        {
                                            string reason = UnblockFailReason.GetString();
                                            NotificationLib.QueueNotification($"[<color=red>ERROR</color>] Failed to unblock user: {reason}");
                                        }
                                        break;


                                    default:
                                        MelonLogger.Msg($"[AirlockFriends] Unhandled event / reliable raised?! {msg}");
                                        break;
                                }
                                }
                            }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"[AirlockFriends] Failed to deserialize reliable: {ex.Message}");
                            NotificationLib.QueueNotification($"[<color=red>ERROR</color>] Failed to deserialize reliable.\nPlease report this to us.\n: Check your console for info");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AirlockFriends] Server sent an exception: {ex.Message}");
                    if (!IsConnected)
                        MelonCoroutines.Start(AttemptReconnection());
                    break;
                }
            }
            await Disconnect();
        }


        // Raise reliable
        public static async Task RaiseEvent(string OperationInfo)
        {
            if (IsConnected)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(OperationInfo);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
            }
        }

        public static void AssignFriendCode(string ServerPublicKey)
        {
            FriendCode = ServerPublicKey;
            if (string.IsNullOrEmpty(MyName)) MyName = ServerPublicKey;
            MelonLogger.Msg($"[AirlockFriends] Server assigned FriendCode: {FriendCode}");
        }

        public static async Task RPC_FriendshipRequest(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            if (FriendRequests.Contains(targetFriendCode))
            {
                await RPC_FriendAccept(targetFriendCode);
                FriendRequests.Remove(targetFriendCode);
                return;
            }

            var request = new
            {
                type = "friendRequest",
                fromPrivateKey = PrivateKey,
                toFriendCode = targetFriendCode
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);

            MelonCoroutines.Start(GetUsername(targetFriendCode, name =>
                MelonLogger.Msg($"[AirlockFriends] Sent friend request to {name}")
            ));
        }

        public static void RPC_OnRequestReceived(string fromFriendCode)
        {
            MelonCoroutines.Start(GetUsername(fromFriendCode, name =>
            {
                MelonLogger.Msg($"[AirlockFriends] Friend request received from {name}");
            }));
            NewFriendRequest = true;
            FriendRequests.Add(fromFriendCode);
        }

        public static async Task RPC_SendMessage(string targetFriendCode, string message)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(FriendCode))
                return;

            var payload = new
            {
                type = "sendMessage",
                targetFriendCode = targetFriendCode,
                fromPrivateKey = PrivateKey,
                fromFriendCode = FriendCode,
                message = message
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(EventData);
            MelonLogger.Msg($"[AirlockFriends] Sent message to {targetFriendCode}: {message}");
        }




        public static async Task RPC_FriendAccept(string AcceptingFriend)
        {
            if (string.IsNullOrEmpty(AcceptingFriend) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "friendAccept",
                fromPrivateKey = PrivateKey,
                acceptFriendCode = AcceptingFriend
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
            MelonLogger.Msg($"[AirlockFriends] Accepted friend request from {AcceptingFriend}");

            FriendRequests.Remove(AcceptingFriend);
        }
        public static async Task RPC_FriendReject(string incomingFriendCode)
        {
            if (string.IsNullOrEmpty(incomingFriendCode) || string.IsNullOrEmpty(FriendCode))
                return;

            var payload = new
            {
                type = "friendReject",
                fromPrivateKey = PrivateKey,
                rejectFriendCode = incomingFriendCode
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(EventData);

            MelonLogger.Msg($"[AirlockFriends] Rejected friend request from {incomingFriendCode}");
        }


        public static async Task RPC_FriendRemove(string RemovingFriend)
        {
            if (string.IsNullOrEmpty(RemovingFriend) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "friendRemove",
                fromPrivateKey = PrivateKey,
                removeFriendCode = RemovingFriend
            };


            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
            MelonLogger.Msg($"[AirlockFriends] Removed friend {RemovingFriend}");
            MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(RemovingFriend, name => NotificationLib.QueueNotification($"[<color=red>UNFRIEND</color>] Unfriended {name}")));
        }
























        public static async Task RPC_UpdateSettings(bool? allowFriendRequests = null, string joinPrivacy = null, bool? allowMessages = null, bool? allowInvites = null)
        {
            if (!IsConnected || socket == null) return;

            if (allowFriendRequests.HasValue) AllowFriendRequests = allowFriendRequests.Value;
            if (!string.IsNullOrEmpty(joinPrivacy)) JoinPrivacy = joinPrivacy;
            if (allowMessages.HasValue) AllowMessages = allowMessages.Value;
            if (allowInvites.HasValue) AllowInvites = allowInvites.Value;

            var payload = new
            {
                type = "updateSettings",
                fromPrivateKey = PrivateKey,
                AllowFriendRequests = AllowFriendRequests,
                JoinPrivacy = JoinPrivacy,
                AllowMessages = AllowMessages,
                AllowInvites = AllowInvites
            };

            try
            {
                string EventData = System.Text.Json.JsonSerializer.Serialize(payload);
                await RaiseEvent(EventData);
                MelonLogger.Msg($"[AirlockFriends debug] Sent updated settings to server: FriendRequests {AllowFriendRequests}, JoinPrivacy {JoinPrivacy}, Messages {AllowMessages}, Invites {AllowInvites}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AirlockFriends] Failed to send updated settings: {ex}");
            }
        }






        public static async Task RPC_NotifyFriendGroup(string name = "", bool updateSelf = true, string ModName = "", string customName = "")
        {
            if (!IsConnected)
                return;

            if (!string.IsNullOrEmpty(customName))
            {
                MyName = customName;
            }
            else if (updateSelf)
            {
                if (!string.IsNullOrEmpty(name) && IsUnwantedName(name) && MyName == FriendCode)
                    name = ModName;

                if (string.IsNullOrEmpty(name) && MyName == FriendCode)
                {
                    var rig = UnityEngine.Object.FindObjectOfType<XRRig>();
                    if (rig != null && !rig.PState.NetworkName.Value.Contains("#") && !rig.PState.IsSpectating)
                        MyName = rig.PState.NetworkName.Value ?? "";
                    else
                        MyName = "";
                }
                else if (MyName == FriendCode)
                {
                    MyName = name;
                }
            }

            var payload = new
            {
                type = "getFriends",
                fromPrivateKey = PrivateKey,
                currentName = MyName
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(EventData);
        }



        private static float JoinRequestTime = -999f;
        public static async Task RPC_RequestJoin(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;
            if (Time.realtimeSinceStartup - JoinRequestTime < 5f)
                return;
            JoinRequestTime = Time.realtimeSinceStartup;

            var payload = new
            {
                type = "joinRequest",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(EventData);
            NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Sent join request to <color=lime>{GetFriend(targetFriendCode).Name}</color>");
        }


        private static float InviteTime = -999f;
        public static async Task RPC_SendInvite(string targetFriendCode, string roomID)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            if (Time.realtimeSinceStartup - InviteTime < 5f)
                return;
            InviteTime = Time.realtimeSinceStartup;

            var payload = new
            {
                type = "sendInvite",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode,
                roomID = roomID
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(EventData);
            NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Sent join request to <color=lime>{GetFriend(targetFriendCode).Name}</color>");
        }



        public static async Task RPC_RespondToInvite(string targetFriendCode, bool accepted)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            var payload = new
            {
                type = "inviteResponse",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode,
                accepted = accepted
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(EventData);
        }

        public static async Task RPC_RespondToJoin(string senderFriendCode, bool accepted, bool InGame = true)
        {
            string roomCode = "";

            if (accepted)
            {
                var runner = UnityEngine.Object.FindObjectOfType<AirlockNetworkRunner>();
                if (runner != null)
                {
                    roomCode = runner.SessionInfo.Name;
                    MelonCoroutines.Start(GetUsername(senderFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Accepted {name}'s join request")));
                }
            }
            else if (InGame)
                MelonCoroutines.Start(GetUsername(senderFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Rejected {name}'s join request")));

            var payload = new
            {
                type = "joinRequestResponse",
                fromPrivateKey = PrivateKey,
                targetFriendCode = senderFriendCode,
                accepted = accepted,
                roomID = roomCode,
                inGame = InGame
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(payload, options);
            await RaiseEvent(EventData);

            MelonLogger.Msg($"[AirlockFriends] Responded to join request from {senderFriendCode}: {(accepted ? $"accepted (room {roomCode})" : "rejected")}");
        }


        public static async Task RPC_GetUsername(string friendCode)
        {
            if (!string.IsNullOrEmpty(friendCode) && IsConnected)
            {
                var request = new
                {
                    type = "getUserName",
                    fromPrivateKey = PrivateKey,
                    friendCode = friendCode
                };

                string EventData = System.Text.Json.JsonSerializer.Serialize(request);
                await RaiseEvent(EventData);
            }
        }


        public static async Task RPC_BlockUser(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode))
                return;

            var payload = new
            {
                type = "blockUser",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode
            };

            string data = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(data);
            MelonCoroutines.Start(GetUsername(targetFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] You've blocked <color=lime>{name}</color>")));
        }

        public static async Task RPC_UnblockUser(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode))
                return;

            var payload = new
            {
                type = "unblockUser",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode
            };

            string data = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(data);
            MelonCoroutines.Start(GetUsername(targetFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] You've unblocked <color=lime>{name}</color>")));
        }


        // this took too long to make and understand lmao
        public static IEnumerator GetUsername(string friendCode, System.Action<string> Callback)
        {
            if (GetNameFromID.TryGetValue(friendCode, out var name) && !string.IsNullOrEmpty(name))
            {
                Callback?.Invoke(name);
                yield break;
            }

            if (!GettingNames.Contains(friendCode))
            {
                GettingNames.Add(friendCode);
                _ = RPC_GetUsername(friendCode);
            }

            float elapsedTime = 0f;
            while ((!GetNameFromID.TryGetValue(friendCode, out name) || string.IsNullOrEmpty(name)) && elapsedTime < 5f)
            {
                elapsedTime += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (string.IsNullOrEmpty(name))
                name = friendCode;

            Callback?.Invoke(name);
        }




        public static void SaveUsername(string friendCode, string name)
        {
            if (!string.IsNullOrEmpty(friendCode))
            {
                GetNameFromID[friendCode] = name;
                GettingNames.Remove(friendCode);
                OnUsernameUpdated?.Invoke(friendCode, name);
            }
        }







        public static async Task Disconnect(bool force = false, bool rejected = false)
        {
            if (!IsConnected) return;
            try
            {
                if (rejected)
                    connectionStatus = ConnectionStatus.Rejected;
                else if (force)
                    connectionStatus = ConnectionStatus.ForciblyClosed;
                else
                    connectionStatus = ConnectionStatus.Disconnected;

                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", cts.Token);

                MelonLogger.Msg("[AirlockFriends] Disconnected from Websocket server.");
                NotificationLib.QueueNotification("[<color=magenta>Disconnect</color>] Disconnected from Websocket Server.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AirlockFriends] Disconnect failed: {ex.Message}");
            }
        }




    }
}
