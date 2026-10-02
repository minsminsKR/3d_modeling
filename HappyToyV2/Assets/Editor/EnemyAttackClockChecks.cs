using System;

namespace HappyToy.V2.Editor
{
    // No Unity dependency: runs in the safe editor quality command or standalone dotnet test harness.
    public static class EnemyAttackClockChecks
    {
        public static int Run()
        {
            int assertions = 0;
            Action<bool, string> check = (condition, message) =>
            {
                assertions++;
                if (!condition) throw new InvalidOperationException("EnemyAttackClock: " + message);
            };
            var clock = new EnemyAttackClock();
            check(!clock.Active && !clock.WindingUp && !clock.Recovering, "starts idle");
            check(!clock.Tick(1), "idle tick never strikes");
            check(!clock.Begin(float.NaN, 1), "rejects NaN windup");
            check(!clock.Begin(1, float.PositiveInfinity), "rejects infinite recovery");
            check(!clock.Begin(float.NegativeInfinity, 1), "rejects infinite windup");
            check(!clock.Begin(1, float.NaN), "rejects NaN recovery");
            check(clock.Begin(.8f, .9f), "starts a valid warning");
            check(clock.WindingUp && !clock.Recovering && clock.Windup == 0, "initial windup state");
            check(!clock.Begin(.2f, .2f), "cannot overlap active attacks");
            check(!clock.Tick(0) && clock.Windup == 0, "paused time does not consume warning");
            check(!clock.Tick(-1) && clock.Windup == 0, "negative time ignored");
            check(!clock.Tick(float.NaN) && clock.Windup == 0, "NaN time ignored");
            check(!clock.Tick(float.PositiveInfinity) && clock.Windup == 0, "infinite time ignored");
            check(!clock.Tick(.4f) && Math.Abs(clock.Windup - .5f) < .0001f, "half warning cannot strike");
            check(clock.Tick(.4f), "strike occurs once at warning endpoint");
            check(!clock.WindingUp && clock.Recovering && clock.Recovery == 1, "strike enters full recovery");
            check(!clock.Begin(.1f, .1f), "cannot rearm during recovery");
            check(!clock.Tick(0) && clock.Recovery == 1, "pause preserves recovery");
            check(!clock.Tick(.45f) && Math.Abs(clock.Recovery - .5f) < .0001f, "recovery progresses without a strike");
            check(!clock.Tick(.45f) && !clock.Active, "recovery completes without a second strike");
            check(clock.Begin(.75f, 1), "rearms after recovery");
            check(clock.Tick(10) && clock.Recovering && clock.Recovery == 1, "long frame cannot erase recovery");
            check(!clock.Tick(10) && !clock.Active, "long recovery frame never strikes");
            check(clock.Begin(-1, -1), "finite negative durations clamp to minimum");
            check(!clock.Tick(.05f), "minimum warning is not skipped");
            check(clock.Tick(.05f) && clock.Recovering, "minimum warning resolves");
            clock.Reset();
            check(!clock.Active && clock.Windup == 0 && clock.Recovery == 0, "reset clears recovery");
            check(clock.Begin(1, 1), "can begin after reset");
            clock.Reset();
            check(!clock.Tick(2) && !clock.Active, "reset cancels pending strike");
            return assertions;
        }
    }
}
