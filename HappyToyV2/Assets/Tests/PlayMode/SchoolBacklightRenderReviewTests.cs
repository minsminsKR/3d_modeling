using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class BacklightSurfaceSample
        {
            public string surface; public int rays,brighter,redder;
            public float averageRedDifference,averageLumaDifference;
        }
        [Serializable] sealed class BacklightShadowSurfaceSample
        {
            public string surface; public int rays,changed; public float averageAbsoluteLumaDifference;
        }
        [Serializable] sealed class BacklightCaptureReport
        {
            public string scope="Real gameplay camera, URP StandardRequest, stationary event state, environmental lamps enabled versus disabled. Pixel differences are sampled on actual floor, wall and ceiling colliders; render is not an FPS or subjective-quality certification.";
            public string graphicsDevice;public float cameraLocalZ;public bool spotShadows,fillShadows;
            public BacklightSurfaceSample[] surfaces;
            public int pixelsAffectedByShadowCasting;
            public Rect creatureScreenBounds;public BacklightShadowSurfaceSample[] shadowOnBackground,structureShadowOnSurface,shadowOutsideBakedCreature;
            public int shadowCandidates,creatureOccludedCandidates;
        }
        // The review is opt-in. It renders into private textures while using the
        // production school, event owner, lights, camera, material and pipeline.
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolEmergencyBacklightActualCameraReview()
        {
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_BACKLIGHT_REVIEW_DIR");
            if(string.IsNullOrWhiteSpace(output))yield break;
            Assert.That(Application.isBatchMode,Is.True,"Review must run without an interactive Unity window");
            Assert.That(Environment.GetCommandLineArgs(),Does.Contain("-v2-audit-no-desktop-interaction"),"Desktop input and cursor isolation are required");
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null),"This review needs a real graphics backend");
            output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
            Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(!Directory.Exists(output),Is.True,"Use a fresh output folder");Directory.CreateDirectory(output);
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Call(shell,"Pause");UseSchoolAnomalyFixture(world,"DancingWalker");
            var walker=Get<Component>(world,"Walker");Call(walker,"RestoreTraversal",Get<int>(world,"Traversals"));
            var state=Call(walker,"CaptureState");Set(state,"Elapsed",.01d);Set(state,"Z",33f);Call(walker,"RestoreState",state);
            var creature=Get<Transform>(walker,"Creature");var backlight=Get<Component>(walker,"Backlight");
            var spot=Get<Light>(backlight,"Source");var fill=Get<Light>(backlight,"FillSource");
            Assert.That(spot&&fill&&spot.enabled&&fill.enabled,Is.True,"The actual event lamps must both be lit");
            var eyes=Get<Camera>(player,"eyes");var torch=Get<Light>(player,"flashlight");
            float previousAspect=eyes.aspect;eyes.aspect=960f/540f;
            ((Behaviour)player).enabled=false;torch.enabled=false;
            PlacePlayer(LoopOrigin+new Vector3(0,.03f,25),false);
            eyes.transform.rotation=Quaternion.LookRotation(LoopOrigin+new Vector3(0,1.45f,36)-eyes.transform.position);
            Physics.SyncTransforms();var budget=session.GetComponent(RequireType("LocalShadowBudget"));if(budget)Call(budget,"RefreshNow");
            var renderers=creature.GetComponentsInChildren<Renderer>(true);
            Texture2D lit=null,dark=null,far=null,near=null,noShadows=null,structureNoShadows=null;
            try
            {
                creature.gameObject.SetActive(false);
                lit=SchoolHorrorFrame(eyes,960,540);File.WriteAllBytes(Path.Combine(output,"01-corridor-lights-on.png"),lit.EncodeToPNG());
                spot.enabled=false;fill.enabled=false;
                dark=SchoolHorrorFrame(eyes,960,540);File.WriteAllBytes(Path.Combine(output,"00-corridor-lights-off.png"),dark.EncodeToPNG());
                spot.enabled=true;fill.enabled=true;
                var samples=BacklightGeometrySamples(eyes,lit,dark);
                var originalSpotShadows=spot.shadows;
                try
                {
                    spot.shadows=LightShadows.None;
                    structureNoShadows=SchoolHorrorFrame(eyes,960,540);
                    File.WriteAllBytes(Path.Combine(output,"05-structure-without-spot-shadows.png"),structureNoShadows.EncodeToPNG());
                }
                finally{spot.shadows=originalSpotShadows;}
                var structureShadows=BacklightShadowSamples(eyes,lit,structureNoShadows,new Rect());
                creature.gameObject.SetActive(true);
                far=SchoolHorrorFrame(eyes,960,540);File.WriteAllBytes(Path.Combine(output,"02-creature-distant.png"),far.EncodeToPNG());
                Set(state,"Z",29f);Set(state,"Elapsed",.26d);Call(walker,"RestoreState",state);
                if(budget)Call(budget,"RefreshNow");
                near=SchoolHorrorFrame(eyes,960,540);File.WriteAllBytes(Path.Combine(output,"03-creature-near.png"),near.EncodeToPNG());
                var shadowStates=renderers.Select(r=>r.shadowCastingMode).ToArray();
                try
                {
                    foreach(var renderer in renderers)renderer.shadowCastingMode=ShadowCastingMode.Off;
                    noShadows=SchoolHorrorFrame(eyes,960,540);File.WriteAllBytes(Path.Combine(output,"04-creature-casts-no-shadow.png"),noShadows.EncodeToPNG());
                }
                finally{for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].shadowCastingMode=shadowStates[i];}
                var a=near.GetPixels32();var b=noShadows.GetPixels32();int shadowPixels=0;
                for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>8)shadowPixels++;
                var screenBounds=CreaturePixelBounds(eyes,creature);
                var shadowSurfaces=BacklightShadowSamples(eyes,near,noShadows,screenBounds);
                var colliderResult=BacklightBakedCreatureShadowSamples(eyes,creature,near,noShadows);
                var report=new BacklightCaptureReport{graphicsDevice=SystemInfo.graphicsDeviceName,cameraLocalZ=25,
                    spotShadows=spot.shadows!=LightShadows.None,fillShadows=fill.shadows!=LightShadows.None,surfaces=samples,pixelsAffectedByShadowCasting=shadowPixels,creatureScreenBounds=screenBounds,shadowOnBackground=shadowSurfaces,structureShadowOnSurface=structureShadows,
                    shadowOutsideBakedCreature=colliderResult.samples,shadowCandidates=colliderResult.candidates,
                    creatureOccludedCandidates=colliderResult.occluded};
                File.WriteAllText(Path.Combine(output,"measurement.json"),JsonUtility.ToJson(report,true));
                Debug.Log("HAPPYTOY_BACKLIGHT_REVIEW "+JsonUtility.ToJson(report));
                Assert.That(samples.Any(s=>s.surface=="floor"&&s.redder>10),Is.True,"Real floor pixels did not receive measurable red light");
                Assert.That(samples.Any(s=>s.surface=="wall"&&s.redder>10),Is.True,"Real wall pixels did not receive measurable red light");
                Assert.That(samples.Any(s=>s.surface=="ceiling"&&s.redder>10),Is.True,"Real ceiling pixels did not receive measurable red light");
                Assert.That(report.spotShadows||report.fillShadows,Is.True,"Neither actual emergency light is casting shadows at the review position");
                Assert.That(shadowPixels,Is.GreaterThan(10),"The creature's shadow casting made no measurable pixel contribution");
            }
            finally{eyes.aspect=previousAspect;foreach(var texture in new[]{lit,dark,far,near,noShadows,structureNoShadows})if(texture)Object.Destroy(texture);}
        }
        // Conservative foreground test: bake the actual animated high-detail
        // skin into a temporary invisible collider. It has no Renderer and is
        // queried directly only here; the game collision and scene are untouched.
        static (BacklightShadowSurfaceSample[] samples,int candidates,int occluded) BacklightBakedCreatureShadowSamples(
            Camera camera,Transform creature,Texture2D shadows,Texture2D noShadows)
        {
            var result=new[]{"floor","wall","ceiling"}.Select(n=>new BacklightShadowSurfaceSample{surface=n}).ToArray();
            var skin=creature.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r=>r.sharedMesh).OrderByDescending(r=>r.sharedMesh.vertexCount).First();
            var mesh=new Mesh();var probe=new GameObject("Diagnostic invisible creature ray mask");
            try
            {
                skin.BakeMesh(mesh);var vertices=mesh.vertices;
                for(int i=0;i<vertices.Length;i++)vertices[i]=skin.transform.TransformPoint(vertices[i]);
                mesh.vertices=vertices;mesh.RecalculateBounds();probe.layer=29;
                var mask=probe.AddComponent<MeshCollider>();mask.sharedMesh=mesh;Physics.SyncTransforms();
                var a=shadows.GetPixels32();var b=noShadows.GetPixels32();var sums=new double[3];int candidates=0,occluded=0;
                for(int y=0;y<shadows.height;y++)for(int x=0;x<shadows.width;x++)
                {
                    int i=y*shadows.width+x,dr=a[i].r-b[i].r,dg=a[i].g-b[i].g,db=a[i].b-b[i].b;
                    if(Math.Abs(dr)+Math.Abs(dg)+Math.Abs(db)<=8)continue;candidates++;
                    var ray=camera.ViewportPointToRay(new Vector3((x+.5f)/shadows.width,(y+.5f)/shadows.height,0));
                    if(!Physics.Raycast(ray,out var surface,60,1<<8,QueryTriggerInteraction.Ignore))continue;
                    if(mask.Raycast(ray,out var foreground,surface.distance+.03f))
                    {if(foreground.distance<surface.distance+.03f){occluded++;continue;}}
                    string name=surface.collider.name.ToLowerInvariant();int k=name.Contains("floor")?0:name.Contains("wall")?1:name.Contains("ceiling")||name.Contains("roof")?2:-1;
                    if(k<0)continue;
                    result[k].rays++;result[k].changed++;
                    sums[k]+=Mathf.Abs(dr*.2126f+dg*.7152f+db*.0722f)/255f;
                }
                for(int k=0;k<3;k++)if(result[k].rays>0)result[k].averageAbsoluteLumaDifference=(float)(sums[k]/result[k].rays);
                return(result,candidates,occluded);
            }
            finally{Object.Destroy(probe);Object.Destroy(mesh);}
        }
        // Exclude the projected bounds of the actual animated highest-detail
        // mesh before testing its shadow effect on physical scene surfaces.
        static Rect CreaturePixelBounds(Camera camera,Transform creature)
        {
            var skin=creature.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r=>r.sharedMesh).OrderByDescending(r=>r.sharedMesh.vertexCount).First();
            var mesh=new Mesh();try
            {
                skin.BakeMesh(mesh);float minX=1,minY=1,maxX=0,maxY=0;int valid=0;
                foreach(var v in mesh.vertices)
                {
                    var p=camera.WorldToViewportPoint(skin.transform.TransformPoint(v));if(p.z<=0)continue;
                    minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);valid++;
                }
                Assert.That(valid,Is.GreaterThan(0));
                return Rect.MinMaxRect(minX-.01f,minY-.01f,maxX+.01f,maxY+.01f);
            }
            finally{Object.Destroy(mesh);}
        }
        static BacklightShadowSurfaceSample[] BacklightShadowSamples(Camera camera,Texture2D shadows,Texture2D noShadows,Rect actorBounds)
        {
            var result=new[]{"floor","wall","ceiling"}.Select(n=>new BacklightShadowSurfaceSample{surface=n}).ToArray();
            var a=shadows.GetPixels32();var b=noShadows.GetPixels32();var sums=new double[3];
            for(int y=1;y<shadows.height;y+=2)for(int x=1;x<shadows.width;x+=2)
            {
                var uv=new Vector2((x+.5f)/shadows.width,(y+.5f)/shadows.height);if(actorBounds.Contains(uv))continue;
                var ray=camera.ViewportPointToRay(new Vector3(uv.x,uv.y,0));
                if(!Physics.Raycast(ray,out var hit,60,1<<8,QueryTriggerInteraction.Ignore))continue;
                string name=hit.collider.name.ToLowerInvariant();int k=name.Contains("floor")?0:name.Contains("wall")?1:name.Contains("ceiling")||name.Contains("roof")?2:-1;
                if(k<0)continue;int i=y*shadows.width+x;
                int dr=a[i].r-b[i].r,dg=a[i].g-b[i].g,db=a[i].b-b[i].b;
                float delta=Mathf.Abs(dr*.2126f+dg*.7152f+db*.0722f)/255f;
                result[k].rays++;if(Math.Abs(dr)+Math.Abs(dg)+Math.Abs(db)>8)result[k].changed++;sums[k]+=delta;
            }
            for(int k=0;k<3;k++)if(result[k].rays>0)result[k].averageAbsoluteLumaDifference=(float)(sums[k]/result[k].rays);
            return result;
        }
        static BacklightSurfaceSample[] BacklightGeometrySamples(Camera camera,Texture2D lit,Texture2D dark)
        {
            var names=new[]{"floor","wall","ceiling"};var result=names.Select(n=>new BacklightSurfaceSample{surface=n}).ToArray();
            var a=lit.GetPixels32();var b=dark.GetPixels32();var red=new double[3];var luma=new double[3];
            for(int y=3;y<lit.height;y+=6)for(int x=3;x<lit.width;x+=6)
            {
                var ray=camera.ViewportPointToRay(new Vector3((x+.5f)/lit.width,(y+.5f)/lit.height,0));
                if(!Physics.Raycast(ray,out var hit,60,1<<8,QueryTriggerInteraction.Ignore))continue;
                string name=hit.collider.name.ToLowerInvariant();int k=name.Contains("floor")?0:name.Contains("wall")?1:name.Contains("ceiling")||name.Contains("roof")?2:-1;
                if(k<0)continue;var sample=result[k];int i=y*lit.width+x;
                int dr=a[i].r-b[i].r,dg=a[i].g-b[i].g,db=a[i].b-b[i].b;
                float dl=(dr*.2126f+dg*.7152f+db*.0722f)/255f;
                sample.rays++;if(dl>.008f)sample.brighter++;if(dr>5&&dr>dg+2&&dr>db+2)sample.redder++;
                red[k]+=dr/255d;luma[k]+=dl;
            }
            for(int k=0;k<3;k++)if(result[k].rays>0){result[k].averageRedDifference=(float)(red[k]/result[k].rays);result[k].averageLumaDifference=(float)(luma[k]/result[k].rays);}
            return result;
        }
    }
}
