using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ModernThreeEnemiesUseTheirOwnClipsDeformAndGroundAtEverySampledPhase()
        {
            var keys = new[] { "Cyclopse", "Uncat", "Baby" };
            Call(session, "CreateCorridor", 73);
            foreach (var built in Components("StalkerBrain").Where(actor => actor.name.EndsWith("— corridor")))
            {
                ((Behaviour)built).enabled = false;
                var navigation = built.GetComponent<NavMeshAgent>();
                if (navigation.enabled && navigation.isOnNavMesh) navigation.isStopped = true;
            }
            Begin(); yield return Delay(.25f);
            ((Behaviour)player).enabled = false;
            // Controlled art fixture: open real leaves so longer views use actual doorways.
            foreach (var door in Components("Interactable").Where(item => item.name == "Corridor sliding door"))
                Call(door, "OpenForPursuer");
            yield return Delay(.8f);
            var camera = Get<Camera>(player, "eyes"); Assert.That(camera, Is.SameAs(Camera.main));
            Get<Light>(player, "flashlight").enabled = true;
            var views = new List<RefinedEnemyCameraView>();
            var phases = new List<RefinedEnemyGroundPhase>();
            var enemies = Components("StalkerBrain").Where(x => x.name.EndsWith("— corridor")).ToArray();
            Assert.That(enemies.Length, Is.EqualTo(3));
            var scratch = new Mesh();
            try
            {
                foreach (var enemy in enemies)
                {
                    ((Behaviour)enemy).enabled = false;
                    var agent = enemy.GetComponent<NavMeshAgent>(); agent.enabled = false;
                    enemy.gameObject.SetActive(true); agent.enabled = true;
                    Assert.That(agent.Warp(enemy.transform.position), Is.True); agent.isStopped = true;
                    Assert.That(agent.baseOffset, Is.Zero, "Original V1 navigation feet origin changed");
                    var motion = enemy.GetComponent(RequireType("V1MonsterMotion"));
                    Assert.That(Get<bool>(motion, "RefinedVisual"), Is.True, enemy.name + " still uses its old scanned surface");
                    var model = Get<Transform>(motion, "model"); var animation = Get<Animation>(motion, "animationPlayer");
                    string key = keys.Single(value => enemy.name.Contains(value));
                    bool studio = Get<bool>(motion,"StudioVisual");
                    Assert.That(studio, Is.True, "Wrong generation of visual: " + key);
                        Assert.That(Resources.Load<GameObject>("EnemyStudio/" + key + "/" + key), Is.Not.Null);
                        foreach (string alias in key == "Baby" ? new[] { "patrol", "chase", "cry" } : new[] { "patrol", "chase" })
                        {
                            var asset = Resources.Load<AnimationClip>("EnemyStudio/" + key + "/" + alias);
                            Assert.That(asset, Is.Not.Null, "Missing newly authored motion: " + key + "/" + alias);
                            Assert.That(animation.GetClip(alias), Is.SameAs(asset), "Live alias is not the new rig's motion");
                        }
                        Assert.That(Get<bool>(motion, "AttackRigAvailable"), Is.True);
                        if (key == "Cyclopse") Assert.That(Get<bool>(motion, "IntroPoseRigAvailable"), Is.True);
                        if (key == "Uncat") Assert.That(Get<bool>(motion, "LimbContactRigAvailable"), Is.True);
                    var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                    Assert.That(skins.Length, Is.GreaterThan(0));
                    var priorMeshes = Resources.Load<GameObject>("EnemyRefinement/" + key + "-v2").GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin => skin.sharedMesh).ToArray();
                    foreach (var skin in skins)
                    {
                        int triangles = Enumerable.Range(0, skin.sharedMesh.subMeshCount).Sum(sub => (int)skin.sharedMesh.GetIndexCount(sub) / 3);
                        Assert.That(triangles, studio ? Is.InRange(20000, 160000) : Is.InRange(9980, 10000), "Unexpected runtime surface budget");
                        if (studio) Assert.That(skin.sharedMesh.vertexCount, Is.LessThanOrEqualTo(80000), "Source sculpt was imported without a game mesh");
                        if (studio) Assert.That(priorMeshes.Contains(skin.sharedMesh), Is.False, "New visual retained an old scanned mesh");
                        Assert.That(skin.bones.All(bone => bone && bone.IsChildOf(model)), Is.True);
                        foreach (var material in skin.sharedMaterials)
                        {
                            Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, "Character atlas lost");
                            if (studio) Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(Resources.Load<Texture2D>("EnemyStudio/" + key + "/albedo")), "New body retained the old scanned atlas");
                            Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null);
                            Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Not.Null);
                            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
                        }
                    }
                    yield return null; yield return null;
                    Assert.That(model.GetComponentsInChildren<Animation>().Length, Is.EqualTo(1), "Stale imported root Animation interferes with Baby cry lookup");
                    foreach (string clipName in new[] { "patrol", "chase" })
                    {
                        var clip = animation.GetClip(clipName);
                        Assert.That(clip && clip.legacy && clip.length > .1f, Is.True, enemy.name + ": " + clipName);
                        Set(enemy, "state", clipName == "chase" ? "Chase" : "Patrol");
                        animation.Play(clipName); yield return null;
                        Vector3[] first = null; float largestChange = 0;
                        foreach (float phase in new[] { 0f, .125f, .25f, .375f, .5f, .625f, .75f, .875f })
                        {
                            animation[clipName].speed = 0; animation[clipName].time = phase * clip.length; animation.Sample();
                            yield return null;
                            var points = skins.SelectMany(skin =>
                            {
                                return RefinedEnemySkinPoints(skin, scratch).ToArray();
                            }).ToArray();
                            Assert.That(points.All(point => !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y) && !float.IsNaN(point.z) && !float.IsInfinity(point.z)), Is.True);
                            if (first == null) first = points;
                            else largestChange = Mathf.Max(largestChange, points.Zip(first, (a, b) => Vector3.Distance(a, b)).Max());
                            float actualSole = points.Min(point => point.y);
                            Assert.That(Physics.Raycast(enemy.transform.position + Vector3.up * .25f, Vector3.down, out var floor, .8f), Is.True);
                            float groundGap = Get<float>(motion, "GroundGap");
                            phases.Add(new RefinedEnemyGroundPhase { key=key,clip=clipName,phase=phase,actorY=enemy.transform.position.y,
                                floorY=floor.point.y,renderedSoleY=actualSole,reportedGroundGap=groundGap,
                                renderedGap=actualSole-floor.point.y,modelLocalY=model.localPosition.y });
                            Assert.That(Mathf.Abs(groundGap), Is.LessThan(.04f), enemy.name + ": " + clipName + " phase " + phase);
                            Assert.That(Mathf.Abs(actualSole - floor.point.y), Is.LessThan(.045f), "Visual feet do not meet the actual floor: " + enemy.name);
                        }
                        Assert.That(largestChange, Is.GreaterThan(.025f), "Authored clip no longer deforms its refined skin: " + enemy.name + ": " + clipName);
                    }
                    Set(enemy, "state", "Patrol"); animation.Play("patrol"); animation["patrol"].time = .25f * animation["patrol"].length;
                    animation["patrol"].speed = 0; animation.Sample(); yield return null;
                    var facing = enemy.transform.rotation;
                    foreach (string view in new[] { "near", "turn", "hall" })
                    {
                        enemy.transform.rotation = facing * Quaternion.Euler(0, view == "turn" ? 45 : 0, 0);
                        yield return null;
                        var body = RefinedEnemyBodyBounds(model);
                        RefinedEnemyViewpoint(enemy.transform.position, body.center, facing * Vector3.forward,
                            view == "near" ? 2f : view == "turn" ? 2.7f : 5.5f);
                        yield return null; IntroLook(body.center);
                        string file = "enemy-refined-" + key.ToLowerInvariant() + "-" + view + "-main-camera.png";
                        CloudPresentationMovementTests.Capture(camera, file);
                        string output = Path.GetFullPath(Path.Combine("Temp", "HappyToyCloudEvidence", file));
                        Assert.That(File.Exists(output), Is.True, "Missing actual main-camera enemy view: " + file);
                        Assert.That(new FileInfo(output).Length, Is.GreaterThan(1000));
                        views.Add(new RefinedEnemyCameraView { key=key,view=view,image=file,actor=enemy.transform.position,
                            camera=camera.transform.position,look=camera.transform.rotation,actorRotation=enemy.transform.rotation,
                            bodyCenter=body.center,bodySize=body.size });
                    }
                    enemy.transform.rotation = facing;
                    var pose = model.position; Call(shell, "Pause"); yield return Delay(.15f);
                    Assert.That(model.position, Is.EqualTo(pose)); Call(shell, "Resume");
                    Debug.Log("HAPPYTOY_ENEMY_REFINEMENT_PASS " + enemy.name + ": own atlas and rig clips, normal/smoothness maps, eight phases, actual sole/floor and pause");
                    enemy.gameObject.SetActive(false);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(scratch);
                // One filename has one immutable transport envelope. Preserve the
                // sampled prefix even if an assertion throws, without emitting
                // conflicting cumulative versions of the same artifact.
                CloudExperienceTests.Artifact("enemy-refinement-ground-phases.json", Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new RefinedEnemyGroundEvidence { phases=phases.ToArray() },true)));
            }
            Assert.That(views.Count, Is.EqualTo(9));
            CloudExperienceTests.Artifact("enemy-refinement-main-camera-views.json", Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                new RefinedEnemyCameraEvidence { scope="Controlled actual main-camera near/turn/long-doorway views; not fear or survival certification",views=views.ToArray() },true)));
        }
        [Serializable] sealed class RefinedEnemyGroundPhase
        { public string key,clip; public float phase,actorY,floorY,renderedSoleY,reportedGroundGap,renderedGap,modelLocalY; }
        [Serializable] sealed class RefinedEnemyGroundEvidence
        { public RefinedEnemyGroundPhase[] phases; }
        [Serializable] sealed class RefinedEnemyCameraView
        { public string key,view,image; public Vector3 actor,camera,bodyCenter,bodySize; public Quaternion look,actorRotation; }
        [Serializable] sealed class RefinedEnemyCameraEvidence
        { public string scope; public RefinedEnemyCameraView[] views; }
        static Bounds RefinedEnemyBodyBounds(Transform model)
        {
            var mesh=new Mesh();var bounds=new Bounds();bool first=true;
            try
            {
                foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(!skin.enabled)continue;
                    foreach(var world in RefinedEnemySkinPoints(skin,mesh))
                    {
                        if(first){bounds=new Bounds(world,Vector3.zero);first=false;}else bounds.Encapsulate(world);
                    }
                }
                Assert.That(first,Is.False,"No actual refined render vertices");return bounds;
            }
            finally {UnityEngine.Object.Destroy(mesh);}
        }
        static IEnumerable<Vector3> RefinedEnemySkinPoints(SkinnedMeshRenderer skin, Mesh scratch)
        {
            var motion=skin.GetComponentInParent(RequireType("V1MonsterMotion"));
            bool studio=motion && Get<bool>(motion,"StudioVisual");
            skin.BakeMesh(scratch,!studio);
            var inherited=studio && skin.transform.parent ? skin.transform.parent.lossyScale : Vector3.one;
            foreach(var point in scratch.vertices)
            {
                var local=studio ? new Vector3(point.x/inherited.x,point.y/inherited.y,point.z/inherited.z) : point;
                yield return skin.transform.TransformPoint(local);
            }
        }
        void RefinedEnemyViewpoint(Vector3 anchor, Vector3 target, Vector3 front, float distance)
        {
            front.y=0; front.Normalize(); var right=Vector3.Cross(Vector3.up,front);
            foreach(var direction in new[]{front,right,-right,-front})
            {
                if(!NavMesh.SamplePosition(anchor+direction*distance,out var hit,.45f,NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y-anchor.y)>.4f)continue;
                PlacePlayer(hit.position,false); IntroLook(target);
                if(!Physics.Linecast(IntroCamera.transform.position,target,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return;
            }
            Assert.Fail("No same-floor real-doorway model view around "+anchor+" at "+distance+"m");
        }
    }
}
