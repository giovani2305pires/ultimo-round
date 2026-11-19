using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace UltimoRound.Core
{
    /// <summary>
    /// Medição de desempenho no executável: `UltimoRound.exe -benchmark [-benchmark-seconds 40]`.
    /// Abre uma luta CPU × CPU, descarta o aquecimento, registra o tempo de cada quadro e grava
    /// benchmark.txt em Application.persistentDataPath. Não é usado no jogo normal.
    /// </summary>
    public sealed class BenchmarkRecorder : MonoBehaviour
    {
        public static bool Requested => HasArg("-benchmark");

        float _warmup = 4f;
        float _duration = 40f;
        float _elapsed;
        readonly List<float> _frames = new List<float>(8192);

        static bool HasArg(string a) => Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0;

        static float ArgFloat(string name, float fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            if (i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
            return fallback;
        }

        void Awake()
        {
            _duration = Mathf.Clamp(ArgFloat("-benchmark-seconds", 40f), 5f, 600f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_warmup > 0f) { _warmup -= dt; return; }
            _frames.Add(dt * 1000f);
            _elapsed += dt;
            if (_elapsed >= _duration) Finish();
        }

        void Finish()
        {
            enabled = false;
            var sorted = new List<float>(_frames);
            sorted.Sort();
            float sum = 0f;
            int over = 0;
            foreach (var f in _frames) { sum += f; if (f > 16.67f) over++; }
            float avg = sum / Mathf.Max(1, _frames.Count);
            float P(float q) => sorted.Count == 0 ? 0 : sorted[Mathf.Clamp(Mathf.CeilToInt(q * sorted.Count) - 1, 0, sorted.Count - 1)];
            var sb = new StringBuilder();
            sb.AppendLine("Último Round — benchmark (luta CPU x CPU, cena Fight)");
            sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Resolução: {Screen.width}x{Screen.height} modo={Screen.fullScreenMode} vSync={QualitySettings.vSyncCount} alvoFPS={Application.targetFrameRate} qualidade={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize} MB)");
            sb.AppendLine($"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} threads)  RAM: {SystemInfo.systemMemorySize} MB  SO: {SystemInfo.operatingSystem}");
            sb.AppendLine($"Quadros: {_frames.Count} em {_elapsed:F1}s (aquecimento descartado)");
            sb.AppendLine($"Tempo de quadro (ms): média {avg:F2} | mediana {P(0.5f):F2} | p95 {P(0.95f):F2} | p99 {P(0.99f):F2} | máx {P(1f):F2}");
            sb.AppendLine($"FPS médio: {(avg > 0 ? 1000f / avg : 0):F1} | quadros acima de 16,67 ms: {over} ({100f * over / Mathf.Max(1, _frames.Count):F2}%)");
            string path = Path.Combine(Application.persistentDataPath, "benchmark.txt");
            try { File.WriteAllText(path, sb.ToString()); } catch (Exception e) { Debug.LogWarning(e.Message); }
            Debug.Log("[Benchmark]\n" + sb);
            Application.Quit();
        }
    }
}
