using TMPro;
using UltimoRound.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace UltimoRound.UI
{
    /// <summary>HUD da luta. Vida e energia são legíveis pelo preenchimento e por números, não só pela cor.</summary>
    public sealed class HudView
    {
        sealed class Side
        {
            public RectTransform HealthFill, HealthTrail, EnergyFill;
            public Image HealthFillImg, EnergyFillImg;
            public TextMeshProUGUI Name, HealthText, EnergyText, Combo, ComboDamage, Notice;
            public Image[] Pips;
            public float Trail = 1f;
            public float TrailDelay;
            public float ComboTimer;
            public float NoticeTimer;
            public bool Reverse;
        }

        public readonly Canvas Canvas;
        readonly Side[] _sides = new Side[2];
        readonly TextMeshProUGUI _timer;
        readonly TextMeshProUGUI _banner;
        readonly TextMeshProUGUI _subBanner;
        readonly TextMeshProUGUI _devInfo;
        readonly TextMeshProUGUI[] _history = new TextMeshProUGUI[2];
        readonly RectTransform _top;
        float _bannerTimer;
        float _bannerScale;
        float _subTimer;

        public HudView(Transform parent)
        {
            Canvas = UIFactory.Canvas("HUD", 10, parent);
            var root = (RectTransform)Canvas.transform;
            _top = UIFactory.Rect("Topo", root);
            UIFactory.Place(_top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(1820, 170));

            _sides[0] = BuildSide(0, _top);
            _sides[1] = BuildSide(1, _top);

            var timerBg = UIFactory.Image("CronometroFundo", _top, new Color(0, 0, 0, 0.55f));
            UIFactory.Place(timerBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(130, 96));
            UIFactory.Outline(timerBg, new Color(1, 1, 1, 0.25f));
            _timer = UIFactory.Label("Cronometro", timerBg.rectTransform, "60", 64, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_timer.rectTransform);

            _banner = UIFactory.Label("Aviso", root, "", 120, Color.white, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            UIFactory.Place(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(1600, 200));
            _banner.outlineWidth = 0.18f;
            _banner.outlineColor = new Color32(20, 10, 0, 255);
            _banner.textWrappingMode = TextWrappingModes.NoWrap;
            _subBanner = UIFactory.Label("SubAviso", root, "", 44, UITheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Place(_subBanner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1600, 80));
            _subBanner.outlineWidth = 0.15f;
            _subBanner.outlineColor = new Color32(0, 0, 0, 255);

            _devInfo = UIFactory.Label("InfoDev", root, "", 22, new Color(0.8f, 1f, 0.8f), TextAlignmentOptions.TopLeft);
            UIFactory.Place(_devInfo.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30, 120), new Vector2(560, 420));
            _devInfo.gameObject.SetActive(false);

            for (int i = 0; i < 2; i++)
            {
                var h = UIFactory.Label("Historico" + i, root, "", 24, UITheme.Text, i == 0 ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight);
                UIFactory.Place(h.rectTransform, new Vector2(i, 0f), new Vector2(i, 0f), new Vector2(i == 0 ? 40 : -40, 150), new Vector2(380, 420));
                h.gameObject.SetActive(false);
                _history[i] = h;
            }
        }

        Side BuildSide(int i, RectTransform top)
        {
            var s = new Side { Reverse = i == 1 };
            float sign = i == 0 ? -1 : 1;
            var anchor = new Vector2(0.5f, 1f);
            var pivot = new Vector2(i == 0 ? 1f : 0f, 1f);
            Color pc = UITheme.PlayerColor(i);

            // Barra de vida
            var frame = UIFactory.Image("VidaMoldura" + i, top, new Color(0, 0, 0, 0.6f));
            UIFactory.Place(frame.rectTransform, anchor, pivot, new Vector2(sign * 80, -14), new Vector2(760, 46));
            UIFactory.Outline(frame, new Color(1, 1, 1, 0.5f), 2f);
            var trail = UIFactory.Image("VidaRastro", frame.rectTransform, UITheme.HealthTrail);
            s.HealthTrail = trail.rectTransform;
            var fill = UIFactory.Image("Vida", frame.rectTransform, UITheme.Health);
            s.HealthFill = fill.rectTransform;
            s.HealthFillImg = fill;
            // marcas a cada 25% para leitura por preenchimento
            for (int k = 1; k < 4; k++)
            {
                var tick = UIFactory.Image("Marca", frame.rectTransform, new Color(0, 0, 0, 0.45f));
                UIFactory.Place(tick.rectTransform, new Vector2(k * 0.25f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 46));
            }
            s.HealthText = UIFactory.Label("VidaNum", frame.rectTransform, "100%", 26, Color.black, i == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UIFactory.Stretch(s.HealthText.rectTransform, 14, 14, 0, 0);

            // Nome e retrato (selo com inicial)
            var badge = UIFactory.Image("Retrato" + i, top, pc);
            UIFactory.Place(badge.rectTransform, anchor, pivot, new Vector2(sign * 850, -8), new Vector2(64, 64));
            UIFactory.Outline(badge, Color.white, 2f);
            var initial = UIFactory.Label("Inicial", badge.rectTransform, "?", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(initial.rectTransform);
            s.Name = UIFactory.Label("Nome" + i, top, "", 34, Color.white, i == 0 ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.Place(s.Name.rectTransform, anchor, pivot, new Vector2(sign * 80, -64), new Vector2(560, 44));
            s.Name.outlineWidth = 0.15f;
            s.Name.outlineColor = new Color32(0, 0, 0, 255);
            s.Name.gameObject.AddComponent<LayoutElement>();
            s.Name.transform.SetAsLastSibling();
            badge.name = "Retrato";
            _initials[i] = initial;

            // Rounds vencidos
            s.Pips = new Image[2];
            for (int k = 0; k < 2; k++)
            {
                var pip = UIFactory.Image("Round" + k, top, new Color(1, 1, 1, 0.2f));
                UIFactory.Place(pip.rectTransform, anchor, new Vector2(0.5f, 1f), new Vector2(sign * (690 - k * 40), -70), new Vector2(28, 28));
                UIFactory.Outline(pip, new Color(1, 1, 1, 0.7f), 2f);
                s.Pips[k] = pip;
            }

            // Energia (canto inferior)
            var root = (RectTransform)Canvas.transform;
            var eFrame = UIFactory.Image("EnergiaMoldura" + i, root, new Color(0, 0, 0, 0.6f));
            UIFactory.Place(eFrame.rectTransform, new Vector2(i, 0f), new Vector2(i, 0f), new Vector2(i == 0 ? 40 : -40, 40), new Vector2(460, 30));
            UIFactory.Outline(eFrame, new Color(1, 1, 1, 0.5f), 2f);
            var eFill = UIFactory.Image("Energia", eFrame.rectTransform, UITheme.Energy);
            s.EnergyFill = eFill.rectTransform;
            s.EnergyFillImg = eFill;
            for (int k = 1; k < 4; k++)
            {
                var tick = UIFactory.Image("Marca", eFrame.rectTransform, new Color(0, 0, 0, 0.5f));
                UIFactory.Place(tick.rectTransform, new Vector2(k * 0.25f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 30));
            }
            s.EnergyText = UIFactory.Label("EnergiaTxt", eFrame.rectTransform, "ENERGIA 0%", 20, Color.white, i == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UIFactory.Place(s.EnergyText.rectTransform, new Vector2(i, 1f), new Vector2(i, 0f), new Vector2(0, 4), new Vector2(460, 28));
            s.EnergyText.outlineWidth = 0.2f;
            s.EnergyText.outlineColor = new Color32(0, 0, 0, 255);

            // Combo
            s.Combo = UIFactory.Label("Combo" + i, root, "", 64, UITheme.Accent, i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, FontStyles.Bold | FontStyles.Italic);
            UIFactory.Place(s.Combo.rectTransform, new Vector2(i, 0.5f), new Vector2(i, 0.5f), new Vector2(i == 0 ? 50 : -50, 170), new Vector2(520, 80));
            s.Combo.outlineWidth = 0.2f;
            s.Combo.outlineColor = new Color32(0, 0, 0, 255);
            s.ComboDamage = UIFactory.Label("ComboDano" + i, root, "", 28, Color.white, i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, FontStyles.Bold);
            UIFactory.Place(s.ComboDamage.rectTransform, new Vector2(i, 0.5f), new Vector2(i, 0.5f), new Vector2(i == 0 ? 54 : -54, 118), new Vector2(520, 40));
            s.ComboDamage.outlineWidth = 0.2f;
            s.ComboDamage.outlineColor = new Color32(0, 0, 0, 255);
            s.Notice = UIFactory.Label("Aviso" + i, root, "", 26, UITheme.Accent, i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, FontStyles.Bold);
            UIFactory.Place(s.Notice.rectTransform, new Vector2(i, 0f), new Vector2(i, 0f), new Vector2(i == 0 ? 44 : -44, 104), new Vector2(560, 36));
            s.Notice.outlineWidth = 0.2f;
            s.Notice.outlineColor = new Color32(0, 0, 0, 255);
            return s;
        }

        readonly TextMeshProUGUI[] _initials = new TextMeshProUGUI[2];

        public void SetFighters(FighterDefinition a, FighterDefinition b, string tagA, string tagB)
        {
            _sides[0].Name.text = a.DisplayName.ToUpperInvariant() + (string.IsNullOrEmpty(tagA) ? "" : "  <size=60%><color=#9fb6ff>" + tagA + "</color></size>");
            _sides[1].Name.text = (string.IsNullOrEmpty(tagB) ? "" : "<size=60%><color=#ff9f9f>" + tagB + "</color></size>  ") + b.DisplayName.ToUpperInvariant();
            _initials[0].text = a.DisplayName.Substring(0, 1);
            _initials[1].text = b.DisplayName.Substring(0, 1);
            _sides[0].Trail = _sides[1].Trail = 1f;
        }

        public void SetTimer(string text) => _timer.text = text;

        public void SetWins(int[] wins)
        {
            for (int i = 0; i < 2; i++)
                for (int k = 0; k < 2; k++)
                    _sides[i].Pips[k].color = wins[i] > k ? UITheme.Accent : new Color(1, 1, 1, 0.2f);
        }

        public void ShowBanner(string text, float seconds, Color color)
        {
            _banner.text = text;
            _banner.color = color;
            _bannerTimer = seconds;
            _bannerScale = 1.35f;
        }

        public void ShowSub(string text, float seconds)
        {
            _subBanner.text = text;
            _subTimer = seconds;
        }

        public void ShowCombo(int side, int hits, int damage)
        {
            var s = _sides[side];
            if (hits < 2) return;
            s.Combo.text = hits + " HITS";
            s.ComboDamage.text = damage + " de dano";
            s.ComboTimer = 1.6f;
        }

        public void ShowNotice(int side, string text, float seconds = 1.2f)
        {
            _sides[side].Notice.text = text;
            _sides[side].NoticeTimer = seconds;
        }

        public void SetDevInfo(string text)
        {
            _devInfo.gameObject.SetActive(!string.IsNullOrEmpty(text));
            _devInfo.text = text;
        }

        public void SetHistory(int side, string text)
        {
            _history[side].gameObject.SetActive(!string.IsNullOrEmpty(text));
            _history[side].text = text;
        }

        public void SetTopVisible(bool on) => _top.gameObject.SetActive(on);

        public void UpdateBars(FightSimulation sim, int maxEnergy, float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                var f = sim[i];
                var s = _sides[i];
                float hp = f.HealthRatio;
                UIFactory.SetFill(s.HealthFill, hp, s.Reverse);
                s.HealthFillImg.color = hp < 0.25f ? UITheme.HealthLow : UITheme.Health;
                s.HealthText.text = Mathf.CeilToInt(hp * 100f) + "%";
                if (s.Trail < hp) s.Trail = hp;
                else if (s.Trail > hp)
                {
                    s.TrailDelay -= dt;
                    if (s.TrailDelay <= 0f) s.Trail = Mathf.MoveTowards(s.Trail, hp, dt * 0.6f);
                }
                if (f.InComboState) s.TrailDelay = 0.5f;
                UIFactory.SetFill(s.HealthTrail, s.Trail, s.Reverse);

                float en = maxEnergy > 0 ? f.Energy / (float)maxEnergy : 0f;
                UIFactory.SetFill(s.EnergyFill, en, s.Reverse);
                bool full = f.Energy >= maxEnergy;
                s.EnergyFillImg.color = full ? Color.Lerp(UITheme.EnergyFull, Color.white, Mathf.PingPong(Time.unscaledTime * 2f, 0.4f)) : UITheme.Energy;
                s.EnergyText.text = full ? "ASSINATURA PRONTA!" : "ENERGIA " + Mathf.FloorToInt(en * 100f) + "%";

                if (s.ComboTimer > 0f)
                {
                    s.ComboTimer -= dt;
                    float a = Mathf.Clamp01(s.ComboTimer * 2f);
                    s.Combo.alpha = a;
                    s.ComboDamage.alpha = a;
                }
                else { s.Combo.text = ""; s.ComboDamage.text = ""; }
                // Contador ao vivo durante o combo
                var victim = sim[1 - i];
                if (victim.ComboHits >= 2)
                {
                    s.Combo.text = victim.ComboHits + " HITS";
                    s.ComboDamage.text = victim.ComboDamage + " de dano";
                    s.Combo.alpha = s.ComboDamage.alpha = 1f;
                    s.ComboTimer = 1.6f;
                }
                if (s.NoticeTimer > 0f)
                {
                    s.NoticeTimer -= dt;
                    if (s.NoticeTimer <= 0f) s.Notice.text = "";
                }
            }

            if (_bannerTimer > 0f)
            {
                _bannerTimer -= dt;
                _bannerScale = Mathf.MoveTowards(_bannerScale, 1f, dt * 3f);
                _banner.rectTransform.localScale = Vector3.one * _bannerScale;
                _banner.alpha = Mathf.Clamp01(_bannerTimer * 4f);
                if (_bannerTimer <= 0f) _banner.text = "";
            }
            if (_subTimer > 0f)
            {
                _subTimer -= dt;
                _subBanner.alpha = Mathf.Clamp01(_subTimer * 4f);
                if (_subTimer <= 0f) _subBanner.text = "";
            }
        }

        public void ClearTransient()
        {
            _banner.text = "";
            _subBanner.text = "";
            _bannerTimer = _subTimer = 0f;
            foreach (var s in _sides)
            {
                s.Combo.text = s.ComboDamage.text = s.Notice.text = "";
                s.ComboTimer = s.NoticeTimer = 0;
                s.Trail = 1f;
            }
        }

        public void SetVisible(bool on) => Canvas.gameObject.SetActive(on);
    }
}
