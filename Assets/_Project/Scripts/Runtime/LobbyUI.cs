// Lobby, ported from app/page.tsx lobby screen (Phase 7, first pass).
// All user-visible copy is in English (per user instruction Nov 2026; the source game was Korean). Buttons: quick / create / join-by-code are online
// actions — they call OnlineClient and show its status/error in the lobby;
// practice starts the local match immediately. Help lists all 11 weapons.
// Settings holds server URL + mute; values are kept in PlayerPrefs.
using UnityEngine;
using UnityEngine.UI;
using SmashKarts.Core;
using System.Text;

namespace SmashKarts.Runtime
{
    public sealed class LobbyUI : MonoBehaviour
    {
        private GameManager _manager;
        private OnlineClient _online;
        private InputField _nameField, _codeField, _serverField;
        private Toggle _botsToggle, _muteToggle;
        private Text _status;
        private GameObject _panel, _helpPanel, _settingsPanel;
        private int _color = 0;

        public void Build(GameManager manager, OnlineClient online)
        {
            _manager = manager; _online = online;
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) { var go = new GameObject("Canvas"); canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; go.AddComponent<UnityEngine.UI.CanvasScaler>(); go.AddComponent<GraphicRaycaster>(); }
            _panel = new GameObject("Lobby");
            _panel.transform.SetParent(canvas.transform, false);
            var rt = _panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = _panel.AddComponent<Image>(); bg.color = new Color(0.1f, 0.08f, 0.06f, 0.85f);

            MakeText(_panel.transform, "SMASH KARTS ARENA", new Vector2(0.5f, 0.95f), 36);
            MakeText(_panel.transform, "ALL GAS. TOTAL CHAOS.\nSmall karts.\nOne huge hit.\nPick up weapons, aim, and blast them away.\nA 3-minute kart battle arena.\nUp to 8 players · 11 weapons · 03:00 deathmatch", new Vector2(0.5f, 0.8f), 20);

            _nameField = MakeInput(_panel.transform, "Driver name", new Vector2(0.5f, 0.62f));
            _nameField.text = PlayerPrefs.GetString("driver_name", "Driver");
            _nameField.characterLimit = 16;

            // Colour swatches: first 6 colours, as in the source lobby.
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                var btn = MakeButton(_panel.transform, GameConstants.Colors[i], new Vector2(0.35f + i * 0.06f, 0.55f), () => _color = idx);
                var img = btn.GetComponent<Image>(); img.color = Coord.Hex(GameConstants.Colors[i]);
            }
            MakeButton(_panel.transform, "Quick Battle", new Vector2(0.5f, 0.48f), () => StartOnline("quick"));
            MakeButton(_panel.transform, "Create Room", new Vector2(0.35f, 0.42f), () => StartOnline("create"));
            MakeButton(_panel.transform, "Practice Alone", new Vector2(0.65f, 0.42f), StartPractice);
            _codeField = MakeInput(_panel.transform, "6-character room code", new Vector2(0.5f, 0.36f));
            _codeField.characterLimit = 6;
            MakeButton(_panel.transform, "Join by Code", new Vector2(0.5f, 0.30f), () => StartOnline("join"));
            _botsToggle = MakeToggle(_panel.transform, "Fill empty seats with bots", new Vector2(0.5f, 0.25f), true);
            MakeButton(_panel.transform, "How to Play", new Vector2(0.35f, 0.18f), () => _helpPanel.SetActive(true));
            MakeButton(_panel.transform, "Settings", new Vector2(0.65f, 0.18f), () => _settingsPanel.SetActive(true));
            _status = MakeText(_panel.transform, "", new Vector2(0.5f, 0.12f), 16);
            MakeText(_panel.transform, "Drive with WASD / arrow keys · SPACE to fire\nIndependently made kart battle · Inspired by SMASH KARTS", new Vector2(0.5f, 0.06f), 14);

