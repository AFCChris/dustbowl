using System.Collections.Generic;
using Dustbowl.Bike;
using Dustbowl.Course;
using UnityEngine;

namespace Dustbowl.Race
{
    /// <summary>
    /// Development race HUD styled after the web build's chrome: dark cut-corner
    /// panels, small spaced monospace labels, condensed display numerals, amber
    /// accents, a half-arc speedo and a course minimap with live rider markers.
    /// Presentation only; it reads race and rider state and never writes it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NationalRaceHud : MonoBehaviour
    {
        private const float DesignHeight = 900f;
        private const float PanelCut = 12f;
        private const float SpeedArcMaxMph = 110f;
        private const float MetresPerSecondToMph = 2.2369f;
        private const float MinimapPanelSize = 236f;
        private const float MinimapInnerSize = MinimapPanelSize - 24f;
        private const int CourseMapResolution = 512;

        [SerializeField] private NationalRaceManager race;
        [SerializeField] private ArcadeBikeController player;
        [SerializeField] private Font displayFont;
        [SerializeField] private Font labelFont;

        // Web shell.html palette.
        private static readonly Color Sand = new(.890f, .745f, .525f);
        private static readonly Color Ochre = new(.706f, .471f, .235f);
        private static readonly Color Amber = new(1f, .690f, .125f);
        private static readonly Color Alarm = new(.894f, .271f, .227f);
        private static readonly Color Ink = new(.043f, .051f, .063f);
        private static readonly Color Paper = new(.925f, .898f, .847f);
        private static readonly Color Panel = new(.043f, .051f, .063f, .74f);
        private static readonly Color PanelLine = new(.890f, .745f, .525f, .28f);
        private static readonly Color LabelDim = new(.925f, .898f, .847f, .55f);
        private static readonly Color MapRoute = new(.914f, .894f, .855f, .82f);
        private static readonly Color MapRouteShadow = new(.043f, .051f, .063f, .70f);

        private static readonly Color[] OpponentMapColors =
        {
            new(.08f, .36f, .82f), new(.08f, .65f, .20f), new(.92f, .68f, .06f),
            new(.50f, .08f, .78f), new(.82f, .06f, .48f), new(.02f, .63f, .77f),
            new(.91f, .27f, .05f)
        };

        private GUIStyle label;
        private GUIStyle labelLeft;
        private GUIStyle numeral;
        private GUIStyle numeralLarge;
        private GUIStyle speedNumeral;
        private GUIStyle countdown;
        private GUIStyle eyebrow;
        private GUIStyle titleDisplay;
        private GUIStyle mono;
        private GUIStyle monoRight;
        private GUIStyle button;
        private Texture2D white;
        private Texture2D cutTopRight;
        private Texture2D cutBottomLeft;
        private Texture2D dot;
        private Texture2D arrow;

        private Vector2 mapMin;
        private Vector2 mapMax;
        private bool mapBoundsReady;
        private Texture2D courseMap;

        public int MinimapTrackedRiderCount => race == null ? 0 : race.Opponents.Count + 1;
        public int MinimapCoursePointCount => race?.Course?.Definition?.LinePoints.Count ?? 0;
        public bool HasCustomFonts => displayFont != null && labelFont != null;

        public void Configure(NationalRaceManager manager, ArcadeBikeController controller)
        {
            Configure(manager, controller, displayFont, labelFont);
        }

        public void Configure(NationalRaceManager manager, ArcadeBikeController controller, Font display, Font labels)
        {
            race = manager;
            player = controller;
            displayFont = display;
            labelFont = labels;
            mapBoundsReady = false;
            ReleaseCourseMap();
            label = null;
        }

        private void OnDestroy()
        {
            ReleaseCourseMap();
        }

        private void ReleaseCourseMap()
        {
            if (courseMap != null)
            {
                Destroy(courseMap);
                courseMap = null;
            }
        }

        private void OnGUI()
        {
            if (race == null || player == null)
            {
                return;
            }

            EnsureStyles();
            float scale = Mathf.Max(.8f, Screen.height / DesignHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            DrawRacePanel(new Rect(16f, 16f, 312f, 178f));
            DrawMinimapPanel(new Rect(width - 16f - MinimapPanelSize, 16f, MinimapPanelSize, MinimapPanelSize));
            DrawCameraChip(new Rect(width - 16f - MinimapPanelSize, 16f + MinimapPanelSize + 8f, MinimapPanelSize, 26f));
            DrawSpeedo(new Rect(16f, height - 18f - 128f, 190f, 128f));
            DrawControlsHint(new Rect(0f, height - 30f, width, 18f));

            if (race.State == NationalRaceState.Countdown)
            {
                DrawCountdown(width, height);
            }
            else if (race.State == NationalRaceState.Results)
            {
                DrawResults(width, height);
            }
        }

        // ------------------------------------------------------------ panels

        private void DrawRacePanel(Rect rect)
        {
            DrawPanel(rect);
            float x = rect.x + 18f;
            float column = (rect.width - 36f) * .5f;
            float y = rect.y + 12f;

            Stat(new Rect(x, y, column, 48f), "LAP TIME", FormatLapClock(race.CurrentLapTime), Amber, numeral);
            Stat(new Rect(x + column, y, column, 48f), "BEST LAP",
                race.BestLapTime > 0f ? FormatLapClock(race.BestLapTime) : "\u2014:\u2014\u2014", Paper, numeral);
            y += 52f;
            float lapDone = race.LapLength > 0f ? Mathf.Clamp01(race.PlayerAlong / race.LapLength) : 0f;
            Stat(new Rect(x, y, column, 48f), "LAP DONE", $"{Mathf.RoundToInt(lapDone * 100f)}%", Paper, numeral);
            Stat(new Rect(x + column, y, column, 48f), "LAP", $"{race.PlayerLap}/{race.TotalLaps}", Paper, numeral);
            y += 52f;
            Stat(new Rect(x, y, rect.width - 36f, 56f), "POSITION", $"P {race.PlayerPosition}/{race.TotalRiders}", Amber, numeralLarge);
        }

        private void Stat(Rect rect, string caption, string value, Color color, GUIStyle style)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 14f), caption, labelLeft);
            Rect valueRect = new(rect.x - 1f, rect.y + 13f, rect.width, rect.height - 13f);
            style.normal.textColor = color;
            GUI.Label(valueRect, value, style);
        }

        private void DrawMinimapPanel(Rect rect)
        {
            DrawPanel(rect);
            DrawMinimap(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f));
        }

        private void DrawCameraChip(Rect rect)
        {
            DrawChip(rect);
            GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width * .5f, rect.height), "CAMERA", labelLeft);
            GUI.Label(new Rect(rect.x, rect.y, rect.width - 12f, rect.height),
                race.CameraMode.ToString().ToUpperInvariant(), Right(monoRight, Sand));
        }

        private void DrawSpeedo(Rect rect)
        {
            float speed = new Vector2(player.State.velocity.x, player.State.velocity.z).magnitude;
            float mph = speed * MetresPerSecondToMph;
            float fill = Mathf.Clamp01(mph / SpeedArcMaxMph);
            Vector2 centre = new(rect.x + rect.width * .5f, rect.y + rect.height - 22f);
            float radius = rect.width * .5f - 12f;

            DrawArc(centre, radius, 0f, 1f, 12f, new Color(Ink.r, Ink.g, Ink.b, .70f));
            DrawArc(centre, radius, 0f, 1f, 12f, new Color(Sand.r, Sand.g, Sand.b, .18f));
            if (fill > 0f)
            {
                DrawArc(centre, radius, 0f, fill, 12f, Amber);
            }

            string value = Mathf.RoundToInt(mph).ToString();
            Rect valueRect = new(rect.x, centre.y - 68f, rect.width, 66f);
            ShadowedLabel(valueRect, value, speedNumeral, Paper, 2f);
            GUI.Label(new Rect(rect.x, centre.y - 6f, rect.width, 16f), "MPH", label);
        }

        private void DrawControlsHint(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .62f);
            GUI.Label(rect,
                "STEER  A/D · LEFT STICK      BRAKE  S · LT      AIR  R/F + Q/E · RIGHT STICK      CAMERA  C/V · RB/LB      RESET  BACKSPACE · Y",
                label);
            GUI.color = previous;
        }

        private void DrawCountdown(float width, float height)
        {
            string text = race.CountdownText;
            bool go = text == "GO!";
            GUI.Label(new Rect(0f, height * .22f, width, 22f),
                $"{DustbowlFlatsNationalCourse.CourseName.ToUpperInvariant()}  ·  NATIONAL  ·  {race.TotalLaps} LAPS",
                Center(eyebrow, Sand));
            ShadowedLabel(new Rect(0f, height * .22f + 10f, width, 220f), text, countdown, go ? Paper : Amber, 6f);
        }

        private void DrawResults(float width, float height)
        {
            IReadOnlyList<string> results = race.BuildResults();
            IReadOnlyList<float> laps = race.PlayerLapTimes;
            float panelWidth = 560f;
            float panelHeight = 318f + results.Count * 26f + laps.Count * 22f;
            Rect panel = new((width - panelWidth) * .5f, (height - panelHeight) * .5f, panelWidth, panelHeight);
            DrawPanel(panel, new Color(Ink.r, Ink.g, Ink.b, .90f));

            float x = panel.x + 40f;
            float inner = panel.width - 80f;
            float y = panel.y + 26f;
            GUI.Label(new Rect(x, y, inner, 16f),
                $"RACE COMPLETE  ·  {DustbowlFlatsNationalCourse.CourseName.ToUpperInvariant()}  ·  {race.TotalLaps} LAPS",
                Left(eyebrow, Ochre));
            y += 20f;
            ShadowedLabel(new Rect(x - 2f, y, inner, 76f), "FINISHED!", titleDisplay, Sand, 4f);
            y += 84f;
            Rule(new Rect(x, y, inner, 1f));
            y += 14f;
            Stat(new Rect(x, y, inner * .5f, 52f), "RACE TIME", FormatLapClock(race.RaceTime), Amber, numeralLarge);
            Stat(new Rect(x + inner * .5f, y, inner * .5f, 52f), "BEST LAP",
                race.BestLapTime > 0f ? FormatLapClock(race.BestLapTime) : "\u2014:\u2014\u2014", Amber, numeralLarge);
            y += 60f;
            Rule(new Rect(x, y, inner, 1f));
            y += 10f;

            for (int index = 0; index < results.Count; index++)
            {
                bool isPlayer = results[index].Contains("YOU");
                GUI.Label(new Rect(x, y, inner, 24f), results[index], Left(mono, isPlayer ? Amber : Paper));
                y += 26f;
            }

            Rule(new Rect(x, y, inner, 1f));
            y += 8f;
            GUI.Label(new Rect(x, y, inner, 16f), "YOUR LAP TIMES", labelLeft);
            y += 18f;
            for (int index = 0; index < laps.Count; index++)
            {
                bool best = laps[index] <= race.BestLapTime + .0005f;
                GUI.Label(new Rect(x, y, inner, 20f), $"LAP {index + 1}", Left(mono, LabelDim));
                GUI.Label(new Rect(x, y, inner, 20f), FormatLapClock(laps[index]) + (best ? "  BEST" : ""),
                    Right(monoRight, best ? Amber : Paper));
                y += 22f;
            }

            Rect action = new(x, panel.y + panel.height - 62f, 236f, 42f);
            DrawCutRect(action, Amber);
            GUI.Label(action, "RACE AGAIN", Center(button, Ink));
            GUI.Label(new Rect(action.xMax + 16f, action.y, inner - action.width - 16f, action.height),
                "ENTER  ·  GAMEPAD A", Left(mono, LabelDim));
        }

        // ----------------------------------------------------------- minimap

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

        /// <summary>
        /// Rasterises the full course loop and the play-area ring into one square
        /// texture whose pixels map 1:1 onto the minimap's inner rect. Drawing the
        /// map as a single texture keeps every route pixel inside the panel by
        /// construction; only the rider markers are positioned per frame.
        /// </summary>
        public Texture2D BuildCourseMapTexture(int resolution)
        {
            IReadOnlyList<CourseLinePoint> points = race.Course.Definition.LinePoints;
            EnsureMapBounds();

            // Stroke widths are authored in design pixels for a MinimapInnerSize map.
            float pixelsPerDesignUnit = resolution / MinimapInnerSize;
            float outlineRadius = 4f * pixelsPerDesignUnit;
            float fillRadius = 2f * pixelsPerDesignUnit;
            float ringRadius = (MinimapInnerSize * .5f - 2.5f) * pixelsPerDesignUnit;
            float ringHalfWidth = .6f * pixelsPerDesignUnit;
            Vector2 centre = new(resolution * .5f, resolution * .5f);

            var routeDistance = new float[resolution * resolution];
            for (int index = 0; index < routeDistance.Length; index++)
            {
                routeDistance[index] = float.PositiveInfinity;
            }

            int margin = Mathf.CeilToInt(outlineRadius) + 2;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 a = FitToMap(WorldToMinimapNormalized(points[index].center)) * resolution;
                Vector2 b = FitToMap(WorldToMinimapNormalized(points[(index + 1) % points.Count].center)) * resolution;
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x)) - margin);
                int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x)) + margin);
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y)) - margin);
                int maxY = Mathf.Min(resolution - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y)) + margin);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float distance = DistanceToSegment(new Vector2(x + .5f, y + .5f), a, b);
                        int cell = y * resolution + x;
                        if (distance < routeDistance[cell])
                        {
                            routeDistance[cell] = distance;
                        }
                    }
                }
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "DustbowlFlats_MinimapCourse"
            };
            var pixels = new Color32[resolution * resolution];
            Color ring = new(Amber.r, Amber.g, Amber.b, .22f);
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int cell = y * resolution + x;
                    float ringDistance = Mathf.Abs((new Vector2(x + .5f, y + .5f) - centre).magnitude - ringRadius);
                    Color color = ring;
                    color.a *= Mathf.Clamp01(ringHalfWidth - ringDistance + .5f);
                    color = Over(color, MapRouteShadow, Mathf.Clamp01(outlineRadius - routeDistance[cell] + .5f));
                    color = Over(color, MapRoute, Mathf.Clamp01(fillRadius - routeDistance[cell] + .5f));
                    // Texture rows run bottom-up while map y runs top-down.
                    pixels[(resolution - 1 - y) * resolution + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void DrawMinimap(Rect rect)
        {
            if (MinimapCoursePointCount < 2)
            {
                return;
            }

            EnsureCourseMap();

            // Everything except the rotated player chevron is drawn inside a GUI
            // group, which clips to the inner rect even if a marker strays.
            GUI.BeginGroup(rect);
            Rect local = new(0f, 0f, rect.width, rect.height);
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(local, courseMap, ScaleMode.StretchToFill, true);
            GUI.color = previous;

            IReadOnlyList<CourseLinePoint> points = race.Course.Definition.LinePoints;
            Vector2 start = ClampMarker(MapToRect(points[0].center, local), local, 6f);
            DrawDot(start, 11f, MapRouteShadow);
            DrawDot(start, 8f, Amber);

            for (int index = 0; index < race.Opponents.Count; index++)
            {
                Vector2 point = ClampMarker(MapToRect(race.Opponents[index].transform.position, local), local, 6f);
                DrawDot(point, 11f, MapRouteShadow);
                DrawDot(point, 8f, OpponentMapColors[index % OpponentMapColors.Length]);
            }

            GUI.EndGroup();

            // The chevron rotates through GUI.matrix, so it is drawn outside the
            // group in plain design coordinates and clamped so its whole rotated
            // footprint stays inside the panel.
            Vector2 playerPoint = ClampMarker(MapToRect(player.State.position, rect), rect, 12f);
            Vector2 heading = new(Mathf.Sin(player.State.yawRadians), -Mathf.Cos(player.State.yawRadians));
            float angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg + 90f;
            DrawArrow(playerPoint, angle, 22f, MapRouteShadow);
            DrawArrow(playerPoint, angle, 17f, Alarm);
        }

        private void EnsureMapBounds()
        {
            if (mapBoundsReady || race?.Course?.Definition == null)
            {
                return;
            }

            mapMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            mapMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (CourseLinePoint point in race.Course.Definition.LinePoints)
            {
                Vector2 source = new(point.center.x, DustbowlFlatsNationalCourse.WebZFromUnity(point.center.z));
                mapMin = Vector2.Min(mapMin, source);
                mapMax = Vector2.Max(mapMax, source);
            }

            Vector2 padding = (mapMax - mapMin) * .08f;
            mapMin -= padding;
            mapMax += padding;
            mapBoundsReady = true;
        }

        private void EnsureCourseMap()
        {
            if (courseMap != null)
            {
                return;
            }

            courseMap = BuildCourseMapTexture(CourseMapResolution);
        }

        /// <summary>Aspect-fits normalized course coordinates into the square map, centred.</summary>
        private Vector2 FitToMap(Vector2 normalized)
        {
            float sourceAspect = Mathf.Max(.001f, (mapMax.x - mapMin.x) / (mapMax.y - mapMin.y));
            if (sourceAspect > 1f)
            {
                float height = 1f / sourceAspect;
                return new Vector2(normalized.x, (1f - height) * .5f + normalized.y * height);
            }

            float width = sourceAspect;
            return new Vector2((1f - width) * .5f + normalized.x * width, normalized.y);
        }

        private Vector2 MapToRect(Vector3 worldPosition, Rect rect)
        {
            Vector2 fitted = FitToMap(WorldToMinimapNormalized(worldPosition));
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, fitted.x),
                Mathf.Lerp(rect.yMin, rect.yMax, fitted.y));
        }

        private static Vector2 ClampMarker(Vector2 point, Rect rect, float inset)
        {
            return new Vector2(
                Mathf.Clamp(point.x, rect.xMin + inset, rect.xMax - inset),
                Mathf.Clamp(point.y, rect.yMin + inset, rect.yMax - inset));
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared) : 0f;
            return (point - (a + ab * t)).magnitude;
        }

        private static Color Over(Color under, Color over, float coverage)
        {
            float alpha = over.a * coverage;
            float outAlpha = alpha + under.a * (1f - alpha);
            if (outAlpha <= 0f)
            {
                return Color.clear;
            }

            Color result = (over * alpha + under * under.a * (1f - alpha)) / outAlpha;
            result.a = outAlpha;
            return result;
        }

        // ------------------------------------------------------- primitives

        private void DrawPanel(Rect rect) => DrawPanel(rect, Panel);

        private void DrawPanel(Rect rect, Color fill)
        {
            DrawCutRect(rect, fill);
            Color previous = GUI.color;
            GUI.color = PanelLine;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width - PanelCut, 1f), white);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height - PanelCut), white);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y + PanelCut, 1f, rect.height - PanelCut), white);
            GUI.DrawTexture(new Rect(rect.x + PanelCut, rect.yMax - 1f, rect.width - PanelCut, 1f), white);
            GUI.color = previous;
        }

        private void DrawChip(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = Panel;
            GUI.DrawTexture(rect, white);
            GUI.color = PanelLine;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), white);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), white);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), white);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), white);
            GUI.color = previous;
        }

        /// <summary>Filled rectangle with the web panel's clipped top-right and bottom-left corners.</summary>
        private void DrawCutRect(Rect rect, Color fill)
        {
            Color previous = GUI.color;
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width - PanelCut, PanelCut), white);
            GUI.DrawTexture(new Rect(rect.x, rect.y + PanelCut, rect.width, rect.height - PanelCut * 2f), white);
            GUI.DrawTexture(new Rect(rect.x + PanelCut, rect.yMax - PanelCut, rect.width - PanelCut, PanelCut), white);
            GUI.DrawTexture(new Rect(rect.xMax - PanelCut, rect.y, PanelCut, PanelCut), cutTopRight);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - PanelCut, PanelCut, PanelCut), cutBottomLeft);
            GUI.color = previous;
        }

        private void Rule(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = PanelLine;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }

        private void ShadowedLabel(Rect rect, string text, GUIStyle style, Color color, float offset)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, .55f);
            GUI.Label(new Rect(rect.x + offset * .5f, rect.y + offset, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private void DrawArc(Vector2 centre, float radius, float from, float to, float thickness, Color color)
        {
            const int segments = 40;
            int first = Mathf.FloorToInt(from * segments);
            int last = Mathf.CeilToInt(to * segments);
            Vector2 previous = ArcPoint(centre, radius, from);
            for (int index = first + 1; index <= last; index++)
            {
                float t = Mathf.Min(to, index / (float)segments);
                Vector2 next = ArcPoint(centre, radius, t);
                DrawLine(previous, next, color, thickness);
                previous = next;
                if (t >= to) break;
            }
        }

        private static Vector2 ArcPoint(Vector2 centre, float radius, float t)
        {
            // Half circle from the left (0) over the top to the right (1), as in the web speedo.
            float angle = Mathf.PI * (1f - t);
            return new Vector2(centre.x + Mathf.Cos(angle) * radius, centre.y - Mathf.Sin(angle) * radius);
        }

        private void DrawDot(Vector2 centre, float size, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(centre.x - size * .5f, centre.y - size * .5f, size, size), dot);
            GUI.color = previous;
        }

        private void DrawArrow(Vector2 centre, float angle, float size, Color color)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            RotateAround(angle, centre);
            GUI.color = color;
            GUI.DrawTexture(new Rect(centre.x - size * .5f, centre.y - size * .5f, size, size), arrow);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        /// <summary>
        /// Rotates subsequent GUI drawing around a pivot given in the current design
        /// coordinates. GUIUtility.RotateAroundPivot is deliberately not used: it
        /// composes the rotation on the screen side of GUI.matrix without scaling
        /// the pivot, so under the HUD's resolution scale every rotated quad swung
        /// around the wrong point and the minimap route sprayed across the view.
        /// </summary>
        private static void RotateAround(float angle, Vector2 pivot)
        {
            GUI.matrix *= Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, angle), Vector3.one)
                * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < .01f)
            {
                return;
            }

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            RotateAround(angle, start);
            GUI.color = color;
            GUI.DrawTexture(new Rect(start.x - thickness * .5f, start.y - thickness * .5f, delta.magnitude + thickness, thickness), white);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        // ------------------------------------------------------------ styles

        private void EnsureStyles()
        {
            if (label != null) return;
            Font display = displayFont != null ? displayFont : GUI.skin.font;
            Font monoFont = labelFont != null ? labelFont : GUI.skin.font;

            label = Style(monoFont, 11, TextAnchor.MiddleCenter, LabelDim);
            labelLeft = Style(monoFont, 11, TextAnchor.MiddleLeft, LabelDim);
            mono = Style(monoFont, 15, TextAnchor.MiddleLeft, Paper);
            monoRight = Style(monoFont, 13, TextAnchor.MiddleRight, Sand);
            eyebrow = Style(monoFont, 13, TextAnchor.MiddleCenter, Sand);
            numeral = Style(display, 30, TextAnchor.UpperLeft, Paper);
            numeralLarge = Style(display, 38, TextAnchor.UpperLeft, Amber);
            speedNumeral = Style(display, 64, TextAnchor.LowerCenter, Paper);
            countdown = Style(display, 190, TextAnchor.MiddleCenter, Amber);
            titleDisplay = Style(display, 74, TextAnchor.MiddleLeft, Sand);
            button = Style(display, 26, TextAnchor.MiddleCenter, Ink);

            white = Solid(Color.white);
            cutTopRight = CornerCut(false);
            cutBottomLeft = CornerCut(true);
            dot = Dot();
            arrow = Arrow();
        }

        private static GUIStyle Style(Font font, int size, TextAnchor alignment, Color color)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                fontStyle = FontStyle.Normal,
                alignment = alignment,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = color }
            };
        }

        private static GUIStyle Left(GUIStyle source, Color color)
        {
            source.alignment = TextAnchor.MiddleLeft;
            source.normal.textColor = color;
            return source;
        }

        private static GUIStyle Right(GUIStyle source, Color color)
        {
            source.alignment = TextAnchor.MiddleRight;
            source.normal.textColor = color;
            return source;
        }

        private static GUIStyle Center(GUIStyle source, Color color)
        {
            source.alignment = TextAnchor.MiddleCenter;
            source.normal.textColor = color;
            return source;
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// White square with one transparent diagonal half; tinted through GUI.color.
        /// The bottom-left variant keeps the top-right half, the other keeps the bottom-left half.
        /// </summary>
        private static Texture2D CornerCut(bool keepTopRight)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size;
                    float v = (y + .5f) / size;
                    float signedDistance = (u + v - 1f) * size;
                    float alpha = Mathf.Clamp01((keepTopRight ? signedDistance : -signedDistance) + .5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D Dot()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f;
                    float dy = y + .5f - size * .5f;
                    float alpha = Mathf.Clamp01(size * .5f - 1f - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>Upward-pointing chevron arrow used for the player marker.</summary>
        private static Texture2D Arrow()
        {
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Vector2 tip = new(.5f, .96f);
            Vector2 left = new(.12f, .08f);
            Vector2 right = new(.88f, .08f);
            Vector2 notch = new(.5f, .34f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < 3; sy++)
                    {
                        for (int sx = 0; sx < 3; sx++)
                        {
                            Vector2 p = new((x + (sx + .5f) / 3f) / size, (y + (sy + .5f) / 3f) / size);
                            if (InsideTriangle(p, tip, left, notch) || InsideTriangle(p, tip, notch, right))
                            {
                                covered++;
                            }
                        }
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, covered / 9f));
                }
            }

            texture.Apply();
            return texture;
        }

        private static bool InsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(Vector2 p, Vector2 a, Vector2 b)
        {
            return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        }

        private static string FormatLapClock(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes}:{seconds - minutes * 60f:00.00}";
        }
    }
}
