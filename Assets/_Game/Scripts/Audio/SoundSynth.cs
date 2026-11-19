using UnityEngine;

namespace UltimoRound.Audio
{
    /// <summary>
    /// Síntese procedural dos efeitos e músicas do jogo (conteúdo original gerado em tempo de carregamento).
    /// Taxa de 32 kHz mono para economizar memória.
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 32000;

        // Gerador pseudoaleatório determinístico (xorshift) — evita depender de UnityEngine.Random.
        struct Noise
        {
            uint _s;
            public Noise(uint seed) { _s = seed == 0 ? 2463534242u : seed; }
            public float Next()
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s / (float)uint.MaxValue) * 2f - 1f;
            }
        }

        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        static AudioClip ToClip(string name, float[] data, bool normalize = true, float peak = 0.9f)
        {
            if (normalize)
            {
                float max = 0.0001f;
                for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
                float g = peak / max;
                for (int i = 0; i < data.Length; i++) data[i] *= g;
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Env(float t, float attack, float decay)
        {
            if (t < attack) return t / Mathf.Max(attack, 1e-4f);
            return Mathf.Exp(-(t - attack) * decay);
        }

        // ---------------------------------------------------------------- ELEMENTOS

        static void AddNoise(float[] d, uint seed, float start, float dur, float cutoff, float attack, float decay, float gain, float cutoffEnd = -1f)
        {
            var n = new Noise(seed);
            int s0 = (int)(start * Rate);
            int len = (int)(dur * Rate);
            float lp = 0f;
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float c = cutoffEnd > 0 ? Mathf.Lerp(cutoff, cutoffEnd, t / dur) : cutoff;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * c / Rate);
                lp += a * (n.Next() - lp);
                d[s0 + i] += lp * Env(t, attack, decay) * gain;
            }
        }

        static void AddTone(float[] d, float start, float dur, float f0, float f1, float attack, float decay, float gain, int harmonics = 1)
        {
            int s0 = (int)(start * Rate);
            int len = (int)(dur * Rate);
            float phase = 0f;
            for (int i = 0; i < len && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(f0, f1, Mathf.Sqrt(Mathf.Clamp01(t / dur)));
                phase += 2f * Mathf.PI * f / Rate;
                float v = 0f;
                for (int h = 1; h <= harmonics; h++) v += Mathf.Sin(phase * h) / h;
                d[s0 + i] += v * Env(t, attack, decay) * gain;
            }
        }

        static void Saturate(float[] d, float drive)
        {
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * drive);
        }

        static void Echo(float[] d, float delay, float feedback)
        {
            int dl = (int)(delay * Rate);
            for (int i = dl; i < d.Length; i++) d[i] += d[i - dl] * feedback;
        }

        // ---------------------------------------------------------------- EFEITOS

        public static AudioClip LightHit()
        {
            var d = Buffer(0.16f);
            AddNoise(d, 11, 0, 0.16f, 2600f, 0.001f, 32f, 0.9f);
            AddTone(d, 0, 0.14f, 180f, 70f, 0.001f, 26f, 0.8f);
            Saturate(d, 1.6f);
            return ToClip("sfx_hit_light", d);
        }

        public static AudioClip HeavyHit()
        {
            var d = Buffer(0.32f);
            AddNoise(d, 23, 0, 0.3f, 1500f, 0.001f, 16f, 1f);
            AddTone(d, 0, 0.3f, 130f, 42f, 0.001f, 11f, 1.1f);
            AddNoise(d, 29, 0, 0.05f, 6000f, 0.0005f, 70f, 0.5f);
            Saturate(d, 2.4f);
            return ToClip("sfx_hit_heavy", d);
        }

        public static AudioClip Block()
        {
            var d = Buffer(0.14f);
            AddNoise(d, 37, 0, 0.12f, 5200f, 0.0005f, 55f, 0.7f);
            AddTone(d, 0, 0.1f, 820f, 600f, 0.0005f, 45f, 0.45f, 3);
            AddTone(d, 0, 0.12f, 140f, 90f, 0.001f, 40f, 0.5f);
            return ToClip("sfx_block", d);
        }

        public static AudioClip Grab()
        {
            var d = Buffer(0.3f);
            AddNoise(d, 41, 0, 0.18f, 1800f, 0.05f, 14f, 0.6f);
            AddTone(d, 0.12f, 0.18f, 110f, 60f, 0.002f, 20f, 0.9f);
            AddNoise(d, 43, 0.12f, 0.15f, 900f, 0.002f, 25f, 0.6f);
            return ToClip("sfx_grab", d);
        }

        public static AudioClip Whoosh(float dur = 0.22f, uint seed = 53)
        {
            var d = Buffer(dur);
            AddNoise(d, seed, 0, dur, 500f, dur * 0.45f, 9f, 1f, 3500f);
            return ToClip("sfx_whoosh", d, true, 0.6f);
        }

        public static AudioClip Special()
        {
            var d = Buffer(0.4f);
            AddTone(d, 0, 0.38f, 260f, 880f, 0.01f, 6f, 0.5f, 2);
            AddNoise(d, 61, 0, 0.38f, 700f, 0.12f, 6f, 0.7f, 4200f);
            return ToClip("sfx_special", d, true, 0.75f);
        }

        public static AudioClip Signature()
        {
            var d = Buffer(1.0f);
            AddTone(d, 0, 0.9f, 70f, 40f, 0.002f, 4f, 1f);
            AddTone(d, 0.05f, 0.9f, 440f, 880f, 0.05f, 3f, 0.25f, 3);
            AddTone(d, 0.05f, 0.9f, 554f, 1108f, 0.05f, 3f, 0.2f, 2);
            AddNoise(d, 67, 0, 0.6f, 900f, 0.001f, 6f, 0.6f, 5000f);
            Saturate(d, 1.8f);
            Echo(d, 0.11f, 0.25f);
            return ToClip("sfx_signature", d);
        }

        public static AudioClip KO()
        {
            var d = Buffer(1.6f);
            AddTone(d, 0, 1.4f, 90f, 28f, 0.001f, 3.2f, 1.2f);
            AddNoise(d, 71, 0, 0.8f, 1200f, 0.001f, 6f, 1f);
            Saturate(d, 2.2f);
            Echo(d, 0.17f, 0.35f);
            return ToClip("sfx_ko", d);
        }

        /// <summary>Gongo/sino de ringue: parciais inarmônicos com decaimento longo.</summary>
        public static AudioClip Bell(int strikes = 1)
        {
            float spacing = 0.28f;
            var d = Buffer(1.6f + spacing * (strikes - 1));
            float[] partials = { 1f, 2.02f, 2.74f, 3.76f, 5.4f };
            float[] gains = { 1f, 0.55f, 0.4f, 0.25f, 0.12f };
            for (int s = 0; s < strikes; s++)
                for (int p = 0; p < partials.Length; p++)
                    AddTone(d, s * spacing, 1.5f, 880f * partials[p], 880f * partials[p], 0.002f, 2.6f + p * 0.9f, gains[p] * 0.5f);
            return ToClip("sfx_bell", d, true, 0.8f);
        }

        public static AudioClip Bounce()
        {
            var d = Buffer(0.45f);
            int len = d.Length;
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                float f = 85f + 12f * Mathf.Sin(t * 40f);
                phase += 2f * Mathf.PI * f / Rate;
                d[i] += Mathf.Sin(phase) * Env(t, 0.003f, 7f) * 0.9f;
            }
            AddNoise(d, 79, 0, 0.1f, 1600f, 0.001f, 40f, 0.6f);
            return ToClip("sfx_bounce", d);
        }

        public static AudioClip Tech()
        {
            var d = Buffer(0.22f);
            AddNoise(d, 83, 0, 0.06f, 4000f, 0.0005f, 70f, 0.8f);
            AddNoise(d, 89, 0.09f, 0.06f, 4000f, 0.0005f, 70f, 0.8f);
            AddTone(d, 0, 0.2f, 600f, 900f, 0.001f, 20f, 0.3f);
            return ToClip("sfx_tech", d);
        }

        public static AudioClip Land()
        {
            var d = Buffer(0.14f);
            AddTone(d, 0, 0.12f, 95f, 55f, 0.002f, 30f, 0.8f);
            AddNoise(d, 97, 0, 0.1f, 700f, 0.001f, 35f, 0.5f);
            return ToClip("sfx_land", d, true, 0.5f);
        }

        public static AudioClip Thud()
        {
            var d = Buffer(0.35f);
            AddTone(d, 0, 0.3f, 80f, 38f, 0.001f, 12f, 1f);
            AddNoise(d, 101, 0, 0.25f, 600f, 0.001f, 14f, 0.8f);
            return ToClip("sfx_thud", d, true, 0.8f);
        }

        public static AudioClip UiMove()
        {
            var d = Buffer(0.05f);
            AddTone(d, 0, 0.05f, 880f, 880f, 0.002f, 60f, 0.5f, 2);
            return ToClip("ui_move", d, true, 0.4f);
        }

        public static AudioClip UiConfirm()
        {
            var d = Buffer(0.16f);
            AddTone(d, 0, 0.07f, 660f, 660f, 0.002f, 30f, 0.5f, 2);
            AddTone(d, 0.07f, 0.09f, 990f, 990f, 0.002f, 25f, 0.5f, 2);
            return ToClip("ui_confirm", d, true, 0.5f);
        }

        public static AudioClip UiBack()
        {
            var d = Buffer(0.16f);
            AddTone(d, 0, 0.07f, 880f, 880f, 0.002f, 30f, 0.5f, 2);
            AddTone(d, 0.07f, 0.09f, 587f, 587f, 0.002f, 25f, 0.5f, 2);
            return ToClip("ui_back", d, true, 0.45f);
        }

        /// <summary>Sinal sonoro que substitui a locução enquanto não há voz autorizada.</summary>
        public static AudioClip AnnouncerCue(float f0, float f1)
        {
            var d = Buffer(0.5f);
            AddTone(d, 0, 0.45f, f0, f1, 0.01f, 5f, 0.6f, 4);
            AddTone(d, 0, 0.45f, f0 * 1.5f, f1 * 1.5f, 0.01f, 5f, 0.3f, 3);
            return ToClip("cue", d, true, 0.7f);
        }

        // ---------------------------------------------------------------- AMBIENTE

        public static AudioClip CrowdLoop()
        {
            float dur = 6f;
            var d = Buffer(dur);
            var n = new Noise(131);
            float lp1 = 0, lp2 = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp1 += 0.08f * (n.Next() - lp1);
                lp2 += 0.03f * (lp1 - lp2);
                float mod = 0.7f + 0.15f * Mathf.Sin(t * 2.1f) + 0.1f * Mathf.Sin(t * 5.3f + 1f) + 0.05f * Mathf.Sin(t * 11f);
                d[i] = lp2 * mod;
            }
            // Loop sem emenda: crossfade do fim com o começo.
            int fade = Rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                int end = d.Length - fade + i;
                d[i] = d[i] * a + d[end] * (1 - a);
            }
            var trimmed = new float[d.Length - fade];
            System.Array.Copy(d, trimmed, trimmed.Length);
            return ToClip("amb_crowd", trimmed, true, 0.5f);
        }

        public static AudioClip Cheer()
        {
            var d = Buffer(2.4f);
            AddNoise(d, 137, 0, 2.4f, 1300f, 0.35f, 1.4f, 1f, 900f);
            AddNoise(d, 139, 0.1f, 2.2f, 2600f, 0.25f, 1.8f, 0.4f);
            return ToClip("amb_cheer", d, true, 0.7f);
        }

        // ---------------------------------------------------------------- MÚSICA

        static readonly float[] MinorPent = { 0, 3, 5, 7, 10, 12 };

        static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        /// <summary>Faixa de luta: 128 BPM, 8 compassos em Lá menor (bumbo, caixa, chimbal, baixo e acordes).</summary>
        public static AudioClip FightMusic()
        {
            const float bpm = 128f;
            float beat = 60f / bpm;
            int bars = 8;
            float dur = bars * 4 * beat;
            var d = Buffer(dur);
            int[] bassRoots = { 45, 45, 48, 43, 45, 45, 41, 43 };
            for (int bar = 0; bar < bars; bar++)
            {
                float b0 = bar * 4 * beat;
                for (int q = 0; q < 4; q++)
                {
                    float t = b0 + q * beat;
                    AddTone(d, t, 0.25f, 120f, 42f, 0.001f, 16f, 0.9f); // bumbo
                    if (q == 1 || q == 3)
                    {
                        AddNoise(d, (uint)(200 + bar * 4 + q), t, 0.18f, 3500f, 0.001f, 22f, 0.55f);
                        AddTone(d, t, 0.12f, 200f, 160f, 0.001f, 30f, 0.3f);
                    }
                    for (int e = 0; e < 2; e++)
                        AddNoise(d, (uint)(400 + bar * 8 + q * 2 + e), t + e * beat * 0.5f, 0.05f, 9000f, 0.0005f, 80f, 0.12f);
                    // baixo em colcheias
                    int root = bassRoots[bar];
                    for (int e = 0; e < 2; e++)
                    {
                        float n = root + (e == 1 && q == 3 ? 7 : 0);
                        AddTone(d, t + e * beat * 0.5f, beat * 0.45f, Midi(n), Midi(n), 0.004f, 7f, 0.32f, 4);
                    }
                }
                // acorde curto no início do compasso
                int chordRoot = bassRoots[bar] + 24;
                foreach (int iv in new[] { 0, 3, 7 })
                    AddTone(d, b0 + beat * 0.5f, beat * 0.9f, Midi(chordRoot + iv), Midi(chordRoot + iv), 0.01f, 5f, 0.08f, 3);
                // frase melódica nos compassos pares
                if (bar % 2 == 1)
                    for (int k = 0; k < 4; k++)
                    {
                        float note = 69 + MinorPent[(bar + k * 2) % MinorPent.Length];
                        AddTone(d, b0 + (2 + k * 0.5f) * beat, beat * 0.45f, Midi(note), Midi(note), 0.005f, 6f, 0.1f, 2);
                    }
            }
            Saturate(d, 1.3f);
            return ToClip("music_fight", d, true, 0.8f);
        }

        /// <summary>Faixa de menu: 92 BPM, mais calma.</summary>
        public static AudioClip MenuMusic()
        {
            const float bpm = 92f;
            float beat = 60f / bpm;
            int bars = 8;
            float dur = bars * 4 * beat;
            var d = Buffer(dur);
            int[] roots = { 45, 41, 48, 43, 45, 41, 43, 40 };
            for (int bar = 0; bar < bars; bar++)
            {
                float b0 = bar * 4 * beat;
                int r = roots[bar];
                foreach (int iv in new[] { 12, 15, 19, 24 })
                    AddTone(d, b0, 4 * beat, Midi(r + iv), Midi(r + iv), 0.4f, 0.9f, 0.07f, 3);
                for (int q = 0; q < 4; q++)
                {
                    float t = b0 + q * beat;
                    if (q == 0 || q == 2) AddTone(d, t, 0.3f, 100f, 45f, 0.001f, 12f, 0.6f);
                    if (q == 1 || q == 3) AddNoise(d, (uint)(700 + bar * 4 + q), t, 0.15f, 2500f, 0.002f, 25f, 0.25f);
                    AddTone(d, t, beat * 0.8f, Midi(r), Midi(r), 0.01f, 4f, 0.28f, 3);
                    AddNoise(d, (uint)(900 + bar * 4 + q), t + beat * 0.5f, 0.04f, 8000f, 0.0005f, 90f, 0.08f);
                }
            }
            return ToClip("music_menu", d, true, 0.7f);
        }
    }
}
