using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Owns only the sequence and timing of short story lines. The world owns
    // their meaning and triggers; the HUD receives the current presentation.
    [DisallowMultipleComponent]
    public sealed class StoryNarration : MonoBehaviour
    {
        readonly struct Line
        {
            public readonly string text;
            public readonly float seconds;
            public readonly Texture2D ink;
            public Line(string text, float seconds, Texture2D ink = null)
            { this.text = text; this.seconds = seconds; this.ink = ink; }
        }
        readonly Queue<Line> pending = new Queue<Line>();
        Func<bool> held;
        float remaining;
        public string CurrentText { get; private set; } = string.Empty;
        // Borrowed from the evidence owner; this component never destroys it.
        public Texture2D CurrentInk { get; private set; }

        public void Prepare(Func<bool> simulationHeld)
        {
            if (held != null) throw new InvalidOperationException("Narration already prepared");
            held = simulationHeld ?? throw new ArgumentNullException(nameof(simulationHeld));
        }

        Line ValidateLine(string text, float seconds)
        {
            if (held == null) throw new InvalidOperationException("Narration is not prepared");
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Narration requires text", nameof(text));
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            return new Line(text.Trim(), seconds);
        }

        public void Enqueue(string text, float seconds) => pending.Enqueue(ValidateLine(text, seconds));

        // Explicit story events and clue rereads replace stale guidance rather
        // than replaying lines from an earlier phase after a masked transition.
        public void Replace(string text, float seconds)
        {
            var line = ValidateLine(text, seconds); // A bad request preserves the current line.
            Clear(); pending.Enqueue(line);
        }

        // The same borrowed texture shown on the world paper accompanies only
        // this reading line; queued ordinary narration has no evidence image.
        public void ReplaceEvidence(string text, float seconds, Texture2D ink)
        {
            var line = ValidateLine(text, seconds);
            if (!ink) throw new ArgumentNullException(nameof(ink));
            Clear(); pending.Enqueue(new Line(line.text, line.seconds, ink));
        }

        public void Clear()
        {
            pending.Clear(); CurrentText = string.Empty; CurrentInk = null; remaining = 0;
        }

        void Update() => Advance(Time.deltaTime);

        void Advance(float delta)
        {
            if (held == null || held()) return;
            if (string.IsNullOrEmpty(CurrentText))
            {
                if (pending.Count == 0) return;
                var line = pending.Dequeue();
                CurrentText = line.text;
                CurrentInk = line.ink;
                remaining = line.seconds;
                return; // A new line receives its full reading time even after a slow frame.
            }
            remaining -= Mathf.Max(0, delta);
            if (remaining <= 0) { CurrentText = string.Empty; CurrentInk = null; remaining = 0; }
        }

        void OnDestroy()
        {
            Clear(); held = null;
        }
    }
}
