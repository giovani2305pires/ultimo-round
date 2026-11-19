using System;
using System.Collections.Generic;
using TMPro;
using UltimoRound.Audio;
using UltimoRound.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UltimoRound.UI
{
    /// <summary>Item de menu: ação (confirmar) e/ou valor ajustável (esquerda/direita).</summary>
    public sealed class MenuEntry
    {
        public RectTransform Root;
        public Image Background;
        public Image Marker;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Value;
        public Action OnConfirm;
        public Action<int> OnAdjust;
        public Func<string> GetValue;
        public Func<bool> IsEnabled;
        public bool Enabled => IsEnabled == null || IsEnabled();
        public bool IsHeader;
    }

    /// <summary>
    /// Lista vertical navegável por teclado/controle (com foco visível) e mouse. Rola automaticamente.
    /// </summary>
    public sealed class MenuList
    {
        public readonly RectTransform Root;
        readonly RectTransform _content;
        readonly ScrollBarView _bar;
        readonly List<MenuEntry> _entries = new List<MenuEntry>();
        readonly float _itemHeight;
        readonly float _width;
        readonly int _visible;
        int _focus = -1;
        int _scroll;
        public Action OnBack;
        public AudioService Audio;
        public int Focus => _focus;
        public int ScrollIndex => _scroll;
        public int Count => _entries.Count;
        public IReadOnlyList<MenuEntry> Entries => _entries;

        public MenuList(Transform parent, Vector2 anchor, Vector2 pos, float width, float itemHeight, int visibleItems = 12)
        {
            _itemHeight = itemHeight;
            _width = width;
            _visible = visibleItems;
            Root = UIFactory.Rect("Menu", parent);
            UIFactory.Place(Root, anchor, new Vector2(0.5f, 1f), pos, new Vector2(width, itemHeight * visibleItems));
            // A máscara fica num filho para a barra de rolagem não ser recortada.
            var viewport = UIFactory.Stretch(UIFactory.Rect("Visor", Root));
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            _bar = new ScrollBarView(Root, new Vector2(1, 1), new Vector2(0, 1), new Vector2(12, 0), itemHeight * visibleItems);
            _bar.OnDrag = t => { _scroll = Mathf.RoundToInt(t * MaxScroll); UpdateVisuals(); };
            _content = UIFactory.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0, 0);
        }

        public MenuEntry Add(string label, Action onConfirm, Func<string> getValue = null, Action<int> onAdjust = null, Func<bool> enabled = null)
        {
            int index = _entries.Count;
            var rt = UIFactory.Rect("Item_" + label, _content);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, _itemHeight - 6);
            rt.anchoredPosition = new Vector2(0, -index * _itemHeight);

            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = UITheme.Panel;
            bg.raycastTarget = true;
            var marker = UIFactory.Image("Marker", rt, UITheme.Accent);
            UIFactory.Place(marker.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(8, _itemHeight - 6));

            bool hasValue = getValue != null;
            var lbl = UIFactory.Label("Label", rt, label, _itemHeight * 0.42f, UITheme.Text, hasValue ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
            UIFactory.Stretch(lbl.rectTransform, 28, hasValue ? _width * 0.45f : 28, 0, 0);
            TextMeshProUGUI val = null;
            if (hasValue)
            {
                val = UIFactory.Label("Value", rt, "", _itemHeight * 0.4f, UITheme.Accent, TextAlignmentOptions.MidlineRight);
                UIFactory.Stretch(val.rectTransform, _width * 0.45f, 28, 0, 0);
            }

            var e = new MenuEntry { Root = rt, Background = bg, Marker = marker, Label = lbl, Value = val, OnConfirm = onConfirm, OnAdjust = onAdjust, GetValue = getValue, IsEnabled = enabled };
            _entries.Add(e);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.onClick.AddListener(() =>
            {
                SetFocus(index, false);
                Activate();
                EventSystem.current?.SetSelectedGameObject(null);
            });
            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                // Só segue o mouse quando ele se move: um ponteiro parado não pode roubar o foco do teclado/controle.
                var mouse = Mouse.current;
                if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.01f) SetFocus(index, true);
            });

            trigger.triggers.Add(enter);

            if (_focus < 0 && e.Enabled) _focus = index;
            Refresh();
            return e;
        }

        public MenuEntry AddHeader(string text)
        {
            int index = _entries.Count;
            var rt = UIFactory.Rect("Header_" + text, _content);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, _itemHeight - 6);
            rt.anchoredPosition = new Vector2(0, -index * _itemHeight);
            var lbl = UIFactory.Label("Label", rt, text.ToUpperInvariant(), _itemHeight * 0.36f, UITheme.Accent, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            UIFactory.Stretch(lbl.rectTransform, 8, 8, 0, 4);
            var e = new MenuEntry { Root = rt, Label = lbl, IsHeader = true, IsEnabled = () => false };
            _entries.Add(e);
            return e;
        }

        public void Clear()
        {
            foreach (var e in _entries) UnityEngine.Object.Destroy(e.Root.gameObject);
            _entries.Clear();
            _focus = -1;
            _scroll = 0;
        }

        public void SetFocus(int index, bool sound)
        {
            if (index < 0 || index >= _entries.Count || !_entries[index].Enabled) return;
            if (index != _focus && sound) Audio?.PlayUi(UiSound.Move);
            _focus = index;
            Refresh();
        }

        public void FocusFirst()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Enabled) { _focus = i; break; }
            Refresh();
        }

        void Move(int dir)
        {
            if (_entries.Count == 0) return;
            int i = _focus;
            for (int n = 0; n < _entries.Count; n++)
            {
                i = (i + dir + _entries.Count) % _entries.Count;
                if (_entries[i].Enabled)
                {
                    SetFocus(i, true);
                    return;
                }
            }
        }

        void Activate()
        {
            if (_focus < 0 || _focus >= _entries.Count) return;
            var e = _entries[_focus];
            if (!e.Enabled) return;
            if (e.OnConfirm != null)
            {
                Audio?.PlayUi(UiSound.Confirm);
                e.OnConfirm();
            }
            else if (e.OnAdjust != null)
            {
                Audio?.PlayUi(UiSound.Move);
                e.OnAdjust(1);
            }
            Refresh();
        }

        int MaxScroll => Mathf.Max(0, _entries.Count - _visible);

        /// <summary>Roda do mouse: rola a lista sem mudar o foco (o foco pode ficar fora da área visível).</summary>
        void HandleWheel()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.001f) return;
            _scroll = Mathf.Clamp(_scroll - (int)Mathf.Sign(wheel) * 2, 0, MaxScroll);
            UpdateVisuals();
        }

        public void Handle(in MenuInput m)
        {
            HandleWheel();
            if (m.NavY != 0) Move(-m.NavY);
            if (m.NavX != 0 && _focus >= 0 && _focus < _entries.Count)
            {
                var e = _entries[_focus];
                if (e.Enabled && e.OnAdjust != null)
                {
                    e.OnAdjust(m.NavX);
                    Audio?.PlayUi(UiSound.Move);
                    Refresh();
                }
            }
            if (m.Confirm) Activate();
            else if (m.Cancel && OnBack != null)
            {
                Audio?.PlayUi(UiSound.Back);
                OnBack();
            }
        }

        public void Refresh()
        {
            if (_focus >= 0)
            {
                // Inclui o título da seção logo acima do item, quando existir.
                int first = _focus > 0 && _entries[_focus - 1].IsHeader ? _focus - 1 : _focus;
                if (first < _scroll) _scroll = first;
                if (_focus >= _scroll + _visible) _scroll = _focus - _visible + 1;
            }
            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            _scroll = Mathf.Clamp(_scroll, 0, MaxScroll);
            _content.anchoredPosition = new Vector2(0, _scroll * _itemHeight);
            _bar.Set(_entries.Count > 0 ? Mathf.Min(1f, _visible / (float)_entries.Count) : 1f, MaxScroll > 0 ? _scroll / (float)MaxScroll : 0f);
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.IsHeader) continue;
                bool focused = i == _focus;
                bool enabled = e.Enabled;
                e.Background.color = focused ? UITheme.PanelLight : UITheme.Panel;
                e.Marker.enabled = focused;
                e.Label.color = !enabled ? UITheme.Disabled : focused ? Color.white : UITheme.Text;
                e.Label.fontStyle = focused ? FontStyles.Bold : FontStyles.Normal;
                e.Root.localScale = focused ? new Vector3(1.03f, 1.03f, 1f) : Vector3.one;
                if (e.Value != null)
                {
                    string v = e.GetValue();
                    e.Value.text = focused && e.OnAdjust != null ? "‹  " + v + "  ›" : v;
                    e.Value.color = enabled ? UITheme.Accent : UITheme.Disabled;
                }
            }
        }

        public void SetActive(bool on) => Root.gameObject.SetActive(on);
    }
}
