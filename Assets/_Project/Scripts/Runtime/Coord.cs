// Coordinate mapping for this game, verified against renderer.ts, NOT the
// generic three.js -> Unity mirror rule.
// TS movement: dx = sin(angle) * speed * dt, dz = cos(angle) * speed * dt.
// three.js kart rotation.y = angle, and its +Z forward is (sin angle, cos angle).
// Unity rotation Y (degrees) gives the same +Z forward: (sin angle, cos angle).
// Therefore: Unity position = (x, y, z) unchanged, rotation Y = angle in degrees.
// Mirroring Z here would flip steering. Do not apply FromThree.Position to
// gameplay positions in this game; that helper is only for raw three.js mesh
// data, which this game does not use (all geometry is procedural).
using UnityEngine;
using SmashKarts.Core;

namespace SmashKarts.Runtime
{
    public static class Coord
    {
        public static Vector3 ToUnity(double x, double y, double z)
            => new Vector3((float)x, (float)y, (float)z);

        public static Vector3 ToUnity(Player p) => ToUnity(p.X, p.Y, p.Z);
        public static Vector3 ToUnity(Projectile q) => ToUnity(q.X, q.Y, q.Z);

        public static Quaternion RotationY(double angleRad)
            => Quaternion.Euler(0f, (float)(angleRad * Mathf.Rad2Deg), 0f);

        public static Vector3 Forward(double angleRad)
            => new Vector3((float)System.Math.Sin(angleRad), 0f, (float)System.Math.Cos(angleRad));

        public static Color Hex(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) { c.a = alpha; return c; }
            return Color.white;
        }

        public static Material LitMaterial(string hex, float metallic = 0.05f, bool emissive = false)
        {
            // This project has no Scriptable Render Pipeline asset assigned
            // (no GraphicsSettings / URP asset in the repo), so the player
            // runs on the built-in render pipeline. A URP/Lit material under
            // the built-in pipeline renders as Unity's bright-magenta error
            // material in a player build — exactly what was seen on a local
            // PC (tan sky, everything else magenta). So prefer the built-in
            // Standard shader whenever no SRP asset is active, and only use
            // URP/Lit when a pipeline asset is actually assigned.
            var shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                ? Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            var col = Hex(hex);
            mat.SetColor("_BaseColor", col);
            mat.SetColor("_Color", col);
            if (emissive) { mat.SetColor("_EmissionColor", col); mat.EnableKeyword("_EMISSION"); }
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", 0.6f);
            mat.SetFloat("_Glossiness", 0.6f);
            return mat;
        }

        public static GameObject Primitive(PrimitiveType type, Vector3 pos, Vector3 scale, string hex, Transform parent = null)
        {
            // Unity cylinder primitive: 2 units high, 1 unit diameter (Unity Manual,
            // Primitive Objects). Callers pass scale as (diameter, height, diameter),
            // so divide the Y scale by 2 for cylinders only. Cube is 1x1x1 and
            // sphere diameter is 1, so those need no adjustment.
            if (type == PrimitiveType.Cylinder) scale = new Vector3(scale.x, scale.y / 2f, scale.z);
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.material = LitMaterial(hex);
            return go;
        }

        public static GameObject Box(Transform parent, float w, float h, float d, float x, float y, float z, string hex)
            => Primitive(PrimitiveType.Cube, new Vector3(x, y, z), new Vector3(w, h, d), hex, parent);

        // Cone primitive (not in PrimitiveType on all Unity versions): a simple
        // procedural cone mesh, centred at its position, height along Y.
        public static GameObject Cone(Transform parent, Vector3 pos, float radius, float height, string hex, int segments = 8)
        {
            var go = new GameObject("Cone");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var verts = new System.Collections.Generic.List<Vector3> { new Vector3(0, height / 2, 0) };
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2;
                verts.Add(new Vector3(Mathf.Sin(a) * radius, -height / 2, Mathf.Cos(a) * radius));
            }
            for (int i = 0; i < segments; i++)
                tris.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % segments });
            var mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            var filter = go.AddComponent<MeshFilter>(); filter.mesh = mesh;
            var rend = go.AddComponent<MeshRenderer>(); rend.material = LitMaterial(hex);
            return go;
        }
    }
}
