using System;
using UnityEngine;

namespace HappyToy.V2.CloudTests
{
    // Test-only one-shot observation after production default-order LateUpdate.
    // Unlike yield-null, this can pause the actual rendered attachment pose before
    // another Update rewrites it. No end-of-frame dependency in batch mode.
    [DefaultExecutionOrder(300)]
    public sealed class CloudIntroLateUpdateObserver : MonoBehaviour
    {
        public Action ObserveOnce;
        public bool Completed { get; private set; }
        public Exception Error { get; private set; }
        void LateUpdate()
        {
            if (Completed || ObserveOnce == null) return;
            var observation = ObserveOnce; ObserveOnce = null;
            try { observation(); }
            catch (Exception error) { Error = error; }
            finally { Completed = true; enabled = false; }
        }
    }
}
