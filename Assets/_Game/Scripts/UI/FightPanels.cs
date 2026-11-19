using System;
using System.Text;
using TMPro;
using UltimoRound.Combat;
using UltimoRound.Input;
using UnityEngine.InputSystem;
using UnityEngine;

namespace UltimoRound.UI
{
    /// <summary>Painel em tela cheia com título e uma <see cref="MenuList"/>.</summary>
    public sealed class ListPanel
    {
        public readonly RectTransform Root;
        public readonly MenuList List;
        public readonly TextMeshProUGUI Title;
        public readonly TextMeshProUGUI Body;

        public ListPanel(Transform parent, string title, float width = 640, float itemHeight = 64, int visible = 9, float dim = 0.75f, float listY = -260)
        {
            Root = UIFactory.Rect("Painel_" + title, parent);
            UIFactory.Stretch(Root);
            var bg = UIFactory.Image("Fundo", Root, new Color(0.01f, 0.015f, 0.04f, dim), true);
            UIFactory.Stretch(bg.rectTransform);
            Title = UIFactory.Label("Titulo", Root, title, 72, UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Place(Title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(1400, 110));
            Title.outlineWidth = 0.15f;
            Title.outlineColor = new Color32(0, 0, 0, 255);
            Body = UIFactory.Label("Texto", Root, "", 32, UITheme.Text, TextAlignmentOptions.Center);
            UIFactory.Place(Body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(1400, 60));
            List = new MenuList(Root, new Vector2(0.5f, 1f), new Vector2(0, listY), width, itemHeight, visible);
            Root.gameObject.SetActive(false);
        }

        public bool IsOpen => Root.gameObject.activeSelf;

        public void Open()
        {
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            List.FocusFirst();
            List.Refresh();
        }

        public void Close() => Root.gameObject.SetActive(false);
    }

    /// <summary>Lista de golpes com classificação (alto/médio/baixo/overhead/agarrão) e combos sugeridos.</summary>
    public sealed class MoveListPanel
    {
        public readonly RectTransform Root;
        readonly TextMeshProUGUI _text;
        readonly TextMeshProUGUI _title;
        readonly RectTransform _content;
        float _scroll;
        const float ViewHeight = 860f;
        readonly ScrollBarView _bar;
        public Action OnClose;
        public MenuList Actions;

