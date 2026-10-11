using UnityEngine;
using UnityEngine.UI;
using Warfront.Core;
using Warfront.Game;

namespace Warfront.UI
{
    /// <summary>
    /// Short onboarding flow for the tutorial contract.
    ///
    /// Each step advances the moment the player actually performs the action (detected from the real
    /// input flags and the event bus, not a timer), so it teaches the real controls rather than
    /// narrating them. It is only armed for missions flagged IsTutorial and can be skipped instantly.
    /// </summary>
    public class TutorialController
    {
        private enum Step
        {
            None = -1,
            Move = 0,
            Camera = 1,
            Aim = 2,
            Fire = 3,
            Reload = 4,
            Loot = 5,
            Objective = 6,
            Extraction = 7,
            Finished = 8
        }

        private static readonly string[] Titles =
        {
            "MOVEMENT",
            "CAMERA",
            "AIMING",
            "FIRE",
            "RELOAD",
            "LOOT",
            "OBJECTIVES",
            "EXTRACTION"
        };

        private readonly GameContext _context;
        private UITheme _theme;
        private RectTransform _root;
        private Text _stepText;
        private Text _titleText;
        private Text _bodyText;
        private Image _progressFill;
        private bool _built;
        private bool _active;
        private Step _step = Step.None;
        private float _stepHold;

        public bool IsActive { get { return _active; } }

        public TutorialController(GameContext context)
        {
            _context = context;
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            if (_built) return;
            UIRoot ui = _context != null ? _context.UI : null;
            if (ui == null || ui.OverlayLayer == null) return;

            _theme = ui.Theme;
            Image panel = UIFactory.CreatePanel(ui.OverlayLayer, "Tutorial", new Color(0.04f, 0.05f, 0.05f, 0.86f));
            _root = panel.rectTransform;
            UIFactory.SetAnchor(_root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(520f, 210f), new Vector2(30f, 0f));

            _stepText = UIFactory.CreateText(_root, "Step", string.Empty, 20, _theme.Accent, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_stepText.rectTransform, 20f, 10f, 340f, 24f);

            _titleText = UIFactory.CreateText(_root, "Title", string.Empty, 32, _theme.Primary, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_titleText.rectTransform, 20f, 38f, 340f, 40f);

            _bodyText = UIFactory.CreateText(_root, "Body", string.Empty, 24, _theme.Text, TextAnchor.UpperLeft);
            UIFactory.TopLeft(_bodyText.rectTransform, 20f, 82f, 480f, 86f);

            Image bar = UIFactory.CreatePanel(_root, "Progress", new Color(0.10f, 0.11f, 0.11f, 0.9f));
            UIFactory.SetAnchor(bar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(300f, 10f), new Vector2(20f, 34f));
            _progressFill = UIFactory.CreateImage(bar.transform, "Fill", _theme.Primary, false);
            UIFactory.Stretch(_progressFill.rectTransform, 1f, 1f, 1f, 1f);
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _progressFill.fillAmount = 0f;

            Button skip = UIFactory.CreateButton(_root, "Skip", "SKIP", _theme.PanelLight, Skip, 22);
            UIFactory.SetAnchor(skip.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(110f, 40f), new Vector2(-16f, 18f));

            GameEvents.WeaponFired += OnWeaponFired;
            GameEvents.LootCollected += OnLootCollected;
            GameEvents.ObjectiveCompleted += OnObjectiveCompleted;
            GameEvents.ExtractionChanged += OnExtractionChanged;

            _root.gameObject.SetActive(false);
            _built = true;
        }

        // ------------------------------------------------------------------ state

        public void Begin(MissionDef def)
        {
            Build();
            if (!_built)
            {
                _active = false;
                return;
            }

            bool tutorial = def != null && def.IsTutorial;
            _active = tutorial;
            _root.gameObject.SetActive(tutorial);
            if (!tutorial)
            {
                _step = Step.None;
                return;
            }

            SetStep(Step.Move);
        }

