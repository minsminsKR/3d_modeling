using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Component CurrentLoadingCurtain => RequireType("SceneLoadingOverlay").GetProperty("Current").GetValue(null) as Component;
        [UnityTest,Timeout(120000)]
        public IEnumerator LoadingCurtainStaysAcrossActualCorridorToSchoolReloadAndReleasesInputAfterCompletion()
        {
            Call(session,"CreateCorridor",731);Begin();
            Call(session,"Finish",true);yield return null;
            Assert.That(RecoveryPage,Is.EqualTo("Result"));
            var previous=session;
            Call(shell,"BeginSchoolFromOffering");
            yield return Wait(()=>CurrentLoadingCurtain,8,"Chapter transition has no curtain");
            var curtain=CurrentLoadingCurtain;
            Assert.That(Get<bool>(curtain,"LoadingScene"),Is.True);
            float previousFraction=0;bool sawDestination=false,sawComplete=false;
            float deadline=Time.realtimeSinceStartup+60;
            while(curtain && curtain.gameObject.activeSelf)
            {
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));
                var progress=Get<object>(curtain,"Progress");
                float fraction=Get<float>(progress,"Fraction");
                Assert.That(fraction,Is.GreaterThanOrEqualTo(previousFraction));previousFraction=fraction;
                if(Get<int>(progress,"Percent")==100)sawComplete=true;
                var games=Components("GameSession");
                if(games.Length==1 && games[0]!=previous)
                {
                    sawDestination=true;
                    Assert.That(Get<bool>(games[0],"InputAllowed"),Is.False,"New chapter moved behind the loading curtain");
                }
                yield return null;
            }
            yield return null;
            Assert.That(sawDestination&&sawComplete,Is.True,"Curtain disappeared before destination/final seal");
            session=One("GameSession");player=Get<Component>(session,"player");shell=Get<Component>(session,"Shell");
            Assert.That(Get<bool>(session,"ChapterMode"),Is.True);
            Assert.That(Get<bool>(session,"InputAllowed"),Is.True);
            Assert.That(CurrentLoadingCurtain,Is.Null);
            Assert.That(Resources.FindObjectsOfTypeAll<PanelSettings>(),Has.None.Matches<PanelSettings>(x=>x.name=="Loading curtain panel"));
        }

        [UnityTest,Timeout(60000)]
        public IEnumerator LoadingLotusRendersItsFillAndPercentageAtBottomRightAcrossAspectRatios()
        {
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2520,1080)})
            {
                var curtain=(Component)RequireType("SceneLoadingOverlay").GetMethod("Show").Invoke(null,new object[]{size.x==1024,size.x==1024});
                var document=curtain.GetComponent<UIDocument>();
                var target=new RenderTexture(size.x,size.y,0);target.Create();document.panelSettings.targetTexture=target;
                try
                {
                    float previousEnergy=0;
                    foreach(float fraction in new[]{0f,.5f,.99f})
                    {
                        Call(curtain,"Report",fraction);
                        for(int i=0;i<5;i++)yield return null;
                        var root=Get<VisualElement>(curtain,"Root");var bounds=root.Q("loading-indicator").worldBound;
                        Assert.That(bounds.xMin,Is.GreaterThan(root.worldBound.width*.65f));
                        Assert.That(bounds.yMin,Is.GreaterThan(root.worldBound.height*.7f));
                        Assert.That(root.worldBound.Contains(bounds.min)&&root.worldBound.Contains(bounds.max),Is.True);
                        Assert.That(root.Q<Label>("loading-label").text,Is.EqualTo("Loading.."));
                        Assert.That(root.Q<Label>("loading-percent").text,Is.EqualTo(((int)(fraction*100))+"%"));
                        var image=CloudExperienceTests.Read(target);
                        try
                        {
                            CloudExperienceTests.Artifact("loading-"+size.x+"x"+size.y+"-"+(int)(fraction*100)+".png",image.EncodeToPNG());
                            int ink=0;float energy=0;
                            for(int y=0;y<size.y*.3f;y++)for(int x=(int)(size.x*.65f);x<size.x;x++)
                                {
                                    var color=image.GetPixel(x,y);float luminance=Mathf.Max(color.r,color.g,color.b);
                                    energy+=luminance;if(luminance>.08f)ink++;
                                }
                            Assert.That(ink,Is.GreaterThan(100),"Loading label/seal failed to render at bottom right");
                            Assert.That(energy,Is.GreaterThan(previousEnergy),"More completed work did not brighten the actual rendered seal");
                            previousEnergy=energy;
                        }
                        finally { Object.Destroy(image); }
                    }
                    yield return (IEnumerator)Call(curtain,"Finish");
                }
                finally
                {
                    if(curtain)Call(curtain,"Hide");
                    target.Release();Object.Destroy(target);
                }
                yield return null;
            }
        }
    }
}
