using System;
using NUnit.Framework;
using R = HappyToy.V2.TempleStoneLayoutRules;

namespace HappyToy.V2.CoreTests
{
    public sealed class TempleStoneLayoutRulesTests
    {
        static readonly R.Rectangle Path = new R.Rectangle(-1.7, -15.5, 1.7, 15.5);
        static readonly R.Footprint[] Models = {
            new R.Footprint(.30, .25, .85, 1.10),
            new R.Footprint(.22, .12, .8, 1.05) };

        [Test]
        public void FixedSeedUsesPortableGoldenSequenceAndIgnoresUnrelatedRandomCalls()
        {
            var bounds = new R.Rectangle(0, 0, 10, 10);
            var footprints = new[] { new R.Footprint(.2, .3, 1, 1) };
            var first = R.Generate(bounds, footprints, null, 1234, 0, .01, 0, 3, 30);
            Assert.That(first.Count, Is.EqualTo(3)); Assert.That(first.Attempts, Is.EqualTo(3));
            Assert.That(first[0].HeadingStep, Is.EqualTo(1));
            Assert.That(first[0].CenterX, Is.EqualTo(7.796004414558411).Within(1e-12));
            Assert.That(first[0].CenterZ, Is.EqualTo(8.734586238861084).Within(1e-12));
            Assert.That(first[1].HeadingStep, Is.EqualTo(7));
            Assert.That(first[1].CenterX, Is.EqualTo(7.021759748458862).Within(1e-12));
            Assert.That(first[2].HeadingStep, Is.EqualTo(14));
            var unrelated = new Random(7); for (int i = 0; i < 100; i++) unrelated.Next();
            var again = R.Generate(bounds, footprints, null, 1234, 0, .01, 0, 3, 30);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(again[i].CenterX, Is.EqualTo(first[i].CenterX));
                Assert.That(again[i].CenterZ, Is.EqualTo(first[i].CenterZ));
                Assert.That(again[i].Scale, Is.EqualTo(first[i].Scale));
                Assert.That(again[i].HeadingStep, Is.EqualTo(first[i].HeadingStep));
            }
            var changed = R.Generate(bounds, footprints, null, 1235, 0, .01, 0, 3, 30);
            Assert.That(changed[0].CenterX, Is.Not.EqualTo(first[0].CenterX));
            // Zero seed is allowed but must not freeze xorshift at zero.
            var zero = R.Generate(bounds, footprints, null, 0, 0, .01, 0, 3, 30);
            Assert.That(zero.Count, Is.EqualTo(3));
            Assert.That(zero[0].CenterX, Is.Not.EqualTo(zero[1].CenterX));
        }

        [Test]
        public void EntireRotatedFootprintsStayInBoundsAndClearEachOtherAndExcludedRegion()
        {
            const double border = .04, gap = .008;
            var exclusion = new R.Rectangle(-.4, -2, .4, 2);
            var layout = R.Generate(Path, Models, new[] { exclusion }, 43093, border, gap, .3, 125, 9000);
            Assert.That(layout.Count, Is.GreaterThan(80));
            Assert.That(layout.Count, Is.LessThanOrEqualTo(125)); Assert.That(layout.Attempts, Is.LessThanOrEqualTo(9000));
            for (int i = 0; i < layout.Count; i++)
            {
                var stone = layout[i]; Assert.That(stone.FootprintIndex, Is.InRange(0, Models.Length - 1));
                var shape = Models[stone.FootprintIndex]; Assert.That(stone.Scale, Is.InRange(shape.MinScale, shape.MaxScale));
                var corners = Corners(stone, shape);
                foreach (var p in corners)
                {
                    Assert.That(p.X, Is.InRange(Path.MinX + border - 1e-10, Path.MaxX - border + 1e-10));
                    Assert.That(p.Z, Is.InRange(Path.MinZ + border - 1e-10, Path.MaxZ - border + 1e-10));
                }
                Assert.That(Distance(corners, RectangleCorners(exclusion)), Is.GreaterThanOrEqualTo(gap - 1e-9), "excluded area");
                for (int j = 0; j < i; j++)
                    Assert.That(Distance(corners, Corners(layout[j], Models[layout[j].FootprintIndex])),
                        Is.GreaterThanOrEqualTo(gap - 1e-9), "stone pair " + i + "," + j);
            }
        }

        [Test]
        public void ImpossibleOrExhaustedRequestsReturnBoundedExplicitShortfall()
        {
            var impossible = R.Generate(new R.Rectangle(0, 0, 1, 1), new[] { new R.Footprint(2, 2, 1, 1) },
                null, 0, 0, 0, 0, 7, 25);
            Assert.That(impossible.Count, Is.Zero); Assert.That(impossible.Attempts, Is.EqualTo(25));
            Assert.That(impossible.Requested, Is.EqualTo(7)); Assert.That(impossible.Underfilled, Is.True);
            var empty = R.Generate(Path, Models, null, 0, 0, 0, 0, 0, 500);
            Assert.That(empty.Count, Is.Zero); Assert.That(empty.Attempts, Is.Zero); Assert.That(empty.Underfilled, Is.False);
            var limited = R.Generate(Path, Models, null, 2, 0, 0, 0, 10, 0);
            Assert.That(limited.Count, Is.Zero); Assert.That(limited.Underfilled, Is.True);
        }

