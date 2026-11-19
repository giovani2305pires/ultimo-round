using System;
using System.Collections.Generic;
using TMPro;
using UltimoRound.Audio;
using UltimoRound.Core;
using UltimoRound.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UltimoRound.UI
{
    /// <summary>Opções compartilhadas pelo menu principal e pela pausa. Salva ao sair.</summary>
    public sealed class OptionsPanel
    {
        public readonly RectTransform Root;
        readonly MenuList _list;
        readonly TextMeshProUGUI _hint;
        readonly TextMeshProUGUI _listenText;
        readonly RectTransform _listenPanel;
        readonly GameServices _gs;
        readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
        public Action OnClose;
        public Action OnChanged;

        bool _listening;
        bool _listenKeyboard;
        // Controles já pressionados quando a escuta começou: só contam depois de soltos e apertados de novo.
        readonly HashSet<ButtonControl> _heldAtListen = new HashSet<ButtonControl>();
        GameAction _listenAction;
        float _listenTimeout;

        public bool IsOpen => Root.gameObject.activeSelf;
        public int ListScroll => _list.ScrollIndex;

        public OptionsPanel(Transform parent, GameServices gs)
        {
            _gs = gs;
            Root = UIFactory.Rect("Opcoes", parent);
            UIFactory.Stretch(Root);
            var bg = UIFactory.Image("Fundo", Root, UITheme.Background, true);
            UIFactory.Stretch(bg.rectTransform);
            var title = UIFactory.Label("Titulo", Root, "OPÇÕES", 64, UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(900, 90));

            _list = new MenuList(Root, new Vector2(0.5f, 1f), new Vector2(0, -150), 980, 54, 15) { Audio = gs.Audio, OnBack = Close };
            _hint = UIFactory.Label("Dica", Root, "", 24, UITheme.TextDim, TextAlignmentOptions.Center);
            UIFactory.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1600, 40));

            _listenPanel = UIFactory.Rect("Escuta", Root);
            UIFactory.Stretch(_listenPanel);
            var lbg = UIFactory.Image("FundoEscuta", _listenPanel, new Color(0, 0, 0, 0.85f), true);
            UIFactory.Stretch(lbg.rectTransform);
            _listenText = UIFactory.Label("Texto", _listenPanel, "", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_listenText.rectTransform);
            _listenPanel.gameObject.SetActive(false);

            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 1024 && !_resolutions.Contains(v)) _resolutions.Add(v);
            }
            if (_resolutions.Count == 0) _resolutions.Add(new Vector2Int(1920, 1080));

            Build();
            Root.gameObject.SetActive(false);
        }

        GameSettings S => _gs.Settings;

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        void Build()
        {
            _list.Clear();
            _list.AddHeader("Áudio");
            _list.Add("Volume geral", null, () => Pct(S.MasterVolume), d => { S.MasterVolume = Step(S.MasterVolume, d); Apply(); });
            _list.Add("Música", null, () => Pct(S.MusicVolume), d => { S.MusicVolume = Step(S.MusicVolume, d); Apply(); });
            _list.Add("Efeitos", null, () => Pct(S.SfxVolume), d => { S.SfxVolume = Step(S.SfxVolume, d); Apply(); _gs.Audio.PlaySfx(Combat.SfxKind.LightHit); });
            _list.Add("Voz / anúncios", null, () => Pct(S.VoiceVolume), d => { S.VoiceVolume = Step(S.VoiceVolume, d); Apply(); _gs.Audio.PlayAnnouncer(AnnouncerLine.Fight); });

            _list.AddHeader("Vídeo");
            _list.Add("Resolução", null, ResolutionLabel, d => { CycleResolution(d); ApplyDisplay(); });
            _list.Add("Modo de tela", null, () => S.DisplayModeLabel, d => { S.DisplayMode = Wrap(S.DisplayMode + d, 3); ApplyDisplay(); });
            _list.Add("Qualidade gráfica", null, () => S.QualityLabel, d => { S.Quality = Wrap(S.Quality + d, 3); ApplyDisplay(); });
            _list.Add("VSync", null, () => S.VSync ? "Ligado" : "Desligado", d => { S.VSync = !S.VSync; ApplyDisplay(); });
            _list.Add("Limite de FPS (sem VSync)", null, () => S.FpsLabel, d =>
            {
                int i = Array.IndexOf(GameSettings.FpsOptions, S.FpsLimit);
                S.FpsLimit = GameSettings.FpsOptions[Wrap(i + d, GameSettings.FpsOptions.Length)];
                ApplyDisplay();
            }, () => !S.VSync);
            _list.Add("Intensidade da câmera", null, () => Pct(S.CameraIntensity), d => { S.CameraIntensity = Step(S.CameraIntensity, d); OnChanged?.Invoke(); });
            _list.Add("Histórico de comandos (treino)", null, () => S.ShowInputHistory ? "Visível" : "Oculto", d => { S.ShowInputHistory = !S.ShowInputHistory; OnChanged?.Invoke(); });

            _list.AddHeader("Controles — teclado");
            foreach (GameAction a in Enum.GetValues(typeof(GameAction)))
            {
                if (a == GameAction.Count) continue;
                var act = a;
                _list.Add(ControlBindings.ActionLabel(act), () => BeginListen(true, act), () => KeyLabel(S.Bindings.Keyboard[(int)act]));
            }
            _list.Add("Restaurar teclado padrão", () => { S.Bindings.ResetKeyboard(); Apply(); });

            _list.AddHeader("Controles — gamepad");
            foreach (GameAction a in Enum.GetValues(typeof(GameAction)))
            {
                if (a == GameAction.Count || ControlBindings.IsDirection(a)) continue;
                var act = a;
                _list.Add(ControlBindings.ActionLabel(act), () => BeginListen(false, act), () => PadLabel(S.Bindings.Gamepad[(int)act]));
            }
            _list.Add("Restaurar gamepad padrão", () => { S.Bindings.ResetGamepad(); Apply(); });
            _list.Add("Restaurar todas as opções", () =>
            {
                var b = new GameSettings();
                CopyFrom(b);
                ApplyDisplay();
            });
            _list.Add("Voltar", Close);
            _list.FocusFirst();
            _hint.text = "Navegar: setas/analógico  •  Alterar: esquerda/direita  •  Confirmar: Enter/A  •  Voltar: Esc/B";
        }

        void CopyFrom(GameSettings d)
        {
            S.MasterVolume = d.MasterVolume; S.MusicVolume = d.MusicVolume; S.SfxVolume = d.SfxVolume; S.VoiceVolume = d.VoiceVolume;
            S.DisplayMode = d.DisplayMode; S.Quality = d.Quality; S.VSync = d.VSync; S.FpsLimit = d.FpsLimit;
            S.CameraIntensity = d.CameraIntensity; S.ShowInputHistory = d.ShowInputHistory;
            S.ResolutionWidth = 0; S.ResolutionHeight = 0;
            S.Bindings = new ControlBindings();
            Apply();
        }

        static float Step(float v, int d) => Mathf.Clamp01(Mathf.Round((v + d * 0.05f) * 20f) / 20f);
        static int Wrap(int v, int n) => ((v % n) + n) % n;

        string ResolutionLabel()
        {
            if (S.ResolutionWidth <= 0) return "Nativa (" + Screen.currentResolution.width + "×" + Screen.currentResolution.height + ")";
            return S.ResolutionWidth + "×" + S.ResolutionHeight;
        }

        void CycleResolution(int d)
        {
            int idx = _resolutions.IndexOf(new Vector2Int(S.ResolutionWidth, S.ResolutionHeight));
            idx = idx < 0 ? (d > 0 ? 0 : _resolutions.Count - 1) : Wrap(idx + d, _resolutions.Count + 1);
            if (idx >= _resolutions.Count) { S.ResolutionWidth = 0; S.ResolutionHeight = 0; }
            else { S.ResolutionWidth = _resolutions[idx].x; S.ResolutionHeight = _resolutions[idx].y; }
        }

        void Apply()
        {
            _gs.SaveSettings();
            OnChanged?.Invoke();
        }

        void ApplyDisplay()
        {
            _gs.SaveSettings();
            _gs.ApplyDisplaySettings(true);
            OnChanged?.Invoke();
        }

        public static string KeyLabel(string path)
        {
            if (string.IsNullOrEmpty(path)) return "—";
            var c = Keyboard.current != null ? Keyboard.current.TryGetChildControl(path) : null;
            if (c != null && !string.IsNullOrEmpty(c.displayName)) return c.displayName.ToUpperInvariant();
            return path.ToUpperInvariant();
        }

        public static string PadLabel(string path)
        {
            switch (path)
            {
                case "buttonSouth": return "A";
                case "buttonEast": return "B";
                case "buttonWest": return "X";
                case "buttonNorth": return "Y";
                case "leftShoulder": return "LB";
                case "rightShoulder": return "RB";
                case "leftTrigger": return "LT";
                case "rightTrigger": return "RT";
                case "leftStickPress": return "L3";
                case "rightStickPress": return "R3";
                case "": case null: return "—";
                default: return path;
            }
        }

        void BeginListen(bool keyboard, GameAction action)
        {
            _listening = true;
            _listenKeyboard = keyboard;
            _listenAction = action;
            _listenTimeout = 6f;
            _heldAtListen.Clear();
            if (Keyboard.current != null)
                foreach (var k in Keyboard.current.allKeys) if (k != null && k.isPressed) _heldAtListen.Add(k);
            foreach (var g in Gamepad.all)
                foreach (var c in g.allControls) if (c is ButtonControl b && b.isPressed) _heldAtListen.Add(b);
            _listenPanel.gameObject.SetActive(true);
            _listenText.text = $"Pressione {(keyboard ? "a nova tecla" : "o novo botão do controle")} para\n<color=#ffb030>{ControlBindings.ActionLabel(action)}</color>\n\n<size=60%>Esc / Start cancelam</size>";
        }

        void EndListen()
        {
            _listening = false;
            _listenPanel.gameObject.SetActive(false);
            _list.Refresh();
        }

        static readonly HashSet<string> ReservedPad = new HashSet<string> { "start", "select", "leftStick", "rightStick", "dpad" };

        /// <summary>Novo aperto: pressionado agora e não estava segurado desde o início da escuta.</summary>
        bool NewPress(ButtonControl c)
        {
            if (c == null) return false;
            if (!c.isPressed) { _heldAtListen.Remove(c); return false; }
            return !_heldAtListen.Contains(c);
        }

        void PollListen(float dt)
        {
            _listenTimeout -= dt;
            if (_listenTimeout <= 0f) { EndListen(); return; }
            if (_listenKeyboard)
            {
                var k = Keyboard.current;
                if (k == null) return;
                if (NewPress(k.escapeKey)) { EndListen(); return; }
                foreach (var key in k.allKeys)
                {
                    if (!NewPress(key)) continue;
                    if (key == k.enterKey || key == k.numpadEnterKey) continue; // reservada para menus
                    Assign(S.Bindings.Keyboard, (int)_listenAction, key.name);
                    EndListen();
                    return;
                }
                foreach (var g in Gamepad.all) if (NewPress(g.startButton)) { EndListen(); return; }
            }
            else
            {
                if (Keyboard.current != null && NewPress(Keyboard.current.escapeKey)) { EndListen(); return; }
                foreach (var g in Gamepad.all)
                {
                    if (NewPress(g.startButton) || NewPress(g.selectButton)) { EndListen(); return; }
                    foreach (var c in g.allControls)
                    {
                        if (!(c is ButtonControl b) || !NewPress(b)) continue;
                        if (c.parent != g) continue; // ignora direções de analógico/direcional
                        if (ReservedPad.Contains(c.name)) continue;
                        Assign(S.Bindings.Gamepad, (int)_listenAction, c.name);
                        EndListen();
                        return;
                    }
                }
            }
        }

        void Assign(string[] table, int action, string path)
        {
            // Conflito: troca com a ação que usava o mesmo controle.
            for (int i = 0; i < table.Length; i++)
                if (i != action && table[i] == path) table[i] = table[action];
            table[action] = path;
            Apply();
            _gs.Audio.PlayUi(UiSound.Confirm);
        }

        public void Open()
        {
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            _list.FocusFirst();
            _list.Refresh();
        }

        public void Close()
        {
            if (_listening) return;
            _gs.SaveSettings();
            Root.gameObject.SetActive(false);
            OnClose?.Invoke();
        }

        public void Tick(in MenuInput m, float dt)
        {
            if (!IsOpen) return;
            if (_listening) { PollListen(dt); return; }
            _list.Handle(m);
        }
    }
}
