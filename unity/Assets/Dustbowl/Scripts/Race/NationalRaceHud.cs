using System.Collections.Generic;
using Dustbowl.Bike;
using Dustbowl.Course;
using UnityEngine;

namespace Dustbowl.Race
{
    [DisallowMultipleComponent]
    public sealed class NationalRaceHud : MonoBehaviour
    {
        private const float ReferenceWidth = 1280f;
        private const float ReferenceHeight = 720f;
        private const float MinimumScale = .65f;

        [SerializeField] private NationalRaceManager race;
        [SerializeField] private ArcadeBikeController player;

        private GUIStyle logo;
        private GUIStyle displayLeft;
        private GUIStyle displayCenter;
        private GUIStyle statValue;
        private GUIStyle statLabel;
        private GUIStyle body;
        private GUIStyle small;
        private GUIStyle tiny;
        private GUIStyle button;
        private GUIStyle ghostButton;
        private GUIStyle ranking;
        private Texture2D panel;
        private Texture2D panelSolid;
        private Texture2D overlay;
        private Texture2D accent;
        private Texture2D sand;
        private Texture2D line;
        private Texture2D row;
        private Texture2D mapRoute;
        private Texture2D mapShadow;
        private Texture2D mapBoundary;
        private Texture2D mapStart;
        private Texture2D mapPlayer;
        private Texture2D[] mapOpponents;
        private Vector2 mapMin;
        private Vector2 mapMax;
        private bool mapBoundsReady;

        private static readonly Color[] OpponentMapColors =
        {
            new(.08f, .36f, .82f), new(.08f, .65f, .20f), new(.92f, .68f, .06f),
            new(.50f, .08f, .78f), new(.82f, .06f, .48f), new(.02f, .63f, .77f),
            new(.91f, .27f, .05f)
        };

        public int MinimapTrackedRiderCount => race == null ? 0 : race.Opponents.Count + 1;
        public int MinimapCoursePointCount => race?.Course?.Definition?.LinePoints.Count ?? 0;

        public void Configure(NationalRaceManager manager, ArcadeBikeController controller)
        {
            race = manager;
            player = controller;
            mapBoundsReady = false;
        }

        private void OnGUI()
        {
            if (race == null || player == null)
            {
                return;
            }

            EnsureStyles();
            float scale = CalculateScale(Screen.width, Screen.height);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            switch (race.State)
            {
                case NationalRaceState.PreRace:
                    DrawPreRace(width, height);
                    break;
                case NationalRaceState.Results:
                    DrawResults(width, height);
                    break;
                default:
                    DrawRaceHud(width, height);
                    break;
            }

            GUI.matrix = previous;
        }