        [Test]
        public void SideFadeReducesEdgeOccupancyAcrossFixedSeeds()
        {
            var bounds = new R.Rectangle(-5, -5, 5, 5);
            var f = new[] { new R.Footprint(.2, .2, 1, 1) };
            int noFade = 0, fade = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                var a = R.Generate(bounds, f, null, seed, 0, .01, 0, 40, 1500);
                var b = R.Generate(bounds, f, null, seed, 0, .01, 2, 40, 1500);
                Assert.That(a.Count, Is.EqualTo(40)); Assert.That(b.Count, Is.EqualTo(40));
                for (int i = 0; i < a.Count; i++) if (Math.Abs(a[i].CenterX) > 3.5) noFade++;
                for (int i = 0; i < b.Count; i++) if (Math.Abs(b[i].CenterX) > 3.5) fade++;
            }
            Assert.That(fade, Is.LessThan(noFade * .80), "Fixed seeded sample should taper the sides, not merely move the path");
        }

        [Test]
        public void RejectsInvalidDimensionsScalesAndUnboundedWork()
        {
            Assert.Throws<ArgumentException>(() => R.Generate(new R.Rectangle(0, 0, double.PositiveInfinity, 1), Models, null, 1, 0, 0, 0, 1, 1));
            Assert.Throws<ArgumentException>(() => R.Generate(Path, new R.Footprint[0], null, 1, 0, 0, 0, 1, 1));
            Assert.Throws<ArgumentException>(() => R.Generate(Path, new[] { new R.Footprint(.2, .2, double.NaN, 1) }, null, 1, 0, 0, 0, 1, 1));
            Assert.Throws<ArgumentException>(() => R.Generate(Path, new[] { new R.Footprint(double.Epsilon, .2, double.Epsilon, 1) }, null, 1, 0, 0, 0, 1, 1));
            Assert.Throws<ArgumentException>(() => R.Generate(Path, Models, new[] { new R.Rectangle(0, 0, 0, 1) }, 1, 0, 0, 0, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => R.Generate(Path, Models, null, 1, -.01, 0, 0, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => R.Generate(Path, Models, null, 1, 0, -1, 0, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => R.Generate(Path, Models, null, 1, 0, 0, double.NaN, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => R.Generate(Path, Models, null, 1, 0, 0, 0, 2049, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => R.Generate(Path, Models, null, 1, 0, 0, 0, 1000, 50000));
        }

        readonly struct P
        {
            public readonly double X, Z;
            public P(double x, double z) { X = x; Z = z; }
            public static P operator -(P a, P b) { return new P(a.X - b.X, a.Z - b.Z); }
        }
        static P[] Corners(R.Stone s, R.Footprint f)
        {
            double angle = s.HeadingDegrees * Math.PI / 180, c = Math.Cos(angle), sn = Math.Sin(angle);
            double x = f.HalfWidth * s.Scale, z = f.HalfLength * s.Scale;
            var raw = new[] { new P(-x, -z), new P(x, -z), new P(x, z), new P(-x, z) };
            var result = new P[4];
            for (int i = 0; i < 4; i++) result[i] = new P(s.CenterX + c * raw[i].X - sn * raw[i].Z,
                s.CenterZ + sn * raw[i].X + c * raw[i].Z);
            return result;
        }
        static P[] RectangleCorners(R.Rectangle r)
        { return new[] { new P(r.MinX, r.MinZ), new P(r.MaxX, r.MinZ), new P(r.MaxX, r.MaxZ), new P(r.MinX, r.MaxZ) }; }
        static double Cross(P a, P b) { return a.X * b.Z - a.Z * b.X; }
        static double Dot(P a, P b) { return a.X * b.X + a.Z * b.Z; }
        static bool Contains(P[] polygon, P p)
        {
            bool negative = false, positive = false;
            for (int i = 0; i < 4; i++)
            {
                double c = Cross(polygon[(i + 1) % 4] - polygon[i], p - polygon[i]);
                negative |= c < -1e-12; positive |= c > 1e-12;
            }
            return !(positive && negative);
        }
        static bool Intersects(P a, P b, P c, P d)
        {
            double p = Cross(b - a, c - a), q = Cross(b - a, d - a);
            double r = Cross(d - c, a - c), s = Cross(d - c, b - c);
            // Collinear cases are handled by point-to-segment distance below.
            return ((p > 0 && q < 0) || (p < 0 && q > 0)) && ((r > 0 && s < 0) || (r < 0 && s > 0));
        }
        static double PointSegmentDistance(P p, P a, P b)
        {
            P d = b - a; double t = Math.Max(0, Math.Min(1, Dot(p - a, d) / Dot(d, d)));
            double dx = p.X - (a.X + t * d.X), dz = p.Z - (a.Z + t * d.Z);
            return Math.Sqrt(dx * dx + dz * dz);
        }
        static double Distance(P[] a, P[] b)
        {
            if (Contains(a, b[0]) || Contains(b, a[0])) return 0;
            double best = double.PositiveInfinity;
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++)
            {
                P a1 = a[i], a2 = a[(i + 1) % 4], b1 = b[j], b2 = b[(j + 1) % 4];
                if (Intersects(a1, a2, b1, b2)) return 0;
                best = Math.Min(best, PointSegmentDistance(a1, b1, b2));
                best = Math.Min(best, PointSegmentDistance(b1, a1, a2));
            }
            return best;
        }
    }
}
