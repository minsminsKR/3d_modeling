using System;

namespace HappyToy.V2.Editor
{
    // Pure C# checks: no renderer, scene, Unity timing or shader compilation is implied.
    public static class SurfaceRippleChecks
    {
        public static int Run()
        {
            int count = 0;
            Action<bool, string> check = (condition, message) =>
            {
                count++;
                if (!condition) throw new InvalidOperationException("SurfaceRippleBuffer: " + message);
            };
            var buffer = new SurfaceRippleBuffer();
            check(SurfaceRippleBuffer.Capacity == 4 && buffer.Clock == 0, "bounded initial state");
            check(buffer[0].Strength == 0 && buffer[3].Strength == 0, "starts with no active contacts");
            check(buffer.Add(12, -28, .55f), "accepts a walking contact");
            check(buffer[0].X == 12 && buffer[0].Z == -28 && buffer[0].Started == 0 && buffer[0].Strength == .55f,
                "preserves coordinates and strength");
            check(!buffer.Add(float.NaN, 0, 1), "rejects NaN location");
            check(!buffer.Add(0, float.PositiveInfinity, 1), "rejects infinite location");
            check(!buffer.Add(0, 0, float.NaN), "rejects NaN strength");
            check(!buffer.Add(0, 0, 0) && !buffer.Add(0, 0, -1), "rejects inaudible contacts");
            buffer.Tick(0); buffer.Tick(-1); buffer.Tick(float.NaN); buffer.Tick(float.PositiveInfinity);
            check(buffer.Clock == 0 && buffer[0].Strength == .55f, "invalid or paused time does not consume ripples");
            buffer.Tick(.5f);
            check(buffer.Clock == .5f && buffer[0].Strength > 0, "active contact survives early tick");
            check(buffer.Add(13, -29, 5) && buffer[1].Strength == 1, "clamps stronger contacts");
            check(buffer[1].Started == .5f, "records the current contact clock");
            buffer.Tick(1.9f);
            check(buffer[0].Strength == 0 && buffer[1].Strength > 0, "expires each contact by its own age");
            buffer.Clear();
            check(buffer.Clock == 2.4f, "comfort clear preserves time continuity");
            check(buffer[0].Strength == 0 && buffer[1].Strength == 0, "comfort clear removes existing contacts");
            for (int i = 0; i < 6; i++) buffer.Add(i, -i, 1);
            check(buffer[0].X == 4 && buffer[1].X == 5 && buffer[2].X == 2 && buffer[3].X == 3,
                "ring retains exactly the four newest contacts");
            buffer.Tick(20);
            check(buffer.Clock == 12.4f, "long frame is bounded and never replays missed contacts");
            check(buffer[0].Strength == 0 && buffer[1].Strength == 0 && buffer[2].Strength == 0 && buffer[3].Strength == 0,
                "long hitch expires all contacts");
            check(buffer.Add(-1, -2, 1), "can add after expiration");
            buffer.Clear();
            check(buffer.Add(1, 2, .5f) && buffer[0].X == 1, "clearing resets the bounded write cursor");
            return count;
        }
    }
}
