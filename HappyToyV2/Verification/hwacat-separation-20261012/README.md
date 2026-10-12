# Separate Chapter 1 Uncat from school Hwacat

Uncat (운캣) is the Chapter 1 four-legged cat. Hwacat (화캣) is a distinct school
portrait monster: the original normal doll stands and dances, freezes briefly,
then becomes a devil-like creature before a protected chase handoff.

EnemyVisualRefinement now preserves Hwacat_angry's key instead of passing Uncat
to the Studio replacement loader. It uses the existing committed
Assets/Resources/EnemyRefinement/Hwacat_angry-v2.fbx, its normal/smoothness maps,
original BaseMap and patrol/chase curves, and ThreatEyes/Hwacat_angry emission.
Chapter 1 counterplay text now uses the correct 운캣 spelling; the inactive legacy
Tracker role uses a generic label instead of implying school Hwacat is in Chapter 1.
The normal doll and its own stand/dance clips are retained. No base model is
regenerated; no authored scene, navigation capsule, reveal timing, attack,
recorded sound, or save schema is replaced. Source/authoring assets and licenses
remain with their existing project locations.

The PlayMode report contains four passing real-scene cases: Chapter 1's three
Studio actors, school Hwacat mesh/clip/eye identity and checkpoint capture,
physical portrait/dance/transform/grace/pause/chase handoff, and corridor threat
release with no Hwacat slot. Test transport image payloads are removed; assertions
and outcomes remain. The opt-in HwacatAudit now uses the actual school chapter,
checks the distinct mesh/clip binding and inactive school Uncat, and captures
original stand/dance/demonic stages via a controlled public reveal. It uses the
silent process guard and isolated save directory. The native report/PNGs do not
claim a complete survival run or hardware performance.

Use pinned Unity 6000.6.0f1 and the standard QualityValidation.BuildWindows
pipeline. Run the player with -v2-hwacat-output and -quiet-diagnostics for native
verification. build-inputs.json and source-completeness.json verify current
source/build hashes and required editable sources. Work is committed locally;
no remote push is made unless explicitly requested.
