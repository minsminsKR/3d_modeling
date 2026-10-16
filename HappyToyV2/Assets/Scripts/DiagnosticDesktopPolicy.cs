using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Read before scene Awake, without installing a component or changing any
    // desktop state. Ordinary players retain their original cursor ownership.
    public static class DiagnosticDesktopPolicy
    {
        public const string NoDesktopInteractionFlag = "-v2-audit-no-desktop-interaction";
        public static bool Requested { get; } = Array.IndexOf(Environment.GetCommandLineArgs(), NoDesktopInteractionFlag) >= 0;
        public static bool SuppressCursorWrites => ShouldSuppressCursorWrites(Requested, Application.isBatchMode);
        public static bool ShouldSuppressCursorWrites(bool explicitlyRequested, bool batchMode) => explicitlyRequested && batchMode;
    }
}
