# Forest earth source-derived quilting (production)

This material is derived exclusively from the preserved original
`imagegen-original.png` described in the adjacent `README.md` and governed by
the same provenance and source/license terms. The source-derived Repeat tile
is the production default; the unmodified original and its Mirror import
remain available under `-v2-forest-earth-original` for diagnostic comparison.
The Unity asset filenames retain `candidate` for GUID and handoff continuity.

`derive_quilt_candidate.py` verifies the original SHA-256
`27ca10c3e6fab171c0feba68eab3afbaea5ab7351fb814f34c8e1cc2e6cb28bf`.
It converts the original to 1024px and selects 4x4 source-derived 640px crops
using NumPy seed 211107, 32 choices per patch, 90-degree rotations and 128px
minimum-error overlap cuts. No two crops are alpha-blended; foliage detail is
preserved, though cut twigs and repeated source features remain possible.
The 2048px result covers 2x2 of the source tile's physical footprint; Unity
uses (7,9) rather than (14,18) tiling. The output is shifted periodically so
both outer cuts cross uninterrupted adjacent pixels from the same source crop.
The production tile uses Repeat wrapping in both albedo and normal importers
to avoid the long mirrored axis visible in the actual first-person capture.
The original diagnostic texture remains Mirror. In the fixed-seed candidate, across-edge
pixel mean/p95 is 11.88/35 and 11.99/34 versus interior 13.00/37 and 12.99/37;
a CPU central-difference normal proxy also has similar edge and interior
means. These are useful guards, not proof of Unity normal-map/mipmap continuity.
The height input is an exact pixel copy of the generated albedo; Unity's luminance-to-normal
conversion remains an approximation, not a separately captured PBR normal,
height, roughness or specular map.

Reproduction environment: Python 3.11.3, NumPy 1.26.4, Pillow 10.4.0. From the
repository root:

```
python HappyToyV2/SourceArt/TempleForest/forest-earth/derive_quilt_candidate.py --preview
python HappyToyV2/SourceArt/TempleForest/forest-earth/derive_quilt_candidate.py --verify
```

`--preview` writes only to the OS temp review directory. `--verify` recomputes
and compares both tracked Unity input PNG pixels to the deterministic output,
reporting source hash, internal seam, Repeat-edge color differences and the
CPU luminance-derived normal proxy. To create
missing candidate inputs on a fresh checkout use `--write`; it refuses to
overwrite either existing input. `TempleForestEarthImport.VerifyCandidateImports`
forces Unity imports and checks the 2048px albedo and normal settings.

The offscreen actual-camera comparisons used two U and two V boundaries,
moonlight and flashlight, plus albedo-only and normal-only materials. Their
visual ground marker and collider checks reject occluding opaque props, but
translucent fog, puddles, glass, postprocessing and antialiasing can still mix
with the marker. The per-sample RGB deltas are not a perceptual quality score,
a direct GPU normal-map measurement, or proof for all possible positions.

`-v2-forest-earth-original` restores the old albedo/normal, Mirror wrapping
and (14,18) tiling for an isolated diagnostic player or test. Ordinary
players use Repeat and (7,9). Both keep the same color, smoothness, normal
scale, collision and footstep regions. The deliberately retained profile name
`temple-soil-candidate-presentation` now reviews the production Repeat tile;
`temple-soil-original-presentation` reviews the old Mirror baseline.

The new 2048px maps contain four times as many base-level texels as the old
1024px maps. For illustration, two uncompressed RGBA32 base levels require
32 MiB versus 8 MiB (about 42.7 versus 10.7 MiB with a full mip chain); two
8-bits-per-pixel block-compressed maps would require about 10.7 versus 2.7 MiB
with mips. Actual Unity platform formats, driver allocation, texture residency,
CPU-readable copies, GPU time and target-hardware VRAM have not been measured.
Both original and new sources reside in Resources and increase build footprint.

## 2026-11-07 offscreen memory and timing audit

The isolated Unity 6000.6.0f1 D3D12 Editor test on a GeForce RTX 2060 SUPER used the same 1920x1080 camera and A/B/B/A ordering. Unity `Profiler.GetRuntimeMemorySizeLong` estimated 5.42 MiB for the two imported 1024 maps and 21.42 MiB for the two 2048 maps, an approximately 16 MiB increase. These are engine estimates, not measured GPU residency. GPU timing samples were unavailable; synchronous render-and-readback and CPU samples included other Editor work and cannot establish GPU cost or displayed FPS. The raw report is a local verification artifact, not a source dependency.
