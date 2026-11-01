// Simple event effects, matching renderer.ts particle kinds at a first pass:
// shot flashes, explosion fragments + shockwave, pickup glow.
// Full particle-system tuning is a Phase 8 task; this keeps the same events,
// colours, and positions as the source.
using UnityEngine;
using System.Collections;
using SmashKarts.Core;

namespace SmashKarts.Runtime
{
    public sealed class Effects : MonoBehaviour
    {
        public void Burst(Vector3 pos, string hex, float size, int count = 12)
        {
            for (int i = 0; i < count; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.transform.position = pos;
                s.transform.localScale = Vector3.one * size * 0.3f;
                var r = s.GetComponent<Renderer>(); r.material = Coord.LitMaterial(hex, 0, true);
                StartCoroutine(FadeAndDestroy(s, 0.5f, Random.insideUnitSphere * 6f));
            }
        }

        public void Ring(Vector3 pos, string hex, float size)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.transform.position = pos;
            t.transform.localScale = new Vector3(size, 0.1f, size);
            var r = t.GetComponent<Renderer>(); r.material = Coord.LitMaterial(hex, 0, true);
            StartCoroutine(ScaleAndDestroy(t, 0.5f, size * 2f));
        }

        private IEnumerator FadeAndDestroy(GameObject go, float life, Vector3 vel)
        {
            float t = 0;
            while (t < life)
            {
                t += Time.deltaTime;
                go.transform.position += vel * Time.deltaTime;
                vel.y -= 12 * Time.deltaTime;
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator ScaleAndDestroy(GameObject go, float life, float end)
        {
            float t = 0; var start = go.transform.localScale;
            while (t < life)
            {
                t += Time.deltaTime;
                go.transform.localScale = Vector3.Lerp(start, new Vector3(end, 0.1f, end), t / life);
                yield return null;
            }
            Destroy(go);
        }

        public void OnEvent(GameEvent e)
        {
            var pos = Coord.ToUnity(e.X, e.Y, e.Z);
            switch (e.Kind)
            {
                case "shot": Burst(pos, e.Color, e.Size, 3); break;
                case "explosion": Burst(pos, e.Color, e.Size, 20); Ring(pos, e.Color, e.Size); break;
                case "hit": Burst(pos, e.Color, e.Size, 5); break;
                case "smash": Burst(pos, e.Color, 5, 24); Ring(pos, "#fff3a0", 5); break;
                case "pickup": Burst(pos, e.Color, 1, 8); break;
                case "freeze": Burst(pos, "#a9f4ff", 2, 10); break;
                case "shield": Ring(pos, e.Color, 4); break;
            }
        }
    }
}