        private void DrawPreRace(float width, float height)
        {
            GUI.DrawTexture(new Rect(0f, 0f, width, height), overlay);
            const float panelWidth = 680f;
            const float panelHeight = 560f;
            Rect plate = new((width - panelWidth) * .5f, (height - panelHeight) * .5f, panelWidth, panelHeight);
            DrawPanel(plate, true);

            float x = plate.x + 38f;
            float contentWidth = plate.width - 76f;
            GUI.Label(new Rect(x, plate.y + 26f, contentWidth, 22f),
                "N A T I O N A L   S E R I E S   ·   R O U N D   1", tiny);
            GUI.Label(new Rect(x, plate.y + 50f, contentWidth, 74f), "DUSTBOWL", logo);
            GUI.Label(new Rect(x, plate.y + 126f, contentWidth, 34f), "DUSTBOWL FLATS", displayLeft);
            GUI.Label(new Rect(x, plate.y + 164f, contentWidth, 54f),
                "Rolling desert. Banked corners. A three-lap National that builds from a flowing opener to the big one.",
                body);
            GUI.DrawTexture(new Rect(x, plate.y + 226f, contentWidth, 1f), line);

            GUI.Label(new Rect(x, plate.y + 244f, contentWidth, 20f), "RACE CONTROLS", statLabel);
            DrawControlHint(new Rect(x, plate.y + 272f, 285f, 24f), "A / D  ·  LEFT STICK", "STEER");
            DrawControlHint(new Rect(x + 315f, plate.y + 272f, 285f, 24f), "S / ↓  ·  LT", "BRAKE");
            DrawControlHint(new Rect(x, plate.y + 304f, 285f, 24f), "C  ·  RB", "CAMERA");
            DrawControlHint(new Rect(x + 315f, plate.y + 304f, 285f, 24f), "BACKSPACE  ·  Y", "RESET");

            Rect throttle = new(x, plate.y + 344f, 290f, 58f);
            Rect camera = new(x + 314f, plate.y + 344f, 290f, 58f);
            GUI.DrawTexture(throttle, row);
            GUI.DrawTexture(camera, row);
            GUI.Label(new Rect(throttle.x + 14f, throttle.y + 8f, throttle.width - 28f, 18f), "THROTTLE MODE  ·  T / X", statLabel);
            GUI.Label(new Rect(throttle.x + 14f, throttle.y + 27f, throttle.width - 28f, 24f),
                race.AutoThrottleEnabled ? "AUTO · STEER + BRAKE" : "MANUAL · HOLD W / RT",
                displayLeft);
            GUI.Label(new Rect(camera.x + 14f, camera.y + 8f, camera.width - 28f, 18f), "GAMEPLAY CAMERA  ·  C / RB", statLabel);
            GUI.Label(new Rect(camera.x + 14f, camera.y + 27f, camera.width - 28f, 24f),
                race.CameraMode.ToString().ToUpperInvariant(), displayLeft);

            if (GUI.Button(new Rect(x, plate.y + 424f, 270f, 54f), "START RACE", button))
            {
                race.BeginRace();
            }
            if (GUI.Button(new Rect(x + 286f, plate.y + 424f, 158f, 54f), "THROTTLE", ghostButton))
            {
                race.ToggleAutoThrottle();
            }
            if (GUI.Button(new Rect(x + 460f, plate.y + 424f, 144f, 54f), "CAMERA", ghostButton))
            {
                race.CycleCamera();
            }

            GUI.Label(new Rect(x, plate.y + 498f, contentWidth, 24f),
                "ENTER / A  START     ·     T / X  THROTTLE     ·     ESC  QUIT",
                small);
            if (GUI.Button(new Rect(plate.x + plate.width - 100f, plate.y + 18f, 66f, 28f), "QUIT", ghostButton))
            {
                race.RequestQuit();
            }
        }

