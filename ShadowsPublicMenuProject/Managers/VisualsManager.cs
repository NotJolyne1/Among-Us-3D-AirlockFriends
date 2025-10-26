using System.Collections.Generic;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using MelonLoader;
using ShadowsPublicMenu.Config;
using UnityEngine;

namespace ShadowsPublicMenu.Managers
{
    public static class PlayerVisualManager
    {
        public static bool AdminFound = false;
        private static GameObject espHolder;
        private static GameObject lineRenderHolder;
        private static GameObject textHolder;

        public static Dictionary<PlayerState, GameObject[]> playerESPs = new Dictionary<PlayerState, GameObject[]>();
        public static Dictionary<PlayerState, LineRenderer> playerLines = new Dictionary<PlayerState, LineRenderer>();
        public static Dictionary<PlayerState, TextMesh> cooldownTexts = new Dictionary<PlayerState, TextMesh>();
        private static Dictionary<PlayerState, NetworkedLocomotionPlayer> playerLocomotions = new Dictionary<PlayerState, NetworkedLocomotionPlayer>();
        private static Dictionary<PlayerState, (float lastCooldown, float lastUpdateTime)> cooldownCache = new Dictionary<PlayerState, (float, float)>();
        public static Dictionary<PlayerState, TextMesh> MenuUserTags = new Dictionary<PlayerState, TextMesh>();
        public static Dictionary<PlayerState, TextMesh> MenuAdminTags = new Dictionary<PlayerState, TextMesh>();

        public static void CreateMenuUserTag(PlayerState state)
        {
            if (state == null || MenuUserTags.ContainsKey(state))
                return;

            if (textHolder == null)
                textHolder = new GameObject("UserTagESP_Holder");

            GameObject textObj = new GameObject($"TagText_{state.PlayerId}");
            textObj.transform.parent = textHolder.transform;
            TextMesh mesh = textObj.AddComponent<TextMesh>();

            mesh.fontSize = 60;
            mesh.characterSize = 0.03f;
            mesh.color = Color.magenta;
            mesh.alignment = TextAlignment.Center;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.richText = true;
            mesh.text = "Shadow's Menu";

            MenuUserTags[state] = mesh;
        }




        public static void CreateMenuAdminTag(PlayerState state)
        {
            if (state == null || MenuAdminTags.ContainsKey(state))
                return;
            AdminFound = true;
            if (textHolder == null)
                textHolder = new GameObject("UserTagESP_Holder");

            GameObject textObj = new GameObject($"TagText_{state.PlayerId}");
            textObj.transform.parent = textHolder.transform;
            TextMesh mesh = textObj.AddComponent<TextMesh>();

            mesh.fontSize = 60;
            mesh.characterSize = 0.03f;
            mesh.color = Color.magenta;
            mesh.alignment = TextAlignment.Center;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.richText = true;
            mesh.text = "Admin";

            MenuAdminTags[state] = mesh;
        }


        private static void UpdateMenuUserText(TextMesh mesh, Vector3 pos, Camera cam)
        {
            if (mesh == null)
                return;

            mesh.transform.position = pos + Vector3.up * 1.1f;
            mesh.transform.rotation = Quaternion.LookRotation(mesh.transform.position - cam.transform.position);
        }

        private static void UpdateMenuAdminText(TextMesh mesh, Vector3 pos, Camera cam)
        {
            if (mesh == null)
                return;

            mesh.transform.position = pos + Vector3.up * 1.1f;
            mesh.transform.rotation = Quaternion.LookRotation(mesh.transform.position - cam.transform.position);
        }

        public static void RemoveTagText(PlayerState state)
        {
            if (state == null) return;
            if (MenuUserTags.TryGetValue(state, out var txt))
            {
                if (txt != null) Object.Destroy(txt.gameObject);
                MenuUserTags.Remove(state);
            }
        }

