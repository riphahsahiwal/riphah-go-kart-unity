// HUD + menus, ported from app/page.tsx for the vertical slice.
// All user-visible copy is in English (per user instruction Nov 2026; the source game was Korean). Full lobby (name, colour,
// quick/create/join/practice, settings, help) is a Phase 7 task; this file
// covers the in-game HUD, leaderboard, countdown/results, and Esc menu.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SmashKarts.Core;
using System.Linq;
using System.Text;

namespace SmashKarts.Runtime
{
    public sealed class GameUI : MonoBehaviour
    {
        private Text _clock, _round, _weapon, _leaderboard, _centre, _feed;
        private GameObject _leaderboardPanel, _menuPanel;
        private bool _menuOpen;

        public bool MenuOpen => _menuOpen;

        public void Build()
        {
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem").AddComponent<EventSystem>();
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            _clock = MakeText(canvasGo.transform, "Clock", new Vector2(0.5f, 1), new Vector2(0, -20), 28);
            _round = MakeText(canvasGo.transform, "Round", new Vector2(0, 1), new Vector2(20, -20), 20);
            _weapon = MakeText(canvasGo.transform, "Weapon", new Vector2(1, 0), new Vector2(-20, 20), 20);
            _centre = MakeText(canvasGo.transform, "Centre", new Vector2(0.5f, 0.5f), Vector2.zero, 48);
            _feed = MakeText(canvasGo.transform, "Feed", new Vector2(0, 0.5f), new Vector2(20, 0), 16);

            _leaderboardPanel = new GameObject("Leaderboard");
            _leaderboardPanel.transform.SetParent(canvasGo.transform, false);
            var bg = _leaderboardPanel.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.6f);
            var rt = _leaderboardPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.3f, 0.3f); rt.anchorMax = new Vector2(0.7f, 0.7f); rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _leaderboard = MakeText(_leaderboardPanel.transform, "LeaderboardText", new Vector2(0.5f, 0.5f), Vector2.zero, 20);
            _leaderboardPanel.SetActive(false);

            _menuPanel = new GameObject("Menu");
            _menuPanel.transform.SetParent(canvasGo.transform, false);
            var mbg = _menuPanel.AddComponent<Image>(); mbg.color = new Color(0, 0, 0, 0.7f);
            var mrt = _menuPanel.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0.35f, 0.35f); mrt.anchorMax = new Vector2(0.65f, 0.65f); mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;
            MakeText(_menuPanel.transform, "MenuText", new Vector2(0.5f, 0.5f), Vector2.zero, 20).text = "MENU\nPress Esc again to continue\n(The match keeps running)";
            _menuPanel.SetActive(false);
        }

        private Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 offset, int size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = offset; rt.sizeDelta = new Vector2(600, 200);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size; t.color = Color.white; t.alignment = TextAnchor.UpperCenter;
            return t;
        }

        public void UpdateUI(Snapshot snap, Player me)
        {
            if (snap == null) return;
            string phaseLabel = snap.Phase == "results" ? "NEXT ROUND" : snap.Phase == "countdown" ? "GET READY" : "TIME LEFT";
            _clock.text = $"{phaseLabel}\n{FormatTime(snap.Remaining)}";
            _round.text = $"SUNSET CIRCUIT\nFREE FOR ALL · ROUND {snap.Round}\n{(snap.Code == "PRACTICE" ? "BOT PRACTICE" : "ROOM " + snap.Code)}";
            if (snap.Phase == "countdown") _centre.text = "GET READY";
            else if (snap.Phase == "results") _centre.text = "NEXT ROUND";
            else _centre.text = "";

            if (me != null)
            {
                if (me.Weapon != null)
                {
                    var def = Weapons.Defs[me.Weapon.Value];
                    _weapon.text = $"WEAPON READY\n{def.NameEn}\n{def.Description}\nAmmo: {me.Ammo}";
                }
                else _weapon.text = "FIND A WEAPON\nFind a loot box\nTouch a ? box to get a weapon";
            }

            var ranked = snap.Players.OrderByDescending(p => p.Kills).ThenBy(p => p.Deaths).ToList();
            var sb = new StringBuilder("Driver\tKills\tDeaths\n");
            foreach (var p in ranked) sb.AppendLine($"{p.Name}\t{p.Kills}\t{p.Deaths}");
            _leaderboard.text = sb.ToString();

            var feed = snap.Events.Where(e => e.Kind == "smash").TakeLast(3).ToList();
            _feed.text = string.Join("\n", feed.Select(e => $"{e.Name} → {e.Victim}"));

            if (InputProvider.MenuPressed) { _menuOpen = !_menuOpen; _menuPanel.SetActive(_menuOpen); }
            _leaderboardPanel.SetActive(InputProvider.ScoreboardHeld);
        }

        private string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int m = (int)(seconds / 60), s = (int)(seconds % 60);
            return $"{m:D2}:{s:D2}";
        }
    }
}
