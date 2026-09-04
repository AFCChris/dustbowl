using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Dustbowl.Telemetry
{
    [DisallowMultipleComponent]
    public sealed class DevelopmentTelemetryRecorder : MonoBehaviour, IBehaviouralTelemetrySink
    {
        [SerializeField, Min(1)] private int capacity = 36000;
        [SerializeField] private bool recording = true;
        [SerializeField] private string captureName = "Stage3 BehaviourLab";
        [SerializeField] private string lastSavedPath;

        private readonly List<string> records = new();

        public bool IsAvailable => Application.isEditor || Debug.isDebugBuild;
        public IReadOnlyList<string> Records => records;
        public bool IsRecording => recording;
        public string CaptureName => captureName;
        public string LastSavedPath => lastSavedPath;

        public static string Serialize<T>(T value)
        {
            return JsonUtility.ToJson(value);
        }

        public string ExportJsonLines()
        {
            return string.Join("\n", records);
        }

        public void Clear()
        {
            records.Clear();
        }

        public void ConfigureCapacity(int newCapacity)
        {
            capacity = Mathf.Max(1, newCapacity);
        }

        public void BeginCapture(string runName)
        {
            Clear();
            captureName = string.IsNullOrWhiteSpace(runName) ? "Stage3 BehaviourLab" : runName;
            recording = true;
        }

        public string EndCaptureAndSave()
        {
            if (!IsAvailable)
            {
                return string.Empty;
            }

            string folder = Path.Combine(Application.persistentDataPath, "DustbowlTelemetry");
            Directory.CreateDirectory(folder);
            string safeName = SanitizeFileName(captureName);
            lastSavedPath = Path.Combine(
                folder,
                $"{System.DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeName}.jsonl");
            File.WriteAllText(lastSavedPath, ExportJsonLines());
            recording = false;
            Debug.Log($"Dustbowl telemetry saved: {lastSavedPath}", this);
            return lastSavedPath;
        }

        public void Record(TelemetrySample sample) => Append("sample", Serialize(sample));
        public void Record(TakeoffEvent takeoff) => Append("takeoff", Serialize(takeoff));
        public void Record(AirborneState airborne) => Append("airborne", Serialize(airborne));
        public void Record(LandingEvent landing) => Append("landing", Serialize(landing));
        public void Record(WipeoutEvent wipeout) => Append("wipeout", Serialize(wipeout));
        public void Record(ResetEvent reset) => Append("reset", Serialize(reset));
        public void Record(RecoveryEvent recovery) => Append("recovery", Serialize(recovery));

        private void Append(string kind, string payload)
        {
            if (!IsAvailable || !recording)
            {
                return;
            }

            if (records.Count >= capacity)
            {
                records.RemoveAt(0);
            }

            records.Add(JsonUtility.ToJson(new TelemetryEnvelope(kind, payload)));
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '-');
            }

            return value.Replace(' ', '-');
        }

        [Serializable]
        private struct TelemetryEnvelope
        {
            public string kind;
            public string payload;

            public TelemetryEnvelope(string recordKind, string recordPayload)
            {
                kind = recordKind;
                payload = recordPayload;
            }
        }
    }
}