        public static void DrawVisuals()
        {
            if (GameReferences.Rig == null || GameReferences.Spawn?.PlayerStates == null)
            {
                CleanupAll();
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 localPos = GameReferences.Rig.transform.position;

            var currentPlayers = new HashSet<PlayerState>();
            foreach (var ps in GameReferences.Spawn.PlayerStates)
                if (ps != null) currentPlayers.Add(ps);

            CleanupMissingPlayers(currentPlayers);

            foreach (var state in currentPlayers)
            {
                if (state == GameReferences.Rig.PState || !state.IsSpawned || !state.IsConnected || state.IsSpectating)
                {
                    RemoveESP(state);
                    RemoveTracer(state);
                    RemoveCooldownText(state);
                    RemoveTagText(state);
                    continue;
                }

                if (state.NetworkName.Value.Contains("Color##"))
                {
                    RemoveESP(state);
                    RemoveTracer(state);
                    RemoveCooldownText(state);
                    RemoveTagText(state);
                    continue;
                }

                if (!playerLocomotions.TryGetValue(state, out var loco) || loco == null)
                {
                    GameObject playerObj = GameObject.Find($"NetworkedLocomotionPlayer ({state.PlayerId})");
                    if (playerObj == null)
                    {
                        RemoveESP(state);
                        RemoveTracer(state);
                        RemoveCooldownText(state);
                        RemoveTagText(state);
                        continue;
                    }

                    loco = playerObj.GetComponent<NetworkedLocomotionPlayer>();
                    if (loco == null)
                    {
                        RemoveESP(state);
                        RemoveTracer(state);
                        RemoveCooldownText(state);
                        RemoveTagText(state);
                        continue;
                    }

                    playerLocomotions[state] = loco;
                }

                Vector3 targetPos = loco.RigidbodyPosition + new Vector3(0f, 0.6f, 0f);
                Color playerColor = Helpers.GetColorCodeFromID(state.ColorId);

                // Box ESP
                if (Mods.BoxESP)
                {
                    if (!playerESPs.TryGetValue(state, out var esp) || esp == null)
                    {
                        esp = CreateHollowESPBox();
                        playerESPs[state] = esp;
                    }
                    UpdateHollowBox(esp, targetPos, cam, playerColor);
                }
                else RemoveESP(state);

                if (Mods.Tracers)
                {
                    if (!playerLines.TryGetValue(state, out var line) || line == null)
                    {
                        line = CreateLineRenderer();
                        playerLines[state] = line;
                    }
                    UpdateTracer(line, localPos, targetPos, playerColor);
                }
                else RemoveTracer(state);

                if (Mods.CooldownESP)
                {
                    if (!cooldownTexts.TryGetValue(state, out var text) || text == null)
                    {
                        text = CreateCooldownText();
                        cooldownTexts[state] = text;
                    }
                    UpdateCooldownText(text, state, targetPos + Vector3.up * 1f, cam);
                }
                else RemoveCooldownText(state);

                if (MenuUserTags.TryGetValue(state, out var tagMesh) && tagMesh != null)
                {
                    UpdateMenuUserText(tagMesh, targetPos, cam);
                }

                if (MenuAdminTags.TryGetValue(state, out var AdminTagMesh) && tagMesh != null)
                {
                    UpdateMenuAdminText(AdminTagMesh, targetPos, cam);
                }
            }
        }

        private static TextMesh CreateCooldownText()
        {
            if (textHolder == null)
                textHolder = new GameObject("CooldownESP_Holder");

            GameObject textObj = new GameObject("CooldownText");
            textObj.transform.parent = textHolder.transform;
            TextMesh mesh = textObj.AddComponent<TextMesh>();

            mesh.fontSize = 60;
            mesh.characterSize = 0.03f;
            mesh.color = Color.green;
            mesh.alignment = TextAlignment.Center;
            mesh.anchor = TextAnchor.MiddleCenter;

            return mesh;
        }

        private static void UpdateCooldownText(TextMesh mesh, PlayerState state, Vector3 pos, Camera cam)
        {
            if (mesh == null || state == null) return;

            if (GameReferences.GameState != null && GameReferences.GameState.InLobbyState())
            {
                cooldownCache.Clear();
                mesh.text = "";
                return;
            }

            mesh.transform.position = pos;
            mesh.transform.rotation = Quaternion.LookRotation(mesh.transform.position - cam.transform.position);

            float cooldown = state.ActionCooldownRemaining;
            if (!cooldownCache.TryGetValue(state, out var data))
                data = (cooldown, Time.time);

            if (Mathf.Abs(data.lastCooldown - cooldown) > 0.001f)
                data = (cooldown, Time.time);

            cooldownCache[state] = data;

            if (cooldown > -1f)
            {
                if ((Time.time - data.lastUpdateTime) > 3f && !GameReferences.GameState.InVotingState() && cooldown != 0 && !state.InVent)
                    mesh.text = "";
                else
                {
                    mesh.text = $"Cooldown: {cooldown:F1}";
                    mesh.color = Color.green;
                }
            }
            else mesh.text = "";
        }


        private static void RemoveCooldownText(PlayerState state)
        {
            if (state == null) return;
            if (cooldownTexts.TryGetValue(state, out var txt))
            {
                if (txt != null) Object.Destroy(txt.gameObject);
                cooldownTexts.Remove(state);
            }
        }

        private static void CleanupMissingPlayers(HashSet<PlayerState> currentPlayers)
        {
            foreach (var kvp in new Dictionary<PlayerState, GameObject[]>(playerESPs))
                if (!currentPlayers.Contains(kvp.Key) || kvp.Key == null) RemoveESP(kvp.Key);

            foreach (var kvp in new Dictionary<PlayerState, LineRenderer>(playerLines))
                if (!currentPlayers.Contains(kvp.Key) || kvp.Key == null) RemoveTracer(kvp.Key);

            foreach (var kvp in new Dictionary<PlayerState, TextMesh>(cooldownTexts))
                if (!currentPlayers.Contains(kvp.Key) || kvp.Key == null) RemoveCooldownText(kvp.Key);

            foreach (var kvp in new Dictionary<PlayerState, NetworkedLocomotionPlayer>(playerLocomotions))
                if (!currentPlayers.Contains(kvp.Key) || kvp.Key == null) playerLocomotions.Remove(kvp.Key);

            foreach (var kvp in new Dictionary<PlayerState, TextMesh>(MenuUserTags))
                if (!currentPlayers.Contains(kvp.Key) || kvp.Key == null) RemoveTagText(kvp.Key);
        }

        private static void CleanupAll()
        {
            foreach (var kvp in playerESPs) DestroyESP(kvp.Value);
            playerESPs.Clear();

            foreach (var kvp in playerLines)
                if (kvp.Value != null) Object.Destroy(kvp.Value.gameObject);
            playerLines.Clear();

            foreach (var kvp in cooldownTexts)
                if (kvp.Value != null) Object.Destroy(kvp.Value.gameObject);
            cooldownTexts.Clear();

            foreach (var kvp in MenuUserTags)
                if (kvp.Value != null) Object.Destroy(kvp.Value.gameObject);
            MenuUserTags.Clear();

            playerLocomotions.Clear();
        }

        private static void RemoveESP(PlayerState state)
        {
            if (state == null) return;
            if (playerESPs.TryGetValue(state, out var oldESP))
            {
                DestroyESP(oldESP);
                playerESPs.Remove(state);
            }
        }

        private static void DestroyESP(GameObject[] esp)
        {
            if (esp == null) return;
            foreach (var o in esp)
                if (o != null) Object.Destroy(o);
        }

        private static GameObject[] CreateHollowESPBox()
        {
            if (espHolder == null) espHolder = new GameObject("ESP_Holder");

            GameObject[] lines = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(line.GetComponent<Collider>());
                line.transform.parent = espHolder.transform;

                var rend = line.GetComponent<Renderer>();
                rend.material = new Material(Shader.Find("GUI/Text Shader"));
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;

                lines[i] = line;
            }
            return lines;
        }

