using System;
using System.Collections;
using UnityEngine;

namespace HappyToy.V2
{
    // Installed only by an explicit diagnostic review. The regular gate does
    // not acquire this component until its cut surfaces have been approved.
    public sealed class TempleGateReviewMotion : MonoBehaviour
    {
        public const float Duration = 3f;
        const float Angle = 80f;
        Transform left, right;
        Quaternion leftClosed, rightClosed;
        float leftSign, rightSign;
        Action<bool> passageChanged;
        public float Progress { get; private set; }
        public bool Started { get; private set; }

        public void Prepare(Transform gate, Action<bool> setPassageOpen)
        {
            if (left || right || !gate || setPassageOpen == null) throw new ArgumentException("Fresh gate review is required");
            foreach (var item in gate.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == "Left thin leaf") left = item;
                if (item.name == "Right thin leaf") right = item;
            }
            if (!left || !right) throw new InvalidOperationException("Review gate leaves are missing");
            leftClosed = left.rotation; rightClosed = right.rotation;
            leftSign = Vector3.Dot(left.position - gate.position, gate.right) > 0 ? -1 : 1;
            rightSign = Vector3.Dot(right.position - gate.position, gate.right) > 0 ? -1 : 1;
            if (leftSign == rightSign) throw new InvalidOperationException("Review hinges must be on opposite sides");
            passageChanged = setPassageOpen;
        }
        public void SetProgress(float value)
        {
            if (!left || !right) throw new InvalidOperationException("Review gate is not prepared");
            Progress = Mathf.Clamp01(value);
            float angle = Mathf.SmoothStep(0, Angle, Progress);
            left.rotation = Quaternion.AngleAxis(leftSign * angle, Vector3.up) * leftClosed;
            right.rotation = Quaternion.AngleAxis(rightSign * angle, Vector3.up) * rightClosed;
            passageChanged(Progress >= 1);
        }
        public IEnumerator Open() => Animate(true, 0);
        public IEnumerator OpenDuringGameplay() => Animate(false, 0);
        public IEnumerator ResumeDuringGameplay(float progress) => Animate(false, progress);
        IEnumerator Animate(bool unscaled, float progress)
        {
            if (Started) throw new InvalidOperationException("Review gate opening already started");
            if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0 || progress > 1)
                throw new ArgumentOutOfRangeException(nameof(progress));
            Started = true;
            float elapsed = progress * Duration;
            while (elapsed < Duration)
            {
                SetProgress(elapsed / Duration);
                yield return null;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            SetProgress(1);
        }
    }
}
