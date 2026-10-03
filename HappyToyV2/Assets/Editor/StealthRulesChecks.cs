using System;

namespace HappyToy.V2.Editor
{
    // Executes the shared gameplay implementation, with no Unity or mirrored formula dependency.
    public static class StealthRulesChecks
    {
        public static int Run()
        {
            int count = 0;
            Action<bool, string> check = (condition, message) =>
            {
                count++;
                if (!condition) throw new InvalidOperationException("StealthRules: " + message);
            };
            Func<float, float, bool> near = (actual, expected) => Math.Abs(actual - expected) < .0001f;

            check(StealthRules.FootstepRadius(false, false, false) == 4, "dry walking radius");
            check(StealthRules.FootstepRadius(false, false, true) == 7, "wet walking radius");
            check(StealthRules.FootstepRadius(false, true, false) == 10, "dry sprint radius");
            check(StealthRules.FootstepRadius(false, true, true) == 14, "wet sprint radius");
            check(StealthRules.FootstepRadius(true, false, false) == 1.75f, "dry crouch radius");
            check(StealthRules.FootstepRadius(true, false, true) == 3.5f, "wet crouch radius");
            check(StealthRules.FootstepRadius(true, true, false) == 1.75f, "crouch wins over stale sprint on dry ground");
            check(StealthRules.FootstepRadius(true, true, true) == 3.5f, "crouch wins over stale sprint on wet ground");
            for (int posture = 0; posture < 3; posture++)
            {
                bool crouching = posture == 2, running = posture == 1;
                check(StealthRules.FootstepRadius(crouching, running, true) >
                    StealthRules.FootstepRadius(crouching, running, false), "wet contact travels farther for posture " + posture);
            }
            check(near(StealthRules.SightRange(false, true, false, 14), 14), "lit standing uses baseline sight range");
            check(near(StealthRules.SightRange(false, false, false, 14), 9.8f), "switching off light reduces acquisition range");
            check(near(StealthRules.SightRange(true, true, false, 14), 8.4f), "crouching reduces acquisition range");
            check(near(StealthRules.SightRange(true, false, false, 14), 5.88f), "crouching in darkness combines both benefits");
            check(near(StealthRules.SightRange(true, false, false, 12), 5.04f), "lantern uses the same proportional tuning");
            for (int crouch = 0; crouch < 2; crouch++)
                for (int light = 0; light < 2; light++)
                    check(StealthRules.SightRange(crouch == 1, light == 1, true, 14) == 14,
                        "established chase persists for stance/light " + crouch + "/" + light);
            check(StealthRules.SightRange(false, true, false, 0) == 0, "zero baseline cannot see");
            check(StealthRules.SightRange(false, true, true, -1) == 0, "negative baseline cannot see even in chase");
            check(StealthRules.SightRange(false, true, true, float.NaN) == 0, "NaN range cannot see");
            check(StealthRules.SightRange(false, true, false, float.PositiveInfinity) == 0, "infinite range cannot see");
            check(StealthRules.SightRange(false, true, false, float.NegativeInfinity) == 0, "negative infinity range cannot see");

            float standing = StealthRules.AcquisitionSeconds(false, true, false, 8);
            check(near(standing, .93f), "lit standing acquisition is measured in seconds");
            check(StealthRules.AcquisitionSeconds(false, false, false, 8) > standing, "darkness slows recognition");
            check(StealthRules.AcquisitionSeconds(true, true, false, 8) > standing, "crouch slows recognition");
            check(StealthRules.AcquisitionSeconds(false, true, true, 8) < standing, "sprinting is recognized faster");
            check(StealthRules.AcquisitionSeconds(true, false, true, 8) ==
                StealthRules.AcquisitionSeconds(true, false, false, 8), "crouch overrides sprint recognition modifier");
            check(StealthRules.AcquisitionSeconds(false, true, true, 0) >= .3f, "close sprint contact is never instant");
            check(StealthRules.AcquisitionSeconds(true, false, false, float.MaxValue) == 2.8f,
                "extreme finite distance is bounded without arithmetic overflow");
            check(float.IsPositiveInfinity(StealthRules.AcquisitionSeconds(false, true, false, -1)), "negative distance cannot acquire");
            check(float.IsPositiveInfinity(StealthRules.AcquisitionSeconds(false, true, false, float.NaN)), "NaN distance cannot acquire");
            check(float.IsPositiveInfinity(StealthRules.AcquisitionSeconds(false, true, false, float.PositiveInfinity)), "infinite distance cannot acquire");
            check(float.IsPositiveInfinity(StealthRules.AcquisitionSeconds(false, true, false, float.NegativeInfinity)), "negative infinite distance cannot acquire");
            check(StealthRules.AcquisitionSeconds(false, true, false, 12) > standing, "distant recognition is slower");

            var meter = new StealthRules.Awareness();
            check(meter.Value == 0 && !meter.Acquired, "awareness starts empty");
            check(!meter.Tick(true, 1, .25f) && near(meter.Value, .25f), "one glimpse accumulates partial evidence");
            float before = meter.Value;
            check(!meter.Tick(true, 1, 0) && meter.Value == before, "pause freezes visible evidence");
            check(!meter.Tick(false, 1, 0) && meter.Value == before, "pause freezes decay");
            check(!meter.Tick(true, 1, -1) && meter.Value == before, "negative elapsed time ignored");
            check(!meter.Tick(true, 1, float.NaN) && meter.Value == before, "NaN elapsed time ignored");
            check(!meter.Tick(false, 1, float.PositiveInfinity) && meter.Value == before, "infinite elapsed time cannot erase evidence");
            check(!meter.Tick(true, 1, float.NegativeInfinity) && meter.Value == before, "negative infinity elapsed time ignored");
            check(!meter.Tick(true, 0, .25f) && meter.Value == before, "zero acquisition duration cannot grant recognition");
            check(!meter.Tick(true, -1, .25f) && meter.Value == before, "negative acquisition duration ignored");
            check(!meter.Tick(true, float.NaN, .25f) && meter.Value == before, "NaN acquisition duration ignored");
            check(!meter.Tick(true, float.PositiveInfinity, .25f) && meter.Value == before, "invalid target duration cannot acquire");
            check(!meter.Tick(true, float.NegativeInfinity, .25f) && meter.Value == before, "negative infinity duration ignored");
            check(!meter.Tick(false, float.NaN, .25f) && meter.Value < before && meter.Value > 0,
                "occlusion decays evidence even if the unseen target has no usable duration");
            check(!meter.Tick(false, 1, .25f) && meter.Value == 0, "decay clamps at zero");
            check(!meter.Tick(false, 1, 10) && meter.Value == 0, "long occlusion never creates negative evidence");

            check(!meter.Tick(true, .5f, .25f) && near(meter.Value, .5f), "half acquisition does not chase");
            check(meter.Tick(true, .5f, .25f) && meter.Value == 1 && meter.Acquired, "complete evidence acquires");
            check(meter.Tick(true, .5f, .25f) && meter.Value == 1, "continued sight clamps to one");
            check(meter.Tick(false, .5f, 0) && meter.Value == 1, "pause retains acquired evidence");
            check(!meter.Tick(false, .5f, .25f) && meter.Value < 1, "pure meter can decay after occlusion");
            meter.Reset();
            check(meter.Value == 0 && !meter.Acquired, "reset clears both progress and acquisition");
            check(!meter.Tick(true, .3f, 30) && meter.Value < 1, "one hitch cannot instantly detect a fresh target");
            check(near(meter.Value, StealthRules.MaximumAwarenessStep / .3f), "hitch uses bounded observed time");
            meter.Reset();
            check(!meter.Tick(true, .00001f, .1f) && meter.Value < 1, "tiny supplied duration cannot bypass minimum recognition");
            meter.Reset();
            meter.Tick(true, 2, .25f); before = meter.Value;
            check(!meter.Tick(true, .3f, 0) && meter.Value == before, "stance or light change cannot retroactively acquire");
            check(!meter.Tick(true, .3f, .01f) && meter.Value > before && meter.Value < .2f,
                "faster recognition applies only to new visible time");
            float partial = meter.Value;
            meter.Tick(true, 2, .01f);
            check(meter.Value > partial && meter.Value - partial < .01f, "slower recognition also applies only going forward");
            meter.Reset();
            for (int i = 0; i < 20; i++) { meter.Tick(true, 2, .05f); meter.Tick(false, 2, .1f); }
            check(meter.Value == 0 && !meter.Acquired, "sufficient cover between short glimpses prevents accumulated detection");
            var largeSteps = new StealthRules.Awareness();
            var smallSteps = new StealthRules.Awareness();
            for (int i = 0; i < 2; i++) largeSteps.Tick(true, 2, .25f);
            for (int i = 0; i < 10; i++) smallSteps.Tick(true, 2, .05f);
            check(near(largeSteps.Value, smallSteps.Value), "normal frame partitions accumulate equal evidence");
            for (int i = 0; i < 2; i++) largeSteps.Tick(false, 2, .1f);
            for (int i = 0; i < 10; i++) smallSteps.Tick(false, 2, .02f);
            check(near(largeSteps.Value, smallSteps.Value), "normal frame partitions decay equal evidence");
            return count;
        }
    }
}