        public MoveListPanel(Transform parent)
        {
            Root = UIFactory.Rect("ListaDeGolpes", parent);
            UIFactory.Stretch(Root);
            var bg = UIFactory.Image("Fundo", Root, UITheme.Background, true);
            UIFactory.Stretch(bg.rectTransform);
            _title = UIFactory.Label("Titulo", Root, "LISTA DE GOLPES", 56, UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(1600, 80));
            var view = UIFactory.Rect("Visor", Root);
            UIFactory.Place(view, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260, -20), new Vector2(1180, 860));
            view.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            _bar = new ScrollBarView(Root, new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-260 + 590 + 10, -20), ViewHeight);
            _bar.OnDrag = t => { _scroll = t * MaxScroll; ApplyScroll(); };
            _content = UIFactory.Rect("Conteudo", view);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.sizeDelta = new Vector2(0, 2400);
            _text = UIFactory.Label("Texto", _content, "", 27, UITheme.Text, TextAlignmentOptions.TopLeft);
            UIFactory.Stretch(_text.rectTransform, 10, 10, 0, 0);
            _text.overflowMode = TextOverflowModes.Overflow;
            Actions = new MenuList(Root, new Vector2(1f, 1f), new Vector2(-300, -140), 540, 60, 8);
            var hint = UIFactory.Label("Dica", Root, "Rolar: cima/baixo, roda do mouse ou barra  •  Trocar botão: esquerda/direita  •  Voltar: Esc/B", 22, UITheme.TextDim, TextAlignmentOptions.Center);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(1600, 36));
            Root.gameObject.SetActive(false);
        }

        public bool IsOpen => Root.gameObject.activeSelf;

        public void Open(FighterDefinition def)
        {
            _title.text = "LISTA DE GOLPES — " + def.DisplayName.ToUpperInvariant();
            _text.text = Describe(def);
            _scroll = 0;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            // O tamanho do conteúdo acompanha o texto real: é ele que define até onde dá para rolar.
            _text.ForceMeshUpdate();
            _content.sizeDelta = new Vector2(0, _text.preferredHeight + 40f);
            ApplyScroll();
            Actions.FocusFirst();
            Actions.Refresh();
        }

        public void Close()
        {
            Root.gameObject.SetActive(false);
            OnClose?.Invoke();
        }

        float MaxScroll => Mathf.Max(0f, _content.sizeDelta.y - ViewHeight);
        public float ScrollPosition => _scroll;

        public void Scroll(float delta)
        {
            _scroll += delta;
            ApplyScroll();
        }

        void ApplyScroll()
        {
            _scroll = Mathf.Clamp(_scroll, 0f, MaxScroll);
            _content.anchoredPosition = new Vector2(0, _scroll);
            float h = _content.sizeDelta.y;
            _bar.Set(h > 0 ? ViewHeight / h : 1f, MaxScroll > 0 ? _scroll / MaxScroll : 0f);
        }

        /// <summary>Cima/baixo e roda rolam o texto; esquerda/direita escolhem o botão de ação.</summary>
        public void Tick(in MenuInput m, float dt)
        {
            if (m.HeldY != 0) Scroll(-m.HeldY * 1100f * dt);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.001f) Scroll(-Mathf.Sign(wheel) * 110f);
            }
            var actions = m;
            actions.NavY = -m.NavX;
            actions.NavX = 0;
            Actions.Handle(actions);
        }

        static string Frames(MoveDefinition m)
        {
            int adv = m.Blockstun - (m.Active - 1 + m.Recovery);
            string advTxt = m.IsThrow ? "—" : (adv >= 0 ? "+" + adv : adv.ToString());
            return $"<color=#9fb0d0>início {m.Startup + 1} • ativo {m.Active} • recup. {m.Recovery} • na defesa {advTxt}</color>";
        }

        public static string Describe(FighterDefinition def)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<size=120%><b>{def.DisplayName}</b> — {def.Epithet}</size>");
            sb.AppendLine($"<color=#ffb030>{def.Style}</color>  •  Vida {def.MaxHealth}");
            sb.AppendLine(def.Summary);
            sb.AppendLine($"<color=#ff8080>Fraqueza:</color> {def.Weakness}");
            sb.AppendLine();
            foreach (var slot in FighterDefinition.AllSlots)
            {
                var m = def.GetMove(slot);
                if (m == null) continue;
                sb.AppendLine($"<b>{m.DisplayName}</b>  <color=#ffd070>[{m.Notation}]</color>  <color=#80d0ff>{m.LevelLabel()}</color>  • dano {m.Damage}{(m.Hits > 1 ? " ×" + m.Hits : "")}");
                sb.AppendLine("    " + Frames(m) + (string.IsNullOrEmpty(m.Description) ? "" : "\n    " + m.Description));
            }
            sb.AppendLine();
            sb.AppendLine("<b>Movimentos gerais</b>");
            sb.AppendLine("    Avanço rápido: toque duplo para frente  •  Recuo rápido: toque duplo para trás");
            sb.AppendLine("    Defesa: segure Defesa (em pé contra overhead/aéreos; agachado contra baixos)");
            sb.AppendLine("    Escapar de agarrão: aperte Agarrão logo após ser agarrado (janela de 10 ticks)");
            sb.AppendLine();
            sb.AppendLine("<b>Combos sugeridos</b>");
            foreach (var c in def.Combos)
                sb.AppendLine($"    <color=#ffb030>{c.Name}</color>: {c.Notation}\n        <color=#9fb0d0>{c.Notes}</color>");
            return sb.ToString();
        }
    }

    /// <summary>Tela de confronto (VS) exibida antes da luta; pulável.</summary>
    public sealed class VersusOverlay
    {
        public readonly RectTransform Root;
        readonly TextMeshProUGUI[] _names = new TextMeshProUGUI[2];
        readonly TextMeshProUGUI[] _info = new TextMeshProUGUI[2];
        readonly UnityEngine.UI.Image[] _bands = new UnityEngine.UI.Image[2];
        readonly TextMeshProUGUI _vs;
        float _t;

        public VersusOverlay(Transform parent)
        {
            Root = UIFactory.Rect("Versus", parent);
            UIFactory.Stretch(Root);
            var bg = UIFactory.Image("Fundo", Root, new Color(0.02f, 0.02f, 0.05f, 0.82f), true);
            UIFactory.Stretch(bg.rectTransform);
            for (int i = 0; i < 2; i++)
            {
                var band = UIFactory.Image("Faixa" + i, Root, UITheme.PlayerColor(i) * new Color(1, 1, 1, 0.85f));
                UIFactory.Place(band.rectTransform, new Vector2(i, 0.5f), new Vector2(i, 0.5f), Vector2.zero, new Vector2(1100, 240));
                band.rectTransform.localEulerAngles = new Vector3(0, 0, i == 0 ? 6 : 6);
                _bands[i] = band;
                _names[i] = UIFactory.Label("Nome" + i, Root, "", 110, Color.white, i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, FontStyles.Bold | FontStyles.Italic);
                UIFactory.Place(_names[i].rectTransform, new Vector2(i, 0.5f), new Vector2(i, 0.5f), new Vector2(i == 0 ? 80 : -80, 30), new Vector2(900, 140));
                _names[i].outlineWidth = 0.2f;
                _names[i].outlineColor = new Color32(0, 0, 0, 255);
                _info[i] = UIFactory.Label("Info" + i, Root, "", 34, UITheme.Text, i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, FontStyles.Bold);
                UIFactory.Place(_info[i].rectTransform, new Vector2(i, 0.5f), new Vector2(i, 0.5f), new Vector2(i == 0 ? 90 : -90, -80), new Vector2(900, 110));
            }
            _vs = UIFactory.Label("VS", Root, "VS", 200, UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            UIFactory.Place(_vs.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 260));
            _vs.outlineWidth = 0.25f;
            _vs.outlineColor = new Color32(0, 0, 0, 255);
            var hint = UIFactory.Label("Dica", Root, "Confirmar: pular", 24, UITheme.TextDim, TextAlignmentOptions.Center);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(800, 40));
            Root.gameObject.SetActive(false);
        }

        public void Show(FighterDefinition a, FighterDefinition b, string tagA, string tagB)
        {
            _names[0].text = a.DisplayName.ToUpperInvariant();
            _names[1].text = b.DisplayName.ToUpperInvariant();
            _info[0].text = $"{a.Epithet} • {a.Style}\n<size=70%><color=#9fb6ff>{tagA}</color></size>";
            _info[1].text = $"{b.Epithet} • {b.Style}\n<size=70%><color=#ff9f9f>{tagB}</color></size>";
            _t = 0;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
        }

        public void Tick(float dt)
        {
            _t += dt;
            float k = Mathf.Clamp01(_t * 3f);
            float e = 1f - (1f - k) * (1f - k);
            _names[0].rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-900, 80, e), 30);
            _names[1].rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(900, -80, e), 30);
            _vs.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.5f, 1f, e);
        }

        public void Hide() => Root.gameObject.SetActive(false);
    }

    /// <summary>Aviso de controle desconectado/reconectado (pausa obrigatória).</summary>
    public sealed class DisconnectOverlay
    {
        public readonly RectTransform Root;
        readonly TextMeshProUGUI _text;

        public DisconnectOverlay(Transform parent)
        {
            Root = UIFactory.Rect("Desconexao", parent);
            UIFactory.Stretch(Root);
            var bg = UIFactory.Image("Fundo", Root, new Color(0, 0, 0, 0.88f), true);
            UIFactory.Stretch(bg.rectTransform);
            _text = UIFactory.Label("Texto", Root, "", 46, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_text.rectTransform, 100, 100, 0, 0);
            Root.gameObject.SetActive(false);
        }

        public bool IsOpen => Root.gameObject.activeSelf;

        public void ShowLost(int slot)
        {
            _text.text = $"<color=#ff7070>CONTROLE DO JOGADOR {slot + 1} DESCONECTADO</color>\n\n<size=70%>A partida está pausada. Reconecte o controle.</size>";
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
        }

        public void ShowRegained(int slot, InputDevice dev)
        {
            _text.text = $"<color=#80ff90>JOGADOR {slot + 1} RECONECTADO</color>\n<size=60%>{DeviceReader.DeviceLabel(dev)}</size>\n\n<size=70%>Pressione Confirmar (A / Enter) nesse dispositivo para continuar.</size>";
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
        }

        public void Hide() => Root.gameObject.SetActive(false);
    }
}