        private void DrawRaceHud(float width, float height)
        {
            Rect racePanel = new(18f, 18f, 310f, 158f);
            DrawPanel(racePanel, false);
            GUI.Label(new Rect(racePanel.x + 22f, racePanel.y + 14f, 268f, 18f),
                "D U S T B O W L   N A T I O N A L", tiny);
            GUI.Label(new Rect(racePanel.x + 22f, racePanel.y + 36f, 268f, 30f),
                "DUSTBOWL FLATS", displayLeft);
            DrawStat(new Rect(racePanel.x + 22f, racePanel.y + 78f, 122f, 52f),
                "LAP TIME", NationalRaceManager.FormatTime(race.CurrentLapTime));
            DrawStat(new Rect(racePanel.x + 157f, racePanel.y + 78f, 128f, 52f),
                "POSITION", $"P {race.PlayerPosition} / {race.TotalRiders}");
            GUI.Label(new Rect(racePanel.x + 22f, racePanel.y + 133f, 124f, 18f),
                $"LAP {race.PlayerLap} / {race.TotalLaps}", statLabel);
            GUI.Label(new Rect(racePanel.x + 157f, racePanel.y + 133f, 128f, 18f),
                race.BestLapTime > 0f ? $"BEST {NationalRaceManager.FormatTime(race.BestLapTime)}" : "BEST  —:——",
                statLabel);

            DrawMinimap(new Rect(width - 276f, 18f, 258f, 224f));

            Rect speedPanel = new(18f, height - 116f, 190f, 98f);
            DrawPanel(speedPanel, false);
            float speed = new Vector2(player.State.velocity.x, player.State.velocity.z).magnitude;
            GUI.Label(new Rect(speedPanel.x + 18f, speedPanel.y + 10f, 140f, 50f),
                $"{speed * 3.6f:000}", logo);
            GUI.Label(new Rect(speedPanel.x + 20f, speedPanel.y + 67f, 80f, 18f), "KM/H", statLabel);
            GUI.Label(new Rect(speedPanel.x + 86f, speedPanel.y + 65f, 82f, 20f),
                race.CameraMode.ToString().ToUpperInvariant(), tiny);

            string throttle = race.AutoThrottleEnabled ? "AUTO" : "MANUAL";
            GUI.Label(new Rect(width - 520f, height - 42f, 500f, 22f),
                $"C / RB  CAMERA     ·     T / X  THROTTLE {throttle}     ·     ESC  QUIT",
                small);

            if (race.State == NationalRaceState.Countdown)
            {
                GUI.Label(new Rect(0f, height * .27f, width, 155f), race.CountdownText, logo);
                GUI.Label(new Rect(0f, height * .27f + 126f, width, 30f),
                    "DUSTBOWL FLATS  ·  3 LAPS", displayCenter);
            }
        }

        private void DrawResults(float width, float height)
        {
            GUI.DrawTexture(new Rect(0f, 0f, width, height), overlay);
            const float panelWidth = 660f;
            const float panelHeight = 610f;
            Rect plate = new((width - panelWidth) * .5f, (height - panelHeight) * .5f, panelWidth, panelHeight);
            DrawPanel(plate, true);
            float x = plate.x + 42f;
            float contentWidth = plate.width - 84f;

            GUI.Label(new Rect(x, plate.y + 25f, contentWidth, 20f),
                "D U S T B O W L   F L A T S   ·   3   L A P S", tiny);
            GUI.Label(new Rect(x, plate.y + 48f, contentWidth, 68f), "FINISHED!", logo);
            GUI.Label(new Rect(x, plate.y + 116f, contentWidth, 30f),
                $"{race.PlayerPosition}{Ordinal(race.PlayerPosition)} PLACE", displayLeft);

            Rect total = new(x, plate.y + 158f, 278f, 72f);
            Rect best = new(x + 298f, plate.y + 158f, 278f, 72f);
            GUI.DrawTexture(total, row);
            GUI.DrawTexture(best, row);
            DrawStat(new Rect(total.x + 16f, total.y + 8f, total.width - 32f, 56f),
                "RACE TIME", NationalRaceManager.FormatTime(race.RaceTime));
            DrawStat(new Rect(best.x + 16f, best.y + 8f, best.width - 32f, 56f),
                "BEST LAP", NationalRaceManager.FormatTime(race.BestLapTime));

            GUI.Label(new Rect(x, plate.y + 248f, contentWidth, 18f), "FINAL CLASSIFICATION", statLabel);
            IReadOnlyList<string> results = race.BuildResults();
            for (int index = 0; index < results.Count; index++)
            {
                Rect resultRow = new(x, plate.y + 273f + index * 31f, contentWidth, 27f);
                if (results[index].Contains("YOU"))
                {
                    GUI.DrawTexture(resultRow, row);
                    GUI.DrawTexture(new Rect(resultRow.x, resultRow.y, 5f, resultRow.height), accent);
                }
                GUI.Label(new Rect(resultRow.x + 14f, resultRow.y, resultRow.width - 28f, resultRow.height),
                    results[index], ranking);
            }

            if (GUI.Button(new Rect(x, plate.y + 532f, 278f, 50f), "RACE AGAIN", button))
            {
                race.RestartRace();
            }
            if (GUI.Button(new Rect(x + 298f, plate.y + 532f, 278f, 50f), "QUIT TO DESKTOP", ghostButton))
            {
                race.RequestQuit();
            }
            GUI.Label(new Rect(x, plate.y + 584f, contentWidth, 18f),
                "ENTER / A  RACE AGAIN     ·     ESC  QUIT", small);
        }

