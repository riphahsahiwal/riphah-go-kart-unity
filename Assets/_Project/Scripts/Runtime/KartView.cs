// Kart visual, ported from renderer.ts makeKart().
// Dimensions/colours follow the source; wheels, shield, ice, spikes, and
// weapon barrel are the same objects the source toggles.
using UnityEngine;
using SmashKarts.Core;
using System.Collections.Generic;

namespace SmashKarts.Runtime
{
    public sealed class KartView : MonoBehaviour
    {
        public string PlayerId;
        public GameObject Body;
        public List<GameObject> Wheels = new();
        public GameObject Shield, Ice, Spikes, WeaponMount, LabelObj;
        private WeaponKind? _lastWeapon;
        private TextMesh _label;

        public static KartView Create(Player p)
        {
            var go = new GameObject($"Kart_{p.Id}");
            var view = go.AddComponent<KartView>();
            view.PlayerId = p.Id;
            string col = GameConstants.Colors[p.Color];
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            view.Body = body;
            var t = body.transform;
            Coord.Box(t, 2.0f, 0.32f, 3.1f, 0, 0.56f, 0, "#212a35");
            Coord.Box(t, 1.8f, 0.46f, 2.6f, 0, 0.83f, 0.2f, col);
            Coord.Box(t, 1.35f, 0.3f, 1, 0, 1.08f, 1, col);
            Coord.Box(t, 0.25f, 0.035f, 1.3f, 0, 1.25f, 1, "#f5f6df");
            Coord.Box(t, 2.25f, 0.17f, 0.35f, 0, 0.5f, 1.7f, "#c6cdd1");
            Coord.Box(t, 1.9f, 0.2f, 0.27f, 0, 0.55f, -1.5f, "#323d48");
            Coord.Box(t, 2.3f, 0.17f, 0.55f, 0, 1.5f, -1.25f, col);
            Coord.Box(t, 1.1f, 0.85f, 0.42f, 0, 1.12f, -0.4f, "#253240");
            Coord.Primitive(PrimitiveType.Sphere, new Vector3(0, 1.45f, -0.25f), new Vector3(0.86f, 1.06f, 0.77f), "#f8e6d2", t);
            Coord.Primitive(PrimitiveType.Sphere, new Vector3(0, 2.15f, -0.2f), new Vector3(1.3f, 1.25f, 1.27f), col, t);
            Coord.Primitive(PrimitiveType.Sphere, new Vector3(0, 2.16f, 0.11f), new Vector3(1.02f, 0.6f, 0.8f), "#142933", t);
            foreach (float side in new[] { -1f, 1f })
            {
                Coord.Primitive(PrimitiveType.Sphere, new Vector3(side * 0.48f, 1.52f, 0.28f), new Vector3(0.38f, 0.38f, 0.72f), "#f6e6c9", t);
                Coord.Box(t, 0.19f, 0.21f, 0.2f, side * 0.45f, 1.5f, 0.63f, "#263642");
            }
            // Wheels, as in the source: x ±1.05, z -0.95 / 1.06, r 0.46, w 0.4.
            foreach (float x in new[] { -1.05f, 1.05f })
                foreach (float z in new[] { -0.95f, 1.06f })
                {
                    var wheel = Coord.Primitive(PrimitiveType.Cylinder, new Vector3(x, 0.48f, z), new Vector3(0.92f, 0.4f, 0.92f), "#17212b", go.transform);
                    wheel.transform.rotation = Quaternion.Euler(0, 0, 90);
                    view.Wheels.Add(wheel);
                }
            foreach (float x in new[] { -0.65f, 0.65f })
                Coord.Primitive(PrimitiveType.Cylinder, new Vector3(x, 0.8f, -1.5f), new Vector3(0.26f, 0.55f, 0.26f), "#6a7b85", t);

            var shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shield.transform.SetParent(go.transform, false);
            shield.transform.localPosition = new Vector3(0, 1, 0);
            shield.transform.localScale = Vector3.one * 4.2f;
            var sm = shield.GetComponent<Renderer>(); sm.material = Coord.LitMaterial("#d7fa52"); sm.material.color = new Color(0.84f, 0.98f, 0.32f, 0.14f);
            shield.SetActive(false); view.Shield = shield;

            var ice = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ice.transform.SetParent(go.transform, false);
            ice.transform.localPosition = new Vector3(0, 1.3f, 0);
            ice.transform.localScale = new Vector3(2.9f, 3.1f, 3.9f);
            var im = ice.GetComponent<Renderer>(); im.material = Coord.LitMaterial("#97e8ff"); im.material.color = new Color(0.59f, 0.91f, 1f, 0.5f);
            ice.SetActive(false); view.Ice = ice;

            var spikes = new GameObject("Spikes");
            spikes.transform.SetParent(go.transform, false);
            spikes.transform.localPosition = new Vector3(0, 0.9f, 0);
            for (int i = 0; i < 5; i++)
            {
                float a = (i / 5f) * Mathf.PI * 2;
                Coord.Primitive(PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * 3.2f, 0, Mathf.Cos(a) * 3.2f), Vector3.one * 0.9f, "#ae90ed", spikes.transform);
            }
            spikes.SetActive(false); view.Spikes = spikes;

