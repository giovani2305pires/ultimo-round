using UltimoRound.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UltimoRound.Input
{
    public struct MenuInput
    {
        public int NavX;
        public int NavY;
        public bool Confirm;
        public bool Cancel;
        public bool Start;
        public bool Alt;   // ação secundária (ex.: trocar cor)
        public int HeldY;  // direção vertical segurada (rolagem contínua de texto)
        public bool Any => NavX != 0 || NavY != 0 || Confirm || Cancel || Start || Alt;
    }

    /// <summary>
    /// Lê um único dispositivo (teclado ou gamepad) usando o perfil de controles.
    /// Os controles são resolvidos uma vez (sem buscas por quadro).
    /// </summary>
    public sealed class DeviceReader
    {
        public const float StickDeadzone = 0.35f;
        const float DiagonalThreshold = 0.38f; // ~22,5°
        const float RepeatDelay = 0.38f;
        const float RepeatRate = 0.11f;

        public readonly InputDevice Device;
        public bool IsKeyboard => Device is Keyboard;

        readonly ButtonControl[] _buttons = new ButtonControl[(int)GameAction.Count];
        int _prevNavX, _prevNavY;
        float _repeatTimer;

        // Detecção de borda própria (além de wasPressedThisFrame): transição solto→pressionado entre
        // quadros consecutivos. Ao voltar a ler depois de um intervalo, não gera borda falsa.
        struct EdgeState { public int Frame; public bool Prev; public bool Result; }
        readonly System.Collections.Generic.Dictionary<ButtonControl, EdgeState> _edges = new System.Collections.Generic.Dictionary<ButtonControl, EdgeState>(32);

        bool Edge(ButtonControl c)
        {
            if (c == null) return false;
            int frame = Time.frameCount;
            bool now = c.isPressed;
            if (_edges.TryGetValue(c, out var e))
            {
                if (e.Frame == frame) return e.Result;
                bool consecutive = frame - e.Frame == 1;
                bool result = c.wasPressedThisFrame || (consecutive && now && !e.Prev);
                _edges[c] = new EdgeState { Frame = frame, Prev = now, Result = result };
                return result;
            }
            bool first = c.wasPressedThisFrame;
            _edges[c] = new EdgeState { Frame = frame, Prev = now, Result = first };
            return first;
        }

        public DeviceReader(InputDevice device, ControlBindings bindings)
        {
            Device = device;
            Rebind(bindings);
        }

        public void Rebind(ControlBindings bindings)
        {
            var paths = IsKeyboard ? bindings.Keyboard : bindings.Gamepad;
            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttons[i] = null;
                if (paths == null || i >= paths.Length || string.IsNullOrEmpty(paths[i])) continue;
                _buttons[i] = Device.TryGetChildControl<ButtonControl>(paths[i]);
            }
        }

        public bool Connected => Device != null && Device.added;

        bool Held(GameAction a)
        {
            var b = _buttons[(int)a];
            return b != null && b.isPressed;
        }

        bool PressedThisFrame(GameAction a) => Edge(_buttons[(int)a]);

        public Vector2Int ReadDirection()
        {
            int x = 0, y = 0;
            if (Device is Keyboard)
            {
                bool l = Held(GameAction.Left), r = Held(GameAction.Right);
                bool u = Held(GameAction.Up) || Held(GameAction.Jump), d = Held(GameAction.Down);
                x = (r ? 1 : 0) - (l ? 1 : 0);   // SOCD: esquerda+direita = neutro
                y = (u ? 1 : 0) - (d ? 1 : 0);
            }
            else if (Device is Gamepad g)
            {
                Vector2 v = g.leftStick.ReadValue();
                float mag = v.magnitude;
                if (mag >= StickDeadzone)
                {
                    Vector2 n = v / mag;
                    x = n.x > DiagonalThreshold ? 1 : n.x < -DiagonalThreshold ? -1 : 0;
                    y = n.y > DiagonalThreshold ? 1 : n.y < -DiagonalThreshold ? -1 : 0;
                }
                var dp = g.dpad;
                int dx = (dp.right.isPressed ? 1 : 0) - (dp.left.isPressed ? 1 : 0);
                int dy = (dp.up.isPressed ? 1 : 0) - (dp.down.isPressed ? 1 : 0);
                if (dx != 0) x = dx;
                if (dy != 0) y = dy;
                if (Held(GameAction.Jump)) y = 1;
            }
            return new Vector2Int(x, y);
        }

        static readonly GameAction[] CombatActions =
            { GameAction.Punch, GameAction.Kick, GameAction.Heavy, GameAction.Block, GameAction.Special, GameAction.Grab, GameAction.Signature };

        static Buttons ToButton(GameAction a)
        {
            switch (a)
            {
                case GameAction.Punch: return Buttons.Punch;
                case GameAction.Kick: return Buttons.Kick;
                case GameAction.Heavy: return Buttons.Heavy;
                case GameAction.Block: return Buttons.Block;
                case GameAction.Special: return Buttons.Special;
                case GameAction.Grab: return Buttons.Grab;
                case GameAction.Signature: return Buttons.Signature;
                default: return Buttons.None;
            }
        }

        /// <summary>Amostra de combate deste quadro: direção absoluta, botões segurados e bordas de pressão.</summary>
        public void SampleCombat(out int x, out int y, out Buttons held, out Buttons pressed)
        {
            var dir = Connected ? ReadDirection() : Vector2Int.zero;
            x = dir.x;
            y = dir.y;
            held = Buttons.None;
            pressed = Buttons.None;
            if (!Connected) return;
            foreach (var a in CombatActions)
            {
                var b = ToButton(a);
                if (Held(a)) held |= b;
                if (PressedThisFrame(a)) pressed |= b;
            }
        }

        public bool PausePressed()
        {
            if (!Connected) return false;
            if (Device is Keyboard k) return Edge(k.escapeKey);
            if (Device is Gamepad g) return Edge(g.startButton);
            return false;
        }

        public bool ResetPressed()
        {
            if (!Connected) return false;
            if (Device is Keyboard k) return Edge(k.rKey) | Edge(k.backspaceKey);
            if (Device is Gamepad g) return Edge(g.selectButton);
            return false;
        }

        /// <summary>Navegação de menu com auto-repetição (teclado: setas/WASD; gamepad: analógico/direcional).</summary>
        public MenuInput ReadMenu(float unscaledDt)
        {
            var m = new MenuInput();
            if (!Connected) return m;
            int nx = 0, ny = 0;
            if (Device is Keyboard k)
            {
                nx = ((k.rightArrowKey.isPressed || k.dKey.isPressed) ? 1 : 0) - ((k.leftArrowKey.isPressed || k.aKey.isPressed) ? 1 : 0);
                ny = ((k.upArrowKey.isPressed || k.wKey.isPressed) ? 1 : 0) - ((k.downArrowKey.isPressed || k.sKey.isPressed) ? 1 : 0);
                bool enter = Edge(k.enterKey) | Edge(k.numpadEnterKey);
                m.Confirm = enter | Edge(k.spaceKey) | PressedThisFrame(GameAction.Punch);
                m.Cancel = Edge(k.escapeKey) | Edge(k.backspaceKey) | PressedThisFrame(GameAction.Heavy);
                m.Start = enter;
                m.Alt = PressedThisFrame(GameAction.Kick) | Edge(k.tabKey);
            }
            else if (Device is Gamepad g)
            {
                var d = ReadDirection();
                nx = d.x;
                ny = Held(GameAction.Jump) ? 0 : d.y;
                Vector2 v = g.leftStick.ReadValue();
                if (v.magnitude >= 0.5f)
                {
                    nx = Mathf.Abs(v.x) > 0.5f ? (int)Mathf.Sign(v.x) : 0;
                    ny = Mathf.Abs(v.y) > 0.5f ? (int)Mathf.Sign(v.y) : 0;
                }
                m.Confirm = Edge(g.buttonSouth);
                m.Cancel = Edge(g.buttonEast);
                m.Start = Edge(g.startButton);
                m.Alt = Edge(g.buttonNorth);
            }

            m.HeldY = ny;
            if (nx != _prevNavX || ny != _prevNavY)
            {
                m.NavX = nx;
                m.NavY = ny;
                _repeatTimer = RepeatDelay;
            }
            else if (nx != 0 || ny != 0)
            {
                _repeatTimer -= unscaledDt;
                if (_repeatTimer <= 0f)
                {
                    m.NavX = nx;
                    m.NavY = ny;
                    _repeatTimer = RepeatRate;
                }
            }
            _prevNavX = nx;
            _prevNavY = ny;
            return m;
        }

        /// <summary>Qualquer tecla ou botão pressionado neste quadro (tela de abertura).</summary>
        public bool AnyPressed()
        {
            if (!Connected) return false;
            if (Device is Keyboard k) return Edge(k.anyKey);
            if (Device is Gamepad g)
                return Edge(g.buttonSouth) | Edge(g.buttonEast) | Edge(g.buttonWest) | Edge(g.buttonNorth) |
                       Edge(g.startButton) | Edge(g.selectButton) | Edge(g.leftShoulder) | Edge(g.rightShoulder) |
                       Edge(g.leftTrigger) | Edge(g.rightTrigger) | Edge(g.dpad.up) | Edge(g.dpad.down) |
                       Edge(g.dpad.left) | Edge(g.dpad.right);
            return false;
        }

        /// <summary>Qualquer botão de "entrar" pressionado neste quadro.</summary>
        public bool JoinPressed()
        {
            if (!Connected) return false;
            if (Device is Keyboard k) return Edge(k.enterKey) | Edge(k.spaceKey) | Edge(k.jKey);
            if (Device is Gamepad g) return Edge(g.buttonSouth) | Edge(g.startButton);
            return false;
        }

        public static string DeviceLabel(InputDevice d)
        {
            if (d == null) return "—";
            if (d is Keyboard) return "Teclado";
            if (d is Gamepad) return "Controle (" + (string.IsNullOrEmpty(d.displayName) ? "gamepad" : d.displayName) + ")";
            return d.displayName;
        }
    }
}