        public void End()
        {
            _active = false;
            _step = Step.None;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public void Skip()
        {
            if (!_active) return;
            if (_context != null && _context.Audio != null) _context.Audio.PlayUi(AudioCue.Back);
            End();
        }

        private void SetStep(Step step)
        {
            _step = step;
            _stepHold = 0f;

            if (_step == Step.Finished)
            {
                if (_root != null) _root.gameObject.SetActive(false);
                _active = false;
                if (_context != null && _context.Audio != null) _context.Audio.PlayUi(AudioCue.ObjectiveComplete);
                GameEvents.RaiseNotice("Training complete - get to the extraction pad");
                return;
            }

            int index = (int)_step;
            if (_stepText != null) _stepText.text = "TRAINING   STEP " + (index + 1) + " / " + Titles.Length;
            if (_titleText != null) _titleText.text = Titles[index];
            if (_bodyText != null) _bodyText.text = Instruction(index);
            if (_progressFill != null) _progressFill.fillAmount = (float)index / Titles.Length;

            if (_context != null && _context.Audio != null) _context.Audio.PlayUi(AudioCue.Click);
        }

        /// <summary>Turns the action the step is teaching into a single line of on-screen instruction.</summary>
        private string Instruction(int index)
        {
            bool touch = _context != null && _context.Platform != null && _context.Platform.IsTouchDevice;

            switch ((Step)index)
            {
                case Step.Move:
                    return touch
                        ? "Drag the LEFT thumbstick to walk. Push it to its edge to sprint."
                        : "Move with WASD. Hold SHIFT to sprint.";
                case Step.Camera:
                    return touch
                        ? "Swipe the RIGHT side of the screen to look around."
                        : "Look around with the mouse.";
                case Step.Aim:
                    return touch
                        ? "Hold AIM to raise the weapon and tighten your spread."
                        : "Hold RIGHT MOUSE to aim down sight.";
                case Step.Fire:
                    return touch
                        ? "Tap FIRE to engage. Keep the trigger down for automatic weapons."
                        : "LEFT MOUSE fires. Tap rather than hold for single shot weapons.";
                case Step.Reload:
                    return touch
                        ? "Tap RELOAD when the magazine runs dry. Ammunition comes out of your pack."
                        : "Press R to reload. Ammunition comes out of your pack.";
                case Step.Loot:
                    return touch
                        ? "Walk up to a container and tap USE to search it for supplies."
                        : "Walk up to a container and press E to search it for supplies.";
                case Step.Objective:
                    return "Work through the objective list on the right. Marked crates hold mission items.";
                case Step.Extraction:
                    return "Carrying the mission items is not enough - reach the pad and hold the zone until the " +
                           "extraction counter finishes.";
                default:
                    return string.Empty;
            }
        }

        // ------------------------------------------------------------------- tick

        public void Tick(float deltaTime)
        {
            if (!_active || _built == false) return;
            InputService input = _context != null ? _context.Input : null;
            if (input == null) return;

            // A short hold keeps a single stray input from blowing through consecutive steps.
            _stepHold += deltaTime;
            bool armed = _stepHold > 0.35f;

            switch (_step)
            {
                case Step.Move:
                    if (armed && input.AnyMovementInput) SetStep(Step.Camera);
                    break;

                case Step.Camera:
                    if (armed && input.LookActive) SetStep(Step.Aim);
                    break;

                case Step.Aim:
                    if (armed && input.AimHeld) SetStep(Step.Fire);
                    break;

                case Step.Fire:
                    // Advanced by the WeaponFired event.
                    break;

                case Step.Reload:
                    if (input.ReloadPressed) SetStep(Step.Loot);
                    break;

                case Step.Loot:
                    // Advanced by the LootCollected event.
                    break;

                case Step.Objective:
                    // Advanced by the ObjectiveCompleted event.
                    break;

                case Step.Extraction:
                    // Advanced by the ExtractionChanged event.
                    break;

                default:
                    break;
            }
        }

        private void OnWeaponFired()
        {
            if (!_active || _step != Step.Fire) return;
            SetStep(Step.Reload);
        }

        private void OnLootCollected(ItemStack stack, int value)
        {
            if (!_active || _step != Step.Loot) return;
            SetStep(Step.Objective);
        }

        private void OnObjectiveCompleted(ObjectiveProgress progress)
        {
            if (!_active || _step != Step.Objective) return;
            SetStep(Step.Extraction);
        }

        private void OnExtractionChanged(ExtractionStatus status)
        {
            if (!_active) return;
            if (_step != Step.Extraction) return;
            if (status.State == ExtractionState.Countdown || status.State == ExtractionState.Complete)
            {
                SetStep(Step.Finished);
            }
        }
    }
}