        private static void UpdateHollowBox(GameObject[] lines, Vector3 pos, Camera cam, Color color)
        {
            if (lines == null || lines.Length < 4) return;

            float width = 0.9f, height = 1.8f, thickness = 0.05f;
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0;
            if (camForward.sqrMagnitude < 0.001f) camForward = Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(camForward.normalized);

            lines[0].transform.position = pos + rotation * Vector3.up * (height / 2f);
            lines[0].transform.localScale = new Vector3(width, thickness, thickness);
            lines[0].transform.rotation = rotation;

            lines[1].transform.position = pos - rotation * Vector3.up * (height / 2f);
            lines[1].transform.localScale = new Vector3(width, thickness, thickness);
            lines[1].transform.rotation = rotation;

            lines[2].transform.position = pos + rotation * Vector3.right * (width / 2f);
            lines[2].transform.localScale = new Vector3(thickness, height, thickness);
            lines[2].transform.rotation = rotation;

            lines[3].transform.position = pos - rotation * Vector3.right * (width / 2f);
            lines[3].transform.localScale = new Vector3(thickness, height, thickness);
            lines[3].transform.rotation = rotation;

            foreach (var line in lines)
                if (line != null)
                    line.GetComponent<Renderer>().material.color = color;
        }

        private static void RemoveTracer(PlayerState state)
        {
            if (state == null) return;
            if (playerLines.TryGetValue(state, out var line))
            {
                if (line != null) Object.Destroy(line.gameObject);
                playerLines.Remove(state);
            }
        }

        private static LineRenderer CreateLineRenderer()
        {
            if (lineRenderHolder == null)
                lineRenderHolder = new GameObject("LineRender_Holder");

            GameObject lineObj = new GameObject("LineObject");
            lineObj.transform.parent = lineRenderHolder.transform;
            LineRenderer renderer = lineObj.AddComponent<LineRenderer>();

            renderer.numCapVertices = 10;
            renderer.numCornerVertices = 5;
            renderer.material.shader = Shader.Find("GUI/Text Shader");
            renderer.positionCount = 2;
            renderer.useWorldSpace = true;
            renderer.startWidth = 0.025f;
            renderer.endWidth = 0.025f;

            return renderer;
        }

        private static void UpdateTracer(LineRenderer line, Vector3 start, Vector3 end, Color color)
        {
            if (line == null) return;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startColor = color;
            line.endColor = color;
            if (!line.gameObject.activeInHierarchy) line.gameObject.SetActive(true);
        }
    }
}
