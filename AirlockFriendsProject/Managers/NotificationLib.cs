using System.Collections.Concurrent;
using System.Collections.Generic;
using AirlockFriends.Config;
using UnityEngine;

namespace AirlockFriends.Managers
{
    public class NotificationLib : MonoBehaviour
    {
        private static GameObject textHolder;
        private static List<Notification> Notifications = new List<Notification>();
        private static bool initialized = false;
        private static Font font;
        private static ConcurrentQueue<string> QueuedMessages = new ConcurrentQueue<string>();
        private class Notification
        {
            public TextMesh textMesh;
            public float SpawnTime;
        }

        public static void QueueNotification(string text, bool force = false)
        {
            if (Settings.ShowNotifications || force)
                QueuedMessages.Enqueue(text);
        }

        public static void SendNotification(string text, bool force = false)
        {
            if (!Settings.ShowNotifications && !force)
                return;

            try
            {
                var cam = Camera.main;
                if (cam == null || cam.gameObject == null)
                    return;

                if (!initialized)
                {
                    initialized = true;
                    if (textHolder == null) textHolder = new GameObject("NotificationLibHolder");
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                if (textHolder == null) textHolder = new GameObject("NotificationLibHolder");

                var textObj = new GameObject("NotificationObj");
                textObj.transform.parent = textHolder.transform;

                var textMesh = textObj.AddComponent<TextMesh>();
                textMesh.font = font;
                textMesh.fontSize = 45;
                textMesh.characterSize = 0.015f;
                textMesh.color = Color.white;
                textMesh.alignment = TextAlignment.Left;
                textMesh.anchor = TextAnchor.LowerLeft;
                textMesh.richText = true;
                textMesh.text = text;

                Notifications.Insert(0, new Notification
                {
                    textMesh = textMesh,
                    SpawnTime = Time.time
                });
                UpdatePositions();
            }
            catch { }
        }

        private static void UpdatePositions()
        {
            if (Camera.main == null)
                return;

            try
            {
                Transform Cam = Camera.main.transform;
                Vector3 basePos;

                if (Settings.IsVR)
                {
                    basePos = Cam.position + Cam.forward * 2f - Cam.up * 0.7f - Cam.right * 1f;
                }
                else
                {
                    if (GameReferences.GameState != null)
                    {
                        if (GameReferences.GameState.InTaskState())
                            basePos = Cam.position + Cam.forward * 2f - Cam.up * 0.30f - Cam.right * 1.5f;
                        else if (GameReferences.GameState.InLobbyState() || GameReferences.GameState.InVotingState())
                            basePos = Cam.position + Cam.forward * 2f - Cam.up * 0.7f - Cam.right * 1.8f;
                        else
                        {
                            basePos = Cam.position + Cam.forward * 2f
                                - Cam.up * (Settings.InGame ? 0.2f : 0.6f)
                                - Cam.right * (Settings.InGame ? 1f : 1.7f);
                        }
                    }
                    else
                    {
                        basePos = Cam.position + Cam.forward * 2f - Cam.up * (Settings.InGame ? 0.2f : 0.6f) - Cam.right * (Settings.InGame ? 1f : 1.7f);
                    }
                }

                float Offset = 0f;
                for (int i = 0; i < Notifications.Count; i++)
                {
                    Notification noti = Notifications[i];
                    if (noti.textMesh == null) continue;

                    float Spacing = 0.14f + (noti.textMesh.text.Split('\n').Length - 1) * 0.05f;
                    Vector3 StackedPos = basePos + Cam.up * Offset;
                    noti.textMesh.transform.position = StackedPos;
                    noti.textMesh.transform.rotation = Cam.rotation;
                    Offset += Spacing;
                }
            }
            catch { }
        }

        public static void ClearNotifications()
        {
            foreach (var Noti in Notifications)
                if (Noti.textMesh != null)
                    Destroy(Noti.textMesh.gameObject);
            Notifications.Clear();
        }

        public static void Update()
        {
            while (QueuedMessages.TryDequeue(out string noti))
                SendNotification(noti);

            if (!initialized || Camera.main == null)
                return;

            for (int i = Notifications.Count - 1; i >= 0; i--)
            {
                try
                {
                    float duration = Notifications[i].textMesh.text.Contains("blacklisted") ? 15f : Settings.NotiDuration;

                    if (Time.time - Notifications[i].SpawnTime > duration)
                    {
                        if (Notifications[i].textMesh != null)
                            Destroy(Notifications[i].textMesh.gameObject);

                        Notifications.RemoveAt(i);
                    }
                }
                catch { }

            }
            UpdatePositions();
        }
    }
}
