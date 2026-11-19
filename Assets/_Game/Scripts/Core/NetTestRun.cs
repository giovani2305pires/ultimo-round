using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace UltimoRound.Core
{
    /// <summary>
    /// Teste da rede no executável: `UltimoRound.exe -net-host` e `UltimoRound.exe -net-join 127.0.0.1`.
    /// As CPUs jogam pelos dois computadores e o relatório vai para Application.persistentDataPath.
    /// Opcionais: -net-test-seconds, -net-fighter, -net-sim-loss (0 a 1), -net-sim-delay e -net-sim-jitter (segundos).
    /// Não é usado no jogo normal.
    /// </summary>
    public static class NetTestRun
    {
        public static readonly bool Active;
        public static readonly bool Hosting;
        public static readonly string JoinAddress;
        public static readonly int Fighter;
        public static readonly float SimLoss;
        public static readonly float SimDelay;
        public static readonly float SimJitter;

        static readonly float Seconds;
        static readonly StringBuilder Log = new StringBuilder();
        static readonly StringBuilder Hashes = new StringBuilder();
        static bool _finished;
        static bool _shot;

        static NetTestRun()
        {
            var args = Environment.GetCommandLineArgs();
            Hosting = Has(args, "-net-host");
            JoinAddress = Text(args, "-net-join");
            Active = Hosting || JoinAddress != null;
            if (!Active) return;
            Seconds = Number(args, "-net-test-seconds", 60f);
            Fighter = (int)Number(args, "-net-fighter", Hosting ? 0 : 1);
            SimLoss = Number(args, "-net-sim-loss", 0f);
            SimDelay = Number(args, "-net-sim-delay", 0f);
            SimJitter = Number(args, "-net-sim-jitter", 0f);
            AudioListener.volume = 0f;
        }

        static bool Has(string[] args, string name) => Array.IndexOf(args, name) >= 0;

        static string Text(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static float Number(string[] args, string name, float fallback)
        {
            string s = Text(args, name);
            return s != null && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }

        public static bool TimeIsUp => Active && Time.realtimeSinceStartup >= Seconds;

        public static void ApplyDisplay()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            AudioListener.volume = 0f;
            Screen.SetResolution(960, 540, FullScreenMode.Windowed);
        }

        public static void Note(string text)
        {
            if (Active) Log.AppendLine($"[{Time.realtimeSinceStartup,6:F1}] {text}");
        }

        public static void Hash(uint tick, int hash)
        {
            if (!Active || tick % 120 != 0) return;
            Hashes.Append(tick).Append(' ').Append(hash).Append('\n');
            if (!_shot && Time.realtimeSinceStartup > Seconds * 0.5f)
            {
                _shot = true;
                ScreenCapture.CaptureScreenshot(FilePath("png"));
            }
        }

        static string FilePath(string extension) =>
            Path.Combine(Application.persistentDataPath, $"nettest_{(Hosting ? "anfitriao" : "convidado")}.{extension}");

        public static void Finish(string lastLine)
        {
            if (!Active || _finished) return;
            _finished = true;
            Note(lastLine);
            File.WriteAllText(FilePath("txt"), Log.ToString());
            File.WriteAllText(FilePath("hash"), Hashes.ToString());
            Application.Quit();
        }
    }
}
