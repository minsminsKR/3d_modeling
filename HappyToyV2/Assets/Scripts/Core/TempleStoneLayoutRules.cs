using System;
using System.Collections.Generic;

namespace HappyToy.V2
{
    // Deterministic placement math. A footprint is a conservative rectangle
    // enclosing one model's actual horizontal silhouette at scale 1. The caller
    // owns extraction of those bounds, model pivot alignment and terrain contact.
    // No Unity API, gameplay RNG, collision or scene state is used here.
    public static class TempleStoneLayoutRules
    {
        public readonly struct Rectangle
        {
            public readonly double MinX, MinZ, MaxX, MaxZ;
            public Rectangle(double minX, double minZ, double maxX, double maxZ)
            { MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; }
        }

        public readonly struct Footprint
        {
            public readonly double HalfWidth, HalfLength, MinScale, MaxScale;
            public Footprint(double halfWidth, double halfLength, double minScale, double maxScale)
            { HalfWidth = halfWidth; HalfLength = halfLength; MinScale = minScale; MaxScale = maxScale; }
        }

        public readonly struct Stone
        {
            public readonly double CenterX, CenterZ, Scale;
            public readonly int FootprintIndex, HeadingStep;
            public Stone(double centerX, double centerZ, double scale, int footprintIndex, int headingStep)
            { CenterX = centerX; CenterZ = centerZ; Scale = scale; FootprintIndex = footprintIndex; HeadingStep = headingStep; }
            // In the x/z plane positive heading turns local +x toward +z.
            // A Unity y-axis yaw therefore uses the negative of this value.
            public double HeadingDegrees { get { return HeadingStep * 22.5; } }
        }

        public sealed class Result
        {
            readonly Stone[] stones;
            internal Result(List<Stone> source, int attempts, int requested)
            { stones = source.ToArray(); Attempts = attempts; Requested = requested; }
            public int Attempts { get; }
            public int Requested { get; }
            public int Count { get { return stones.Length; } }
            public bool Underfilled { get { return Count < Requested; } }
            public Stone this[int index] { get { return stones[index]; } }
        }

        // Fixed direction table avoids runtime-dependent trigonometry. Heading
        // 0 is local +x along world +x. All directions are used at fixed steps.
        static readonly double[] Cos = { 1, .9238795325112867, .7071067811865476, .3826834323650898,
            0, -.3826834323650898, -.7071067811865476, -.9238795325112867,
            -1, -.9238795325112867, -.7071067811865476, -.3826834323650898,
            0, .3826834323650898, .7071067811865476, .9238795325112867 };
        static readonly double[] Sin = { 0, .3826834323650898, .7071067811865476, .9238795325112867,
            1, .9238795325112867, .7071067811865476, .3826834323650898,
            0, -.3826834323650898, -.7071067811865476, -.9238795325112867,
            -1, -.9238795325112867, -.7071067811865476, -.3826834323650898 };

        readonly struct Box
        {
            public readonly double X, Z, HalfX, HalfZ, C, S;
            public Box(double x, double z, double halfX, double halfZ, double c, double s)
            { X = x; Z = z; HalfX = halfX; HalfZ = halfZ; C = c; S = s; }
            public double ExtentX { get { return Math.Abs(C) * HalfX + Math.Abs(S) * HalfZ; } }
            public double ExtentZ { get { return Math.Abs(S) * HalfX + Math.Abs(C) * HalfZ; } }
        }

        struct Random32
        {
            uint state;
            public Random32(uint seed) { state = seed == 0 ? 0x6D2B79F5u : seed; }
            public uint Next()
            {
                // xorshift32, fixed algorithm and unchecked wraparound; not
                // cryptographic and never shared with gameplay RNG.
                uint x = state;
                x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                state = x; return x;
            }
            public double Unit() { return (Next() >> 8) * (1.0 / 16777216.0); }
        }

        // Seeded bounded rejection sampling. 0 <= edgeFade <= half the usable
        // path width; near the long sides probability tapers linearly. Positive
        // gap is conservatively enforced along a separating axis of each pair.
        // Obstacles are world-aligned rectangles and use the same clearance.
        // Shortfall is explicit: a full result or visually pleasing distribution
        // is not guaranteed, and actual meshes must fit their supplied bounds.
        // RNG and direction values are fixed; floating-point boundary decisions
        // are not certified bit-identical across different CPU/runtime builds.
        // Do not persist only the seed without cross-platform tests or storing
        // the approved placement data itself.
        public static Result Generate(Rectangle path, Footprint[] footprints, Rectangle[] exclusions,
            uint seed, double border, double gap, double edgeFade, int maxStones, int maxAttempts)
        {
            return GenerateWithOccupied(path, footprints, exclusions, null, null, seed, border, gap,
                edgeFade, maxStones, maxAttempts);
        }

