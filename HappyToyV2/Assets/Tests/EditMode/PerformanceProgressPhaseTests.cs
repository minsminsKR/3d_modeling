using NUnit.Framework;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class PerformanceProgressPhaseTests
    {
        [Test]
        public void MemoryRoutesClassifyTheLiveProgressAndLegacyKeepsStoryCohorts()
        {
            var helper=RequireType("PerformanceProgressPhase");
            foreach(bool chapter in new[]{false,true})
            {
                bool corridor=!chapter;
                for(int recovered=0;recovered<=5;recovered++)
                {
                    int resolved=(int)Call(helper,"ResolveProgress",chapter,corridor,recovered,0);
                    Assert.That(resolved,Is.EqualTo(recovered),"New route ignored recovered memories at legacy StoryStep zero");
                    Assert.That((bool)Call(helper,"IsThreatProgress",resolved),Is.EqualTo(recovered==2||recovered==3));
                }
            }
            for(int story=0;story<=4;story++)
            {
                int resolved=(int)Call(helper,"ResolveProgress",false,false,7,story);
                Assert.That(resolved,Is.EqualTo(story),"Authored annex inspections changed historical story cohorts");
                Assert.That((bool)Call(helper,"IsThreatProgress",resolved),Is.EqualTo(story==2||story==3));
            }
        }
    }
}
