# Build8 candidate packaging

Actual tested/exported source:923060837b0cb8585d16d9bfffb961434a850504. UBA8 passed6EditMode+15PlayMode and finishedSUCCESS. The export contains the new pursuit runtime; production Assembly-CSharp.dll SHA256 is9bc94f3b6aeed0e5b8429c50a892bf67dea2acbb4156e6b472b72118e44d98fa. Metadata contains SearchArea, BeginSearch, get_SearchOrigin, get_SearchPointsVisited and get_LastKnownPosition, in addition to the verified exact checkout/build log.

- Original ZIP:73,166,499bytes,186entries, SHA256fde361d88b69e4c2ee48bdbf5d71c631bcea9bf2bae2658ab4fb315d0be3b67f; all CRCs pass and original bytes remain unchanged
- Separate candidate ZIP:67,573,893bytes,185entries, SHA2567b5eb902952c3328bbe3b536a10b4ca8246624042cc64fbba8f411e0fca2c8be
- Only excluded payload:228,583byte Burst-generated backup text beneath BackUpThisFolder_ButDontShipItWithYourGame
- Every retained payload is byte-identical to the original. A second run produced an identical ZIP
- Actual player/runtime/scene/native PE structures verified; no UTF/NUnit test assemblies included. Referenced Unity.Collections test-named dependencies remain intact

This operation does not execute the Windows player or resolve redistribution rights. It preserves app.info's Development name, compiled instrumentation and all other behavior. This is a private development/testing candidate, not a commercial release. The per-file integrity and exclusion manifest is build-8-candidate.manifest.json.