        private void DrawMinimap(Rect rect)
        {
            if (MinimapCoursePointCount < 2)
            {
                return;
            }

            EnsureMapBounds();
            DrawPanel(rect, false);
            GUI.Label(new Rect(rect.x + 15f, rect.y + 10f, rect.width - 30f, 18f),
                "L I V E   C O U R S E", tiny);
            Rect courseRect = new(rect.x + 24f, rect.y + 35f, rect.width - 48f, rect.height - 58f);
            float side = Mathf.Min(courseRect.width, courseRect.height);
            courseRect = new Rect(
                courseRect.x + (courseRect.width - side) * .5f,
                courseRect.y + (courseRect.height - side) * .5f,
                side,
                side);

            Vector2 center = MapToRect(Vector3.zero, courseRect);
            float worldSpan = mapMax.x - mapMin.x;
            float radius = NationalRaceManager.PlayableRadius / worldSpan * courseRect.width;
            DrawCircle(center, radius, mapBoundary, 1.5f, 56);

            IReadOnlyList<CourseLinePoint> points = race.Course.Definition.LinePoints;
            for (int index = 0; index < points.Count - 1; index += 3)
            {
                int next = Mathf.Min(index + 3, points.Count - 1);
                Vector2 from = MapToRect(points[index].center, courseRect);
                Vector2 to = MapToRect(points[next].center, courseRect);
                DrawLine(from, to, mapShadow, 6f);
                DrawLine(from, to, mapRoute, 3f);
            }

            Vector2 start = MapToRect(points[0].center, courseRect);
            DrawMarker(start, mapShadow, 11f);
            DrawMarker(start, mapStart, 7f);
            for (int index = 0; index < race.Opponents.Count; index++)
            {
                Vector2 point = MapToRect(race.Opponents[index].transform.position, courseRect);
                DrawMarker(point, mapShadow, 9f);
                DrawMarker(point, mapOpponents[index % mapOpponents.Length], 5.5f);
            }

            Vector2 playerPoint = MapToRect(player.State.position, courseRect);
            Vector2 heading = new(Mathf.Sin(player.State.yawRadians), -Mathf.Cos(player.State.yawRadians));
            if (heading.sqrMagnitude < .001f) heading = Vector2.up;
            heading.Normalize();
            Vector2 right = new(-heading.y, heading.x);
            DrawMarker(playerPoint, mapShadow, 12f);
            DrawMarker(playerPoint, mapPlayer, 7f);
            DrawLine(playerPoint, playerPoint + heading * 13f, mapPlayer, 3f);
            DrawLine(playerPoint + heading * 13f, playerPoint + heading * 7f + right * 4f, mapPlayer, 2f);
            DrawLine(playerPoint + heading * 13f, playerPoint + heading * 7f - right * 4f, mapPlayer, 2f);

            GUI.Label(new Rect(rect.x + 14f, rect.y + rect.height - 23f, rect.width - 28f, 16f),
                "YOU  +  7 RIVALS", tiny);
        }

        public Vector2 WorldToMinimapNormalized(Vector3 worldPosition)
        {
            EnsureMapBounds();
            Vector2 source = new(worldPosition.x, DustbowlFlatsNationalCourse.WebZFromUnity(worldPosition.z));
            return new Vector2(
                Mathf.InverseLerp(mapMin.x, mapMax.x, source.x),
                Mathf.InverseLerp(mapMin.y, mapMax.y, source.y));
        }

