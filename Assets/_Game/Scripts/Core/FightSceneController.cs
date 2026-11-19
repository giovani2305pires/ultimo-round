using System.Collections.Generic;
using System.Text;
using UltimoRound.AI;
using UltimoRound.Audio;
using UltimoRound.Combat;
using UltimoRound.Input;
using UltimoRound.Net;
using UltimoRound.Presentation;
using UltimoRound.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UltimoRound.Core
{
    /// <summary>
    /// Orquestra a cena de luta: tela de confronto → introdução → rounds → resultado/revanche.
    /// Captura a entrada por quadro e a consome em ticks fixos; a apresentação apenas lê a simulação.
    /// </summary>
    public sealed class FightSceneController : MonoBehaviour
    {
        public const string SceneName = "Fight";

        [Header("Referências (montadas pela ferramenta de cena)")]
        public GameDatabase Database;
        public FightCamera Camera;
        public RingRopes Ropes;
        public CrowdRenderer Crowd;
        public ArenaScreen ArenaScreen;

        enum Flow { Versus, Intro, Match, Results, NetWait }

        GameServices _gs;
        Session _session;
        MatchController _match;
        FightSimulation Sim => _match.Sim;
        readonly FighterView[] _views = new FighterView[2];
        VfxPool _vfx;
        DebugOverlay _overlay;
        HudView _hud;
        FixedStepClock _clock;
        readonly InputAccumulator[] _acc = { new InputAccumulator(), new InputAccumulator() };
        readonly CpuBrain[] _cpu = new CpuBrain[2];
        readonly bool[] _human = new bool[2];

        Canvas _menuCanvas;
        ListPanel _pause, _confirm, _results;
        MoveListPanel _moves;
        OptionsPanel _options;
        VersusOverlay _versus;
        DisconnectOverlay _disconnect;

        Flow _flow;
        float _flowTimer;
        int _introStep;
        bool _paused;
        int _pauseOwner;
        float _resultsDelay = -1f;
        bool _training;
        int _trainingIdle;
        int _koTimer;
        readonly List<string> _history = new List<string>();
        InputFrame _lastHistoryFrame;
        int _historyRepeat;

        // Rede local: o anfitrião simula; o convidado só exibe a cópia recebida.
        NetSession _net;
        NetFightHost _netHost;
        NetFightGuest _netGuest;
        byte _netEpoch;
        bool _netDead;
        float _netInfoTimer;
        ListPanel _netLost;
        TextMeshProUGUI _netInfo;
        TextMeshProUGUI _netWaitLabel;

        // Demonstração de combo (treino)
        MoveSlot[] _demo;
        int _demoIndex;
        int _demoTimeout;

        void Start()
        {
            _gs = GameServices.Ensure(Database);
            _session = _gs.Session;
            var db = _gs.Database;
            UIFactory.EnsureEventSystem();
            PrepareSessionForDirectPlay(db);

            _training = _session.Mode == GameMode.Training;
            for (int i = 0; i < 2; i++) _human[i] = !_session.IsCpu(i);
            if (_session.Mode == GameMode.Network)
            {
                if (_gs.Net.Active) _net = _gs.Net;
                else _session.Mode = GameMode.VsCpu; // cena aberta direto ou conexão já perdida
                _human[1] = false; // o outro jogador chega pela rede
            }

            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Lutador" + (i + 1));
                go.transform.SetParent(transform, false);
                _views[i] = go.AddComponent<FighterView>();
                _views[i].Build(_session.Fighters[i], _session.Palettes[i], db.LitMaterial, db.FindVisuals(_session.Fighters[i]));
            }

            var vfxGo = new GameObject("VFX");
            vfxGo.transform.SetParent(transform, false);
            _vfx = vfxGo.AddComponent<VfxPool>();
            _vfx.Init(db.ParticleMaterial, db.VfxPrefabs);

            var ovGo = new GameObject("DebugOverlay");
            ovGo.transform.SetParent(transform, false);
            _overlay = ovGo.AddComponent<DebugOverlay>();
            _overlay.Init(db.ParticleMaterial);

            _match = new MatchController(db.Combat, _session.Fighters[0], _session.Fighters[1], _session.Palettes[0], _session.Palettes[1], _training);
            ApplyTrainingFlags();
            _clock = new FixedStepClock(CombatConfig.TickRate);
            int seed = System.Environment.TickCount;
            if (_net != null)
            {
                _net.BeginFight();
                _netHost = _net.FightHost;
                _netGuest = _net.FightGuest;
                _net.TimeoutSeconds = 20f;
                if (_netGuest != null)
                {
                    // O anúncio do round chega do anfitrião: a réplica não gera eventos próprios.
                    Sim.Events.Clear();
                    _match.Events.Clear();
                }
                if (NetTestRun.Active)
                {
                    _human[0] = _netGuest != null;
                    _cpu[_net.LocalIndex] = new CpuBrain(CpuDifficulty.Hard, seed + _net.LocalIndex * 7919);
                    NetTestRun.Note("luta carregada");
                }
            }
            else for (int i = 0; i < 2; i++)
                if (!_human[i])
                {
                    var diff = _training ? _session.TrainingCpuDifficulty : _session.Difficulty;
                    _cpu[i] = new CpuBrain(diff, seed + i * 7919);
                }

            BuildUI();
            ApplySettings();
            _gs.Input.DeviceLost += OnDeviceLost;
            _gs.Input.DeviceRegained += OnDeviceRegained;
            _gs.Audio.StopAllSfx();
            _gs.Audio.PlayMusic(MusicTrack.Fight);
            _gs.Audio.SetAmbience(true);

            SnapViews();
            Camera.Snap(Sim[0].Pos, Sim[1].Pos);
            if (BenchmarkRecorder.Requested)
            {
                // Mede a folga real de desempenho: sem VSync e sem limite de FPS durante a medição.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                gameObject.AddComponent<BenchmarkRecorder>();
                EnterMatch();
                return;
            }
            if (_net != null) EnterNetWait();
            else if (_training) EnterMatch();
            else EnterVersus();
        }

        /// <summary>Testes/demonstração: CPU nos dois lados e luta reiniciada. Pula tela de confronto.</summary>
        public void EnableAutoPlay(CpuDifficulty difficulty = CpuDifficulty.Hard)
        {
            _session.AutoPlay = true;
            for (int i = 0; i < 2; i++)
            {
                _human[i] = false;
                _cpu[i] = new CpuBrain(difficulty, 1000 + i * 31);
            }
            Rematch();
        }

        public MatchController Match => _match;
        public bool IsInResults => _flow == Flow.Results;
        public MoveListPanel MoveList => _moves;
        public VfxPool Vfx => _vfx;
        public OptionsPanel Options => _options;

        void OnDestroy()
        {
            if (_gs != null)
            {
                _gs.Input.DeviceLost -= OnDeviceLost;
                _gs.Input.DeviceRegained -= OnDeviceRegained;
            }
        }

        /// <summary>Permite abrir a cena de luta direto no editor (sem passar pelo menu).</summary>
        void PrepareSessionForDirectPlay(GameDatabase db)
        {
            var input = _gs.Input;
            if (!_session.HasFighters)
            {
                _session.Mode = GameMode.VsCpu;
                _session.Fighters[0] = db.Fighters[0];
                _session.Fighters[1] = db.Fighters.Count > 1 ? db.Fighters[1] : db.Fighters[0];
            }
            if (!input.Slots[0].Assigned)
            {
                var dev = UnityEngine.InputSystem.Gamepad.current != null ? (UnityEngine.InputSystem.InputDevice)UnityEngine.InputSystem.Gamepad.current : UnityEngine.InputSystem.Keyboard.current;
                if (dev != null) input.Assign(0, dev);
            }
            if (_session.Mode == GameMode.Versus && !input.Slots[1].Assigned) _session.Mode = GameMode.VsCpu;
            if (_session.Fighters[0] == _session.Fighters[1] && _session.Palettes[0] == _session.Palettes[1])
                _session.Palettes[1] = (_session.Palettes[0] + 1) % db.VariantCount(_session.Fighters[1]);
        }

        string Tag(int i)
        {
            if (_net != null) return i == _net.LocalIndex ? "VOCÊ" : "RIVAL";
            if (_training) return i == 0 ? "J1" : "BONECO";
            if (!_human[i]) return "CPU " + Session.DifficultyLabel(_session.Difficulty).ToUpperInvariant();
            return "J" + (i + 1);
        }

        // ---------------------------------------------------------------- UI

        void BuildUI()
        {
            _hud = new HudView(transform);
            _hud.SetFighters(_session.Fighters[0], _session.Fighters[1], Tag(0), Tag(1));
            _menuCanvas = UIFactory.Canvas("MenusLuta", 30, transform);
            var root = _menuCanvas.transform;
            _versus = new VersusOverlay(root);

            _pause = new ListPanel(root, "PAUSA", 640, 66, 10);
            _pause.List.Audio = _gs.Audio;
            _pause.List.OnBack = Resume;
            BuildPauseMenu();

            _moves = new MoveListPanel(root);
            _moves.Actions.Audio = _gs.Audio;
            _moves.Actions.OnBack = () => { _moves.Close(); _pause.Open(); };

            _options = new OptionsPanel(root, _gs);
            _options.OnClose = () => { ApplySettings(); _pause.Open(); };
            _options.OnChanged = ApplySettings;

            _confirm = new ListPanel(root, "ABANDONAR A PARTIDA?", 520, 66, 3, 0.9f, -330);
            _confirm.Body.text = "O progresso desta luta será perdido.";
            _confirm.List.Audio = _gs.Audio;
            _confirm.List.Add("Não, continuar", () => { _confirm.Close(); _pause.Open(); });
            _confirm.List.Add("Sim, sair para o menu", () => LeaveTo(false));
            _confirm.List.OnBack = () => { _confirm.Close(); _pause.Open(); };

            _results = new ListPanel(root, "RESULTADO", 600, 70, 4, 0.55f, -520);
            _results.List.Audio = _gs.Audio;
            if (_netGuest == null)
            {
                _results.List.Add("Revanche", Rematch);
                _results.List.Add("Voltar à seleção", () => LeaveTo(true));
            }
            _results.List.Add("Menu principal", () => LeaveTo(false));

            _disconnect = new DisconnectOverlay(root);

            if (_net != null)
            {
                _confirm.Body.text = "A partida termina para os dois jogadores.";
                _netLost = new ListPanel(root, "PARTIDA ENCERRADA", 520, 66, 1, 0.8f, -560);
                _netLost.List.Audio = _gs.Audio;
                _netLost.List.Add("Voltar ao menu", () => LeaveTo(false));
                _netInfo = UIFactory.Label("InfoRede", root, "", 22, UITheme.TextDim, TextAlignmentOptions.Center);
                UIFactory.Place(_netInfo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(500, 30));
                _netWaitLabel = UIFactory.Label("AguardandoRede", root, "Aguardando o outro jogador...", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Place(_netWaitLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 80));
                _netWaitLabel.gameObject.SetActive(false);
            }
        }

        void BuildPauseMenu()
        {
            var l = _pause.List;
            l.Clear();
            l.Add("Continuar", Resume);
            l.Add("Lista de golpes", OpenMoveList);
            if (_training)
            {
                l.Add("Resetar posições (R / Select)", () => { TrainingReset(); Resume(); });
                l.Add("Boneco", null, () => Session.DummyLabel(_session.Dummy), d =>
                {
                    _session.Dummy = (DummyMode)(((int)_session.Dummy + d + 4) % 4);
                    _cpu[1]?.Reset();
                });
                l.Add("Dificuldade da CPU (boneco)", null, () => Session.DifficultyLabel(_session.TrainingCpuDifficulty), d =>
                {
                    _session.TrainingCpuDifficulty = (CpuDifficulty)(((int)_session.TrainingCpuDifficulty + d + 3) % 3);
                    _cpu[1] = new CpuBrain(_session.TrainingCpuDifficulty, System.Environment.TickCount);
                }, () => _session.Dummy == DummyMode.Cpu);
                l.Add("Vida infinita", null, () => _session.TrainingInfiniteHealth ? "Ligada" : "Desligada", d => { _session.TrainingInfiniteHealth = !_session.TrainingInfiniteHealth; ApplyTrainingFlags(); });
                l.Add("Energia infinita", null, () => _session.TrainingInfiniteEnergy ? "Ligada" : "Desligada", d => { _session.TrainingInfiniteEnergy = !_session.TrainingInfiniteEnergy; });
                l.Add("Volumes de colisão (dev)", null, () => _session.TrainingShowBoxes ? "Visíveis" : "Ocultos", d => { _session.TrainingShowBoxes = !_session.TrainingShowBoxes; ApplyTrainingFlags(); });
            }
            l.Add("Opções", () => { _pause.Close(); _options.Open(); });
            if (_net == null) l.Add("Reiniciar luta", () => { Rematch(); });
            l.Add(_training ? "Sair do treino" : "Sair para o menu", () =>
            {
                if (_training) LeaveTo(false);
                else { _pause.Close(); _confirm.Open(); }
            });
        }

        void OpenMoveList()
        {
            _pause.Close();
            var def = _session.Fighters[_net != null ? _net.LocalIndex : _pauseOwner];
            var a = _moves.Actions;
            a.Clear();
            if (_training && _pauseOwner == 0)
                foreach (var c in def.Combos)
                {
                    var combo = c;
                    a.Add("Demonstrar: " + combo.Name, () => StartDemo(combo));
                }
            a.Add("Voltar", () => { _moves.Close(); _pause.Open(); });
            _moves.Open(def);
        }

        void ApplySettings()
        {
            Camera.Intensity = _gs.Settings.CameraIntensity;
            _gs.ApplyCameraQuality(Camera.GetComponent<UnityEngine.Camera>());
            bool shadows = _gs.Settings.Quality > 0;
            foreach (var v in _views) v?.SetShadows(shadows);
            Crowd?.gameObject.SetActive(true);
        }

        void ApplyTrainingFlags()
        {
            if (!_training) return;
            Sim.PreventKO = _session.TrainingInfiniteHealth;
            _overlay?.SetVisible(_session.TrainingShowBoxes);
            if (!_session.TrainingShowBoxes) _hud?.SetDevInfo("");
        }

        // ---------------------------------------------------------------- FLUXO

        void EnterNetWait()
        {
            _flow = Flow.NetWait;
            _hud.SetVisible(false);
            _netWaitLabel.gameObject.SetActive(true);
        }

        void TickNetWait()
        {
            if (_netGuest != null)
            {
                _net.ReportSceneReady();
                if (!_net.HostStartedFight) return;
            }
            else
            {
                if (!_net.GuestSceneReady) return;
                _net.StartFightFlow();
            }
            _netWaitLabel.gameObject.SetActive(false);
            NetTestRun.Note("confronto");
            EnterVersus();
        }

        void EnterVersus()
        {
            _flow = Flow.Versus;
            _flowTimer = 0f;
            _hud.SetVisible(false);
            _versus.Show(_session.Fighters[0], _session.Fighters[1], Tag(0), Tag(1));
        }

        void EnterIntro()
        {
            _versus.Hide();
            _flow = Flow.Intro;
            _flowTimer = 0f;
            _introStep = 0;
            _hud.SetVisible(true);
            _hud.SetTopVisible(false);
            ShowIntroName(0);
        }

        void ShowIntroName(int i)
        {
            var f = _session.Fighters[i];
            _hud.ShowBanner(f.DisplayName.ToUpperInvariant(), 1.3f, UITheme.PlayerColor(i));
            _hud.ShowSub(f.Epithet, 1.3f);
            _gs.Audio.PlayCheer(0.5f);
        }

        void EnterMatch()
        {
            _versus.Hide();
            _flow = Flow.Match;
            _hud.SetVisible(true);
            _hud.SetTopVisible(true);
            _hud.ClearTransient();
            _clock.Reset();
            foreach (var a in _acc) a.Reset();
            Sim.ClearInputBuffers();
            if (_net != null)
            {
                _net.TimeoutSeconds = 8f;
                _net.ResetRttStats();
            }
            if (_training)
            {
                _hud.SetTimer("∞");
                _hud.ShowBanner("TREINO", 1.2f, UITheme.Accent);
                _hud.ShowSub("Start/Esc: menu de treino  •  R/Select: resetar", 3f);
            }
            ProcessEvents(); // anúncio do primeiro round
        }

        void Rematch()
        {
            _pause.Close();
            _results.Close();
            _confirm.Close();
            _moves.Close();
            _paused = false;
            _gs.Audio.PauseAll(false);
            _gs.Audio.StopAllSfx();
            _netHost?.NewEpoch();
            _match.StartMatch();
            if (_netGuest != null)
            {
                Sim.Events.Clear();
                _match.Events.Clear();
                _netEpoch = _netGuest.NewestEpoch;
                _netGuest.PurgeOldEpoch();
            }
            ApplyTrainingFlags();
            foreach (var c in _cpu) c?.Reset();
            _vfx.ClearAll();
            _resultsDelay = -1f;
            _demo = null;
            _history.Clear();
            SnapViews();
            _hud.SetWins(_match.Wins);
            EnterMatch();
        }

        void LeaveTo(bool select)
        {
            if (_net != null)
            {
                if (select)
                {
                    _net.ReturnToSelect();
                    _net.EndFight();
                    _net.TimeoutSeconds = 20f;
                }
                else _net.Stop();
            }
            _gs.Audio.PauseAll(false);
            _gs.Audio.StopAllSfx();
            _gs.Audio.SetAmbience(false);
            _session.ReturnToSelect = select;
            SceneManager.LoadScene(MainMenuController.SceneName);
        }

        void SnapViews()
        {
            for (int i = 0; i < 2; i++) _views[i].SnapTo(Sim[i]);
        }

        // ---------------------------------------------------------------- PAUSA E DESCONEXÃO

        void Pause(int owner)
        {
            if (_paused || _flow != Flow.Match) return;
            _paused = true;
            _pauseOwner = owner;
            if (_net == null) _gs.Audio.PauseAll(true);
            _gs.Audio.PlayUi(UiSound.Confirm);
            _pause.Title.text = _net != null ? "MENU" : _training ? "TREINO" : "PAUSA";
            _pause.Body.text = _net != null ? "A luta não para em rede: seu lutador fica parado." : _human[owner] ? $"Jogador {owner + 1}" : "";
            _pause.Open();
        }

        void Resume()
        {
            if (_gs.Input.AnyLostOrAwaiting(out _)) return;
            _pause.Close();
            _paused = false;
            _gs.Audio.PauseAll(false);
            if (_net == null) _clock.Reset();
            // Não injeta comandos: botões segurados durante a pausa não contam como novos.
            for (int i = 0; i < 2; i++)
            {
                if (!_human[i]) continue;
                var slot = _gs.Input.Slots[i];
                if (slot.Ready)
                {
                    slot.Reader.SampleCombat(out _, out _, out var held, out _);
                    _acc[i].ResumeWithHeld(held);
                }
                else _acc[i].Reset();
            }
            if (_net == null) Sim.ClearInputBuffers();
        }

        void OnDeviceLost(int slot)
        {
            if (!_human[slot]) return;
            if (_flow == Flow.Match && !_paused) Pause(slot);
            _pause.Close();
            _options.Close();
            _moves.Close();
            _disconnect.ShowLost(slot);
        }

        void OnDeviceRegained(int slot)
        {
            if (!_human[slot]) return;
            _disconnect.ShowRegained(slot, _gs.Input.Slots[slot].Device);
        }

        void HandleDisconnectOverlay(float dt)
        {
            if (!_gs.Input.AnyLostOrAwaiting(out int slot))
            {
                if (_disconnect.IsOpen) { _disconnect.Hide(); if (_paused) _pause.Open(); }
                return;
            }
            var p = _gs.Input.Slots[slot];
            if (!_disconnect.IsOpen)
            {
                if (p.Lost) _disconnect.ShowLost(slot);
                else _disconnect.ShowRegained(slot, p.Device);
            }
            if (p.AwaitingConfirm && p.Ready && _gs.Input.ReadMenu(slot, dt).Confirm)
            {
                p.AwaitingConfirm = false;
                _disconnect.Hide();
                if (_flow == Flow.Match)
                {
                    if (!_paused) Pause(slot);
                    else _pause.Open();
                }
            }
        }

        MenuInput PauseMenuInput(float dt)
        {
            var slot = _gs.Input.Slots[_pauseOwner];
            return slot.Ready && _human[_pauseOwner] ? _gs.Input.ReadMenu(_pauseOwner, dt) : _gs.Input.ReadMenu(0, dt);
        }

        // ---------------------------------------------------------------- LAÇO

        void Update() => Frame(Mathf.Min(Time.unscaledDeltaTime, 0.1f));

        /// <summary>Desenvolvimento/testes: avança quadros manualmente (o editor sem foco não executa o player loop).</summary>
        public void DevStep(int frames, float dt = 1f / 60f)
        {
            for (int i = 0; i < frames; i++) Frame(dt);
        }

        void OnApplicationFocus(bool focus)
        {
            // Ao perder o foco da janela durante a luta, pausa (sem simular nada em segundo plano).
            if (!focus && !Application.isEditor && _net == null && _flow == Flow.Match && !_paused)
            {
                int owner = _human[0] ? 0 : (_human[1] ? 1 : -1);
                if (owner >= 0) Pause(owner);
            }
        }

        void Frame(float dt)
        {
            if (_net != null && TickNetStatus(dt))
            {
                RenderFrame(dt);
                return;
            }
            bool live = true;
            bool blocked = _gs.Input.AnyLostOrAwaiting(out int lostSlot) && _human[lostSlot];
            if (blocked || _disconnect.IsOpen)
            {
                live = false;
                if (_flow == Flow.Match && !_paused) Pause(lostSlot >= 0 ? lostSlot : 0);
                // O menu de pausa só aparece depois que o jogador afetado confirma.
                if (_disconnect.IsOpen && _pause.IsOpen) _pause.Close();
                HandleDisconnectOverlay(dt);
            }
            else if (_paused)
            {
                live = false;
                TickPauseMenus(dt);
            }
            else
            {
                switch (_flow)
                {
                    case Flow.NetWait: TickNetWait(); break;
                    case Flow.Versus: TickVersus(dt); break;
                    case Flow.Intro: TickIntro(dt); break;
                    case Flow.Match: TickMatch(dt); break;
                    case Flow.Results: TickResults(dt); break;
                }
            }
            // Em rede a luta não pausa: quem está em um menu só fica sem comandos.
            if (!live && _net != null && _flow == Flow.Match) TickMatch(dt, false);
            RenderFrame(dt);
        }

        void TickResults(float dt)
        {
            if (_netGuest != null && _netGuest.HasSnapshot && _netGuest.NewestEpoch != _netEpoch)
            {
                Rematch();
                return;
            }
            _results.List.Handle(_gs.Input.ReadMenu(0, dt));
            if (NetTestRun.Active) NetTestResults(dt);
        }

        /// <summary>Retorna verdadeiro quando a conexão acabou e só resta o aviso na tela.</summary>
        bool TickNetStatus(float dt)
        {
            if (NetTestRun.Active) NetTestFight();
            if (_netDead)
            {
                _netLost.List.Handle(_gs.Input.ReadMenu(0, dt));
                return true;
            }
            if (_net.Status != NetStatus.Connected)
            {
                _netDead = true;
                _paused = false;
                _pause.Close();
                _options.Close();
                _moves.Close();
                _confirm.Close();
                _results.Close();
                _disconnect.Hide();
                if (_netWaitLabel != null) _netWaitLabel.gameObject.SetActive(false);
                _netLost.Body.text = _net.FailReason;
                _netLost.Open();
                _gs.Audio.PauseAll(false);
                _gs.Audio.PlayUi(UiSound.Back);
                NetTestRun.Finish("conexão encerrada: " + _net.FailReason + " | " + NetReport());
                return true;
            }
            if (_netGuest != null && _net.Lobby.Phase == NetLobbyPhase.Select)
            {
                LeaveTo(true);
                return true;
            }
            _netInfoTimer -= dt;
            if (_netInfoTimer <= 0f)
            {
                _netInfoTimer = 0.5f;
                _netInfo.text = _net.RttMs > 0f ? $"Rede: {Mathf.RoundToInt(_net.RttMs)} ms" : "";
            }
            return false;
        }

        void TickPauseMenus(float dt)
        {
            var m = PauseMenuInput(dt);
            if (_options.IsOpen) { _options.Tick(m, dt); return; }
            if (_moves.IsOpen)
            {
                var r = _gs.Input.Slots[_pauseOwner].Reader;
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    var k = UnityEngine.InputSystem.Keyboard.current;
                    if (k.pageDownKey.isPressed) _moves.Scroll(900 * dt);
                    if (k.pageUpKey.isPressed) _moves.Scroll(-900 * dt);
                }
                if (r != null && r.Device is UnityEngine.InputSystem.Gamepad g)
                {
                    if (g.rightShoulder.isPressed) _moves.Scroll(900 * dt);
                    if (g.leftShoulder.isPressed) _moves.Scroll(-900 * dt);
                }
                _moves.Tick(m, dt);
                return;
            }
            if (_confirm.IsOpen) { _confirm.List.Handle(m); return; }
            // Start/Esc de novo retoma.
            var owner = _gs.Input.Slots[_pauseOwner];
            if (owner.Ready && owner.Reader.PausePressed()) { Resume(); return; }
            if (_pause.IsOpen) _pause.List.Handle(m);
        }

        bool AnyConfirm(float dt)
        {
            var m = _gs.Input.ReadAnyMenu(dt);
            return m.Confirm || m.Start;
        }

        void TickVersus(float dt)
        {
            _flowTimer += dt;
            _versus.Tick(dt);
            if (_netGuest != null && _netGuest.HasSnapshot) { EnterMatch(); return; }
            if (_flowTimer > 2.8f || (_net == null && _flowTimer > 0.3f && AnyConfirm(dt))) EnterIntro();
        }

        void TickIntro(float dt)
        {
            _flowTimer += dt;
            if (_netGuest != null && _netGuest.HasSnapshot) { _hud.ClearTransient(); EnterMatch(); return; }
            bool skip = _net == null && _flowTimer > 0.25f && AnyConfirm(dt);
            if (skip) { _hud.ClearTransient(); EnterMatch(); return; }
            if (_flowTimer > 1.35f)
            {
                _flowTimer = 0f;
                _introStep++;
                if (_introStep >= 2) { EnterMatch(); return; }
                ShowIntroName(_introStep);
            }
            Camera.Focus(_views[_introStep].WorldCenter, 0.8f);
        }

        /// <param name="live">Falso quando este jogador está em um menu: a luta continua, sem os comandos dele.</param>
        void TickMatch(float dt, bool live = true)
        {
            if (_netGuest != null)
            {
                TickGuest(dt, live);
                return;
            }

            // Captura por quadro de renderização.
            for (int i = 0; i < 2; i++)
            {
                if (!_human[i]) continue;
                var slot = _gs.Input.Slots[i];
                if (!slot.Ready) continue;
                if (!live)
                {
                    _acc[i].Reset();
                    continue;
                }
                slot.Reader.SampleCombat(out int x, out int y, out var held, out var pressed);
                _acc[i].Sample(x, y, held, pressed);
                if (slot.Reader.PausePressed())
                {
                    Pause(i);
                    if (_net == null) return;
                    _acc[i].Reset();
                    continue;
                }
                if (_training && i == 0 && slot.Reader.ResetPressed()) TrainingReset();
            }

            if (_net == null && _match.Phase == MatchPhase.PreRound && !_training && AnyConfirm(dt)) _match.SkipPreRound();

            int steps = _clock.Advance(dt);
            for (int s = 0; s < steps; s++)
            {
                _netHost?.BeginTick();
                var f0 = FrameFor(0);
                var f1 = FrameFor(1);
                _match.Tick(f0, f1);
                for (int i = 0; i < 2; i++) _views[i].PushTick(Sim[i]);
                if (_training) TrainingTick(f0);
                ProcessEvents();
                if (_net != null) _net.SendSnapshot(_match);
                if (NetTestRun.Active && _netHost != null) NetTestRun.Hash(_netHost.NetTick, Sim.StateHash());
            }

            TickResultsDelay(dt);
        }

        /// <summary>Convidado: envia os comandos e exibe o estado recebido, no próprio ritmo de 60 ticks/s.</summary>
        void TickGuest(float dt, bool live)
        {
            var slot = _gs.Input.Slots[0];
            if (_human[0] && slot.Ready)
            {
                if (live)
                {
                    slot.Reader.SampleCombat(out int x, out int y, out var held, out var pressed);
                    _acc[0].Sample(x, y, held, pressed);
                    if (slot.Reader.PausePressed())
                    {
                        Pause(0);
                        _acc[0].Reset();
                    }
                }
                else _acc[0].Reset();
            }

            int steps = _clock.Advance(dt);
            for (int s = 0; s < steps; s++)
            {
                bool allow = _match.Phase == MatchPhase.Fighting;
                _net.SendInput(_cpu[1] != null ? _cpu[1].Tick(Sim, 1, allow) : _acc[0].ConsumeTick());
                if (_netGuest.HasSnapshot && _netGuest.NewestEpoch != _netEpoch) Rematch();
                bool applied = _netGuest.Step(_match);
                for (int i = 0; i < 2; i++) _views[i].PushTick(Sim[i]);
                ProcessEvents();
                if (applied && NetTestRun.Active) NetTestRun.Hash(_netGuest.LastAppliedTick, Sim.StateHash());
            }

            TickResultsDelay(dt);
        }

        void TickResultsDelay(float dt)
        {
            if (_resultsDelay >= 0f)
            {
                _resultsDelay -= dt;
                if (_resultsDelay < 0f)
                {
                    if (_session.AutoPlay) Rematch(); // demonstração/benchmark: luta contínua
                    else ShowResults();
                }
            }
        }

        InputFrame FrameFor(int i)
        {
            if (_netHost != null && i == 1) return _netHost.NextRemoteInput();
            if (_human[i])
            {
                var f = _acc[i].ConsumeTick();
                if (i == 0 && _demo != null) return DemoFrame(f);
                return f;
            }
            bool allow = _match.Phase == MatchPhase.Fighting;
            if (_training && i == 1)
            {
                // Durante a demonstração o boneco fica parado para o combo poder ser visto.
                if (_demo != null) return InputFrame.Neutral;
                switch (_session.Dummy)
                {
                    case DummyMode.Stand: return InputFrame.Neutral;
                    case DummyMode.Crouch: return new InputFrame(0, -1, Buttons.None, Buttons.None);
                    case DummyMode.Guard:
                    {
                        // Ajuda de treino: o boneco escolhe a altura correta da defesa.
                        var atk = Sim[0];
                        bool low = atk.State == FighterStateId.Attack && atk.Move != null && atk.Move.Level == AttackLevel.Low;
                        return new InputFrame(0, low ? -1 : 0, Buttons.Block, Buttons.None);
                    }
                }
            }
            return _cpu[i] != null ? _cpu[i].Tick(Sim, i, allow) : InputFrame.Neutral;
        }

        // ---------------------------------------------------------------- TREINO

        void TrainingReset()
        {
            _match.TrainingReset();
            ApplyTrainingFlags();
            SnapViews();
            _vfx.ClearAll();
            _gs.Audio.StopAllSfx();
            _clock.Reset();
            foreach (var c in _cpu) c?.Reset();
            _acc[0].Discard();
            _koTimer = 0;
        }

        void TrainingTick(in InputFrame p1)
        {
            if (_session.TrainingInfiniteEnergy)
            {
                Sim.SetEnergy(0, Sim.Config.MaxEnergy);
                Sim.SetEnergy(1, Sim.Config.MaxEnergy);
            }
            bool busy = Sim[0].InComboState || Sim[1].InComboState || Sim[0].IsDown || Sim[1].IsDown;
            _trainingIdle = busy ? 0 : _trainingIdle + 1;
            if (_session.TrainingInfiniteHealth && _trainingIdle == 40)
            {
                Sim.RefillHealth(0);
                Sim.RefillHealth(1);
            }
            if (Sim[0].State == FighterStateId.KO || Sim[1].State == FighterStateId.KO)
            {
                if (++_koTimer > 120) TrainingReset();
            }
            RecordHistory(p1);
        }

        static string Arrow(int x, int y)
        {
            if (x == 0 && y == 0) return "•";
            if (y > 0) return x < 0 ? "↑←" : x > 0 ? "↑→" : "↑";
            if (y < 0) return x < 0 ? "↓←" : x > 0 ? "↓→" : "↓";
            return x < 0 ? "←" : "→";
        }

        void RecordHistory(in InputFrame f)
        {
            if (!_gs.Settings.ShowInputHistory) { _hud.SetHistory(0, ""); return; }
            // Direção exibida relativa à orientação (→ = frente).
            int relX = f.X * Sim[0].Facing;
            bool changed = f.Pressed != Buttons.None || f.X != _lastHistoryFrame.X || f.Y != _lastHistoryFrame.Y;
            _lastHistoryFrame = f;
            if (!changed) return;
            var sb = new StringBuilder();
            sb.Append(Arrow(relX, f.Y));
            if (f.Pressed != Buttons.None)
            {
                foreach (Buttons b in System.Enum.GetValues(typeof(Buttons)))
                {
                    if (b == Buttons.None || (f.Pressed & b) == 0) continue;
                    sb.Append(" + ").Append(ButtonLabel(b));
                }
            }
            _history.Add(sb.ToString());
            if (_history.Count > 14) _history.RemoveAt(0);
            _hud.SetHistory(0, "<b>HISTÓRICO</b>\n" + string.Join("\n", _history));
        }

        static string ButtonLabel(Buttons b)
        {
            switch (b)
            {
                case Buttons.Punch: return "Soco";
                case Buttons.Kick: return "Chute";
                case Buttons.Heavy: return "Forte";
                case Buttons.Block: return "Defesa";
                case Buttons.Special: return "Especial";
                case Buttons.Grab: return "Agarrão";
                case Buttons.Signature: return "Assinatura";
                default: return b.ToString();
            }
        }

        void StartDemo(ComboRecipe combo)
        {
            _moves.Close();
            Resume();
            TrainingReset();
            float gap = Sim[0].Def.PushHalfWidth + Sim[1].Def.PushHalfWidth + 0.25f;
            Sim[0].Pos = new Vector2(-gap * 0.5f, 0);
            Sim[1].Pos = new Vector2(gap * 0.5f, 0);
            SnapViews();
            _demo = combo.Sequence;
            _demoIndex = 0;
            _demoTimeout = 240;
            _hud.ShowSub("DEMONSTRAÇÃO: " + combo.Name + " — " + combo.Notation, 4f);
        }

        InputFrame DemoFrame(in InputFrame human)
        {
            var a = Sim[0];
            if (--_demoTimeout <= 0 || human.Pressed != Buttons.None) { _demo = null; return human; }
            bool ready = _demoIndex == 0 ? a.IsActionable
                : a.State == FighterStateId.Attack && a.Move != null && a.Move.Slot == _demo[_demoIndex - 1] && a.MoveContact;
            if (_demoIndex >= _demo.Length)
            {
                if (!a.InComboState && a.State != FighterStateId.Attack) _demo = null;
                return InputFrame.Neutral;
            }
            if (!ready) return InputFrame.Neutral;
            var slot = _demo[_demoIndex++];
            int fwd = a.Facing;
            switch (slot)
            {
                case MoveSlot.Punch: return new InputFrame(0, 0, Buttons.Punch, Buttons.Punch);
                case MoveSlot.Kick: return new InputFrame(0, 0, Buttons.Kick, Buttons.Kick);
                case MoveSlot.Heavy: return new InputFrame(0, 0, Buttons.Heavy, Buttons.Heavy);
                case MoveSlot.ForwardHeavy: return new InputFrame(fwd, 0, Buttons.Heavy, Buttons.Heavy);
                case MoveSlot.CrouchPunch: return new InputFrame(0, -1, Buttons.Punch, Buttons.Punch);
                case MoveSlot.Low: return new InputFrame(0, -1, Buttons.Kick, Buttons.Kick);
                case MoveSlot.AntiAir: return new InputFrame(0, -1, Buttons.Heavy, Buttons.Heavy);
                case MoveSlot.SpecialNeutral: return new InputFrame(0, 0, Buttons.Special, Buttons.Special);
                case MoveSlot.SpecialForward: return new InputFrame(fwd, 0, Buttons.Special, Buttons.Special);
                case MoveSlot.Signature: return new InputFrame(0, 0, Buttons.Signature, Buttons.Signature);
                default: return InputFrame.Neutral;
            }
        }

        // ---------------------------------------------------------------- EVENTOS → APRESENTAÇÃO

        /// <summary>Cada lutador tem a própria cor de impacto nos golpes especiais.</summary>
        static VfxKind SpecialVfx(FighterDefinition def)
        {
            switch (def.Id)
            {
                case "giovani": return VfxKind.Flames;
                case "faisca": return VfxKind.Lightning;
                case "vertice": return VfxKind.Tornado;
                default: return VfxKind.Special;
            }
        }

        static float LevelHeight(AttackLevel l) => l == AttackLevel.Low ? 0.35f : l == AttackLevel.High ? 1.45f : l == AttackLevel.Overhead ? 1.3f : 1.05f;

        void ProcessEvents()
        {
            var audio = _gs.Audio;
            _netHost?.CollectEvents(_match);
            foreach (var e in Sim.Events)
            {
                switch (e.Type)
                {
                    case CombatEventType.Hit:
                    {
                        var atk = Sim[e.Fighter];
                        var pos = new Vector3(e.Position.x - atk.MoveFacing * 0.22f, e.Position.y + LevelHeight(e.Level), -0.3f);
                        _vfx.Play(e.Heavy ? VfxKind.HeavyHit : VfxKind.Hit, pos, atk.MoveFacing);
                        audio.PlaySfx(e.Sfx == SfxKind.Signature ? SfxKind.HeavyHit : e.Sfx, 1f, e.Heavy ? 0.95f : 1.05f);
                        if (atk.Move != null && atk.Move.Slot == MoveSlot.Signature) audio.PlaySfx(SfxKind.SignatureImpact, 0.8f);
                        else if (atk.Move != null && atk.Move.IsSpecial)
                        {
                            audio.PlaySfx(SfxKind.SpecialImpact, 0.7f);
                            _vfx.Play(SpecialVfx(atk.Def), pos, atk.MoveFacing);
                        }
                        _views[e.Target].NotifyHit(e.Level);
                        _views[e.Target].Flash(Color.white * 0.8f, 0.09f);
                        _views[e.Target].Shake(e.Heavy ? 0.08f : 0.05f);
                        Camera.AddShake(e.Heavy ? 0.12f : 0.04f);
                        Crowd?.Excite(e.Heavy ? 0.5f : 0.2f);
                        break;
                    }
                    case CombatEventType.Block:
                    {
                        var atk = Sim[e.Fighter];
                        var pos = new Vector3(e.Position.x - atk.MoveFacing * 0.3f, e.Position.y + LevelHeight(e.Level), -0.3f);
                        _vfx.Play(VfxKind.Block, pos, atk.MoveFacing);
                        audio.PlaySfx(e.Heavy ? SfxKind.HeavyBlock : SfxKind.Block);
                        _views[e.Target].NotifyBlock(e.Level);
                        _views[e.Target].Flash(new Color(0.2f, 0.45f, 1f), 0.07f);
                        break;
                    }
                    case CombatEventType.GrabStart:
                        audio.PlaySfx(SfxKind.Grab);
                        break;
                    case CombatEventType.ThrowHit:
                        audio.PlaySfx(SfxKind.ThrowImpact);
                        audio.PlayThud();
                        Camera.AddShake(0.14f);
                        Crowd?.Excite(0.6f);
                        _views[e.Target].Flash(Color.white * 0.8f, 0.1f);
                        break;
                    case CombatEventType.ThrowTech:
                        audio.PlaySfx(SfxKind.Tech);
                        _vfx.Play(VfxKind.Tech, new Vector3((Sim[0].Pos.x + Sim[1].Pos.x) * 0.5f, 1.2f, -0.3f));
                        _hud.ShowNotice(e.Target >= 0 ? e.Target : 1, "ESCAPOU!");
                        break;
                    case CombatEventType.Knockdown:
                        _vfx.Play(VfxKind.Dust, new Vector3(e.Position.x, 0.05f, 0));
                        audio.PlayThud(0.8f);
                        Camera.AddShake(0.06f);
                        break;
                    case CombatEventType.KO:
                        audio.PlaySfx(SfxKind.KO);
                        audio.PlaySfx(SfxKind.Finisher);
                        _vfx.Play(VfxKind.KO, new Vector3(e.Position.x, e.Position.y + 1.1f, -0.3f));
                        audio.Duck(0.25f);
                        Camera.AddShake(0.25f);
                        Camera.ZoomPunch(1.2f);
                        Crowd?.Excite(1f);
                        audio.PlayCheer(1f);
                        _views[e.Target].Flash(new Color(1f, 0.6f, 0.3f), 0.25f);
                        break;
                    case CombatEventType.RopeBounce:
                        audio.PlaySfx(SfxKind.Bounce);
                        Ropes?.Bounce(e.Position.x < 0 ? -1 : 1);
                        _hud.ShowNotice(1 - e.Target, "REBOTE NAS CORDAS!");
                        Camera.AddShake(0.08f);
                        break;
                    case CombatEventType.Land:
                        audio.PlaySfx(SfxKind.Land, 0.5f);
                        break;
                    case CombatEventType.Jump:
                        audio.PlaySfx(SfxKind.Jump, 0.4f);
                        break;
                    case CombatEventType.Dash:
                        audio.PlaySfx(SfxKind.Dash, 0.5f);
                        _vfx.Play(VfxKind.Dust, new Vector3(e.Position.x, 0.05f, 0));
                        break;
                    case CombatEventType.MoveStart:
                    {
                        var f = Sim[e.Fighter];
                        var m = f.Move;
                        if (m != null && (m.IsSpecial || m.Damage >= 80) && m.Slot != MoveSlot.Signature && !m.IsThrow) audio.PlaySfx(SfxKind.Special, 0.6f);
                        else if (m != null && !m.IsThrow) audio.PlaySfx(SfxKind.Whoosh, 0.45f);
                        break;
                    }
                    case CombatEventType.SignatureFlash:
                    {
                        var f = Sim[e.Fighter];
                        audio.PlaySfx(SfxKind.Signature);
                        audio.Duck(0.4f);
                        Camera.Focus(_views[e.Fighter].WorldCenter, 0.6f);
                        _vfx.Play(VfxKind.Signature, _views[e.Fighter].WorldCenter + new Vector3(0, 0, -0.4f));
                        _vfx.Play(VfxKind.Implosion, _views[e.Fighter].WorldCenter + new Vector3(0, 0, -0.4f));
                        _views[e.Fighter].Flash(new Color(1f, 0.85f, 0.3f), 0.4f);
                        _hud.ShowSub(f.Def.Signature.DisplayName.ToUpperInvariant() + "!", 1.0f);
                        Crowd?.Excite(0.7f);
                        break;
                    }
                    case CombatEventType.SignatureDenied:
                        _hud.ShowNotice(e.Fighter, "ENERGIA INSUFICIENTE");
                        break;
                    case CombatEventType.ComboEnd:
                        _hud.ShowCombo(e.Fighter, e.ComboHits, e.Damage);
                        break;
                }
            }
            Sim.Events.Clear();

            foreach (var me in _match.Events)
            {
                switch (me.Type)
                {
                    case MatchEventType.RoundAnnounce:
                    {
                        bool final = _match.Wins[0] == Sim.Config.RoundsToWin - 1 && _match.Wins[1] == Sim.Config.RoundsToWin - 1;
                        _hud.ShowBanner(final ? "ROUND FINAL" : "ROUND " + me.Round, 1.6f, Color.white);
                        audio.PlayAnnouncer(AnnouncerLine.Round);
                        _hud.SetWins(_match.Wins);
                        SnapViews();
                        ArenaScreen?.Show(final ? "ROUND FINAL" : "ROUND " + me.Round, $"{_session.Fighters[0].DisplayName} {_match.Wins[0]} × {_match.Wins[1]} {_session.Fighters[1].DisplayName}");
                        break;
                    }
                    case MatchEventType.Fight:
                        _hud.ShowBanner("LUTEM!", 0.9f, UITheme.Accent);
                        audio.PlayAnnouncer(AnnouncerLine.Fight);
                        audio.PlayCheer(0.5f);
                        break;
                    case MatchEventType.KO:
                        _hud.ShowBanner("K.O.!", 2.2f, new Color(1f, 0.3f, 0.2f));
                        audio.PlayAnnouncer(AnnouncerLine.KO);
                        break;
                    case MatchEventType.DoubleKO:
                        _hud.ShowBanner("DUPLO K.O.!", 2.2f, new Color(1f, 0.3f, 0.2f));
                        audio.PlayAnnouncer(AnnouncerLine.KO);
                        break;
                    case MatchEventType.TimeUp:
                        _hud.ShowBanner("TEMPO!", 1.8f, Color.white);
                        audio.PlayAnnouncer(AnnouncerLine.TimeUp);
                        break;
                    case MatchEventType.RoundWon:
                        _hud.ShowSub($"{_session.Fighters[me.Winner].DisplayName.ToUpperInvariant()} VENCE O ROUND", 2.2f);
                        _hud.SetWins(_match.Wins);
                        break;
                    case MatchEventType.RoundDraw:
                        _hud.ShowSub("EMPATE — O ROUND SERÁ REPETIDO", 2.4f);
                        audio.PlayAnnouncer(AnnouncerLine.Draw);
                        break;
                    case MatchEventType.MatchWon:
                        _hud.ShowBanner($"{_session.Fighters[me.Winner].DisplayName.ToUpperInvariant()} VENCE!", 2.4f, UITheme.PlayerColor(me.Winner));
                        audio.PlayAnnouncer(AnnouncerLine.Victory);
                        audio.PlayCheer(1f);
                        Crowd?.Excite(1f);
                        ArenaScreen?.Show("VITÓRIA", _session.Fighters[me.Winner].DisplayName.ToUpperInvariant());
                        _vfx.Play(VfxKind.Heal, _views[me.Winner].transform.position);
                        _resultsDelay = 2.2f;
                        break;
                }
            }
            _match.Events.Clear();
        }

        void ShowResults()
        {
            _flow = Flow.Results;
            int w = _match.MatchWinner;
            string who = w < 0 ? "EMPATE" : _session.Fighters[w].DisplayName.ToUpperInvariant() + " VENCE";
            _results.Title.text = who;
            string tag = w >= 0 ? Tag(w) : "";
            _results.Body.text = $"{tag}  •  Placar {_match.Wins[0]} × {_match.Wins[1]}";
            if (_netGuest != null) _results.Body.text += "\nAguardando o anfitrião escolher a próxima luta";
            _results.Open();
            NetTestRun.Note("resultado " + _match.Wins[0] + "x" + _match.Wins[1] + (_net != null ? " | " + NetReport() : ""));
        }

        // ---------------------------------------------------------------- RENDER

        void RenderFrame(float dt)
        {
            bool inMatch = _flow == Flow.Match || _flow == Flow.Results;
            float alpha = _clock.Alpha;
            bool stopped = (_paused && _net == null) || _netDead;
            bool frozen = stopped || Sim.Hitstop > 0 || Sim.FreezeTicks > 0;
            for (int i = 0; i < 2; i++)
            {
                if (inMatch) _views[i].Render(Sim[i], alpha, frozen, stopped ? 0f : dt);
                else
                {
                    _views[i].transform.position = new Vector3(Sim[i].Pos.x, 0, 0);
                    _views[i].SetFacing(Sim[i].Facing);
                    _views[i].RenderPreview(_flow == Flow.Intro && _introStep == i ? PreviewMode.Intro : PreviewMode.Guard, dt);
                }
            }
            Vector2 a = _views[0].transform.position;
            Vector2 b = _views[1].transform.position;
            if (!stopped) Camera.Tick(a, b, dt);

            if (_flow == Flow.Match && !_training)
                _hud.SetTimer(_match.TimerSeconds.ToString());
            _hud.UpdateBars(Sim, Sim.Config.MaxEnergy, dt);

            if (_training && _session.TrainingShowBoxes)
            {
                _overlay.Draw(Sim);
                _hud.SetDevInfo(DevInfo());
            }
        }

        // ---------------------------------------------------------------- ROTEIRO DE TESTE EM REDE

        float _netTestResultsTimer;
        static int _netTestMatches;

        void NetTestFight()
        {
            if (NetTestRun.TimeIsUp) NetTestRun.Finish(NetReport());
        }

        /// <summary>O anfitrião alterna revanche e volta à seleção, para o teste passar pelos dois caminhos.</summary>
        void NetTestResults(float dt)
        {
            _netTestResultsTimer += dt;
            if (_netHost == null || _netTestResultsTimer < 1.5f) return;
            _netTestResultsTimer = 0f;
            NetTestRun.Note(NetReport());
            if (++_netTestMatches % 2 == 1) Rematch();
            else LeaveTo(true);
        }

        string NetReport()
        {
            var sb = new StringBuilder();
            sb.Append($"rtt={_net.RttMs:F1}ms max={_net.RttMaxMs:F1}ms");
            if (_netHost != null) sb.Append($" tick={_netHost.NetTick} comandos_faltantes={_netHost.MissingInputs} comandos_unidos={_netHost.MergedInputs} eventos_pendentes={_netHost.PendingEvents}");
            if (_netGuest != null) sb.Append($" tick={_netGuest.LastAppliedTick} sem_estado={_netGuest.Holds} pulados={_netGuest.Skipped} perdidos={_netGuest.LostSnapshots} fila_max={_netGuest.MaxBufferSeen} fila_alvo={_netGuest.BufferTarget}");
            return sb.ToString();
        }

        string DevInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>Tick {Sim.Tick}</b>  hitstop {Sim.Hitstop}");
            for (int i = 0; i < 2; i++)
            {
                var f = Sim[i];
                sb.Append($"<color=#{(i == 0 ? "80b0ff" : "ff9090")}>J{i + 1}</color> {f.State} t{f.StateTick}");
                if (f.State == FighterStateId.Attack && f.Move != null)
                {
                    var m = f.Move;
                    string phase = f.MoveTick < m.Startup ? "preparação" : f.MoveTick < m.Startup + m.Active ? "ATIVO" : "recuperação";
                    sb.Append($"  {m.DisplayName} [{m.Startup}/{m.Active}/{m.Recovery}] {phase}");
                }
                sb.AppendLine();
                sb.AppendLine($"   vida {f.Health}  energia {f.Energy}  combo {f.ComboHits}  x {f.Pos.x:F2}");
            }
            sb.AppendLine("<size=80%>amarelo: pushbox • verde: hurtbox • vermelho: hitbox ativo • magenta: agarrão</size>");
            return sb.ToString();
        }
    }
}
