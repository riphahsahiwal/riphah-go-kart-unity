// Procedural arena, ported from renderer.ts makeWorld().
// Colours, sizes, and positions are copied from the source; the ramp is the
// same triangle quad as in three.js, built as a Unity Mesh.
using UnityEngine;
using SmashKarts.Core;
using System.Collections.Generic;

namespace SmashKarts.Runtime
{
    public sealed class ArenaBuilder : MonoBehaviour
    {
        public List<GameObject> LootBoxes = new();

        public void Build()
        {
            // Sky / fog / lights — renderer.ts: background #dcaf89, fog #d5ae91 100-225,
            // hemisphere #c8e5ff/#735543 2.3, sun #ffdfae 3.4 at (-30,60,-25).
            RenderSettings.fog = true;
            RenderSettings.fogColor = Coord.Hex("#d5ae91");
            RenderSettings.fogStartDistance = 100f;
            RenderSettings.fogEndDistance = 225f;
            Camera.main.backgroundColor = Coord.Hex("#dcaf89");

            var hemi = gameObject.AddComponent<Light>();
            hemi.type = LightType.Directional; // Unity has no hemisphere light component; use a soft directional fill + ambient below.
            hemi.color = Coord.Hex("#c8e5ff");
            hemi.intensity = 1.2f;
            RenderSettings.ambientLight = Coord.Hex("#735543");

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Coord.Hex("#ffdfae");
            sun.intensity = 3.4f;
            sun.transform.position = new Vector3(-30, 60, -25);
            sun.transform.LookAt(Vector3.zero);
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = -0.0005f;
            sun.shadowNormalBias = 0.06f;

            var root = new GameObject("Arena").transform;
            Coord.Box(root, 400, 0.7f, 400, 0, -1.1f, 0, "#b88762");
            Coord.Box(root, 98, 1, 82, 0, -0.45f, 0, "#37404a");
            Coord.Box(root, 92, 0.14f, 76, 0, 0.02f, 0, "#5e6770");
            for (float z = -31; z <= 31; z += 62)
                for (float x = -42; x < 44; x += 7)
                    Coord.Box(root, 3.5f, 0.02f, 0.24f, x, 0.11f, z, "#c4c1a7");
            for (float x = -40; x <= 40; x += 80)
                for (float z = -26; z < 29; z += 7)
                    Coord.Box(root, 0.24f, 0.02f, 3.5f, x, 0.11f, z, "#c4c1a7");

            // Central raised crossing + ramps.
            Coord.Box(root, 15, 3, 12, 0, 1.5f, 0, "#444f5a");
            Coord.Box(root, 15, 0.05f, 12, 0, 3.02f, 0, "#6c7680");
            foreach (float side in new[] { -1f, 1f }) BuildRamp(root, side);

            // Border walls, alternating colours.
            for (float x = -46; x <= 46; x += 4)
                foreach (float z in new[] { -39f, 39f })
                    Coord.Box(root, 3.85f, 1.4f, 1, x, 0.7f, z, Mathf.RoundToInt(x / 4) % 2 == 0 ? "#e8ca64" : "#343c45");
            for (float z = -35; z <= 35; z += 4)
                foreach (float x in new[] { -47f, 47f })
                    Coord.Box(root, 1, 1.4f, 3.85f, x, 0.7f, z, Mathf.RoundToInt(z / 4) % 2 == 0 ? "#e8ca64" : "#343c45");

            // Obstacles from GameRoom.Obstacles.
            foreach (var o in GameRoom.Obstacles)
            {
                string col = o.X * o.Z > 0 ? "#b7674a" : "#5f8e91";
                Coord.Box(root, (float)o.W, (float)o.H, (float)o.D, (float)o.X, (float)o.H / 2, (float)o.Z, col);
                Coord.Box(root, (float)o.W + 0.2f, 0.18f, (float)o.D + 0.2f, (float)o.X, (float)o.H, (float)o.Z, "#263c46");
                AddLabel(root, new Vector3((float)o.X, (float)o.H + 0.75f, (float)o.Z), o.X * o.Z > 0 ? "ARENA 01" : "SMASH", o.X * o.Z > 0 ? "#ffe6b3" : "#b9fcfc", 4f);
            }

            // Corner lamps, SUNSET CIRCUIT board, distant cones, trees.
            foreach (var (x, z) in new[] { (-48f, -40f), (48f, 40f), (48f, -40f), (-48f, 40f) })
            {
                var cyl = Coord.Primitive(PrimitiveType.Cylinder, new Vector3(x, 7, z), new Vector3(0.44f, 14, 0.8f), "#33434f", root);
                _ = cyl;
                Coord.Box(root, 4, 1, 1, x, 14, z, "#e9e4bb");
            }
            AddLabel(root, new Vector3(0, 12, -45), "SUNSET CIRCUIT", "#d7fa52", 29f);
            for (int i = 0; i < 18; i++)
            {
                float a = (i / 18f) * Mathf.PI * 2, r = 90 + (i % 3) * 8, h = 12 + (i % 5) * 6;
                var cone = Coord.Cone(root, new Vector3(Mathf.Sin(a) * r, h / 2 - 1, Mathf.Cos(a) * r), 13 + (i % 4) * 3, h, i % 2 == 1 ? "#aa7c61" : "#c0926f");
                cone.transform.rotation = Quaternion.Euler(0, i, 0);
            }
            for (int i = 0; i < 16; i++)
            {
                float a = (i * Mathf.PI) / 8, x = Mathf.Sin(a) * 62, z = Mathf.Cos(a) * 54;
                Coord.Primitive(PrimitiveType.Cylinder, new Vector3(x, 2, z), new Vector3(0.8f, 5, 1.4f), "#6f5945", root);
                for (int j = 0; j < 4; j++)
                    Coord.Cone(root, new Vector3(x + Mathf.Sin(j * 1.57f) * 1.7f, 4.7f, z + Mathf.Cos(j * 1.57f) * 1.7f), 1.8f, 5, "#687757");
            }

            // Loot boxes, one per BOX_POSITIONS entry, in source order.
            foreach (var bp in GameRoom.BoxPositions)
            {
                var g = new GameObject("LootBox");
                g.transform.SetParent(root, false);
                g.transform.position = Coord.ToUnity(bp[0], GameConstants.GroundHeight(bp[0], bp[1]) + 1.35, bp[1]);
                Coord.Box(g.transform, 1.65f, 1.65f, 1.65f, 0, 0, 0, "#bacf46");
                AddLabel(g.transform, new Vector3(0, 0.05f, 0), "?", "#fffaca", 1.35f);
                LootBoxes.Add(g);
            }
        }

        private void BuildRamp(Transform root, float side)
        {
            // renderer.ts ramp vertices, in TS (x, y, z) order, used unchanged here
            // because gameplay XZ is mapped identity (see Coord.cs).
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-7.5f, 0, 19 * side), new Vector3(7.5f, 0, 19 * side), new Vector3(-7.5f, 3, 6 * side),
                new Vector3(7.5f, 0, 19 * side), new Vector3(7.5f, 3, 6 * side), new Vector3(-7.5f, 3, 6 * side),
            };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            mesh.RecalculateNormals();
            var go = new GameObject("Ramp");
            go.transform.SetParent(root, false);
            var filter = go.AddComponent<MeshFilter>(); filter.mesh = mesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.material = Coord.LitMaterial("#737c85");
            rend.material.SetInt("_DoubleSidedEnable", 1);
        }

        public static void AddLabel(Transform parent, Vector3 pos, string text, string hex, float scaleX)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.color = Coord.Hex(hex);
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.25f * scaleX / 4f;
            // Face the default camera direction; GameManager billboards labels each frame in a later pass.
        }
    }
}
