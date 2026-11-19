using System.Collections.Generic;
using UltimoRound.Combat;
using UltimoRound.Core;
using UnityEngine;

namespace UltimoRound.Audio
{
    public enum UiSound { Move, Confirm, Back }
    public enum AnnouncerLine { Round, Fight, KO, TimeUp, Victory, Draw }
    public enum MusicTrack { None, Menu, Fight }

    /// <summary>
    /// Áudio com grupos separados (Música, Efeitos, Voz, Ambiente) controlados pelas preferências.
    /// Evita empilhar o mesmo som: intervalo mínimo e limite de instâncias simultâneas por clipe.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int PoolSize = 16;
        const float MinRepeatInterval = 0.035f;
        const int MaxInstancesPerClip = 3;

        AudioSource _music;
        AudioSource _ambience;
        AudioSource[] _pool;
        int _next;
        readonly Dictionary<AudioClip, float> _lastPlay = new Dictionary<AudioClip, float>();
        readonly Dictionary<SfxKind, AudioClip> _sfx = new Dictionary<SfxKind, AudioClip>();
        AudioClip _uiMove, _uiConfirm, _uiBack, _crowd, _cheer, _thud;
        AudioClip[] _announcer;
        AudioClip _menuMusic, _fightMusic;
        MusicTrack _currentTrack;
        SoundBank _bank;
        readonly Dictionary<AudioClip[], int> _lastIndex = new Dictionary<AudioClip[], int>();
        GameSettings _settings;
        float _musicDuck = 1f;

        public void Init(GameSettings settings, SoundBank bank = null)
        {
            _settings = settings;
            _bank = bank;
            _music = NewSource("Music", true);
            _ambience = NewSource("Ambience", true);
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++) _pool[i] = NewSource("Sfx" + i, false);

            _sfx[SfxKind.LightHit] = SoundSynth.LightHit();
            _sfx[SfxKind.HeavyHit] = SoundSynth.HeavyHit();
            _sfx[SfxKind.Block] = SoundSynth.Block();
            _sfx[SfxKind.Grab] = SoundSynth.Grab();
            _sfx[SfxKind.Dash] = SoundSynth.Whoosh(0.2f, 53);
            _sfx[SfxKind.Whoosh] = SoundSynth.Whoosh(0.16f, 59);
            _sfx[SfxKind.Special] = SoundSynth.Special();
            _sfx[SfxKind.Signature] = SoundSynth.Signature();
            _sfx[SfxKind.KO] = SoundSynth.KO();
            _sfx[SfxKind.Bell] = SoundSynth.Bell(1);
            _sfx[SfxKind.Bounce] = SoundSynth.Bounce();
            _sfx[SfxKind.Tech] = SoundSynth.Tech();
            _sfx[SfxKind.Land] = SoundSynth.Land();
            _sfx[SfxKind.Jump] = SoundSynth.Whoosh(0.12f, 61);
            _thud = SoundSynth.Thud();
            _uiMove = SoundSynth.UiMove();
            _uiConfirm = SoundSynth.UiConfirm();
            _uiBack = SoundSynth.UiBack();
            _crowd = SoundSynth.CrowdLoop();
            _cheer = SoundSynth.Cheer();
            // Locução pendente: sinais sonoros distintos por chamada (voz autorizada não disponível).
            _announcer = new[]
            {
                SoundSynth.Bell(1),                       // Round
                SoundSynth.AnnouncerCue(330f, 660f),      // Lutem
                SoundSynth.Bell(3),                       // KO
                SoundSynth.AnnouncerCue(520f, 260f),      // Tempo
                SoundSynth.AnnouncerCue(392f, 784f),      // Vitória
                SoundSynth.AnnouncerCue(440f, 440f),      // Empate
            };
            _menuMusic = SoundSynth.MenuMusic();
            _fightMusic = SoundSynth.FightMusic();
            ApplyVolumes();
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        float Group(float g) => _settings == null ? 1f : _settings.MasterVolume * g;
        float SfxVol => Group(_settings != null ? _settings.SfxVolume : 1f);
        float MusicVol => Group(_settings != null ? _settings.MusicVolume : 1f);
        float VoiceVol => Group(_settings != null ? _settings.VoiceVolume : 1f);

        public void ApplyVolumes()
        {
            if (_music != null) _music.volume = MusicVol * 0.6f * _musicDuck;
            if (_ambience != null) _ambience.volume = SfxVol * 0.35f;
        }

