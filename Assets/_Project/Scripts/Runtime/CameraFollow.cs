// Camera, ported from renderer.ts animate() follow + demo branches.
// Follow: behind kart 11.7 units, +7.5 height, clamped, lerp 1-exp(-dt*6),
// lookAt kart + forward*6, height +1.3, FOV 57 + |speed|*0.15.
// Demo (no local player): slow orbit, lookAt (4,0,0), FOV 51.
using UnityEngine;
using SmashKarts.Core;

namespace SmashKarts.Runtime
{
    public sealed class CameraFollow : MonoBehaviour
    {
        public float Shake;
        private float _clock;

        public void UpdateCamera(Player me, Transform kartTransform, float dt)
        {
            _clock += dt;
            var cam = Camera.main;
            if (cam == null) return;
            if (me == null)
            {
                float a = _clock * 0.035f;
                var v = new Vector3(54 * Mathf.Cos(a), 43, 58 * Mathf.Sin(a) + 50);
                cam.transform.position = Vector3.Lerp(cam.transform.position, v, 0.03f);
                cam.transform.LookAt(new Vector3(4, 0, 0));
                cam.fieldOfView = 51;
                return;
            }
            var focus = kartTransform.position;
            var forward = Coord.Forward(me.Angle);
            var pos = new Vector3(
                Mathf.Clamp(focus.x - forward.x * 11.7f, -49, 49),
                focus.y + 7.5f,
                Mathf.Clamp(focus.z - forward.z * 11.7f, -41, 41));
            cam.transform.position = Vector3.Lerp(cam.transform.position, pos, 1 - Mathf.Exp(-dt * 6));
            Shake = Mathf.Max(0, Shake - dt * 2);
            cam.transform.position += new Vector3((Random.value - 0.5f) * Shake, (Random.value - 0.5f) * Shake, 0);
            cam.transform.LookAt(new Vector3(focus.x + forward.x * 6, focus.y + 1.3f, focus.z + forward.z * 6));
            cam.fieldOfView = 57 + Mathf.Abs((float)me.Speed) * 0.15f;
        }
    }
}
