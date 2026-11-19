using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UltimoRound.UI
{
    public static class UITheme
    {
        public static readonly Color Background = new Color(0.035f, 0.045f, 0.085f, 0.94f);
        public static readonly Color Panel = new Color(0.07f, 0.09f, 0.16f, 0.92f);
        public static readonly Color PanelLight = new Color(0.12f, 0.15f, 0.26f, 0.95f);
        public static readonly Color Accent = new Color(1f, 0.62f, 0.12f);
        public static readonly Color AccentSoft = new Color(1f, 0.62f, 0.12f, 0.25f);
        public static readonly Color Text = new Color(0.93f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.67f, 0.8f);
        public static readonly Color Disabled = new Color(0.4f, 0.42f, 0.5f);
        public static readonly Color P1 = new Color(0.25f, 0.6f, 1f);
        public static readonly Color P2 = new Color(1f, 0.3f, 0.3f);
        public static readonly Color Health = new Color(1f, 0.82f, 0.18f);
        public static readonly Color HealthLow = new Color(1f, 0.32f, 0.2f);
        public static readonly Color HealthTrail = new Color(1f, 1f, 1f, 0.85f);
        public static readonly Color Energy = new Color(0.25f, 0.85f, 1f);
        public static readonly Color EnergyFull = new Color(1f, 0.45f, 0.95f);

        public static Color PlayerColor(int i) => i == 0 ? P1 : P2;
    }

    /// <summary>Barra de rolagem simples: mostra a posição e aceita clique/arraste com o mouse.</summary>
    public sealed class ScrollBarView
    {
        public readonly RectTransform Track;
        readonly RectTransform _thumb;
        public System.Action<float> OnDrag; // 0 = topo, 1 = fim

        public ScrollBarView(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float height)
        {
            var track = UIFactory.Image("BarraRolagem", parent, new Color(1, 1, 1, 0.12f), true);
            Track = track.rectTransform;
            UIFactory.Place(Track, anchor, pivot, pos, new Vector2(16, height));
            var thumb = UIFactory.Image("Cursor", Track, UITheme.Accent);
            _thumb = thumb.rectTransform;
            var trigger = Track.gameObject.AddComponent<EventTrigger>();
            foreach (var type in new[] { EventTriggerType.PointerDown, EventTriggerType.Drag })
            {
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(e => Pointer((PointerEventData)e));
                trigger.triggers.Add(entry);
            }
        }

        void Pointer(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Track, e.position, e.pressEventCamera, out var local)) return;
            float t = 1f - Mathf.InverseLerp(Track.rect.yMin, Track.rect.yMax, local.y);
            OnDrag?.Invoke(Mathf.Clamp01(t));
        }

        /// <param name="visibleFraction">Fração visível do conteúdo (0..1).</param>
        /// <param name="position">Posição da rolagem (0 = topo, 1 = fim).</param>
        public void Set(float visibleFraction, float position)
        {
            bool needed = visibleFraction < 0.999f;
            Track.gameObject.SetActive(needed);
            if (!needed) return;
            float size = Mathf.Clamp(visibleFraction, 0.08f, 1f);
            float top = 1f - (1f - size) * Mathf.Clamp01(position);
            _thumb.anchorMin = new Vector2(0, top - size);
            _thumb.anchorMax = new Vector2(1, top);
            _thumb.offsetMin = _thumb.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Criação de elementos de interface por código (sem prefabs frágeis).</summary>
    public static class UIFactory
    {
        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // Expand: a área de referência 1920×1080 sempre cabe inteira (16:10 ganha altura, ultrawide ganha laterais),
            // sem distorcer nem cortar o HUD.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            // Navegação por teclado/controle é feita pelo MenuList; o módulo só trata o mouse.
            module.move = null;
            module.submit = null;
            module.cancel = null;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            return t;
        }

        /// <summary>Barra horizontal por âncora (não depende de sprite). fill 0..1; reverse preenche da direita.</summary>
        public static void SetFill(RectTransform fill, float amount, bool reverse)
        {
            amount = Mathf.Clamp01(amount);
            if (reverse)
            {
                fill.anchorMin = new Vector2(1f - amount, 0);
                fill.anchorMax = new Vector2(1, 1);
            }
            else
            {
                fill.anchorMin = new Vector2(0, 0);
                fill.anchorMax = new Vector2(amount, 1);
            }
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        public static Outline Outline(Graphic g, Color c, float d = 2f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(d, -d);
            return o;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
