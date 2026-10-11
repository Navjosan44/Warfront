using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// Base class for screens.
    ///
    /// Provides the common scaffold (dim backdrop, header bar with title and credit readout, content
    /// area, footer with Back) so every screen looks consistent and the repeated layout work lives in
    /// exactly one place.
    /// </summary>
    public abstract class UIScreen
    {
        protected GameContext Context;
        protected UIRoot Ui;
        protected UITheme Theme;

        protected RectTransform Root;
        protected RectTransform Content;
        protected RectTransform Footer;
        protected Text HeaderTitle;
        protected Text CreditText;
        protected Text FooterHint;

        public bool IsVisible { get { return Root != null && Root.gameObject.activeSelf; } }
        public abstract string Title { get; }
        protected virtual string BackLabel { get { return "Back"; } }
        protected virtual bool ShowCredits { get { return true; } }

        public void Build(GameContext context, UIRoot ui, UITheme theme, RectTransform parent)
        {
            Context = context;
            Ui = ui;
            Theme = theme;

            Image backdrop = UIFactory.CreatePanel(parent, GetType().Name + "Root", new Color(0f, 0f, 0f, 0.55f));
            Root = backdrop.rectTransform;
            UIFactory.Stretch(Root, 0f, 0f, 0f, 0f);

            // Header
            Image header = UIFactory.CreatePanel(Root, "Header", Theme.PanelDark);
            UIFactory.SetAnchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 96f), Vector2.zero);
            HeaderTitle = UIFactory.CreateText(header.transform, "Title", Title.ToUpperInvariant(), 44, Theme.Primary, TextAnchor.MiddleLeft);
            UIFactory.Stretch(HeaderTitle.rectTransform, 42f, 320f, 4f, 4f);

            if (ShowCredits)
            {
                CreditText = UIFactory.CreateText(header.transform, "Credits", string.Empty, 30, Theme.TextDim, TextAnchor.MiddleRight);
                UIFactory.Stretch(CreditText.rectTransform, 900f, 42f, 4f, 4f);
            }

            // Content + footer
            Content = UIFactory.CreateRect(Root, "Content");
            UIFactory.Stretch(Content, 42f, 42f, 112f, 104f);

            Footer = UIFactory.CreateRect(Root, "Footer");
            UIFactory.SetAnchor(Footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 92f), Vector2.zero);

            FooterHint = UIFactory.CreateText(Footer, "Hint", string.Empty, 24, Theme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Stretch(FooterHint.rectTransform, 46f, 420f, 0f, 0f);

            Button back = UIFactory.CreateButton(Footer, "BackButton", BackLabel, Theme.PanelLight, OnBackPressed, 30);
            UIFactory.SetAnchor(back.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(260f, 66f), new Vector2(-8f, 0f));

            OnBuild();
            Root.gameObject.SetActive(false);
        }

        protected abstract void OnBuild();

        public virtual void Show()
        {
            if (Root == null) return;
            Root.gameObject.SetActive(true);
            Refresh();
        }

        public virtual void Hide()
        {
            if (Root == null) return;
            Root.gameObject.SetActive(false);
        }

        public virtual void Tick(float deltaTime) { }

        /// <summary>Rebuilds dynamic content. Called on show and whenever data changes.</summary>
        protected virtual void Refresh() { }

        protected virtual void OnBackPressed()
        {
            if (Context == null || Context.Audio == null) return;
            Context.Audio.PlayUi(AudioCue.Back);
            if (Context.Flow != null) Context.Flow.HandleBackNavigation();
        }

        protected void SetHeaderSubtitle(string text)
        {
            if (FooterHint != null) FooterHint.text = text;
        }

        protected void UpdateCreditReadout()
        {
            if (CreditText == null || Context == null || Context.Progression == null) return;
            ProgressionModel model = Context.Progression.Model;
            CreditText.text = "LVL " + model.Level + "   " + model.Credits.ToString("N0") + " CR   " + model.Materials + " MAT";
        }

        /// <summary>Destroys a container's children so a list can be rebuilt.</summary>
        protected static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        /// <summary>Shared "empty list" placeholder so screens never render a blank box.</summary>
        protected Text CreateEmptyLabel(RectTransform parent, string message)
        {
            Text text = UIFactory.CreateText(parent, "Empty", message, 30, Theme.TextDim, TextAnchor.MiddleCenter);
            UIFactory.Stretch(text.rectTransform, 20f, 20f, 80f, 80f);
            return text;
        }
    }
}
