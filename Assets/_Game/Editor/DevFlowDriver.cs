using System.Collections.Generic;
using System.IO;
using System.Text;
using UltimoRound.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace UltimoRound.EditorTools
{
    /// <summary>
    /// Roteiro de verificação em Play Mode dirigido pelo MCP: cria teclado e gamepad virtuais (Input System),
    /// avança quadros com EditorApplication.Step() e registra capturas. Não substitui teste com hardware real.
    /// </summary>
    public static class DevFlowDriver
    {
        static Keyboard _kb;
        static Gamepad _pad;
        public static readonly StringBuilder Log = new StringBuilder();
        public static string ShotDir;

        public static void Setup(string shotDir)
        {
            ShotDir = shotDir;
            Log.Clear();
            // Somente durante o roteiro: aceita entrada mesmo com a Game View sem foco (restaurado no Teardown).
            if (!_captured)
            {
                // Guarda a configuração original só uma vez (várias chamadas de Setup não podem sobrescrevê-la).
                _prevBg = InputSystem.settings.backgroundBehavior;
                _prevEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
                _prevMode = InputSystem.settings.updateMode;
                _captured = true;
            }
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // Remove dispositivos virtuais de execuções anteriores.
            foreach (var d in new List<InputDevice>(InputSystem.devices))
                if (d.name.StartsWith("Teste")) InputSystem.RemoveDevice(d);
            _kb = null;
            _pad = null;
            if (_kb == null || !_kb.added) _kb = InputSystem.AddDevice<Keyboard>("TesteTeclado");
            if (_pad == null || !_pad.added) _pad = InputSystem.AddDevice<Gamepad>("TesteGamepad");
            _kb.MakeCurrent();
            EditorApplication.isPaused = true;
        }

        static InputSettings.BackgroundBehavior _prevBg;
        static InputSettings.EditorInputBehaviorInPlayMode _prevEditor;
        static InputSettings.UpdateMode _prevMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        static bool _captured;

        [InitializeOnLoadMethod]
        static void HookPlayMode()
        {
            // Segurança: fora do Play Mode e ao sair dele, a entrada do editor volta ao normal mesmo se um roteiro não terminou.
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.delayCall += Teardown;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingPlayMode || s == PlayModeStateChange.EnteredEditMode) Teardown();
            };
        }

        public static void Teardown()
        {
            if (_captured)
            {
                InputSystem.settings.backgroundBehavior = _prevBg;
                InputSystem.settings.editorInputBehaviorInPlayMode = _prevEditor;
                InputSystem.settings.updateMode = _prevMode;
                _captured = false;
            }
            // O projeto não tem asset de configurações do Input System: os padrões são a referência.
            // Garante a volta ao normal mesmo se um domain reload apagou os valores guardados.
            var defaults = ScriptableObject.CreateInstance<InputSettings>();
            if (InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsManually)
                InputSystem.settings.updateMode = defaults.updateMode;
            if (InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus)
                InputSystem.settings.backgroundBehavior = defaults.backgroundBehavior;
            if (InputSystem.settings.editorInputBehaviorInPlayMode == InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView)
                InputSystem.settings.editorInputBehaviorInPlayMode = defaults.editorInputBehaviorInPlayMode;
            Object.DestroyImmediate(defaults);
            if (_kb != null && _kb.added) InputSystem.RemoveDevice(_kb);
            if (_pad != null && _pad.added) InputSystem.RemoveDevice(_pad);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
            _kb = null;
            _pad = null;
        }

        /// <summary>Roda do mouse virtual por um quadro.</summary>
        public static void Wheel(float notches)
        {
            if (_mouse == null || !_mouse.added)
            {
                _mouse = InputSystem.AddDevice<Mouse>("TesteMouse");
                _mouse.MakeCurrent();
            }
            // A rolagem é um delta zerado em cada atualização da entrada: o valor entra depois dela, logo antes do quadro.
            _mouse.MakeCurrent();
            InputSystem.Update();
            InputState.Change(_mouse, new MouseState { scroll = new Vector2(0, notches) });
            EditorApplication.Step();
            InputState.Change(_mouse, new MouseState());
            Step(1);
        }

        static Mouse _mouse;

        public static Gamepad Pad => _pad;
        public static Keyboard Kb => _kb;

        /// <summary>Processa eventos de entrada pendentes e avança um quadro real do player loop.</summary>
        public static void Step(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                InputSystem.Update();
                EditorApplication.Step();
            }
        }

        // Estado aplicado diretamente (InputState.Change): o jogo detecta a borda solto→pressionado entre quadros.
        public static void SetKeys(params Key[] keys) => InputState.Change(_kb, new KeyboardState(keys));
        public static void SetPad(GamepadState s) => InputState.Change(_pad, s);

        public static void Key(Key key, int hold = 1)
        {
            SetKeys(key);
            Step(hold);
            SetKeys();
            Step(1);
        }

        public static void Keys(params Key[] keys)
        {
            SetKeys(keys);
            Step(1);
            SetKeys();
            Step(1);
        }

        public static void PadButton(GamepadButton b, int hold = 1)
        {
            SetPad(new GamepadState().WithButton(b));
            Step(hold);
            SetPad(new GamepadState());
            Step(1);
        }

        public static void Shot(string name)
        {
            string p = Path.Combine(ShotDir, name + ".png");
            DevTools.CaptureGame(p, 1280, 720);
            Log.AppendLine("captura: " + name);
        }

        public static void Note(string s) => Log.AppendLine(s);

        public static string Scene => SceneManager.GetActiveScene().name;

        public static FightSceneController Fight => Object.FindAnyObjectByType<FightSceneController>();
        public static MainMenuController Menu => Object.FindAnyObjectByType<MainMenuController>();
        public static GameServices Services => GameServices.Instance;
    }
}
