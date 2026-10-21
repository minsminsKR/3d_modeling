using NUnit.Framework;
namespace HappyToy.V2.CoreTests
{
    public sealed class LoadingProgressTests
    {
        [Test] public void WorkCannotRegressOrCompleteUntilDestinationIsReady()
        {
            var progress=new LoadingProgress();
            foreach(float value in new[]{.2f,.7f,.4f,float.NaN,float.PositiveInfinity,-1f})progress.Report(value);
            Assert.That(progress.Percent,Is.EqualTo(70));
            progress.Report(1);Assert.That(progress.Percent,Is.EqualTo(99));
            Assert.That(progress.Completed,Is.False);
            progress.Complete();progress.Report(0);
            Assert.That(progress.Percent,Is.EqualTo(100));Assert.That(progress.Fraction,Is.EqualTo(1));
        }
    }
}