            var mount = new GameObject("WeaponMount");
            mount.transform.SetParent(go.transform, false);
            mount.transform.localPosition = new Vector3(0, 0.2f, 0.1f);
            view.WeaponMount = mount;

            var label = new GameObject("NameLabel");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0, 3.4f, 0);
            var tm = label.AddComponent<TextMesh>();
            tm.text = p.Name; tm.color = Color.white; tm.fontSize = 36; tm.anchor = TextAnchor.MiddleCenter; tm.characterSize = 0.2f;
            view._label = tm; view.LabelObj = label;
            return view;
        }

        public void Sync(Player p, float dt, float clock, string myId)
        {
            // renderer.ts animate(): lerp position, smooth rotation, body bob/tilt,
            // wheel spin, shield/ice/spikes visibility, weapon barrel rebuild.
            gameObject.SetActive(p.Respawn <= 0);
            if (p.Respawn > 0) return;
            var target = Coord.ToUnity(p);
            if (Vector3.Distance(transform.position, target) > 12) transform.position = target;
            else transform.position = Vector3.Lerp(transform.position, target, 1 - Mathf.Exp(-dt * 20));
            float currentY = transform.rotation.eulerAngles.y * Mathf.Deg2Rad;
            float diff = (float)GameConstants.AngleDiff(p.Angle, currentY);
            transform.rotation = Quaternion.Euler(0, (currentY + diff * (1 - Mathf.Exp(-dt * 22))) * Mathf.Rad2Deg, 0);
            Body.transform.localRotation = Quaternion.Euler(0, 0, (float)(-p.Input.Steer * p.Speed * 0.0019));
            Body.transform.localPosition = new Vector3(0, Mathf.Abs((float)p.Speed) > 0.5f ? Mathf.Sin(clock * 30) * 0.02f : 0, 0);
            foreach (var w in Wheels) w.transform.Rotate((float)p.Speed * dt * 1.7f, 0, 0, Space.Self);
            Shield.SetActive(p.Shield > 0 || (p.Weapon == WeaponKind.Star && p.Active > 0));
            Shield.transform.localScale = Vector3.one * 4.2f * (1 + Mathf.Sin(clock * 7) * 0.03f);
            var shMat = Shield.GetComponent<Renderer>().material;
            shMat.color = p.Shield > 0 ? new Color(0.47f, 0.81f, 1f, 0.14f) : new Color(0.84f, 0.98f, 0.32f, 0.14f);
            Ice.SetActive(p.Frozen > 0);
            Spikes.SetActive(p.Weapon == WeaponKind.Spiky && p.Active > 0);
            Spikes.transform.rotation = Quaternion.Euler(0, clock * 4 * Mathf.Rad2Deg, 0);
            LabelObj.SetActive(p.Id != myId);
            _label.text = p.Name;
            if (_lastWeapon != p.Weapon)
            {
                _lastWeapon = p.Weapon;
                foreach (Transform child in WeaponMount.transform) Destroy(child.gameObject);
                if (p.Weapon != null && (p.Weapon == WeaponKind.Rockets || p.Weapon == WeaponKind.Nuke || p.Weapon == WeaponKind.Machinegun || p.Weapon == WeaponKind.Bullets || p.Weapon == WeaponKind.Cannon || p.Weapon == WeaponKind.Snow))
                {
                    var wcol = Weapons.Defs[p.Weapon.Value].Color;
                    var barrel = Coord.Primitive(PrimitiveType.Cylinder, new Vector3(0, 0, 0), new Vector3(0.24f, 0.9f, 0.24f), "#26323c", WeaponMount.transform);
                    barrel.transform.rotation = Quaternion.Euler(90, 0, 0);
                    Coord.Box(WeaponMount.transform, 0.5f, 0.4f, 0.6f, 0, 0, -0.3f, wcol);
                }
            }
            // Billboard the name label toward the camera.
            if (Camera.main != null) LabelObj.transform.rotation = Quaternion.LookRotation(LabelObj.transform.position - Camera.main.transform.position);
        }
    }
}