        void Update()
        {
            // Recupera a música após "abaixar" (KO, assinatura).
            if (_musicDuck < 1f)
            {
                _musicDuck = Mathf.MoveTowards(_musicDuck, 1f, Time.unscaledDeltaTime * 0.5f);
                ApplyVolumes();
            }
        }

        public void Duck(float level)
        {
            _musicDuck = Mathf.Clamp01(level);
            ApplyVolumes();
        }

        bool CanPlay(AudioClip clip)
        {
            float now = Time.unscaledTime;
            if (_lastPlay.TryGetValue(clip, out float last) && now - last < MinRepeatInterval) return false;
            int playing = 0;
            foreach (var s in _pool) if (s.isPlaying && s.clip == clip) playing++;
            if (playing >= MaxInstancesPerClip) return false;
            _lastPlay[clip] = now;
            return true;
        }

        void PlayOnPool(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || !CanPlay(clip)) return;
            var s = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            s.Stop();
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            s.Play();
        }

        public void PlaySfx(SfxKind kind, float volume = 1f, float pitch = 1f)
        {
            var bankClips = BankFor(kind);
            if (bankClips != null && bankClips.Length > 0)
            {
                // Variação de amostra e de tom para os golpes não soarem repetidos.
                PlayOnPool(Pick(bankClips), SfxVol * volume, pitch * Random.Range(0.96f, 1.04f));
                return;
            }
            if (_sfx.TryGetValue(kind, out var clip)) PlayOnPool(clip, SfxVol * volume, pitch);
        }

        AudioClip[] BankFor(SfxKind kind)
        {
            if (_bank == null) return null;
            switch (kind)
            {
                case SfxKind.LightHit: return _bank.LightHits;
                case SfxKind.HeavyHit: return _bank.HeavyHits;
                case SfxKind.Block: return _bank.LightBlocks;
                case SfxKind.Whoosh: return _bank.ShortWhooshes;
                case SfxKind.Special: return _bank.LongWhooshes;
                case SfxKind.Jump:
                case SfxKind.Dash: return _bank.Acrobatics;
                case SfxKind.Finisher: return _bank.FinisherHits;
                case SfxKind.HeavyBlock: return _bank.HeavyBlocks;
                case SfxKind.SpecialImpact: return _bank.SpecialImpacts;
                case SfxKind.SignatureImpact: return _bank.SignatureImpacts;
                case SfxKind.ThrowImpact: return _bank.ThrowImpacts;
                default: return null;
            }
        }

        AudioClip Pick(AudioClip[] clips)
        {
            _lastIndex.TryGetValue(clips, out int last);
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == last) i = (i + 1) % clips.Length; // não repete a mesma amostra em seguida
            _lastIndex[clips] = i;
            return clips[i];
        }

        public void PlayThud(float volume = 1f) => PlayOnPool(_thud, SfxVol * volume, 1f);

        public void PlayUi(UiSound s)
        {
            var clip = s == UiSound.Move ? _uiMove : s == UiSound.Confirm ? _uiConfirm : _uiBack;
            PlayOnPool(clip, SfxVol * 0.8f, 1f);
        }

        public void PlayAnnouncer(AnnouncerLine line)
        {
            if (_announcer == null) return;
            PlayOnPool(_announcer[(int)line], VoiceVol, 1f);
        }

        public void PlayCheer(float volume = 1f) => PlayOnPool(_cheer, SfxVol * volume * 0.8f, 1f);

        public void PlayMusic(MusicTrack track)
        {
            if (_music == null || track == _currentTrack) return;
            _currentTrack = track;
            _music.Stop();
            _music.clip = track == MusicTrack.Menu ? _menuMusic : track == MusicTrack.Fight ? _fightMusic : null;
            if (_music.clip != null) _music.Play();
            ApplyVolumes();
        }

        public void SetAmbience(bool on)
        {
            if (_ambience == null) return;
            if (on && !_ambience.isPlaying)
            {
                _ambience.clip = _crowd;
                _ambience.Play();
            }
            else if (!on) _ambience.Stop();
        }

        /// <summary>Interrompe todos os efeitos (reset/troca de cena) para não tocar sons repetidos.</summary>
        public void StopAllSfx()
        {
            if (_pool == null) return;
            foreach (var s in _pool) s.Stop();
            _lastPlay.Clear();
        }

        public void PauseAll(bool paused)
        {
            if (_pool == null) return;
            foreach (var s in _pool)
            {
                if (paused) s.Pause();
                else s.UnPause();
            }
        }
    }
}
