using UltimoRound.Audio;
using UltimoRound.Input;
using UltimoRound.Net;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UltimoRound.Core
{
    /// <summary>
    /// Serviços persistentes entre cenas: dados, preferências, sessão, entrada e áudio.
    /// Criado uma única vez; chamadas repetidas de <see cref="Ensure"/> reutilizam a instância (sem duplicar
    /// jogadores, áudio ou gerenciadores ao trocar de cena ou pedir revanche).
    /// </summary>
    public sealed class GameServices : MonoBehaviour
    {
        public static GameServices Instance { get; private set; }

        public GameDatabase Database { get; private set; }
        public GameSettings Settings { get; private set; }
        public Session Session { get; private set; }
        public InputService Input { get; private set; }
        public AudioService Audio { get; private set; }
        public NetSession Net { get; private set; }

        public static GameServices Ensure(GameDatabase db)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[GameServices]");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<GameServices>();
            s.Init(db);
            return s;
        }

        void Init(GameDatabase db)
        {
            Instance = this;
            Database = db;
            Settings = GameSettings.Load();
            Session = new Session();
            Input = new InputService(Settings.Bindings);
            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(transform, false);
            Audio = audioGo.AddComponent<AudioService>();
            Audio.Init(Settings, db.Sounds);
            Net = new NetSession(Application.version);
            ApplyDisplaySettings(false);
        }

        // Os serviços persistem entre cenas: a conexão continua viva ao carregar a luta ou voltar à seleção.
        void Update() => Net?.Update(Time.realtimeSinceStartup);

        void OnApplicationQuit() => Net?.Stop();

        void OnDestroy()
        {
            if (Instance == this)
            {
                Net?.Dispose();
                Input?.Dispose();
                Instance = null;
            }
        }

        public void SaveSettings()
        {
            Settings.Save();
            Input.SetBindings(Settings.Bindings);
            Audio.ApplyVolumes();
        }

        /// <summary>Aplica resolução, modo de tela, qualidade, VSync e limite de FPS.</summary>
        public void ApplyDisplaySettings(bool applyResolution)
        {
            if (NetTestRun.Active)
            {
                NetTestRun.ApplyDisplay();
                return;
            }
            var s = Settings;
            QualitySettings.vSyncCount = s.VSync ? 1 : 0;
            Application.targetFrameRate = s.VSync ? -1 : (s.FpsLimit <= 0 ? -1 : s.FpsLimit);

            int levels = QualitySettings.names.Length;
            int level = s.Quality == 0 ? 0 : levels - 1;
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);

            if (applyResolution || !Application.isEditor)
            {
                var mode = s.DisplayMode == 0 ? FullScreenMode.Windowed : s.DisplayMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.ExclusiveFullScreen;
                int w = s.ResolutionWidth > 0 ? s.ResolutionWidth : Screen.currentResolution.width;
                int h = s.ResolutionHeight > 0 ? s.ResolutionHeight : Screen.currentResolution.height;
                if (mode == FullScreenMode.Windowed && s.ResolutionWidth <= 0)
                {
                    w = Mathf.Min(1600, Screen.currentResolution.width);
                    h = Mathf.Min(900, Screen.currentResolution.height);
                }
                if (!Application.isEditor) Screen.SetResolution(w, h, mode);
            }
        }

        /// <summary>Configura câmera conforme a qualidade (sem alterar assets do projeto).</summary>
        public void ApplyCameraQuality(Camera cam)
        {
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            int q = Settings.Quality;
            data.renderShadows = q > 0;
            data.antialiasing = q == 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : q == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            data.renderPostProcessing = q > 0;
        }
    }
}
