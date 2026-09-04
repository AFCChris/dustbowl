using System.Collections.Generic;
using Dustbowl.Bike;
using Dustbowl.Course;
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
        private Texture2D mapRoute;
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

            DrawMinimap(new Rect(22f, 146f, 228f, 190f));

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
            mapRoute = Solid(new Color(.91f, .89f, .85f, .78f));
            mapStart = Solid(new Color(1f, .69f, .13f, 1f));
            mapPlayer = Solid(new Color(.89f, .27f, .23f, 1f));
            mapOpponents = new Texture2D[OpponentMapColors.Length];
            for (int index = 0; index < mapOpponents.Length; index++)
            {
                mapOpponents[index] = Solid(OpponentMapColors[index]);
            }
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

        private void DrawMinimap(Rect rect)
        {
            if (MinimapCoursePointCount < 2)
            {
                return;
            }

            EnsureMapBounds();
            GUI.DrawTexture(rect, dark);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 6f, rect.height), accent);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 5f, rect.width - 20f, 20f), "COURSE", small);
            Rect courseRect = new(rect.x + 14f, rect.y + 27f, rect.width - 27f, rect.height - 39f);
            IReadOnlyList<CourseLinePoint> points = race.Course.Definition.LinePoints;
            for (int index = 0; index < points.Count - 1; index += 3)
            {
                int next = Mathf.Min(index + 3, points.Count - 1);
                DrawLine(MapToRect(points[index].center, courseRect), MapToRect(points[next].center, courseRect), mapRoute, 2.5f);
            }

            DrawMarker(MapToRect(points[0].center, courseRect), mapStart, 7f);
            for (int index = 0; index < race.Opponents.Count; index++)
            {
                DrawMarker(
                    MapToRect(race.Opponents[index].transform.position, courseRect),
                    mapOpponents[index % mapOpponents.Length],
                    6f);
            }

            Vector2 playerPoint = MapToRect(player.State.position, courseRect);
            DrawMarker(playerPoint, mapPlayer, 8f);
            Vector2 heading = new(
                Mathf.Sin(player.State.yawRadians),
                -Mathf.Cos(player.State.yawRadians));
            DrawLine(playerPoint, playerPoint + heading.normalized * 10f, mapPlayer, 2f);
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

            Vector2 padding = (mapMax - mapMin) * .06f;
            mapMin -= padding;
            mapMax += padding;
            mapBoundsReady = true;
        }

        private Vector2 MapToRect(Vector3 worldPosition, Rect rect)
        {
            Vector2 normalized = WorldToMinimapNormalized(worldPosition);
            float sourceAspect = Mathf.Max(.001f, (mapMax.x - mapMin.x) / (mapMax.y - mapMin.y));
            float rectAspect = rect.width / rect.height;
            Rect fitted = rect;
            if (sourceAspect > rectAspect)
            {
                fitted.height = rect.width / sourceAspect;
                fitted.y += (rect.height - fitted.height) * .5f;
            }
            else
            {
                fitted.width = rect.height * sourceAspect;
                fitted.x += (rect.width - fitted.width) * .5f;
            }

            return new Vector2(
                Mathf.Lerp(fitted.xMin, fitted.xMax, normalized.x),
                Mathf.Lerp(fitted.yMin, fitted.yMax, normalized.y));
        }

        private static void DrawMarker(Vector2 center, Texture2D texture, float size)
        {
            GUI.DrawTexture(new Rect(center.x - size * .5f, center.y - size * .5f, size, size), texture);
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
