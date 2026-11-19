using System.Net;
using TMPro;
using UltimoRound.Audio;
using UltimoRound.Combat;
using UltimoRound.Input;
using UltimoRound.Net;
using UltimoRound.Presentation;
using UltimoRound.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UltimoRound.Core
{
    /// <summary>
    /// Fluxo do menu: abertura → menu principal → modo → associação de controles → (dificuldade) → seleção → luta.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string SceneName = "MainMenu";

        public GameDatabase Database;
        public Camera MenuCamera;
        public Transform[] PreviewAnchors = new Transform[2];

        enum Screen { Title, Main, Join, Difficulty, Select, Options, Credits, NetMenu, NetWait, NetBrowse, NetAddress }

        GameServices _gs;
        Session _session;
        Screen _screen;
        float _t;
        Canvas _canvas;

        RectTransform _title, _join, _select, _credits;
        TextMeshProUGUI _titleLogo, _titleSub, _titlePress;
        ListPanel _main, _difficulty;
        ListPanel _netMenu, _netWait, _netBrowse, _netAddress;
        string _netBrowseKey = "";
        string _netAddressText = "";
        string _netMessage = "";
        bool _addressEnterHeld, _addressEscapeHeld, _addressBackspaceHeld;
        OptionsPanel _options;
        TextMeshProUGUI[] _joinSlots = new TextMeshProUGUI[2];
        TextMeshProUGUI _joinHint;
        float _joinDoneTimer = -1f;

        // Seleção
        readonly int[] _cursor = { 0, 1 };
        readonly bool[] _ready = new bool[2];
        readonly int[] _palette = { 0, 0 };
        int _activeCursor; // modos de 1 jogador: 0 = escolhendo o próprio, 1 = adversário
        RectTransform[] _cards;
        Image[][] _cardFrames;
        TextMeshProUGUI[] _info = new TextMeshProUGUI[2];
        TextMeshProUGUI[] _status = new TextMeshProUGUI[2];
        RectTransform[][] _statBars = new RectTransform[2][];
        TextMeshProUGUI _selectHint;
        readonly FighterView[] _previews = new FighterView[2];
        readonly FighterDefinition[] _previewDef = new FighterDefinition[2];
        readonly int[] _previewPal = { -1, -1 };
        float _launchTimer = -1f;

        public bool IsOnTitle => _screen == Screen.Title;

        bool SinglePlayer => _session.Mode == GameMode.VsCpu || _session.Mode == GameMode.Training;
        bool IsNet => _session.Mode == GameMode.Network;
        bool IsNetGuest => IsNet && _gs.Net.Role == NetRole.Guest;
        int PlayersNeeded => _session.Mode == GameMode.Versus ? 2 : 1;

        void Start()
        {
            _gs = GameServices.Ensure(Database);
            _session = _gs.Session;
            UIFactory.EnsureEventSystem();
            _gs.Audio.PlayMusic(MusicTrack.Menu);
            _gs.Audio.SetAmbience(false);
            if (MenuCamera != null) _gs.ApplyCameraQuality(MenuCamera);
            BuildUI();

            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Previa" + (i + 1));
                go.transform.SetParent(PreviewAnchors[i] != null ? PreviewAnchors[i] : transform, false);
                _previews[i] = go.AddComponent<FighterView>();
            }

            if (BenchmarkRecorder.Requested)
            {
                // Medição no executável: luta CPU x CPU sem passar pelos menus.
                _session.Mode = GameMode.VsCpu;
                _session.AutoPlay = true;
                _session.Difficulty = CpuDifficulty.Hard;
                var list = _gs.Database.Fighters;
                _session.Fighters[0] = list[0];
                _session.Fighters[1] = list.Count > 1 ? list[1] : list[0];
                SceneManager.LoadScene(FightSceneController.SceneName);
                return;
            }

            if (_session.ReturnToSelect && _gs.Input.Slots[0].Assigned)
            {
                _session.ReturnToSelect = false;
                OpenSelect();
            }
            else if (NetTestRun.Active) StartNetTest();
            else
            {
                _gs.Input.ClearAll();
                Show(Screen.Title);
            }
        }

        // ---------------------------------------------------------------- CONSTRUÇÃO

        void BuildUI()
        {
            _canvas = UIFactory.Canvas("Menu", 20, transform);
            var root = _canvas.transform;

            // Abertura
            _title = UIFactory.Rect("Abertura", root);
            UIFactory.Stretch(_title);
            var tbg = UIFactory.Image("Fundo", _title, new Color(0, 0, 0, 0.55f));
            UIFactory.Stretch(tbg.rectTransform);
            _titleLogo = UIFactory.Label("Logo", _title, "ÚLTIMO ROUND", 170, UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            UIFactory.Place(_titleLogo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1800, 220));
            _titleLogo.outlineWidth = 0.2f;
            _titleLogo.outlineColor = new Color32(30, 10, 0, 255);
            _titleSub = UIFactory.Label("Sub", _title, "TORNEIO DOS RIVAIS", 64, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Place(_titleSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1600, 90));
            _titlePress = UIFactory.Label("Press", _title, "Pressione qualquer botão", 36, UITheme.TextDim, TextAlignmentOptions.Center);
            UIFactory.Place(_titlePress.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(1200, 60));

            // Menu principal
            _main = new ListPanel(root, "ÚLTIMO ROUND", 620, 74, 7, 0.45f, -330);
            _main.Body.text = "Torneio dos Rivais";
            _main.List.Audio = _gs.Audio;
            _main.List.Add("Versus Local", () => ChooseMode(GameMode.Versus));
            _main.List.Add("Rede Local", () => ChooseMode(GameMode.Network));
            _main.List.Add("Contra CPU", () => ChooseMode(GameMode.VsCpu));
            _main.List.Add("Treino", () => ChooseMode(GameMode.Training));
            _main.List.Add("Opções", () => Show(Screen.Options));
            _main.List.Add("Créditos", () => Show(Screen.Credits));
            _main.List.Add("Sair", Application.Quit);
            _main.List.OnBack = () => Show(Screen.Title);

            // Associação de controles
            _join = UIFactory.Rect("Associacao", root);
            UIFactory.Stretch(_join);
            var jbg = UIFactory.Image("Fundo", _join, UITheme.Background, true);
            UIFactory.Stretch(jbg.rectTransform);
            var jt = UIFactory.Label("Titulo", _join, "ASSOCIAR CONTROLES", 64, UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Place(jt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(1400, 90));
            for (int i = 0; i < 2; i++)
            {
                var panel = UIFactory.Image("Slot" + i, _join, UITheme.Panel);
                UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -400 : 400, 20), new Vector2(700, 380));
                UIFactory.Outline(panel, UITheme.PlayerColor(i), 4f);
                _joinSlots[i] = UIFactory.Label("Texto", panel.rectTransform, "", 38, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(_joinSlots[i].rectTransform, 30, 30, 30, 30);
            }
            _joinHint = UIFactory.Label("Dica", _join, "", 28, UITheme.TextDim, TextAlignmentOptions.Center);
            UIFactory.Place(_joinHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1700, 120));

            // Dificuldade
            _difficulty = new ListPanel(root, "DIFICULDADE DA CPU", 560, 74, 3, 0.8f);
            _difficulty.List.Audio = _gs.Audio;
            _difficulty.Body.text = "Tempo de reação, chance de erro e repertório mudam com a dificuldade.";
            _difficulty.List.Add("Fácil", () => { _session.Difficulty = CpuDifficulty.Easy; OpenSelect(); });
            _difficulty.List.Add("Normal", () => { _session.Difficulty = CpuDifficulty.Normal; OpenSelect(); });
            _difficulty.List.Add("Difícil", () => { _session.Difficulty = CpuDifficulty.Hard; OpenSelect(); });
            _difficulty.List.OnBack = () => Show(Screen.Join);

            BuildSelect(root);
            BuildNetPanels(root);

            _options = new OptionsPanel(root, _gs);
            _options.OnClose = () => Show(Screen.Main);

            _credits = UIFactory.Rect("Creditos", root);
            UIFactory.Stretch(_credits);
            var cbg = UIFactory.Image("Fundo", _credits, UITheme.Background, true);
            UIFactory.Stretch(cbg.rectTransform);
            var ct = UIFactory.Label("Texto", _credits,
                "<size=150%><color=#ffb030><b>CRÉDITOS</b></color></size>\n\n" +
                "<b>Último Round — Torneio dos Rivais</b>\nJogo de luta original. Lutadores, regras, golpes, arena, interface e músicas criados para este projeto.\n\n" +
                "<b>Desenvolvimento</b>\nGiovani Nogueira Pires — Desenvolvimento de Jogos Digitais\nUnity " + Application.unityVersion + " (URP, Input System, uGUI/TextMeshPro), com apoio de IA (Claude Code).\n\n" +
                "<b>Modelos</b>\nLow Poly Fighter Starter Pack (tt-3D) • Fighter Pack Bundle FREE (Explosive) • Fighting Motions Vol.1 (Magicpot) • Survivalist (Slayver) • Zombie Monster Animations (Kevin Iglesias)\n\n" +
                "<b>Animações</b>\nFighting Motions Vol.1 • Fighter Pack Bundle FREE • Zombie Monster Animations • Starter Assets\n\n" +
                "<b>Cenário</b>\nMilitary Base Pack (Tiny Teacup Studio) • Low Poly Weapons Vol.1 (Simon Mercuzot)\n\n" +
                "<b>Sons e efeitos</b>\nFree Deadly Kombat (Daniel SoundsGood) • Free Quick Effects Vol.1 (Gabriel Aguiar) • música sintetizada por código\n\n" +
                "<b>Fonte</b>\nLiberation Sans — SIL Open Font License.\n\n" +
                "<size=80%><color=#9fb0d0>Voltar: Esc / B</color></size>",
                32, UITheme.Text, TextAlignmentOptions.Center);
            UIFactory.Stretch(ct.rectTransform, 200, 200, 80, 80);
        }

        void BuildSelect(Transform root)
        {
            _select = UIFactory.Rect("Selecao", root);
            UIFactory.Stretch(_select);
            var st = UIFactory.Label("Titulo", _select, "ESCOLHA SEU LUTADOR", 60, UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Place(st.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(1400, 80));
            st.outlineWidth = 0.15f;
            st.outlineColor = new Color32(0, 0, 0, 255);

            var fighters = _gs.Database.Fighters;
            int n = Mathf.Max(4, fighters.Count);
            _cards = new RectTransform[n];
            _cardFrames = new Image[n][];
            float cw = 210, gap = 20;
            float total = n * cw + (n - 1) * gap;
            for (int c = 0; c < n; c++)
            {
                var def = c < fighters.Count ? fighters[c] : null;
                bool available = def != null && def.Selectable;
                var card = UIFactory.Image("Card" + c, _select, available ? UITheme.Panel : new Color(0.05f, 0.05f, 0.08f, 0.9f));
                UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-total / 2 + cw / 2 + c * (cw + gap), 40), new Vector2(cw, 230));
                _cards[c] = card.rectTransform;
                Color col = def != null ? def.GetPalette(0).Primary : Color.gray;
                var swatch = UIFactory.Image("Cor", card.rectTransform, available ? col : new Color(0.2f, 0.2f, 0.22f));
                UIFactory.Place(swatch.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(cw - 24, 130));
                var initial = UIFactory.Label("Inicial", swatch.rectTransform, def != null ? def.DisplayName.Substring(0, 1) : "?", 96, available ? Color.white : UITheme.Disabled, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(initial.rectTransform);
                var name = UIFactory.Label("Nome", card.rectTransform, def != null ? def.DisplayName.ToUpperInvariant() : "???", 30, available ? Color.white : UITheme.Disabled, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(cw, 40));
                var sub = UIFactory.Label("Estado", card.rectTransform, available ? def.Style : "EM BREVE", 18, available ? UITheme.TextDim : UITheme.Accent, TextAlignmentOptions.Center);
                UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(cw - 10, 30));
                _cardFrames[c] = new Image[2];
                for (int p = 0; p < 2; p++)
                {
                    var frame = UIFactory.Image("Cursor" + p, card.rectTransform, UITheme.PlayerColor(p));
                    UIFactory.Place(frame.rectTransform, new Vector2(0.5f, p == 0 ? 1f : 0f), new Vector2(0.5f, p == 0 ? 0f : 1f), new Vector2(0, p == 0 ? 4 : -4), new Vector2(cw + 8, 14));
                    var tag = UIFactory.Label("Tag", frame.rectTransform, p == 0 ? "J1" : "J2", 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    UIFactory.Place(tag.rectTransform, new Vector2(0.5f, p == 0 ? 1f : 0f), new Vector2(0.5f, p == 0 ? 0f : 1f), Vector2.zero, new Vector2(80, 30));
                    _cardFrames[c][p] = frame;
                }
            }

            for (int p = 0; p < 2; p++)
            {
                var panel = UIFactory.Image("Info" + p, _select, UITheme.Panel);
                UIFactory.Place(panel.rectTransform, new Vector2(p, 0.5f), new Vector2(p, 0.5f), new Vector2(p == 0 ? 30 : -30, 70), new Vector2(560, 560));
                UIFactory.Outline(panel, UITheme.PlayerColor(p), 3f);
                _info[p] = UIFactory.Label("Texto", panel.rectTransform, "", 26, UITheme.Text, TextAlignmentOptions.TopLeft);
                UIFactory.Stretch(_info[p].rectTransform, 24, 24, 20, 150);
                _statBars[p] = new RectTransform[3];
                string[] statNames = { "Alcance", "Velocidade", "Força" };
                for (int s = 0; s < 3; s++)
                {
                    var lbl = UIFactory.Label("Stat" + s, panel.rectTransform, statNames[s], 22, UITheme.TextDim, TextAlignmentOptions.MidlineLeft);
                    UIFactory.Place(lbl.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 112 - s * 34), new Vector2(160, 30));
                    var barBg = UIFactory.Image("StatFundo", panel.rectTransform, new Color(0, 0, 0, 0.5f));
                    UIFactory.Place(barBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(190, 116 - s * 34), new Vector2(330, 20));
                    var fill = UIFactory.Image("Stat", barBg.rectTransform, UITheme.Accent);
                    _statBars[p][s] = fill.rectTransform;
                    for (int k = 1; k < 5; k++)
                    {
                        var sep = UIFactory.Image("Sep", barBg.rectTransform, new Color(0, 0, 0, 0.7f));
                        UIFactory.Place(sep.rectTransform, new Vector2(k / 5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 20));
                    }
                }
                _status[p] = UIFactory.Label("Status", panel.rectTransform, "", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Place(_status[p].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(520, 40));
            }

            _selectHint = UIFactory.Label("Dica", _select, "", 22, UITheme.TextDim, TextAlignmentOptions.Center);
            UIFactory.Place(_selectHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(1600, 36));
        }

        void BuildNetPanels(Transform root)
        {
            _netMenu = new ListPanel(root, "REDE LOCAL", 620, 74, 3, 0.8f, -430);
            _netMenu.List.Audio = _gs.Audio;
            _netMenu.List.Add("Hospedar partida", BeginNetHost);
            _netMenu.List.Add("Entrar em partida", BeginNetBrowse);
            _netMenu.List.Add("Voltar", LeaveNetMenu);
            _netMenu.List.OnBack = LeaveNetMenu;

            _netWait = new ListPanel(root, "AGUARDANDO JOGADOR", 520, 74, 1, 0.8f, -640);
            _netWait.List.Audio = _gs.Audio;
            _netWait.List.Add("Cancelar", CancelNet);
            _netWait.List.OnBack = CancelNet;

            _netBrowse = new ListPanel(root, "ENTRAR EM PARTIDA", 860, 70, 6, 0.8f, -400);
            _netBrowse.List.Audio = _gs.Audio;
            _netBrowse.List.OnBack = CancelNet;

            _netAddress = new ListPanel(root, "DIGITAR ENDEREÇO", 520, 74, 2, 0.8f, -560);
            _netAddress.List.Audio = _gs.Audio;
            _netAddress.List.Add("Conectar", ConnectToTypedAddress);
            _netAddress.List.Add("Voltar", CloseAddressEntry);

            // Textos de várias linhas: abaixo do título, antes da lista.
            foreach (var panel in new[] { _netWait, _netAddress, _netBrowse, _netMenu })
                UIFactory.Place(panel.Body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(1500, 200));
        }

        // ---------------------------------------------------------------- TELAS

        void Show(Screen s)
        {
            if (_screen == Screen.NetAddress && s != Screen.NetAddress && UnityEngine.InputSystem.Keyboard.current != null)
                UnityEngine.InputSystem.Keyboard.current.onTextInput -= OnAddressCharacter;
            _screen = s;
            _t = 0f;
            if (s == Screen.NetMenu) _netMenu.Open(); else _netMenu.Close();
            if (s == Screen.NetWait) _netWait.Open(); else _netWait.Close();
            if (s == Screen.NetBrowse) _netBrowse.Open(); else _netBrowse.Close();
            if (s == Screen.NetAddress) _netAddress.Open(); else _netAddress.Close();
            _title.gameObject.SetActive(s == Screen.Title);
            if (s == Screen.Main) _main.Open(); else _main.Close();
            _join.gameObject.SetActive(s == Screen.Join);
            if (s == Screen.Difficulty) _difficulty.Open(); else _difficulty.Close();
            _select.gameObject.SetActive(s == Screen.Select);
            if (s == Screen.Options) _options.Open(); else if (_options.IsOpen) _options.Root.gameObject.SetActive(false);
            _credits.gameObject.SetActive(s == Screen.Credits);
            for (int i = 0; i < 2; i++) if (_previews[i] != null) _previews[i].gameObject.SetActive(s == Screen.Select);
            if (s == Screen.Join) { _joinDoneTimer = -1f; RefreshJoin(); }
        }

        void ChooseMode(GameMode mode)
        {
            _session.Mode = mode;
            _gs.Input.ClearAll();
            Show(Screen.Join);
        }

        void RefreshJoin()
        {
            for (int i = 0; i < 2; i++)
            {
                var slot = _gs.Input.Slots[i];
                bool needed = i < PlayersNeeded;
                string head = $"<color=#{ColorUtility.ToHtmlStringRGB(UITheme.PlayerColor(i))}>JOGADOR {i + 1}</color>\n\n";
                if (!needed)
                    _joinSlots[i].text = head + (IsNet ? "<color=#9fb0d0>OUTRO COMPUTADOR</color>" : _session.Mode == GameMode.VsCpu ? "<color=#9fb0d0>CPU</color>" : "<color=#9fb0d0>BONECO DE TREINO</color>");
                else if (slot.Assigned)
                    _joinSlots[i].text = head + "<color=#80ff90>PRONTO</color>\n<size=70%>" + DeviceReader.DeviceLabel(slot.Device) + "</size>";
                else
                    _joinSlots[i].text = head + "<size=75%>Pressione <b>Enter / Espaço / J</b> no teclado\nou <b>A / Start</b> no controle</size>";
            }
            int pads = UnityEngine.InputSystem.Gamepad.all.Count;
            _joinHint.text = (PlayersNeeded == 1 ? "Um dispositivo controla o seu lutador." : "Cada jogador usa um dispositivo diferente (teclado + controle ou dois controles).") +
                             $"\nControles detectados: {pads}  •  Voltar: Esc / B";
        }

        void OpenSelect()
        {
            for (int i = 0; i < 2; i++)
            {
                _ready[i] = false;
                var cur = _session.Fighters[i];
                int idx = cur != null ? _gs.Database.Fighters.IndexOf(cur) : -1;
                _cursor[i] = idx >= 0 ? idx : FirstAvailable(i);
                _palette[i] = _session.Palettes[i];
            }
            _activeCursor = 0;
            _launchTimer = -1f;
            if (IsNet && _gs.Net.Active)
            {
                int local = _gs.Net.LocalIndex;
                _gs.Net.SetLocalSelection(_cursor[local], _palette[local], false);
                NetTestRun.Note("seleção");
            }
            Show(Screen.Select);
            RefreshSelect();
        }

        int FirstAvailable(int pref)
        {
            var list = _gs.Database.Fighters;
            if (pref < list.Count && list[pref].Selectable) return pref;
            for (int i = 0; i < list.Count; i++) if (list[i].Selectable) return i;
            return 0;
        }

        // ---------------------------------------------------------------- LAÇO

        void Update() => Frame(Time.unscaledDeltaTime);

        /// <summary>Desenvolvimento/testes: avança quadros manualmente.</summary>
        public void DevStep(int frames, float dt = 1f / 60f)
        {
            for (int i = 0; i < frames; i++) Frame(dt);
        }

        void Frame(float dt)
        {
            _t += dt;
            if (NetTestRun.TimeIsUp) NetTestRun.Finish($"tempo esgotado no menu: tela={_screen} rede={_gs.Net.Status} {_gs.Net.FailReason}");
            switch (_screen)
            {
                case Screen.Title: TickTitle(dt); break;
                case Screen.Main: _main.List.Handle(_gs.Input.ReadAnyMenu(dt)); break;
                case Screen.Join: TickJoin(dt); break;
                case Screen.Difficulty: _difficulty.List.Handle(_gs.Input.ReadMenu(0, dt)); break;
                case Screen.Select: TickSelect(dt); break;
                case Screen.NetMenu: _netMenu.List.Handle(_gs.Input.ReadMenu(0, dt)); break;
                case Screen.NetWait: TickNetWait(dt); break;
                case Screen.NetBrowse: TickNetBrowse(dt); break;
                case Screen.NetAddress: TickNetAddress(dt); break;
                case Screen.Options: _options.Tick(_gs.Input.ReadAnyMenu(dt), dt); break;
                case Screen.Credits:
                {
                    var m = _gs.Input.ReadAnyMenu(dt);
                    if (m.Cancel || m.Confirm) { _gs.Audio.PlayUi(UiSound.Back); Show(Screen.Main); }
                    break;
                }
            }
            if (MenuCamera != null)
                MenuCamera.transform.position = new Vector3(Mathf.Sin(Time.unscaledTime * 0.15f) * 0.3f, 1.5f, -8.4f);
        }

        void TickTitle(float dt)
        {
            float k = Mathf.Clamp01(_t / 0.8f);
            _titleLogo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, 1f - (1f - k) * (1f - k));
            _titleLogo.alpha = k;
            _titleSub.alpha = Mathf.Clamp01((_t - 0.5f) * 2f);
            _titlePress.alpha = _t > 1.2f ? 0.5f + 0.5f * Mathf.Sin(_t * 4f) : 0f;
            _gs.Input.ReadAnyMenu(dt); // mantém a leitura de bordas em dia para a próxima tela
            bool any = _gs.Input.AnyPressed();
            if (_t > 0.2f && any) { _gs.Audio.PlayUi(UiSound.Confirm); Show(Screen.Main); }
        }

        void TickJoin(float dt)
        {
            var input = _gs.Input;
            if (_joinDoneTimer >= 0f)
            {
                _joinDoneTimer -= dt;
                if (_joinDoneTimer < 0f)
                {
                    if (_session.Mode == GameMode.VsCpu) Show(Screen.Difficulty);
                    else if (IsNet) OpenNetMenu("");
                    else OpenSelect();
                }
                return;
            }

            // Cancelar: um jogador associado pode sair; sem ninguém, volta ao menu.
            for (int i = 0; i < 2; i++)
            {
                if (!input.Slots[i].Assigned) continue;
                if (input.ReadMenu(i, dt).Cancel)
                {
                    input.Unassign(i);
                    _gs.Audio.PlayUi(UiSound.Back);
                    RefreshJoin();
                    return;
                }
            }
            var dev = input.PollJoin();
            if (dev != null)
            {
                for (int i = 0; i < PlayersNeeded; i++)
                {
                    if (input.Slots[i].Assigned) continue;
                    input.Assign(i, dev);
                    _gs.Audio.PlayUi(UiSound.Confirm);
                    break;
                }
                RefreshJoin();
            }
            else if (!input.Slots[0].Assigned && !input.Slots[1].Assigned)
            {
                // Esc/B de qualquer dispositivo volta ao menu.
                if (input.ReadAnyMenu(dt).Cancel) { _gs.Audio.PlayUi(UiSound.Back); Show(Screen.Main); return; }
            }

            bool all = true;
            for (int i = 0; i < PlayersNeeded; i++) all &= input.Slots[i].Assigned;
            if (all) _joinDoneTimer = 0.45f;
        }

        // ---------------------------------------------------------------- REDE LOCAL

        void OpenNetMenu(string message)
        {
            _netMessage = message;
            _netMenu.Body.text = string.IsNullOrEmpty(message)
                ? "Um jogador hospeda, e o outro entra pela mesma rede."
                : $"<color=#ff9090>{message}</color>";
            Show(Screen.NetMenu);
        }

        void LeaveNetMenu()
        {
            _gs.Net.Stop();
            _gs.Input.ClearAll();
            Show(Screen.Main);
        }

        void CancelNet()
        {
            _gs.Net.Stop();
            OpenNetMenu("");
        }

        void BeginNetHost()
        {
            if (!_gs.Net.StartHost())
            {
                OpenNetMenu(_gs.Net.FailReason);
                return;
            }
            var addresses = NetTransport.LocalAddresses();
            string shown = addresses.Count > 0 ? string.Join("  •  ", addresses.GetRange(0, Mathf.Min(3, addresses.Count))) : "sem rede";
            _netWait.Body.text = $"Seu endereço: <b>{shown}</b>\n\nNo outro computador, escolha <b>Rede Local › Entrar em partida</b>.\nSe o Windows perguntar, permita o acesso à rede.";
            Show(Screen.NetWait);
        }

        void BeginNetBrowse()
        {
            if (!_gs.Net.StartBrowse())
            {
                OpenNetMenu(_gs.Net.FailReason);
                return;
            }
            _netBrowseKey = "";
            _netMessage = "";
            RebuildBrowseList();
            Show(Screen.NetBrowse);
        }

        void StartNetTest()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null) _gs.Input.Assign(0, keyboard);
            _session.Mode = GameMode.Network;
            _gs.Net.SimLoss = NetTestRun.SimLoss;
            _gs.Net.SimDelaySeconds = NetTestRun.SimDelay;
            _gs.Net.SimJitterSeconds = NetTestRun.SimJitter;
            if (NetTestRun.Hosting)
            {
                BeginNetHost();
                return;
            }
            BeginNetBrowse();
            _gs.Net.Join(IPAddress.Parse(NetTestRun.JoinAddress));
        }

        void TickNetWait(float dt)
        {
            _netWait.List.Handle(_gs.Input.ReadMenu(0, dt));
            if (_screen != Screen.NetWait) return;
            if (_gs.Net.Status == NetStatus.Connected) OpenSelect();
            else if (_gs.Net.Status == NetStatus.Failed) OpenNetMenu(_gs.Net.FailReason);
        }

        void TickNetBrowse(float dt)
        {
            var net = _gs.Net;
            if (net.Status == NetStatus.Connected)
            {
                OpenSelect();
                return;
            }
            if (net.Status == NetStatus.Failed)
            {
                // Mostra o motivo e continua procurando partidas.
                string reason = net.FailReason;
                net.StartBrowse();
                _netMessage = reason;
                _netBrowseKey = "";
            }
            if (NetTestRun.Active && net.Status == NetStatus.Browsing) net.Join(IPAddress.Parse(NetTestRun.JoinAddress));
            RebuildBrowseList();
            _netBrowse.Body.text = net.Status == NetStatus.Connecting ? "Conectando..."
                : !string.IsNullOrEmpty(_netMessage) ? $"<color=#ff9090>{_netMessage}</color>"
                : net.Hosts.Count == 0 ? "Procurando partidas na rede..." : "Escolha uma partida";
            _netBrowse.List.Handle(_gs.Input.ReadMenu(0, dt));
        }

        void RebuildBrowseList()
        {
            var hosts = _gs.Net.Hosts;
            var key = new System.Text.StringBuilder();
            foreach (var h in hosts) key.Append(h.Address).Append(h.Name).Append(h.GameVersion).Append(h.Busy).Append(';');
            if (key.ToString() == _netBrowseKey && _netBrowse.List.Count > 0) return;
            _netBrowseKey = key.ToString();

            var list = _netBrowse.List;
            list.Clear();
            foreach (var h in hosts)
            {
                var host = h;
                bool sameVersion = host.GameVersion == Application.version;
                string label = host.Name + "  (" + host.Address + ")";
                if (host.Busy) label += " — em partida";
                else if (!sameVersion) label += " — versão " + host.GameVersion;
                list.Add(label, () => ConnectTo(host.Address, host.Port), null, null, () => !host.Busy && sameVersion);
            }
            list.Add("Digitar endereço", OpenAddressEntry);
            list.Add("Voltar", CancelNet);
            list.FocusFirst();
            list.Refresh();
        }

        void ConnectTo(IPAddress address, int port)
        {
            _netMessage = "";
            _gs.Net.Join(address, port);
        }

        void OpenAddressEntry()
        {
            if (UnityEngine.InputSystem.Keyboard.current == null)
            {
                _netMessage = "É preciso um teclado para digitar o endereço.";
                return;
            }
            _netAddressText = "";
            RefreshAddressText("");
            // O Enter que abriu a tela ainda está pressionado: só vale depois de soltar.
            _addressEnterHeld = _addressEscapeHeld = _addressBackspaceHeld = true;
            Show(Screen.NetAddress);
            UnityEngine.InputSystem.Keyboard.current.onTextInput += OnAddressCharacter;
        }

        void OnAddressCharacter(char c)
        {
            if (((c >= '0' && c <= '9') || c == '.') && _netAddressText.Length < 15)
            {
                _netAddressText += c;
                RefreshAddressText("");
            }
        }

        void RefreshAddressText(string error)
        {
            string typed = _netAddressText.Length > 0 ? _netAddressText : "<color=#9fb0d0>exemplo: 192.168.0.10</color>";
            _netAddress.Body.text = $"<size=130%><b>{typed}</b></size>" + (string.IsNullOrEmpty(error) ? "\nDigite o endereço mostrado no outro computador." : $"\n<color=#ff9090>{error}</color>");
        }

        void TickNetAddress(float dt)
        {
            // As teclas de golpe podem estar nos números: aqui só Enter, Esc e Backspace agem.
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null)
            {
                if (NewPress(k.backspaceKey.isPressed, ref _addressBackspaceHeld) && _netAddressText.Length > 0)
                {
                    _netAddressText = _netAddressText.Substring(0, _netAddressText.Length - 1);
                    RefreshAddressText("");
                }
                if (NewPress(k.enterKey.isPressed || k.numpadEnterKey.isPressed, ref _addressEnterHeld)) ConnectToTypedAddress();
                else if (NewPress(k.escapeKey.isPressed, ref _addressEscapeHeld)) CloseAddressEntry();
            }
            if (_screen == Screen.NetAddress) _netAddress.List.Handle(default);
        }

        static bool NewPress(bool pressed, ref bool held)
        {
            bool press = pressed && !held;
            held = pressed;
            return press;
        }

        void ConnectToTypedAddress()
        {
            if (_netAddressText.Split('.').Length != 4 || !IPAddress.TryParse(_netAddressText, out var address))
            {
                RefreshAddressText("Endereço inválido.");
                return;
            }
            ConnectTo(address, 0);
            Show(Screen.NetBrowse);
        }

        void CloseAddressEntry()
        {
            _gs.Audio.PlayUi(UiSound.Back);
            Show(Screen.NetBrowse);
        }

        /// <summary>Seleção em rede: copia as escolhas do outro lado e cuida da queda de conexão e do início.</summary>
        bool TickNetSelect()
        {
            var net = _gs.Net;
            if (net.Status != NetStatus.Connected)
            {
                string reason = net.FailReason;
                net.Stop();
                OpenNetMenu(string.IsNullOrEmpty(reason) ? "A conexão foi encerrada." : reason);
                return true;
            }
            int count = _gs.Database.Fighters.Count;
            int remote = net.RemoteIndex;
            _cursor[remote] = Mathf.Clamp(net.Lobby.Cursor[remote], 0, count - 1);
            _palette[remote] = Mathf.Max(0, net.Lobby.Palette[remote]);
            _ready[remote] = net.Lobby.Ready[remote];
            if (net.Role == NetRole.Guest && net.Lobby.Phase == NetLobbyPhase.Fight)
            {
                LaunchFromLobby();
                return true;
            }
            return false;
        }

        void LaunchFromLobby()
        {
            var fighters = _gs.Database.Fighters;
            var lobby = _gs.Net.Lobby;
            for (int p = 0; p < 2; p++)
            {
                _session.Fighters[p] = fighters[Mathf.Clamp(lobby.FinalFighter[p], 0, fighters.Count - 1)];
                _session.Palettes[p] = lobby.FinalPalette[p];
            }
            SceneManager.LoadScene(FightSceneController.SceneName);
        }

        int CursorOwnerDevice(int cursor) => SinglePlayer || IsNet ? 0 : cursor;

        void TickSelect(float dt)
        {
            if (IsNet && TickNetSelect()) return;

            if (_launchTimer >= 0f)
            {
                _launchTimer -= dt;
                RenderPreviews(dt);
                if (_launchTimer < 0f)
                {
                    // O convidado espera a ordem do anfitrião.
                    if (IsNetGuest) _launchTimer = 0.1f;
                    else LaunchFight();
                }
                return;
            }

            var fighters = _gs.Database.Fighters;
            for (int p = 0; p < 2; p++)
            {
                // Em modos de 1 jogador, o mesmo dispositivo controla os dois cursores em sequência.
                if (SinglePlayer && p != _activeCursor) continue;
                if (IsNet && p != _gs.Net.LocalIndex) continue;
                int dev = CursorOwnerDevice(p);
                if (!_gs.Input.Slots[dev].Ready) continue;
                var m = _gs.Input.ReadMenu(dev, dt);
                if (!_ready[p])
                {
                    if (m.NavX != 0 || m.NavY != 0)
                    {
                        int step = m.NavX != 0 ? m.NavX : -m.NavY;
                        int c = _cursor[p];
                        for (int k = 0; k < fighters.Count; k++)
                        {
                            c = (c + step + fighters.Count) % fighters.Count;
                            if (fighters[c].Selectable) break;
                        }
                        if (c != _cursor[p]) { _cursor[p] = c; _gs.Audio.PlayUi(UiSound.Move); }
                    }
                    if (m.Alt)
                    {
                        // Mesmo lutador com o mesmo visual do adversário pronto é pulado.
                        int count = _gs.Database.VariantCount(fighters[_cursor[p]]);
                        _palette[p] = (_palette[p] + 1) % count;
                        if (count > 1 && _ready[1 - p] && _cursor[1 - p] == _cursor[p] && _palette[1 - p] == _palette[p])
                            _palette[p] = (_palette[p] + 1) % count;
                        _gs.Audio.PlayUi(UiSound.Move);
                    }
                    if (m.Confirm && fighters[_cursor[p]].Selectable)
                    {
                        // Mesmo lutador: garante cores diferentes.
                        int o = 1 - p;
                        if (_ready[o] && _cursor[o] == _cursor[p] && _palette[o] == _palette[p])
                            _palette[p] = (_palette[o] + 1) % _gs.Database.VariantCount(fighters[_cursor[p]]);
                        _ready[p] = true;
                        _gs.Audio.PlayUi(UiSound.Confirm);
                        // Um único "Confirmar" não pode escolher também o adversário no mesmo quadro.
                        if (SinglePlayer && p == 0) { _activeCursor = 1; break; }
                    }
                    else if (m.Cancel)
                    {
                        _gs.Audio.PlayUi(UiSound.Back);
                        if (SinglePlayer && p == 1) { _activeCursor = 0; _ready[0] = false; }
                        else if (IsNet) { CancelNet(); return; }
                        else { Show(Screen.Join); return; }
                    }
                }
                else if (m.Cancel)
                {
                    _ready[p] = false;
                    _gs.Audio.PlayUi(UiSound.Back);
                }
            }

            if (IsNet)
            {
                int local = _gs.Net.LocalIndex;
                if (NetTestRun.Active && _t > 1.5f && !_ready[local])
                {
                    _cursor[local] = Mathf.Clamp(NetTestRun.Fighter, 0, fighters.Count - 1);
                    _ready[local] = true;
                }
                _gs.Net.SetLocalSelection(_cursor[local], _palette[local], _ready[local]);
            }

            if (_ready[0] && _ready[1] && _launchTimer < 0f)
            {
                _launchTimer = 1.0f;
                _gs.Audio.PlayAnnouncer(AnnouncerLine.Fight);
            }
            RefreshSelect();
            RenderPreviews(dt);
        }

        void RefreshSelect()
        {
            var fighters = _gs.Database.Fighters;
            for (int c = 0; c < _cards.Length; c++)
                for (int p = 0; p < 2; p++)
                {
                    bool show = _cursor[p] == c && (!SinglePlayer || p <= _activeCursor || _ready[p]);
                    _cardFrames[c][p].enabled = show;
                    _cardFrames[c][p].transform.GetChild(0).gameObject.SetActive(show);
                    if (show) ((TextMeshProUGUI)_cardFrames[c][p].transform.GetChild(0).GetComponent<TextMeshProUGUI>()).text = SinglePlayer && p == 1 ? (_session.Mode == GameMode.Training ? "BONECO" : "CPU") : "J" + (p + 1);
                }
            for (int p = 0; p < 2; p++)
            {
                var def = fighters[_cursor[p]];
                string variant = _gs.Database.VariantName(def, _palette[p]);
                string who = IsNet ? (p == _gs.Net.LocalIndex ? "VOCÊ" : "OUTRO JOGADOR") : SinglePlayer ? (p == 0 ? "JOGADOR 1" : _session.Mode == GameMode.Training ? "BONECO" : "CPU") : "JOGADOR " + (p + 1);
                _info[p].text = $"<size=70%><color=#{ColorUtility.ToHtmlStringRGB(UITheme.PlayerColor(p))}>{who}</color></size>\n" +
                                $"<size=150%><b>{def.DisplayName.ToUpperInvariant()}</b></size>\n<color=#ffb030>{def.Epithet}</color> • {def.Style}\n\n" +
                                $"<size=85%>{def.Summary}\n\n<color=#ff9090>Fraqueza:</color> {def.Weakness}\n\nVisual: {variant}</size>";
                UIFactory.SetFill(_statBars[p][0], def.StatReach / 5f, false);
                UIFactory.SetFill(_statBars[p][1], def.StatSpeed / 5f, false);
                UIFactory.SetFill(_statBars[p][2], def.StatPower / 5f, false);
                int devSlot = CursorOwnerDevice(p);
                string devLabel = IsNet && p != _gs.Net.LocalIndex ? "Outro computador" : DeviceReader.DeviceLabel(_gs.Input.Slots[devSlot].Device);
                if (_ready[p]) _status[p].text = "<color=#80ff90>PRONTO!</color>";
                else if (SinglePlayer && p != _activeCursor) _status[p].text = "<color=#9fb0d0>aguardando</color>";
                else _status[p].text = $"<size=80%>{devLabel}</size>";
            }
            string pickWho = SinglePlayer && _activeCursor == 1 ? (_session.Mode == GameMode.Training ? "Escolha o boneco de treino. " : "Escolha o adversário da CPU. ") : "";
            _selectHint.text = pickWho + "Escolher: ←/→  •  Confirmar: Enter ou A  •  Trocar visual: Tab ou Y  •  Voltar: Esc ou B";
        }

        void RenderPreviews(float dt)
        {
            var fighters = _gs.Database.Fighters;
            for (int p = 0; p < 2; p++)
            {
                var def = fighters[_cursor[p]];
                int pal = _palette[p];
                if (SinglePlayer && p == 1 && !_ready[1] && _activeCursor == 0)
                {
                    _previews[p].gameObject.SetActive(false);
                    continue;
                }
                _previews[p].gameObject.SetActive(true);
                if (_previewDef[p] != def || _previewPal[p] != pal)
                {
                    _previews[p].Build(def, pal, _gs.Database.LitMaterial, _gs.Database.FindVisuals(def));
                    _previews[p].SetFacing(p == 0 ? 1 : -1);
                    _previewDef[p] = def;
                    _previewPal[p] = pal;
                }
                _previews[p].RenderPreview(_ready[p] ? PreviewMode.Victory : PreviewMode.Guard, dt);
            }
        }

        void LaunchFight()
        {
            var fighters = _gs.Database.Fighters;
            for (int p = 0; p < 2; p++)
            {
                _session.Fighters[p] = fighters[_cursor[p]];
                _session.Palettes[p] = _palette[p];
            }
            if (_session.Fighters[0] == _session.Fighters[1] && _session.Palettes[0] == _session.Palettes[1])
                _session.Palettes[1] = (_session.Palettes[0] + 1) % _gs.Database.VariantCount(_session.Fighters[1]);
            if (IsNet) _gs.Net.Launch(fighters.IndexOf(_session.Fighters[0]), fighters.IndexOf(_session.Fighters[1]), _session.Palettes[0], _session.Palettes[1]);
            SceneManager.LoadScene(FightSceneController.SceneName);
        }
    }
}
