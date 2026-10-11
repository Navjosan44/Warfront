using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>Shared palette + font for the whole interface, derived from the world art identity.</summary>
    public class UITheme
    {
        public Font Font;
        public Sprite Sprite;

        public Color Screen = new Color(0.04f, 0.05f, 0.05f, 0.96f);
        public Color Panel = new Color(0.09f, 0.10f, 0.10f, 0.97f);
        public Color PanelLight = new Color(0.14f, 0.15f, 0.15f, 0.97f);
        public Color PanelDark = new Color(0.06f, 0.07f, 0.07f, 0.94f);
        public Color Primary = new Color(0.62f, 0.78f, 0.42f, 1f);
        public Color PrimaryDim = new Color(0.30f, 0.38f, 0.22f, 1f);
        public Color Text = new Color(0.93f, 0.94f, 0.91f, 1f);
        public Color TextDim = new Color(0.60f, 0.63f, 0.60f, 1f);
        public Color Warning = new Color(0.95f, 0.70f, 0.26f, 1f);
        public Color Danger = new Color(0.88f, 0.30f, 0.24f, 1f);
        public Color Accent = new Color(0.45f, 0.70f, 0.95f, 1f);
        public Color Health = new Color(0.72f, 0.25f, 0.22f, 1f);
        public Color Armor = new Color(0.42f, 0.62f, 0.85f, 1f);

        public static UITheme Create(GameAssets assets)
        {
            UITheme theme = new UITheme();
            if (assets != null)
            {
                theme.Font = assets.UIFont;
                theme.Sprite = assets.WhiteSprite;
            }
            if (theme.Font == null)
            {
                try
                {
                    theme.Font = Font.CreateDynamicFontFromOSFont("Arial", 24);
                }
                catch (Exception)
                {
                    theme.Font = null;
                }
            }
            return theme;
        }
    }

    /// <summary>
    /// Builds every widget in code.
    ///
    /// WARFRONT ships no UI prefabs or sprites: the interface is constructed from primitives at boot.
    /// That keeps the repository source-only, makes the whole layout reviewable in diffs, and guarantees
    /// the interface scales across phone, tablet and desktop aspect ratios.
    /// </summary>
    public static class UIFactory
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        public static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        public static Image CreateImage(Transform parent, string name, Color color, bool raycastTarget)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text CreateText(Transform parent, string name, string value, int fontSize, Color color, TextAnchor anchor)
        {
            RectTransform rect = CreateRect(parent, name);
            Text text = rect.gameObject.AddComponent<Text>();
            if (UITheme != null && UITheme.Font != null) text.font = UITheme.Font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Active theme, set by UIRoot at boot so widget helpers can share the font.</summary>
        public static UITheme UITheme;

        public static Button CreateButton(Transform parent, string name, string label, Color color, UnityAction onClick, int fontSize)
        {
            Image background = CreatePanel(parent, name, color);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            Text text = CreateText(background.transform, "Label", label, fontSize,
                UITheme != null ? UITheme.Text : Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 12f, 12f, 4f, 4f);

            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public static Slider CreateSlider(Transform parent, string name, float min, float max, float value, bool wholeNumbers, UnityAction<float> onChanged)
        {
            RectTransform root = CreateRect(parent, name);

            Image track = CreatePanel(root, "Track", new Color(0.22f, 0.23f, 0.23f, 1f));
            Stretch(track.rectTransform, 0f, 0f, 14f, 14f);

            RectTransform fillArea = CreateRect(root, "FillArea");
            Stretch(fillArea, 0f, 0f, 14f, 14f);
            Image fill = CreatePanel(fillArea, "Fill", UITheme != null ? UITheme.Primary : Color.green);
            Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform handleArea = CreateRect(root, "HandleArea");
            Stretch(handleArea, 0f, 0f, 0f, 0f);
            Image handle = CreatePanel(handleArea, "Handle", UITheme != null ? UITheme.Text : Color.white);
            handle.rectTransform.sizeDelta = new Vector2(26f, 26f);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string name, string label, bool value, UnityAction<bool> onChanged)
        {
            RectTransform root = CreateRect(parent, name);

            Image box = CreatePanel(root, "Box", new Color(0.20f, 0.21f, 0.21f, 1f));
            SetAnchor(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(44f, 44f), new Vector2(0f, 0f));

            Image check = CreateImage(box.transform, "Check", UITheme != null ? UITheme.Primary : Color.green, false);
            Stretch(check.rectTransform, 8f, 8f, 8f, 8f);

            Text text = CreateText(root, "Label", label, 30,
                UITheme != null ? UITheme.Text : Color.white, TextAnchor.MiddleLeft);
            SetAnchor(text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero);
            text.rectTransform.offsetMin = new Vector2(58f, 0f);
            text.rectTransform.offsetMax = new Vector2(0f, 0f);

            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = value;
            if (onChanged != null) toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        /// <summary>Horizontal bar (health/armour/progress). Returns the fill image to drive fillAmount.</summary>
        public static Image CreateBar(Transform parent, string name, Color fillColor, Color backColor)
        {
            Image background = CreatePanel(parent, name, backColor);
            Image fill = CreateImage(background.transform, "Fill", fillColor, false);
            RectTransform rect = fill.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }

        public static ScrollRect CreateScrollView(Transform parent, string name, out RectTransform content)
        {
            Image background = CreatePanel(parent, name, new Color(0.07f, 0.08f, 0.08f, 0.85f));
            ScrollRect scroll = background.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = CreateRect(background.transform, "Viewport");
            Stretch(viewport, 6f, 6f, 6f, 6f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            return scroll;
        }

        public static RectTransform CreateRow(Transform parent, string name, float height, float spacing)
        {
            RectTransform row = CreateRect(parent, name);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return row;
        }

        // ------------------------------------------------------------------ helpers

        public static RectTransform Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static RectTransform SetAnchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        public static RectTransform TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            return SetAnchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(width, height), new Vector2(x, -y));
        }

        /// <summary>
        /// Full width horizontal band pinned to the top of its parent.
        ///
        /// Stretching vertically and then writing offsets is the single most common layout bug in this
        /// codebase (a stretched anchor with equal offsetMin/offsetMax.y collapses the rect to zero
        /// height), so rows and panels are positioned through this helper instead.
        /// </summary>
        public static RectTransform CreateBandTop(Transform parent, string name, float top, float height, float inset)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(inset, -top - height);
            rect.offsetMax = new Vector2(-inset, -top);
            return rect;
        }

        /// <summary>Full width horizontal band pinned to the bottom of its parent.</summary>
        public static RectTransform CreateBandBottom(Transform parent, string name, float bottom, float height, float inset)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(inset, bottom);
            rect.offsetMax = new Vector2(-inset, bottom + height);
            return rect;
        }

        /// <summary>Stretches a rect across the parent while applying per-side insets.</summary>
        public static RectTransform Inset(RectTransform rect, float left, float right, float top, float bottom)
        {
            return Stretch(rect, left, right, top, bottom);
        }

        public static RectTransform Centered(RectTransform rect, float width, float height)
        {
            return SetAnchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(width, height), Vector2.zero);
        }

        public static Text RowLabel(Transform row, string value, int fontSize, Color color, float preferredWidth)
        {
            Text text = CreateText(row, "Text", value, fontSize, color, TextAnchor.MiddleLeft);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = preferredWidth;
            element.minWidth = preferredWidth;
            return text;
        }

        public static Text RowValue(Transform row, string value, int fontSize, Color color)
        {
            Text text = CreateText(row, "Value", value, fontSize, color, TextAnchor.MiddleRight);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 220f;
            return text;
        }

        /// <summary>Rarity colour for item and weapon labels.</summary>
        public static Color RarityColor(Rarity rarity)
        {
            return RarityRules.Color(rarity);
        }
    }
}
