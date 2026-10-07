using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator CompactPickupFeedbackKeepsTheCentralViewClearAcrossScreenSizes()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            var inventory = Get<Component>(player, "Firecrackers");
            Call(inventory, "SetRunStock", 1);
            Assert.That((bool)Call(inventory, "AddSupply"), Is.True);
            var view = One("GameShellView"); var errors = new List<string>();
            float timeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                foreach (bool large in new[] { false, true })
                {
                    if (large) Call(shell, "ToggleLargeText");
                    foreach (var size in new[] { new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(2560, 1080) })
                    {
                        yield return CaptureMenu(view, "compact-pickup-" + large + "-" + size.x + "x" + size.y + ".png", size.x, size.y, errors);
                        var hud = Get<VisualElement>(view, "Root");
                        var panel = hud.Q("item-action-feedback-panel");
                        var label = hud.Q<Label>("item-action-feedback");
                        Assert.That(label.text, Does.Contain("폭죽 +1"));
                        Assert.That(panel.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                        Assert.That(panel.worldBound.width, Is.LessThan(hud.worldBound.width * .25f));
                        Assert.That(panel.worldBound.height, Is.LessThan(hud.worldBound.height * .1f));
                        var centralView = new Rect(hud.worldBound.center - new Vector2(hud.worldBound.width * .16f, hud.worldBound.height * .15f),
                            new Vector2(hud.worldBound.width * .32f, hud.worldBound.height * .3f));
                        Assert.That(panel.worldBound.Overlaps(centralView), Is.False, "Pickup feedback obscures the navigation/aiming view");
                        Assert.That(panel.worldBound.Contains(label.worldBound.min) && panel.worldBound.Contains(label.worldBound.max - Vector2.one), Is.True,
                            "Compact pickup message is clipped by its background");
                    }
                }
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
            }
            finally { Time.timeScale = timeScale; }
            yield return new WaitForSeconds(1.7f);
            Assert.That(Get<bool>(inventory, "FeedbackVisible"), Is.False);
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator CorridorCeilingFixturesShareRealCandleDangerAndRecover()
        { yield return VerifyCeilingFixtures(true); }

        [UnityTest, Timeout(45000)]
        public IEnumerator SchoolCeilingFixturesShareRealCandleDangerAndRecover()
        { yield return VerifyCeilingFixtures(false); }

        IEnumerator VerifyCeilingFixtures(bool corridor)
        {
            yield return CandleDangerPrepare(corridor);
            ((Behaviour)player).enabled = false;
            var fixture = Components("ThreatFixtureFlicker")
                .Where(item => item.gameObject.activeInHierarchy)
                .OrderBy(item => Vector3.Distance(item.transform.position, player.transform.position)).First();
            var light = Get<Light>(fixture, "Source");
            CandleDangerExpectSafe(); yield return Delay(.15f);
            float baseline = light.intensity; Assert.That(baseline, Is.GreaterThan(0));
            var actor = CandleDangerOwnedBrain();
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 4)); CandleDangerObserve(actor);
            Call(LightRun, "RefreshDanger"); CandleDangerArmAll();
            float low = float.PositiveInfinity, high = 0;
            float until = Time.time + 2.6f;
            while (Time.time < until)
            {
                Assert.That(Get<float>(LightRun, "Danger"), Is.GreaterThan(0));
                low = Mathf.Min(low, light.intensity); high = Mathf.Max(high, light.intensity);
                yield return null;
            }
            Assert.That(low, Is.LessThan(baseline * .5f), "Real ceiling light never dimmed with candle danger");
            Assert.That(high, Is.GreaterThan(baseline * .9f), "Real ceiling light never recovered between dips");
            Call(shell, "Pause"); float frozen = light.intensity;
            yield return Delay(.3f);
            Assert.That(light.intensity, Is.EqualTo(frozen).Within(.0001f), "Pause advanced fixture flicker");
            Call(shell, "ToggleReducedMotion"); yield return null;
            float steady = light.intensity; yield return Delay(.3f);
            Assert.That(light.intensity, Is.EqualTo(steady).Within(.0001f), "Comfort mode retained fixture flashes");
            Call(shell, "Resume");
            light.enabled = false; yield return Delay(.2f);
            Assert.That(light.enabled, Is.False, "Danger re-enabled a deliberately switched-off fixture");
            light.enabled = true; actor.gameObject.SetActive(false); CandleDangerExpectSafe(); yield return Delay(.2f);
            Assert.That(light.intensity, Is.EqualTo(baseline).Within(.0001f), "Repeated dips permanently reduced the light baseline");
        }
    }
}
