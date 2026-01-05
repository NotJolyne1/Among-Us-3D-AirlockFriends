using System.Collections.Generic;
using AirlockFriends.Config;
using AirlockFriends.Managers;
using Il2CppSG.Airlock;
using UnityEngine;
using static AirlockFriends.UI.FriendGUI;

namespace ShadowsMenu.Managers
{
    public static class ModUserVisuals
    {
        private static readonly Dictionary<int, TextMesh> AFUserTags = new();
        private static readonly Vector3 TagHighPos = new(0f, 1.6f, 0f);

        public static void TryAdd(PlayerState PState, string FriendID = "")
        {
            if (!Settings.InGame || PState == null || PState == GameReferences.Rig.PState || !PState.IsConnected || PState.IsSpectating)
                return;

            bool isFriend = false;
            if (!string.IsNullOrEmpty(FriendID))
            {
                foreach (FriendInfo friend in friends)
                {
                    if (friend.FriendCode == FriendID)
                    {
                        isFriend = true;
                        break;
                    }
                }
            }

            if (AFUserTags.TryGetValue(PState.PlayerId, out var existingMesh) && existingMesh != null)
            {
                existingMesh.text = isFriend ? "Friend" : "AirlockFriendsUser";
                existingMesh.color = isFriend ? Color.yellow : Color.magenta;
                return;
            }

            var parent = PState.LocomotionPlayer?.transform.Find("CrewmatePhysics");
            if (parent == null)
                return;

            var textObj = new GameObject($"AFTagText ({PState.PlayerId})");
            textObj.transform.SetParent(parent, false);
            textObj.transform.localPosition = TagHighPos;

            var mesh = textObj.AddComponent<TextMesh>();
            mesh.fontSize = 60;
            mesh.characterSize = 0.03f;
            mesh.alignment = TextAlignment.Center;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.richText = true;
            mesh.text = isFriend ? "Friend" : "AirlockFriendsUser";
            mesh.color = isFriend ? Color.yellow : Color.magenta;

            AFUserTags[PState.PlayerId] = mesh;

            if (isFriend)
                NotificationLib.QueueNotification($"[<color=lime>Friend</color>] <color=lime>{GetFriend(FriendID).Name}</color> is in your room!");
        }

        public static void Remove(PlayerState PState)
        {
            if (PState == null)
                return;

            if (AFUserTags.TryGetValue(PState.PlayerId, out var mesh) && mesh != null)
                Object.Destroy(mesh.gameObject);

            AFUserTags.Remove(PState.PlayerId);
        }

        public static void CleanupAll()
        {
            try
            {
                foreach (var mesh in AFUserTags.Values)
                    if (mesh != null)
                        Object.Destroy(mesh.gameObject);

                AFUserTags.Clear();
            }
            catch { }
        }

        public static void Update()
        {
            if (Camera.main == null)
                return;

            foreach (var kvp in AFUserTags)
            {
                if (kvp.Value == null)
                    continue;

                kvp.Value.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
