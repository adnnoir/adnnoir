using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace E7
{
    /// <summary>
    /// Petite boîte à outils pour construire l'interface par code (UGUI) avec le style de la version web :
    /// fond presque noir, texte clair, accent ambre, bandes « ruban de chantier ».
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.04f, 0.045f, 0.05f, 0.94f);
        public static readonly Color Card = new Color(0.043f, 0.05f, 0.058f, 0.97f);
        public static readonly Color Amber = new Color(1f, 0.69f, 0.125f);
        public static readonly Color TextC = new Color(0.914f, 0.925f, 0.937f);
        public static readonly Color Muted = new Color(0.545f, 0.569f, 0.592f);
        public static readonly Color Red = new Color(1f, 0.353f, 0.314f);
        public static readonly Color Green = new Color(0.44f, 0.82f, 0.55f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.12f);

        static Font font;
        public static Font Font
        {
            get
            {
                if (font) return font;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
                if (!font) try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { }
                if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "DejaVu Sans Mono", "Arial" }, 16);
                return font;
            }
        }

        public static Canvas MakeCanvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1280, 720); s.matchWidthOrHeight = 0.6f;
            go.AddComponent<GraphicRaycaster>();
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(parent, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            return c;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin = default, Vector2 offMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.offsetMin = offMin; r.offsetMax = offMax;
            return r;
        }

        public static RectTransform Fill(Transform parent, string name = "fill") => Rect(parent, name, Vector2.zero, Vector2.one);

        public static Image Panel(Transform parent, string name, Color c, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin = default, Vector2 offMax = default)
        {
            var r = Rect(parent, name, anchorMin, anchorMax, offMin, offMax);
            var img = r.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("txt", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Text LabelAt(Transform parent, string text, int size, Color color, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, TextAnchor align = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            var t = Label(parent, text, size, color, align, style);
            var r = t.rectTransform; r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax;
            return t;
        }

        /// <summary>Colonne qui empile ses enfants (VerticalLayoutGroup).</summary>
        public static RectTransform Column(Transform parent, string name, float spacing, RectOffset padding = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            if (padding != null) v.padding = padding;
            return (RectTransform)go.transform;
        }

        public static RectTransform Row(Transform parent, string name, float spacing, bool expand = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childControlHeight = true; h.childControlWidth = true; h.childForceExpandHeight = false; h.childForceExpandWidth = expand;
            return (RectTransform)go.transform;
        }

        public static LayoutElement Size(Component c, float minH = -1, float prefH = -1, float prefW = -1, float flexW = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (minH >= 0) le.minHeight = minH;
            if (prefH >= 0) le.preferredHeight = prefH;
            if (prefW >= 0) le.preferredWidth = prefW;
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        public enum BtnStyle { Primary, Secondary, Menu, Tab, Seg, Danger }

        /// <summary>Bouton texte. Le style « Menu » imite les gros intitulés du menu principal.</summary>
        public static Button Button(Transform parent, string text, Action onClick, BtnStyle style = BtnStyle.Secondary, string small = null)
        {
            var go = new GameObject("btn_" + text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            var b = go.AddComponent<Button>();
            var colors = b.colors;
            var label = Label(go.transform, text, 16, TextC, TextAnchor.MiddleCenter, FontStyle.Bold);
            var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(12, 2); lr.offsetMax = new Vector2(-12, -2);
            switch (style)
            {
                case BtnStyle.Primary:
                    img.color = Amber; label.color = new Color(0.05f, 0.05f, 0.06f); label.fontSize = 20;
                    Size(go.GetComponent<RectTransform>(), 48, 48, 200);
                    break;
                case BtnStyle.Menu:
                    img.color = new Color(0, 0, 0, 0); label.fontSize = 30; label.alignment = TextAnchor.MiddleLeft; label.color = Muted;
                    if (small != null) label.text = text + "  <size=12><color=#8b9197>" + small + "</color></size>";
                    label.supportRichText = true;
                    Size(go.GetComponent<RectTransform>(), 44, 44);
                    break;
                case BtnStyle.Tab: case BtnStyle.Seg:
                    img.color = new Color(1, 1, 1, 0.04f); label.fontSize = 13; label.color = Muted;
                    Size(go.GetComponent<RectTransform>(), 32, 32);
                    break;
                case BtnStyle.Danger:
                    img.color = new Color(1f, 0.35f, 0.3f, 0.08f); label.color = Red; label.fontSize = 14;
                    Size(go.GetComponent<RectTransform>(), 38, 38);
                    break;
                default:
                    img.color = new Color(1, 1, 1, 0.05f); label.fontSize = 15;
                    Size(go.GetComponent<RectTransform>(), 40, 40);
                    break;
            }
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = colors;
            if (style == BtnStyle.Menu) go.AddComponent<MenuHover>().label = label;
            b.onClick.AddListener(() => { AudioSys.I?.Ui(true); onClick?.Invoke(); });
            return b;
        }

        /// <summary>Met un bouton en état « choisi » (onglets, boutons à choix).</summary>
        public static void SetOn(Button b, bool on)
        {
            var img = b.GetComponent<Image>(); var t = b.GetComponentInChildren<Text>();
            img.color = on ? TextC : new Color(1, 1, 1, 0.04f);
            t.color = on ? new Color(0.04f, 0.05f, 0.06f) : Muted;
        }

        /// <summary>Choix parmi plusieurs valeurs (boutons côte à côte).</summary>
        public static RectTransform Segmented(Transform parent, string[] values, string[] labels, Func<string> get, Action<string> set)
        {
            var row = Row(parent, "seg", 2);
            var btns = new List<Button>();
            for (int i = 0; i < values.Length; i++)
            {
                string v = values[i];
                var b = Button(row, labels[i], null, BtnStyle.Seg);
                Size(b, 32, 32, Mathf.Max(70, labels[i].Length * 9 + 24));
                btns.Add(b);
                b.onClick.AddListener(() => { set(v); for (int k = 0; k < btns.Count; k++) SetOn(btns[k], values[k] == get()); });
            }
            for (int k = 0; k < btns.Count; k++) SetOn(btns[k], values[k] == get());
            row.gameObject.AddComponent<SegRefresh>().refresh = () => { for (int k = 0; k < btns.Count; k++) SetOn(btns[k], values[k] == get()); };
            return row;
        }

        public class SegRefresh : MonoBehaviour { public Action refresh; void OnEnable() { refresh?.Invoke(); } }

        public static Slider Slider(Transform parent, float min, float max, Func<float> get, Action<float> set, Func<float, string> fmt)
        {
            var row = Row(parent, "slider", 10);
            var go = new GameObject("slider", typeof(RectTransform));
            go.transform.SetParent(row, false);
            Size(go.GetComponent<RectTransform>(), 22, 22, 200);
            var bg = Panel(go.transform, "bg", new Color(1, 1, 1, 0.1f), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -3), new Vector2(0, 3));
            var fillArea = Rect(go.transform, "fillArea", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -3), new Vector2(0, 3));
            var fill = Panel(fillArea, "fill", Amber, Vector2.zero, Vector2.one);
            var handleArea = Rect(go.transform, "handleArea", Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-6, 0));
            var handle = Panel(handleArea, "handle", TextC, new Vector2(0, 0), new Vector2(0, 1), new Vector2(-6, 2), new Vector2(6, -2));
            var s = go.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.minValue = min; s.maxValue = max; s.value = get();
            var val = Label(row, fmt(get()), 14, TextC, TextAnchor.MiddleLeft);
            Size(val, 22, 22, 60);
            s.onValueChanged.AddListener(v => { set(v); val.text = fmt(v); });
            go.AddComponent<SliderRefresh>().Init(s, get, val, fmt);
            return s;
        }

        public class SliderRefresh : MonoBehaviour
        {
            Slider s; Func<float> get; Text t; Func<float, string> f;
            public void Init(Slider sl, Func<float> g, Text tx, Func<float, string> fm) { s = sl; get = g; t = tx; f = fm; }
            void OnEnable() { if (s != null) { float v = get(); s.value = v; t.text = f(v); } }
        }

        public static InputField Input(Transform parent, Func<string> get, Action<string> set, int maxLen)
        {
            var go = new GameObject("input", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0.05f);
            Size(go.GetComponent<RectTransform>(), 34, 34, 200);
            var txt = Label(go.transform, get(), 16, TextC, TextAnchor.MiddleLeft, FontStyle.Bold);
            txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one; txt.rectTransform.offsetMin = new Vector2(10, 0); txt.rectTransform.offsetMax = new Vector2(-10, 0);
            txt.supportRichText = false;
            var f = go.AddComponent<InputField>();
            f.textComponent = txt; f.characterLimit = maxLen; f.text = get();
            f.onEndEdit.AddListener(v => set(v));
            return f;
        }

        /// <summary>Bande « ruban de chantier » jaune et noir.</summary>
        public static RawImage Tape(Transform parent, float height = 10)
        {
            var go = new GameObject("tape", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var ri = go.AddComponent<RawImage>();
            ri.texture = TapeTex(); ri.uvRect = new Rect(0, 0, 20, 1);
            Size(ri, height, height);
            return ri;
        }

        static Texture2D tape;
        static Texture2D TapeTex()
        {
            if (tape) return tape;
            tape = new Texture2D(32, 8) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 8; y++) for (int x = 0; x < 32; x++) tape.SetPixel(x, y, ((x + y) % 32) < 16 ? Amber : new Color(0.06f, 0.06f, 0.07f));
            tape.Apply();
            return tape;
        }

        /// <summary>Barre horizontale (santé, statistiques d'arme).</summary>
        public static Image Bar(Transform parent, float value, Color c, float height = 6)
        {
            var go = new GameObject("bar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Size(go.GetComponent<RectTransform>(), height, height, 200, 1);
            var bg = go.AddComponent<Image>(); bg.color = new Color(1, 1, 1, 0.08f);
            var fill = Panel(go.transform, "fill", c, Vector2.zero, new Vector2(Mathf.Clamp01(value), 1));
            return fill;
        }

        public static void SetBar(Image fill, float v) { var r = fill.rectTransform; r.anchorMax = new Vector2(Mathf.Clamp01(v), 1); }
    }

    /// <summary>Effet de survol des gros boutons du menu principal (le texte s'éclaire et glisse).</summary>
    public class MenuHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Text label; bool over;
        public void OnPointerEnter(PointerEventData e) { over = true; AudioSys.I?.Ui(false); }
        public void OnPointerExit(PointerEventData e) { over = false; }
        void Update()
        {
            if (!label) return;
            label.color = Color.Lerp(label.color, over ? UIKit.TextC : UIKit.Muted, Time.unscaledDeltaTime * 12);
            var r = label.rectTransform; var o = r.offsetMin; o.x = Mathf.Lerp(o.x, over ? 24 : 16, Time.unscaledDeltaTime * 12); r.offsetMin = o;
        }
    }
}