        // Fill the gaps around an already-approved layout without moving its
        // stones. Existing and new footprints may use different scale ranges,
        // but retain the same mesh index mapping. The same conservative box
        // separation applies to existing/new and new/new pairs.
        public static Result FillGaps(Rectangle path, Footprint[] primaryFootprints, Result primary,
            Footprint[] fillFootprints, uint seed, double border, double gap, double edgeFade,
            int maxStones, int maxAttempts)
        {
            if (primary == null) throw new ArgumentNullException(nameof(primary));
            if (primaryFootprints == null || fillFootprints == null || primaryFootprints.Length != fillFootprints.Length)
                throw new ArgumentException("Primary and fill footprints must use the same model indices");
            Validate(path, primaryFootprints, null, border, gap, edgeFade, maxStones, maxAttempts);
            if ((long)(primary.Count + maxStones) * maxAttempts > 20000000)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Combined packing work exceeds the budget");
            return GenerateWithOccupied(path, fillFootprints, null, primary, primaryFootprints, seed,
                border, gap, edgeFade, maxStones, maxAttempts);
        }

        static Result GenerateWithOccupied(Rectangle path, Footprint[] footprints, Rectangle[] exclusions,
            Result primary, Footprint[] primaryFootprints, uint seed, double border, double gap,
            double edgeFade, int maxStones, int maxAttempts)
        {
            Validate(path, footprints, exclusions, border, gap, edgeFade, maxStones, maxAttempts);
            var stones = new List<Stone>(maxStones);
            var occupied = new List<Box>(maxStones + (primary == null ? 0 : primary.Count));
            if (primary != null)
                for (int i = 0; i < primary.Count; i++)
                {
                    Stone stone = primary[i];
                    if (stone.FootprintIndex < 0 || stone.FootprintIndex >= primaryFootprints.Length ||
                        stone.HeadingStep < 0 || stone.HeadingStep >= Cos.Length || !Finite(stone.CenterX) ||
                        !Finite(stone.CenterZ) || !Finite(stone.Scale) || stone.Scale <= 0)
                        throw new ArgumentException("Invalid primary stone layout", nameof(primary));
                    Footprint shape = primaryFootprints[stone.FootprintIndex];
                    occupied.Add(new Box(stone.CenterX, stone.CenterZ, shape.HalfWidth * stone.Scale,
                        shape.HalfLength * stone.Scale, Cos[stone.HeadingStep], Sin[stone.HeadingStep]));
                }
            var obstacles = new Box[exclusions == null ? 0 : exclusions.Length];
            for (int i = 0; i < obstacles.Length; i++)
            {
                var r = exclusions[i];
                obstacles[i] = new Box(r.MinX + (r.MaxX - r.MinX) * .5, r.MinZ + (r.MaxZ - r.MinZ) * .5,
                    (r.MaxX - r.MinX) * .5, (r.MaxZ - r.MinZ) * .5, 1, 0);
            }
            var rng = new Random32(seed);
            double left = path.MinX + border, right = path.MaxX - border;
            double near = path.MinZ + border, far = path.MaxZ - border;
            int attempts = 0;
            while (attempts < maxAttempts && stones.Count < maxStones)
            {
                attempts++;
                int index = (int)(rng.Next() % (uint)footprints.Length);
                int heading = (int)(rng.Next() & 15);
                var f = footprints[index];
                double scale = f.MinScale + rng.Unit() * (f.MaxScale - f.MinScale);
                double x = left + rng.Unit() * (right - left), z = near + rng.Unit() * (far - near);
                double c = Cos[heading], s = Sin[heading];
                var candidate = new Box(x, z, f.HalfWidth * scale, f.HalfLength * scale, c, s);
                if (x - candidate.ExtentX < left || x + candidate.ExtentX > right ||
                    z - candidate.ExtentZ < near || z + candidate.ExtentZ > far) continue;
                // Taper the chance by distance from the usable lateral edge,
                // including the full rotated footprint, not only its centre.
                double clearance = Math.Min(x - candidate.ExtentX - left, right - x - candidate.ExtentX);
                if (edgeFade > 0 && rng.Unit() >= Math.Min(1, clearance / edgeFade)) continue;
                bool free = true;
                foreach (var obstacle in obstacles) if (!Separated(candidate, obstacle, gap)) { free = false; break; }
                if (!free) continue;
                foreach (var previous in occupied) if (!Separated(candidate, previous, gap)) { free = false; break; }
                if (!free) continue;
                stones.Add(new Stone(x, z, scale, index, heading)); occupied.Add(candidate);
            }
            return new Result(stones, attempts, maxStones);
        }

