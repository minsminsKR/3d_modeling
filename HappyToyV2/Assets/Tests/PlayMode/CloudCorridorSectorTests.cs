using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class SectorViewEvidence
        {
            public string image,label,material;
            public int cell,sector,changedGlyphPixels;
            public Vector3 feet,eyes,target;
            public Color paperColour;
        }
        [Serializable] sealed class SectorRenderEvidence
        {
            public string scope="Controlled actual first-person camera comparison: identical production flashlight and ambient/fog; point lamps temporarily disabled for palette comparison and restored for natural context. No gameplay, AI, navigation, exposure or threat tuning.";
            public int seed,goalClues,branchClues,visualBatches;
            public float torchIntensity,torchRange,fogDensity;
            public Color ambient;
            public SectorViewEvidence[] views;
        }
        [UnityTest,Timeout(120000)]
        public IEnumerator CorridorSectorsShowFourGoalIdentitiesAndRealBranchesWithoutChangingPhysics()
        {
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;yield return null;
            var run=HauntedRun(session);var art=Get<Component>(run,"Presentation");
            var plan=Get<object>(art,"SectorPlan");var goals=Get<int[]>(plan,"GoalCells");
            var sectors=Get<int[]>(plan,"Sectors");var branches=Get<int[]>(plan,"BranchOrdinals");
            var clues=((IEnumerable<TextMesh>)Get<object>(art,"SectorClues")).ToArray();
            Assert.That(Get<int>(art,"GoalRoomClues"),Is.EqualTo(4));
            Assert.That(Get<int>(art,"BranchClues"),Is.Zero,"Removed branch-number signage returned");
            Assert.That(Get<bool>(art,"PhysicalStateIntact"),Is.True);HauntedCompleteApproaches(run);
            ulong nav=HauntedNavigationHash();
            var additions=Get<Transform>(art,"AdditionRoot");
            Assert.That(additions.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(additions.GetComponentsInChildren<NavMeshObstacle>(true),Is.Empty);
            var papers=goals.Select(cell=>(Material)Call(art,"SectorPaper",cell)).ToArray();
            Assert.That(papers.Distinct().Count(),Is.EqualTo(4),"Sector pigment materials collapsed into one cache entry");
            var sharedPaper=Resources.Load<Texture2D>("GraphicsPbr/paper-aged/albedo");
            Assert.That(sharedPaper,Is.Not.Null);
            foreach(var material in papers)
            {
                Assert.That(material.GetTexture("_BaseMap"),Is.SameAs(sharedPaper),"Sector replaced authentic shared paper with generated stretched noise");
                foreach(var channel in new[]{("_BumpMap","normal"),("_OcclusionMap","ao"),("_MetallicGlossMap","metallic-smoothness")})
                    Assert.That(material.GetTexture(channel.Item1),Is.SameAs(Resources.Load<Texture2D>("GraphicsPbr/paper-aged/"+channel.Item2)));
                Assert.That(material.mainTextureScale,Is.EqualTo(Vector2.one));
            }
            foreach(int cell in goals)
            {
                var structure=additions.Find("Blender architecture cell "+cell);
                Assert.That(structure,Is.Not.Null,"Missing authored architecture at goal "+cell);
                var meshes=structure.GetComponentsInChildren<MeshFilter>();
                Assert.That(meshes.Sum(filter=>filter.sharedMesh.vertexCount),Is.GreaterThan(1000));
                Assert.That(meshes.All(filter=>filter.sharedMesh.uv.Length==filter.sharedMesh.vertexCount),Is.True);
                Assert.That(clues.Single(clue=>clue.name=="Goal chamber clue "+cell).text,Does.Contain("봉인 "+(Array.IndexOf(goals,cell)+1).ToString("D2")));
            }
            Assert.That(papers.Select(x=>x.GetColor("_BaseColor")).Distinct().Count(),Is.EqualTo(4));
            var camera=Get<Camera>(player,"eyes");var torch=Get<Light>(player,"flashlight");
            ((Behaviour)player).enabled=false;player.GetComponent<CharacterController>().enabled=false;torch.enabled=true;
            var pointStates=run.GetComponentsInChildren<Light>(true).Where(x=>x.type==LightType.Point).ToDictionary(x=>x,x=>x.enabled);
            var evidence=new SectorRenderEvidence {seed=73,goalClues=4,branchClues=Get<int>(art,"BranchClues"),
                visualBatches=Get<int>(art,"VisualBatches"),torchIntensity=torch.intensity,torchRange=torch.range,
                fogDensity=RenderSettings.fogDensity,ambient=RenderSettings.ambientLight};
            var views=new List<SectorViewEvidence>();
            try
            {
                foreach(var light in pointStates.Keys) light.enabled=false;
                var selected=goals;
                foreach(int cell in selected)
                {
                    var clue=clues.Single(x=>x.name.EndsWith(" "+cell));
                    bool goal=goals.Contains(cell);var target=clue.transform.position;
                    var feet=goal?HauntedCell(run,cell)-clue.transform.forward*1.1f:target-clue.transform.forward*1.85f;
                    feet.y=.03f;
                    Assert.That(NavMesh.SamplePosition(feet,out var floor,.3f,NavMesh.AllAreas),Is.True,"No physical sector camera approach "+cell);
                    feet=floor.position;PlacePlayer(feet,false);
                    camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position);yield return Delay(.25f);
                    camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position);
                    var frame=SchoolCameraFrame(camera,out var projection,out _,out var viewport);
                    try
                    {
                        Assert.That(CloudExperienceTests.HasContent(frame),Is.True,"Flat/blank controlled room "+cell);
                        string image="corridor-sector-"+(goal?"goal-"+Array.IndexOf(goals,cell):"branch-"+branches[cell])+"-torch.png";
                        CloudExperienceTests.Artifact(image,frame.EncodeToPNG());
                        var renderer=clue.GetComponent<MeshRenderer>();var screen=SchoolScreenBounds(projection,viewport,renderer.bounds);
                        Assert.That(screen.xMin,Is.InRange(0,1));Assert.That(screen.xMax,Is.InRange(0,1));
                        Assert.That(screen.yMin,Is.InRange(0,1));Assert.That(screen.yMax,Is.InRange(0,1));
                        renderer.enabled=false;var absent=SchoolCameraFrame(camera,out _,out _,out _);renderer.enabled=true;
                        int glyphs;
                        try {glyphs=HauntedChanged(frame,absent,screen);Assert.That(glyphs,Is.GreaterThan(12),"Actual room identity text produced no visible glyphs: "+clue.text);}
                        finally {Object.Destroy(absent);}
                        var paper=(Material)Call(art,"SectorPaper",cell);
                        views.Add(new SectorViewEvidence {image=image,label=clue.text,cell=cell,sector=sectors[cell],feet=feet,
                            eyes=camera.transform.position,target=target,changedGlyphPixels=glyphs,material=paper.name,paperColour=paper.GetColor("_BaseColor")});
                        Assert.That(torch.intensity,Is.EqualTo(evidence.torchIntensity));
                        Assert.That(RenderSettings.ambientLight,Is.EqualTo(evidence.ambient));
                        Assert.That(RenderSettings.fogDensity,Is.EqualTo(evidence.fogDensity));
                        Assert.That(Get<bool>(art,"PhysicalStateIntact"),Is.True);
                    }
                    finally {Object.Destroy(frame);}
                }
            }
            finally {foreach(var point in pointStates)if(point.Key)point.Key.enabled=point.Value;}
            yield return Delay(.2f);
            var natural=SchoolCameraFrame(camera,out _,out _,out _);
            try {Assert.That(CloudExperienceTests.HasContent(natural),Is.True);CloudExperienceTests.Artifact("corridor-sector-branch-natural.png",natural.EncodeToPNG());}
            finally {Object.Destroy(natural);}
            evidence.views=views.ToArray();CloudExperienceTests.Artifact("corridor-sector-render.json",Encoding.UTF8.GetBytes(JsonUtility.ToJson(evidence,true)));
            Assert.That(HauntedNavigationHash(),Is.EqualTo(nav));HauntedCompleteApproaches(run);
            Assert.That(Get<bool>(art,"PhysicalStateIntact"),Is.True);
            // Teardown frees owned pigment materials/clues and preserves the shared imported paper channels.
            var textures=papers.Select(x=>x.GetTexture("_BaseMap")).ToArray();
            Object.Destroy(art);yield return null;yield return null;yield return null;
            Assert.That(clues.All(x=>x==null),Is.True);Assert.That(papers.All(x=>x==null),Is.True);
            Assert.That(textures.All(x=>x&&x==sharedPaper),Is.True,"Sector teardown destroyed its imported shared paper scan");
            Assert.That(HauntedNavigationHash(),Is.EqualTo(nav));
            HauntedCompleteApproaches(run);
        }
    }
}
