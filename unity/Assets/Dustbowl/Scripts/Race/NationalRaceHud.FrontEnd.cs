using Dustbowl.Bike;
using Dustbowl.Camera;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dustbowl.Race
{
    /// <summary>
    /// Front-end plates drawn by the race HUD: the opening event-setup screen, the
    /// Escape pause overlay and the results action row. Buttons are hit-tested
    /// against the Input System mouse (not IMGUI events) so clicking works under
    /// the project's Input-System-only handling; every action also has keyboard
    /// and gamepad shortcuts polled by <see cref="GameFlowController"/>.
    /// </summary>
    public sealed partial class NationalRaceHud
    {
        private const float ButtonHeight = 46f;

        [SerializeField] private GameFlowController flow;

        private static readonly Color AmberHover = new(1f, .78f, .32f);
        private static readonly Color GhostFill = new(.890f, .745f, .525f, .07f);
        private static readonly Color GhostHover = new(.890f, .745f, .525f, .18f);
        private static readonly Color Dim = new(.043f, .051f, .063f, .58f);

        private GUIStyle hero;
        private GUIStyle blurb;
        private GUIStyle cardName;
        private GUIStyle buttonSmall;

        private Vector2 pointer = new(-1f, -1f);
        private bool pointerClicked;

        public bool HasFlowController => flow != null;

        public void Configure(
            NationalRaceManager manager,
            ArcadeBikeController controller,
            GameFlowController flowController,
            Font display,
            Font labels)
        {
            Configure(manager, controller, display, labels);
            flow = flowController;
        }

        // ------------------------------------------------------------ pointer

        private void UpdatePointer(float scale)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                pointer = new Vector2(-1f, -1f);
                pointerClicked = false;
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            pointer = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
            // OnGUI runs several passes per frame; only the repaint pass may act.
            pointerClicked = Event.current.type == EventType.Repaint && mouse.leftButton.wasPressedThisFrame;
        }

        /// <summary>Draws a web-style cut-corner button and reports a click on it this frame.</summary>
        private bool MenuButton(Rect rect, string text, string hint, bool primary, GUIStyle style = null)
        {
            bool hover = rect.Contains(pointer);
            if (primary)
            {
                DrawCutRect(rect, hover ? AmberHover : Amber);
            }
            else
            {
                DrawPanel(rect, hover ? GhostHover : GhostFill);
            }

            GUI.Label(rect, text, Center(style ?? buttonSmall, primary ? Ink : Paper));
            if (!string.IsNullOrEmpty(hint))
            {
                GUI.Label(new Rect(rect.x, rect.yMax + 3f, rect.width, 14f), hint, Center(label, LabelDim));
            }

            return hover && pointerClicked;
        }

        /// <summary>Segmented selector; returns the index clicked this frame or -1.</summary>
        private int Segmented(Rect rect, string[] options, int selected)
        {
            int clicked = -1;
            float gap = 6f;
            float width = (rect.width - gap * (options.Length - 1)) / options.Length;
            for (int index = 0; index < options.Length; index++)
            {
                Rect cell = new(rect.x + index * (width + gap), rect.y, width, rect.height);
                if (MenuButton(cell, options[index], null, index == selected))
                {
                    clicked = index;
                }
            }

            return clicked;
        }

        // ------------------------------------------------------- setup screen

        private void DrawSetupScreen(float width, float height)
        {
            RaceEventDefinition selected = flow.SelectedEvent;
            int round = flow.SelectedEventIndex + 1;
            string courseName = selected != null ? selected.Name.ToUpperInvariant() : "—";

            float panelWidth = 700f;
            float panelHeight = 636f;
            Rect panel = new((width - panelWidth) * .5f, (height - panelHeight) * .5f, panelWidth, panelHeight);
            DrawPanel(panel, new Color(Ink.r, Ink.g, Ink.b, .90f));

            float x = panel.x + 44f;
            float inner = panel.width - 88f;
            float y = panel.y + 28f;

            GUI.Label(new Rect(x, y, inner, 16f),
                $"NATIONAL SERIES  ·  ROUND {round} OF {RaceEventCatalog.Count}  —  {courseName}",
                Left(eyebrow, Ochre));
            y += 18f;
            ShadowedLabel(new Rect(x - 3f, y, inner, 104f), "DUSTBOWL", hero, Sand, 5f);
            y += 104f;
            GUI.Label(new Rect(x, y, inner, 44f), selected != null ? selected.Blurb : string.Empty, blurb);
            y += 52f;
            Rule(new Rect(x, y, inner, 1f));
            y += 14f;

            // Event calendar: one card per catalogue entry, the selected one lit.
            GUI.Label(new Rect(x, y, inner, 14f), "EVENT", labelLeft);
            y += 18f;
            float cardWidth = Mathf.Min(220f, (inner - 10f * (RaceEventCatalog.Count - 1)) / RaceEventCatalog.Count);
            for (int index = 0; index < RaceEventCatalog.Count; index++)
            {
                RaceEventDefinition entry = RaceEventCatalog.Events[index];
                Rect card = new(x + index * (cardWidth + 10f), y, cardWidth, 64f);
                bool isSelected = index == flow.SelectedEventIndex;
                bool hover = card.Contains(pointer);
                DrawPanel(card, isSelected ? new Color(Amber.r, Amber.g, Amber.b, .16f) : (hover ? GhostHover : GhostFill));
                if (isSelected)
                {
                    Color previous = GUI.color;
                    GUI.color = Amber;
                    GUI.DrawTexture(new Rect(card.x, card.y, 3f, card.height - PanelCut), white);
                    GUI.color = previous;
                }

                GUI.Label(new Rect(card.x + 16f, card.y + 10f, card.width - 24f, 28f), entry.Name.ToUpperInvariant(),
                    Left(cardName, isSelected ? Amber : Paper));
                GUI.Label(new Rect(card.x + 16f, card.y + 40f, card.width - 24f, 14f),
                    $"ROUND {index + 1}  ·  {entry.Laps} LAPS  ·  {entry.Tag.ToUpperInvariant()}", labelLeft);
                if (hover && pointerClicked)
                {
                    flow.SelectEvent(index);
                }
            }

            // Event facts sit beside the cards.
            float factsX = x + RaceEventCatalog.Count * (cardWidth + 10f) + 14f;
            float factsWidth = inner - (factsX - x);
            if (selected != null && factsWidth > 180f)
            {
                float column = factsWidth / 3f;
                Stat(new Rect(factsX, y + 6f, column, 48f), "LAPS", selected.Laps.ToString(), Amber, numeral);
                Stat(new Rect(factsX + column, y + 6f, column, 48f), "RIDERS", selected.Riders.ToString(), Paper, numeral);
                Stat(new Rect(factsX + column * 2f, y + 6f, column, 48f), "SERIES", "NATIONAL", Paper, numeral);
            }

            y += 64f + 16f;
            Rule(new Rect(x, y, inner, 1f));
            y += 14f;

            // Throttle scheme.
            GUI.Label(new Rect(x, y, 150f, 14f), "THROTTLE", labelLeft);
            GUI.Label(new Rect(x, y, inner, 14f), "T  ·  GAMEPAD Y", Right(monoRight, LabelDim));
            y += 18f;
            int throttleClick = Segmented(new Rect(x, y, inner, ButtonHeight),
                new[] { "AUTO", "MANUAL" }, flow.ThrottleMode == ThrottleMode.Auto ? 0 : 1);
            if (throttleClick >= 0)
            {
                flow.SetThrottleMode(throttleClick == 0 ? ThrottleMode.Auto : ThrottleMode.Manual);
            }

            y += ButtonHeight + 6f;
            GUI.Label(new Rect(x, y, inner, 14f),
                flow.ThrottleMode == ThrottleMode.Auto
                    ? "AUTO: THE BIKE DRIVES ITSELF  ·  YOU STEER AND BRAKE  ·  HOLDING BRAKE CUTS THE THROTTLE"
                    : "MANUAL: W / UP / RIGHT TRIGGER ACCELERATES  ·  S / DOWN / LEFT TRIGGER BRAKES",
                labelLeft);
            y += 24f;

            // Camera.
            GUI.Label(new Rect(x, y, 150f, 14f), "CAMERA", labelLeft);
            GUI.Label(new Rect(x, y, inner, 14f), "C / V  ·  RB / LB", Right(monoRight, LabelDim));
            y += 18f;
            int cameraClick = Segmented(new Rect(x, y, inner, ButtonHeight),
                new[] { "CHASE", "CLOSE", "OVERHEAD" }, (int)flow.CameraMode);
            if (cameraClick >= 0)
            {
                flow.SetCameraMode((CameraMode)cameraClick);
            }

            y += ButtonHeight + 18f;
            Rule(new Rect(x, y, inner, 1f));
            y += 18f;

            // Start row.
            Rect start = new(x, y, 250f, 52f);
            if (MenuButton(start, "DROP IN", "ENTER  ·  GAMEPAD A", true, button))
            {
                flow.StartRace();
            }

            Rect quit = new(panel.xMax - 44f - 150f, y, 150f, 52f);
            if (MenuButton(quit, "QUIT", "Q", false))
            {
                flow.Quit();
            }

            GUI.Label(new Rect(start.xMax + 18f, y, quit.x - start.xMax - 36f, 52f),
                "STEER  A/D · LEFT STICK\nBRAKE  S · LT      AIR  R/F + Q/E      RESET  BACKSPACE",
                labelLeft);
        }

        // ------------------------------------------------------ pause overlay

        private void DrawPauseOverlay(float width, float height)
        {
            Color previous = GUI.color;
            GUI.color = Dim;
            GUI.DrawTexture(new Rect(0f, 0f, width, height), white);
            GUI.color = previous;

            float panelWidth = 440f;
            float panelHeight = 424f;
            Rect panel = new((width - panelWidth) * .5f, (height - panelHeight) * .5f, panelWidth, panelHeight);
            DrawPanel(panel, new Color(Ink.r, Ink.g, Ink.b, .92f));

            float x = panel.x + 40f;
            float inner = panel.width - 80f;
            float y = panel.y + 26f;
            GUI.Label(new Rect(x, y, inner, 16f), "ENGINE IDLING", Left(eyebrow, Ochre));
            y += 20f;
            ShadowedLabel(new Rect(x - 2f, y, inner, 76f), "PAUSED", titleDisplay, Sand, 4f);
            y += 84f;
            Rule(new Rect(x, y, inner, 1f));
            y += 16f;

            if (MenuButton(new Rect(x, y, inner, ButtonHeight), "RESUME", "ESC  ·  ENTER  ·  START", true))
            {
                flow.Resume();
            }

            y += ButtonHeight + 22f;
            if (MenuButton(new Rect(x, y, inner, ButtonHeight), "RESTART RACE", "R  ·  GAMEPAD X", false))
            {
                flow.RestartRace();
            }

            y += ButtonHeight + 22f;
            if (MenuButton(new Rect(x, y, inner, ButtonHeight), "RETURN TO MENU", "M  ·  GAMEPAD B", false))
            {
                flow.ReturnToMenu();
            }

            y += ButtonHeight + 22f;
            if (MenuButton(new Rect(x, y, inner, ButtonHeight), "QUIT", "Q", false))
            {
                flow.Quit();
            }
        }

        // ----------------------------------------------------- results row

        private void DrawResultsActions(Rect row)
        {
            if (flow == null)
            {
                DrawCutRect(new Rect(row.x, row.y, 236f, row.height), Amber);
                GUI.Label(new Rect(row.x, row.y, 236f, row.height), "RACE AGAIN", Center(button, Ink));
                return;
            }

            float gap = 12f;
            float quitWidth = 110f;
            float width = (row.width - quitWidth - gap * 2f) * .5f;
            if (MenuButton(new Rect(row.x, row.y, width, row.height), "RACE AGAIN", "ENTER  ·  GAMEPAD A", true))
            {
                flow.RestartRace();
            }

            if (MenuButton(new Rect(row.x + width + gap, row.y, width, row.height), "RETURN TO MENU", "M  ·  GAMEPAD B", false))
            {
                flow.ReturnToMenu();
            }

            if (MenuButton(new Rect(row.xMax - quitWidth, row.y, quitWidth, row.height), "QUIT", "Q", false))
            {
                flow.Quit();
            }
        }

        private void EnsureFrontEndStyles(Font display, Font monoFont)
        {
            hero = Style(display, 100, TextAnchor.MiddleLeft, Sand);
            cardName = Style(display, 24, TextAnchor.MiddleLeft, Paper);
            buttonSmall = Style(display, 22, TextAnchor.MiddleCenter, Paper);
            blurb = Style(monoFont, 14, TextAnchor.UpperLeft, LabelDim);
            blurb.wordWrap = true;
        }
    }
}
