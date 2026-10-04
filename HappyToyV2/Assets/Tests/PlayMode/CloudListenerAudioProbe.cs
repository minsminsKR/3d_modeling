using System;
using UnityEngine;

namespace HappyToy.V2.CloudTests
{
    // Test-only, pre-device observation of the authored listener's real DSP mix.
    // The audio thread never modifies incoming samples or calls Unity APIs.
    public sealed class CloudListenerAudioProbe : MonoBehaviour
    {
        readonly object gate = new object();
        float[] samples;
        int used, channelCount, callbackCount;
        long observedSamples;
        bool armed, channelChanged;

        public void Arm(int maximumSamples)
        {
            lock (gate)
            {
                samples = new float[maximumSamples];
                used = channelCount = callbackCount = 0;
                observedSamples = 0;
                channelChanged = false;
                armed = true;
            }
        }

        public int CapturedCount { get { lock (gate) return used; } }

        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (gate)
            {
                if (!armed) return;
                callbackCount++;
                observedSamples += data.Length;
                if (channelCount == 0) channelCount = channels;
                if (channelCount != channels) channelChanged = true;
                int copy = Math.Min(data.Length, samples.Length - used);
                if (copy > 0)
                {
                    Buffer.BlockCopy(data, 0, samples, used * sizeof(float), copy * sizeof(float));
                    used += copy;
                }
            }
        }

        public float[] Finish(out int channels, out int callbacks, out long observed, out bool changed)
        {
            lock (gate)
            {
                armed = false;
                channels = channelCount; callbacks = callbackCount;
                observed = observedSamples; changed = channelChanged;
                var result = new float[used];
                if (used > 0) Buffer.BlockCopy(samples, 0, result, 0, used * sizeof(float));
                return result;
            }
        }

        public void Disarm() { lock (gate) armed = false; }
    }
}
