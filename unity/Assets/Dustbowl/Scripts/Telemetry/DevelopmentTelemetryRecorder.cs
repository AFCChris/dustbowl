using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dustbowl.Telemetry
{
    [DisallowMultipleComponent]
    public sealed class DevelopmentTelemetryRecorder : MonoBehaviour, IBehaviouralTelemetrySink
    {
        [SerializeField, Min(1)] private int capacity = 4096;

        private readonly List<string> records = new();

        public bool IsAvailable => Application.isEditor || Debug.isDebugBuild;
        public IReadOnlyList<string> Records => records;

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

        public void Record(TelemetrySample sample) => Append("sample", Serialize(sample));
        public void Record(TakeoffEvent takeoff) => Append("takeoff", Serialize(takeoff));
        public void Record(AirborneState airborne) => Append("airborne", Serialize(airborne));
        public void Record(LandingEvent landing) => Append("landing", Serialize(landing));
        public void Record(WipeoutEvent wipeout) => Append("wipeout", Serialize(wipeout));
        public void Record(ResetEvent reset) => Append("reset", Serialize(reset));
        public void Record(RecoveryEvent recovery) => Append("recovery", Serialize(recovery));

        private void Append(string kind, string payload)
        {
            if (!IsAvailable)
            {
                return;
            }

            if (records.Count >= capacity)
            {
                records.RemoveAt(0);
            }

            records.Add(JsonUtility.ToJson(new TelemetryEnvelope(kind, payload)));
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
