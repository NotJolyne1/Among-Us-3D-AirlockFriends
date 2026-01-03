using System.Collections.Generic;
using AirlockFriends.Config;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using UnityEngine;
using MelonLoader;

namespace ShadowsMenu.Managers
{
    public static class ModUserVisuals
    {
        private static readonly Dictionary<PlayerState, TextMesh> AFUserTags = new();
        private static readonly Vector3 TagHighPos = new Vector3(0f, 1.6f, 0f);

        public static void TryAdd(PlayerState PState)
        {
            if (!Settings.InGame || PState == null || PState == GameReferences.Rig.PState || !PState.IsConnected || PState.IsSpectating || AFUserTags.ContainsKey(PState))
                return;

            var textObj = new GameObject($"AFTagText ({PState.PlayerId})");
            textObj.transform.SetParent(PState.LocomotionPlayer.transform.Find("CrewmatePhysics"), false);
            textObj.transform.localPosition = TagHighPos;

            var mesh = textObj.AddComponent<TextMesh>();
            mesh.fontSize = 60;
            mesh.characterSize = 0.03f;
            mesh.alignment = TextAlignment.Center;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.richText = true;
            mesh.text = "AirlockFriendsUser";
            mesh.color = Color.magenta;

            AFUserTags[PState] = mesh;

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
    }
}