        static bool Separated(Box a, Box b, double gap)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z;
            return AxisSeparates(a, b, dx, dz, a.C, a.S, gap) ||
                AxisSeparates(a, b, dx, dz, -a.S, a.C, gap) ||
                AxisSeparates(a, b, dx, dz, b.C, b.S, gap) ||
                AxisSeparates(a, b, dx, dz, -b.S, b.C, gap);
        }

        static bool AxisSeparates(Box a, Box b, double dx, double dz, double ux, double uz, double gap)
        {
            double centerDistance = Math.Abs(dx * ux + dz * uz);
            double extentA = a.HalfX * Math.Abs(a.C * ux + a.S * uz) + a.HalfZ * Math.Abs(-a.S * ux + a.C * uz);
            double extentB = b.HalfX * Math.Abs(b.C * ux + b.S * uz) + b.HalfZ * Math.Abs(-b.S * ux + b.C * uz);
            return centerDistance >= extentA + extentB + gap;
        }

        static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        static bool ValidRectangle(Rectangle r)
        { return Finite(r.MinX) && Finite(r.MinZ) && Finite(r.MaxX) && Finite(r.MaxZ) &&
            Finite(r.MaxX - r.MinX) && Finite(r.MaxZ - r.MinZ) && r.MaxX > r.MinX && r.MaxZ > r.MinZ; }

        static void Validate(Rectangle path, Footprint[] footprints, Rectangle[] exclusions,
            double border, double gap, double edgeFade, int maxStones, int maxAttempts)
        {
            if (!ValidRectangle(path)) throw new ArgumentException("Path bounds must be finite and nonempty", nameof(path));
            if (footprints == null || footprints.Length == 0 || footprints.Length > 128)
                throw new ArgumentException("Supply between 1 and 128 model footprints", nameof(footprints));
            foreach (var f in footprints)
                if (!Finite(f.HalfWidth) || !Finite(f.HalfLength) || !Finite(f.MinScale) || !Finite(f.MaxScale) ||
                    f.HalfWidth <= 0 || f.HalfLength <= 0 || f.MinScale <= 0 || f.MaxScale < f.MinScale ||
                    f.HalfWidth * f.MinScale < 1e-9 || f.HalfLength * f.MinScale < 1e-9 ||
                    !Finite(f.HalfWidth * f.MaxScale) || !Finite(f.HalfLength * f.MaxScale))
                    throw new ArgumentException("Footprint dimensions and scales must be finite and positive", nameof(footprints));
            if (exclusions != null)
            {
                if (exclusions.Length > 256) throw new ArgumentException("At most 256 exclusions", nameof(exclusions));
                foreach (var r in exclusions) if (!ValidRectangle(r)) throw new ArgumentException("Invalid exclusion bounds", nameof(exclusions));
            }
            if (!Finite(border) || border < 0 || border * 2 >= path.MaxX - path.MinX || border * 2 >= path.MaxZ - path.MinZ)
                throw new ArgumentOutOfRangeException(nameof(border));
            if (!Finite(gap) || gap < 0) throw new ArgumentOutOfRangeException(nameof(gap));
            if (!Finite(edgeFade) || edgeFade < 0 || edgeFade > (path.MaxX - path.MinX - border * 2) * .5)
                throw new ArgumentOutOfRangeException(nameof(edgeFade));
            // Explicit finite work budget; no unbounded packing loop.
            if (maxStones < 0 || maxStones > 2048) throw new ArgumentOutOfRangeException(nameof(maxStones));
            if (maxAttempts < 0 || maxAttempts > 50000 || (long)maxStones * maxAttempts > 20000000)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }
    }
}