        public void CollectMinimapRiderPositions(List<Vector2> positions)
        {
            positions.Clear();
            if (race == null || player == null)
            {
                return;
            }

            positions.Add(WorldToMinimapNormalized(player.State.position));
            foreach (NationalAIRider opponent in race.Opponents)
            {
                positions.Add(WorldToMinimapNormalized(opponent.transform.position));
            }
        }

        public static bool ReferenceLayoutFits(float screenWidth, float screenHeight)
        {
            float scale = CalculateScale(screenWidth, screenHeight);
            float width = screenWidth / scale;
            float height = screenHeight / scale;
            Rect racePanel = new(18f, 18f, 310f, 158f);
            Rect minimap = new(width - 276f, 18f, 258f, 224f);
            Rect speed = new(18f, height - 116f, 190f, 98f);
            Rect menu = new((width - 680f) * .5f, (height - 560f) * .5f, 680f, 560f);
            Rect results = new((width - 660f) * .5f, (height - 610f) * .5f, 660f, 610f);
            Rect screen = new(0f, 0f, width, height);
            return Contains(screen, racePanel)
                && Contains(screen, minimap)
                && Contains(screen, speed)
                && Contains(screen, menu)
                && Contains(screen, results);
        }

        private void EnsureMapBounds()
        {
            if (mapBoundsReady || race?.Course?.Definition == null)
            {
                return;
            }

            float extent = NationalRaceManager.PlayableRadius + 20f;
            foreach (CourseLinePoint point in race.Course.Definition.LinePoints)
            {
                Vector2 source = new(point.center.x, DustbowlFlatsNationalCourse.WebZFromUnity(point.center.z));
                extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(source.x), Mathf.Abs(source.y)) * 1.08f);
            }
            mapMin = new Vector2(-extent, -extent);
            mapMax = new Vector2(extent, extent);
            mapBoundsReady = true;
        }

        private Vector2 MapToRect(Vector3 worldPosition, Rect rect)
        {
            Vector2 normalized = WorldToMinimapNormalized(worldPosition);
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
                Mathf.Lerp(rect.yMin, rect.yMax, normalized.y));
        }

        private void DrawPanel(Rect rect, bool prominent)
        {
            GUI.DrawTexture(rect, prominent ? panelSolid : panel);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), line);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), line);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), line);
            GUI.DrawTexture(new Rect(rect.x, rect.y, prominent ? 9f : 6f, rect.height), accent);
        }

        private void DrawControlHint(Rect rect, string keys, string action)
        {
            GUI.Label(new Rect(rect.x, rect.y, 155f, rect.height), keys, tiny);
            GUI.Label(new Rect(rect.x + 160f, rect.y, rect.width - 160f, rect.height), action, statLabel);
        }

        private void DrawStat(Rect rect, string labelText, string value)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 17f), labelText, statLabel);
            GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, rect.height - 18f), value, statValue);
        }

        private void EnsureStyles()
        {
            if (panel != null) return;

            Color paperColor = Hex(0xECE5D8);
            Color sandColor = Hex(0xE3BE86);
            Color amberColor = Hex(0xFFB020);
            Color mutedColor = new(paperColor.r, paperColor.g, paperColor.b, .62f);
            Font displayFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Haettenschweiler", "Impact", "Arial Narrow", "Arial" }, 64);
            Font monoFont = Font.CreateDynamicFontFromOSFont("Consolas", 18);

            panel = Solid(new Color(.025f, .03f, .038f, .82f));
            panelSolid = Solid(new Color(.025f, .03f, .038f, .94f));
            overlay = Solid(new Color(.025f, .018f, .014f, .75f));
            accent = Solid(amberColor);
            sand = Solid(sandColor);
            line = Solid(new Color(sandColor.r, sandColor.g, sandColor.b, .28f));
            row = Solid(new Color(sandColor.r, sandColor.g, sandColor.b, .085f));
            mapRoute = Solid(new Color(paperColor.r, paperColor.g, paperColor.b, .90f));
            mapShadow = Solid(new Color(.015f, .018f, .022f, .92f));
            mapBoundary = Solid(new Color(amberColor.r, amberColor.g, amberColor.b, .22f));
            mapStart = Solid(amberColor);
            mapPlayer = Solid(Hex(0xE4453A));
            mapOpponents = new Texture2D[OpponentMapColors.Length];
            for (int index = 0; index < mapOpponents.Length; index++)
            {
                mapOpponents[index] = Solid(OpponentMapColors[index]);
            }

            logo = Style(62, FontStyle.Bold, TextAnchor.MiddleLeft, sandColor, displayFont);
            displayLeft = Style(25, FontStyle.Bold, TextAnchor.MiddleLeft, amberColor, displayFont);
            displayCenter = Style(25, FontStyle.Bold, TextAnchor.MiddleCenter, amberColor, displayFont);
            statValue = Style(23, FontStyle.Bold, TextAnchor.MiddleLeft, paperColor, displayFont);
            statLabel = Style(10, FontStyle.Bold, TextAnchor.MiddleLeft, mutedColor, monoFont);
            body = Style(14, FontStyle.Normal, TextAnchor.UpperLeft, mutedColor, monoFont);
            body.wordWrap = true;
            small = Style(11, FontStyle.Bold, TextAnchor.MiddleCenter, mutedColor, monoFont);
            tiny = Style(9, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(sandColor.r, sandColor.g, sandColor.b, .68f), monoFont);
            ranking = Style(15, FontStyle.Bold, TextAnchor.MiddleLeft, paperColor, monoFont);
            button = new GUIStyle(GUI.skin.button)
            {
                font = displayFont,
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow
            };
            button.normal.background = accent;
            button.normal.textColor = Hex(0x0B0D10);
            button.hover.background = sand;
            button.hover.textColor = Hex(0x0B0D10);
            button.active.background = sand;
            button.active.textColor = Hex(0x0B0D10);

            ghostButton = new GUIStyle(button)
            {
                fontSize = 18
            };
            ghostButton.normal.background = panel;
            ghostButton.normal.textColor = sandColor;
            ghostButton.hover.background = row;
            ghostButton.hover.textColor = paperColor;
            ghostButton.active.background = row;
            ghostButton.active.textColor = paperColor;
        }

        private static GUIStyle Style(
            int size,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color,
            Font font)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                fontStyle = fontStyle,
                alignment = alignment,
                clipping = TextClipping.Overflow,
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

        private static void DrawMarker(Vector2 center, Texture2D texture, float size)
        {
            GUI.DrawTexture(new Rect(center.x - size * .5f, center.y - size * .5f, size, size), texture);
        }

        private static void DrawCircle(
            Vector2 center,
            float radius,
            Texture2D texture,
            float thickness,
            int segments)
        {
            Vector2 previous = center + Vector2.right * radius;
            for (int index = 1; index <= segments; index++)
            {
                float angle = index / (float)segments * Mathf.PI * 2f;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                DrawLine(previous, next, texture, thickness);
                previous = next;
            }
        }

        private static void DrawLine(Vector2 start, Vector2 end, Texture2D texture, float thickness)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < .01f)
            {
                return;
            }

            Matrix4x4 previous = GUI.matrix;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - thickness * .5f, delta.magnitude, thickness), texture);
            GUI.matrix = previous;
        }

        private static float CalculateScale(float width, float height)
        {
            return Mathf.Max(MinimumScale, Mathf.Min(width / ReferenceWidth, height / ReferenceHeight));
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin
                && inner.yMin >= outer.yMin
                && inner.xMax <= outer.xMax
                && inner.yMax <= outer.yMax;
        }

        private static Color Hex(uint rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }

        private static string Ordinal(int value)
        {
            if (value is 11 or 12 or 13) return "TH";
            return (value % 10) switch
            {
                1 => "ST",
                2 => "ND",
                3 => "RD",
                _ => "TH"
            };
        }
    }
}
