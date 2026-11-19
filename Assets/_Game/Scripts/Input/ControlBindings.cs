using System;

namespace UltimoRound.Input
{
    /// <summary>Ações remapeáveis. Direções do gamepad (analógico/direcional) são fixas.</summary>
    public enum GameAction
    {
        Punch,
        Kick,
        Heavy,
        Block,
        Special,
        Grab,
        Signature,
        Jump,
        Left,
        Right,
        Up,
        Down,
        Count,
    }

    /// <summary>
    /// Perfil de controles persistido em JSON. Caminhos são relativos ao dispositivo
    /// (ex.: teclado "j", gamepad "buttonWest"). Um perfil para teclado e outro para gamepad.
    /// </summary>
    [Serializable]
    public sealed class ControlBindings
    {
        public string[] Keyboard = DefaultKeyboard();
        public string[] Gamepad = DefaultGamepad();

        public static string[] DefaultKeyboard()
        {
            var k = new string[(int)GameAction.Count];
            k[(int)GameAction.Punch] = "j";
            k[(int)GameAction.Kick] = "k";
            k[(int)GameAction.Heavy] = "l";
            k[(int)GameAction.Block] = "i";
            k[(int)GameAction.Special] = "u";
            k[(int)GameAction.Grab] = "o";
            k[(int)GameAction.Signature] = "p";
            k[(int)GameAction.Jump] = "space";
            k[(int)GameAction.Left] = "a";
            k[(int)GameAction.Right] = "d";
            k[(int)GameAction.Up] = "w";
            k[(int)GameAction.Down] = "s";
            return k;
        }

        public static string[] DefaultGamepad()
        {
            var g = new string[(int)GameAction.Count];
            g[(int)GameAction.Punch] = "buttonWest";
            g[(int)GameAction.Kick] = "buttonNorth";
            g[(int)GameAction.Heavy] = "buttonEast";
            g[(int)GameAction.Block] = "rightTrigger";
            g[(int)GameAction.Special] = "rightShoulder";
            g[(int)GameAction.Grab] = "leftShoulder";
            g[(int)GameAction.Signature] = "leftTrigger";
            g[(int)GameAction.Jump] = "buttonSouth";
            g[(int)GameAction.Left] = "";
            g[(int)GameAction.Right] = "";
            g[(int)GameAction.Up] = "";
            g[(int)GameAction.Down] = "";
            return g;
        }

        public static bool IsDirection(GameAction a) => a == GameAction.Left || a == GameAction.Right || a == GameAction.Up || a == GameAction.Down;

        /// <summary>Corrige arrays ausentes/tamanho errado vindos de arquivo inválido.</summary>
        public void Validate()
        {
            Keyboard = Fix(Keyboard, DefaultKeyboard());
            Gamepad = Fix(Gamepad, DefaultGamepad());
        }

        static string[] Fix(string[] arr, string[] defaults)
        {
            if (arr == null || arr.Length != defaults.Length) return defaults;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] == null) arr[i] = defaults[i];
            return arr;
        }

        public void ResetKeyboard() => Keyboard = DefaultKeyboard();
        public void ResetGamepad() => Gamepad = DefaultGamepad();

        public static string ActionLabel(GameAction a)
        {
            switch (a)
            {
                case GameAction.Punch: return "Soco";
                case GameAction.Kick: return "Chute";
                case GameAction.Heavy: return "Forte";
                case GameAction.Block: return "Defesa";
                case GameAction.Special: return "Especial";
                case GameAction.Grab: return "Agarrão";
                case GameAction.Signature: return "Assinatura";
                case GameAction.Jump: return "Pular";
                case GameAction.Left: return "Esquerda";
                case GameAction.Right: return "Direita";
                case GameAction.Up: return "Cima / Pular";
                case GameAction.Down: return "Baixo / Agachar";
                default: return a.ToString();
            }
        }
    }
}
