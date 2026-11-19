using System;
using System.IO;
using UltimoRound.Input;
using UnityEngine;

namespace UltimoRound.Core
{
    /// <summary>Preferências locais. Salvas em JSON com validação e valores padrão seguros.</summary>
    [Serializable]
    public sealed class GameSettings
    {
        public int Version = 1;
        public float MasterVolume = 0.9f;
        public float MusicVolume = 0.55f;
        public float SfxVolume = 0.85f;
        public float VoiceVolume = 0.85f;
        public int ResolutionWidth;
        public int ResolutionHeight;
        /// <summary>0 = janela, 1 = tela cheia sem bordas, 2 = tela cheia exclusiva.</summary>
        public int DisplayMode = 1;
        /// <summary>0 = baixa, 1 = média, 2 = alta.</summary>
        public int Quality = 2;
        public bool VSync = true;
        /// <summary>0 = sem limite; caso contrário, alvo de FPS quando VSync está desligado.</summary>
        public int FpsLimit = 60;
        public float CameraIntensity = 0.7f;
        public bool ShowInputHistory = true;
        public ControlBindings Bindings = new ControlBindings();

        public static readonly int[] FpsOptions = { 30, 60, 120, 144, 0 };

        static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        public void Validate()
        {
            MasterVolume = Clamp01(MasterVolume, 0.9f);
            MusicVolume = Clamp01(MusicVolume, 0.55f);
            SfxVolume = Clamp01(SfxVolume, 0.85f);
            VoiceVolume = Clamp01(VoiceVolume, 0.85f);
            CameraIntensity = Clamp01(CameraIntensity, 0.7f);
            DisplayMode = Mathf.Clamp(DisplayMode, 0, 2);
            Quality = Mathf.Clamp(Quality, 0, 2);
            if (Array.IndexOf(FpsOptions, FpsLimit) < 0) FpsLimit = 60;
            if (ResolutionWidth < 640 || ResolutionHeight < 360 || ResolutionWidth > 16384 || ResolutionHeight > 16384)
            {
                ResolutionWidth = 0;
                ResolutionHeight = 0;
            }
            if (Bindings == null) Bindings = new ControlBindings();
            Bindings.Validate();
        }

        static float Clamp01(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp01(v);

        public static GameSettings Load()
        {
            GameSettings s = null;
            try
            {
                if (File.Exists(FilePath))
                    s = JsonUtility.FromJson<GameSettings>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Config] Arquivo de configurações inválido; usando padrões. " + e.Message);
                s = null;
            }
            if (s == null) s = new GameSettings();
            s.Validate();
            return s;
        }

        public void Save()
        {
            try
            {
                Validate();
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Config] Não foi possível salvar as configurações: " + e.Message);
            }
        }

        public string QualityLabel => Quality == 0 ? "Baixa" : Quality == 1 ? "Média" : "Alta";
        public string DisplayModeLabel => DisplayMode == 0 ? "Janela" : DisplayMode == 1 ? "Tela cheia (sem bordas)" : "Tela cheia exclusiva";
        public string FpsLabel => FpsLimit == 0 ? "Sem limite" : FpsLimit + " FPS";
    }
}
