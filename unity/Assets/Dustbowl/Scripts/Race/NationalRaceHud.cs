using Dustbowl.Bike;
using UnityEngine;

namespace Dustbowl.Race
{
    [DisallowMultipleComponent]
    public sealed class NationalRaceHud : MonoBehaviour
    {
        [SerializeField] private NationalRaceManager race;
        [SerializeField] private ArcadeBikeController player;

        private GUIStyle title;
        private GUIStyle label;
        private GUIStyle small;
        private GUIStyle countdown;
        private GUIStyle result;
        private Texture2D dark;
        private Texture2D accent;

        public void Configure(NationalRaceManager manager, ArcadeBikeController controller)
        {
            race = manager;
            player = controller;
        }

        private void OnGUI()
        {
            if (race == null || player == null)
            {
                return;
            }

            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            GUI.DrawTexture(new Rect(22f, 22f, 285f, 112f), dark);
            GUI.DrawTexture(new Rect(22f, 22f, 7f, 112f), accent);
            GUI.Label(new Rect(44f, 32f, 250f, 30f), "DUSTBOWL NATIONAL", title);
            GUI.Label(new Rect(44f, 62f, 250f, 26f), $"LAP  {race.PlayerLap} / {race.TotalLaps}", label);
            GUI.Label(new Rect(44f, 88f, 250f, 28f), $"POS  {race.PlayerPosition} / {race.TotalRiders}", label);

            GUI.DrawTexture(new Rect(width - 272f, 22f, 250f, 112f), dark);
            float speed = new Vector2(player.State.velocity.x, player.State.velocity.z).magnitude;
            GUI.Label(new Rect(width - 250f, 32f, 215f, 32f), $"{speed * 3.6f:000}  KM/H", title);
            GUI.Label(new Rect(width - 250f, 69f, 215f, 24f), NationalRaceManager.FormatTime(race.RaceTime), label);
            GUI.Label(new Rect(width - 250f, 96f, 215f, 22f), race.CameraMode.ToString().ToUpperInvariant(), small);

            GUI.Label(new Rect(22f, height - 48f, width - 44f, 28f),
                "STEER  A/D or LEFT STICK     BRAKE  S or LT     AIR  R/F + Q/E or RIGHT STICK     CAMERA  C / RB     RESET  BACKSPACE / Y",
                small);

            if (race.State == NationalRaceState.Countdown)
            {
                GUI.Label(new Rect(0f, height * .30f, width, 150f), race.CountdownText, countdown);
                GUI.Label(new Rect(0f, height * .30f + 130f, width, 40f), "DUSTBOWL FLATS  ·  3 LAPS", result);
            }
            else if (race.State == NationalRaceState.Results)
            {
                float panelWidth = 480f;
                float panelHeight = 430f;
                Rect panel = new((width - panelWidth) * .5f, (height - panelHeight) * .5f, panelWidth, panelHeight);
                GUI.DrawTexture(panel, dark);
                GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 8f), accent);
                GUI.Label(new Rect(panel.x, panel.y + 25f, panel.width, 48f), "NATIONAL COMPLETE", countdown);
                GUI.Label(new Rect(panel.x, panel.y + 86f, panel.width, 32f),
                    $"{race.PlayerPosition}{Ordinal(race.PlayerPosition)} PLACE  ·  {NationalRaceManager.FormatTime(race.RaceTime)}", result);
                var results = race.BuildResults();
                for (int index = 0; index < results.Count; index++)
                {
                    GUI.Label(new Rect(panel.x + 72f, panel.y + 135f + index * 28f, panel.width - 120f, 26f), results[index], label);
                }

                GUI.Label(new Rect(panel.x, panel.y + panel.height - 48f, panel.width, 30f),
                    "ENTER / A  ·  RACE AGAIN", result);
            }
        }

        private void EnsureStyles()
        {
            if (dark != null) return;
            dark = Solid(new Color(.035f, .045f, .055f, .90f));
            accent = Solid(new Color(.96f, .31f, .08f, 1f));
            title = Style(23, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            label = Style(19, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(.93f, .86f, .72f));
            small = Style(14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, .88f, .68f));
            countdown = Style(76, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            result = Style(20, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, .71f, .28f));
        }

        private static GUIStyle Style(int size, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = alignment,
                normal = { textColor = color }
            };
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static string Ordinal(int value)
        {
            if (value is 11 or 12 or 13) return "TH";
            return (value % 10) switch { 1 => "ST", 2 => "ND", 3 => "RD", _ => "TH" };
        }
    }
}
