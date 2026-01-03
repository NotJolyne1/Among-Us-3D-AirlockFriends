using System.Collections.Generic;
using AirlockFriends.Config;
using AirlockFriends.Managers;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using MelonLoader;
using UnityEngine;
using static AirlockFriends.UI.FriendGUI;

namespace ShadowsMenu.Managers
{
    public static class ModUserVisuals
    {
        private static readonly Dictionary<PlayerState, TextMesh> AFUserTags = new();
        private static readonly Vector3 TagHighPos = new Vector3(0f, 1.6f, 0f);

        public static void TryAdd(PlayerState PState, string FriendID = "")
        {
            if (!Settings.InGame || PState == null || PState == GameReferences.Rig.PState || !PState.IsConnected || PState.IsSpectating || AFUserTags.ContainsKey(PState))
                return;

            bool isFriend = false;
            foreach (FriendInfo friend in friends)
            {
                if (!string.IsNullOrEmpty(FriendID) && friend.FriendCode == FriendID)
                {
                    isFriend = true;
                    break;
                }
            }

            var textObj = new GameObject($"AFTagText ({PState.PlayerId})");
            textObj.transform.SetParent(PState.LocomotionPlayer.transform.Find("CrewmatePhysics"), false);
            textObj.transform.localPosition = TagHighPos;

            var mesh = textObj.AddComponent<TextMesh>();
            mesh.fontSize = 60;
            mesh.characterSize = 0.03f;
            mesh.alignment = TextAlignment.Center;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.richText = true;
            mesh.text = isFriend ? "Friend" : "AirlockFriendsUser";
            mesh.color = isFriend ? Color.yellow : Color.magenta;

            AFUserTags[PState] = mesh;

            if (isFriend)
            {
                MelonLogger.Msg($"{PState.NetworkName.Value} is in your room!");
                NotificationLib.QueueNotification($"[<color=lime>Friend</color>] <color=lime>{GetFriend(FriendID).Name}</color> is in your room!");
            }
            else
                MelonLogger.Msg($"{PState.NetworkName.Value} is using Airlock Friends");
        }

        public static void Remove(PlayerState PState)
        {
            if (PState == null)
                return;

            if (AFUserTags.TryGetValue(PState, out var mesh) && mesh != null)
                Object.Destroy(mesh.gameObject);

            AFUserTags.Remove(PState);
        }

        public static void CleanupAll()
        {
            foreach (var kvp in AFUserTags)
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value.gameObject);

            AFUserTags.Clear();
        }

        public static void Update()
        {
            try
            {
                foreach (var kvp in AFUserTags)
                {
                    kvp.Value.transform.rotation = GameReferences.Rig.PState.LocomotionPlayer.RigidbodyRotation;
                }
            }
            catch { }
        }
    }
}