            _helpPanel = MakePanel(canvas.transform, "Help");
            var helpText = new StringBuilder("HOW TO PLAY\nW / Up = forward · S / Down = reverse · A/D = steer · SPACE = fire · Tab = leaderboard · Esc = menu\n\n");
            foreach (var kv in Weapons.Defs) helpText.AppendLine($"{kv.Value.NameEn}: {kv.Value.Description}");
            MakeText(_helpPanel.transform, helpText.ToString(), new Vector2(0.5f, 0.5f), 14);
            MakeButton(_helpPanel.transform, "Close", new Vector2(0.5f, 0.05f), () => _helpPanel.SetActive(false));
            _helpPanel.SetActive(false);

            _settingsPanel = MakePanel(canvas.transform, "Settings");
            _serverField = MakeInput(_settingsPanel.transform, "ws://localhost:3001/ws", new Vector2(0.5f, 0.6f));
            _serverField.text = PlayerPrefs.GetString("server_url", "ws://localhost:3001/ws");
            _muteToggle = MakeToggle(_settingsPanel.transform, "Mute", new Vector2(0.5f, 0.45f), PlayerPrefs.GetInt("muted", 0) == 1);
            MakeButton(_settingsPanel.transform, "Close", new Vector2(0.5f, 0.2f), () => { PlayerPrefs.SetString("server_url", _serverField.text); PlayerPrefs.SetInt("muted", _muteToggle.isOn ? 1 : 0); _settingsPanel.SetActive(false); });
            _settingsPanel.SetActive(false);
        }

        private void StartPractice()
        {
            PlayerPrefs.SetString("driver_name", _nameField.text);
            _panel.SetActive(false);
            _manager.StartPractice(_nameField.text, _color);
        }

        public void Hide() => _panel.SetActive(false);
        public void ShowStatus(string msg) { if (_status != null) _status.text = msg; }

        private void StartOnline(string action)
        {
            PlayerPrefs.SetString("driver_name", _nameField.text);
            _status.text = "Connecting to server…";
            _manager.StartOnline(action, _nameField.text, _color, _codeField.text, _botsToggle.isOn, _serverField != null ? _serverField.text : "ws://localhost:3001/ws");
        }

        private GameObject MakePanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.9f);
            return go;
        }

        private Text MakeText(Transform parent, string text, Vector2 anchor, int size)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(700, 200);
            var t = go.AddComponent<Text>(); t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = size; t.color = Color.white; t.alignment = TextAnchor.UpperCenter;
            return t;
        }

        private InputField MakeInput(Transform parent, string placeholder, Vector2 anchor)
        {
            var go = new GameObject("Input");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(300, 40);
            var img = go.AddComponent<Image>(); img.color = Color.white;
            var field = go.AddComponent<InputField>();
            var textGo = new GameObject("Text"); textGo.transform.SetParent(go.transform, false);
            var tt = textGo.AddComponent<Text>(); tt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tt.fontSize = 18; tt.color = Color.black;
            field.textComponent = tt;
            var phGo = new GameObject("Placeholder"); phGo.transform.SetParent(go.transform, false);
            var ph = phGo.AddComponent<Text>(); ph.text = placeholder; ph.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); ph.fontSize = 18; ph.color = Color.gray;
            field.placeholder = ph;
            return field;
        }

        private Button MakeButton(Transform parent, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(200, 40);
            var img = go.AddComponent<Image>(); img.color = new Color(0.85f, 0.98f, 0.32f);
            var btn = go.AddComponent<Button>(); btn.onClick.AddListener(onClick);
            var tGo = new GameObject("Label"); tGo.transform.SetParent(go.transform, false);
            var t = tGo.AddComponent<Text>(); t.text = label; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = 18; t.color = Color.black; t.alignment = TextAnchor.MiddleCenter;
            var trt = tGo.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return btn;
        }

        private Toggle MakeToggle(Transform parent, string label, Vector2 anchor, bool value)
        {
            var go = new GameObject("Toggle");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(200, 30);
            var tog = go.AddComponent<Toggle>(); tog.isOn = value;
            MakeText(go.transform, label, new Vector2(0.5f, 0.5f), 16);
            return tog;
        }
    }
}
