using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator CorridorMonsterPushesARealClosedDoorBeforeCrossing()
        {
            Call(session, "CreateCorridor", 73); Begin();
            var door = Components("Interactable").First(x => x.name == "Corridor sliding door");
            var actors = Components("StalkerBrain").Where(x => x.name.EndsWith("— corridor")).ToArray();
            var brain = actors.First(x => x.gameObject.activeSelf);
            foreach (var actor in actors) if (actor != brain) actor.gameObject.SetActive(false);
            ((Behaviour)player).enabled = false;
            PlacePlayer(new Vector3(200, 5, 200), false);
            var agent = brain.GetComponent<NavMeshAgent>(); var forward = door.transform.forward;
            var at = door.transform.position - forward * 1.55f; at.y = .03f;
            Assert.That(NavMesh.SamplePosition(at, out var hit, .4f, NavMesh.AllAreas), Is.True);
            Assert.That(agent.Warp(hit.position), Is.True); brain.transform.rotation = Quaternion.LookRotation(forward);
            var target = door.transform.position + forward * 2; target.y = .03f;
            Assert.That((bool)Call(brain, "HearNoise", target, 8f), Is.True, "Closed operable door erased sound investigation route");
            yield return Delay(.35f);
            Assert.That(Get<bool>(door, "IsOpen"), Is.False, "No close-range opening delay");
            Assert.That(Vector3.Dot(brain.transform.position - door.transform.position, forward), Is.LessThan(0), "Walked through closed door");
            Call(shell, "Pause"); var paused = brain.transform.position; yield return Delay(.25f);
            Assert.That(brain.transform.position, Is.EqualTo(paused)); Assert.That(Get<bool>(door, "IsOpen"), Is.False); Call(shell, "Resume");
            yield return Wait(() => Get<bool>(door, "IsOpen"), 4, "Monster never opened generated sliding door");
            yield return Wait(() => Vector3.Dot(brain.transform.position - door.transform.position, forward) > .6f, 5,
                "Monster could not physically pass the opened leaf");
            Assert.That(Get<object>(brain, "state").ToString(), Is.EqualTo("Investigate"));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CorridorHasReachableGoalsOriginalMonstersAndBoundedCollectibleSupplies()
        {
            Call(session, "CreateCorridor", 73); Begin();
            Assert.That(Get<bool>(session, "CorridorMode"), Is.True);
            var run = Get<Component>(session, "Corridor");
            Assert.That(Get<bool>(run, "Ready"), Is.True);
            var wallTexture = Resources.Load<Texture2D>("GraphicsPbr/plaster-damp/albedo");
            Assert.That(wallTexture, Is.Not.Null, "Physical scanned wall albedo missing from Resources");
            Assert.That(wallTexture.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(wallTexture.wrapModeV, Is.EqualTo(TextureWrapMode.Repeat));
            var floorTexture=Resources.Load<Texture2D>("GraphicsPbr/wood-floor/albedo");
            Assert.That(floorTexture,Is.Not.Null); Assert.That(floorTexture.wrapModeU,Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(floorTexture.filterMode,Is.EqualTo(FilterMode.Trilinear)); Assert.That(floorTexture.anisoLevel,Is.EqualTo(8));
            Assert.That(run.GetComponentsInChildren<Renderer>().SelectMany(x=>x.sharedMaterials)
                .Any(x=>x&&x.name.StartsWith("Worn wooden floor")&&x.mainTexture==floorTexture&&x.color.r>.8f),Is.True);
            Assert.That(run.GetComponentsInChildren<Renderer>().SelectMany(x => x.sharedMaterials)
                .Any(x => x && x.name.StartsWith("Damp plaster") && x.mainTexture == wallTexture), Is.True,
                "Generated walls did not use the imported texture");
            foreach(string key in new[]{"wood-floor","plaster-damp"})
            {
                var assigned=run.GetComponentsInChildren<Renderer>().SelectMany(renderer=>renderer.sharedMaterials)
                    .First(material=>material&&material.GetTag("GraphicsSurface",false)==key);
                foreach(var channel in new[]{("_BaseMap","albedo"),("_BumpMap","normal"),("_OcclusionMap","ao"),("_MetallicGlossMap","metallic-smoothness")})
                    Assert.That(assigned.GetTexture(channel.Item1),Is.SameAs(Resources.Load<Texture2D>("GraphicsPbr/"+key+"/"+channel.Item2)));
            }
            var ambience = session.GetComponent(RequireType("RoomAmbience"));
            yield return null;
            var voices = Get<System.Collections.IEnumerable>(ambience, "Voices");
            int spatialVoices = 0;
            foreach (var voice in voices)
            {
                var source = (AudioSource)voice.GetType().GetField("source").GetValue(voice);
                Assert.That(source.transform.position.x, Is.InRange(200f, 248f));
                Assert.That(source.transform.position.z, Is.InRange(200f, 248f));
                Assert.That(source.spatialBlend, Is.EqualTo(1f)); spatialVoices++;
            }
            Assert.That(spatialVoices, Is.EqualTo(3), "Old-school ambience was not moved into the playable corridor");
            var memories = Components("Interactable").Where(x => Get<object>(x, "kind").ToString() == "CorridorMemory").ToArray();
            Assert.That(memories.Length, Is.EqualTo(5));
            yield return null;
            foreach (var item in memories)
            {
                var sound = item.GetComponent<AudioSource>();
                Assert.That(sound, Is.Not.Null); Assert.That(sound.isPlaying, Is.True);
                Assert.That(sound.spatialBlend, Is.EqualTo(1)); Assert.That(sound.maxDistance, Is.EqualTo(9));
            }
            Call(shell, "Pause"); yield return null;
            Assert.That(memories.All(x => !x.GetComponent<AudioSource>().isPlaying), Is.True, "Collectible bells continued while paused");
            Call(shell, "Resume"); yield return null;
            Assert.That(memories.All(x => x.GetComponent<AudioSource>().isPlaying), Is.True);
            Assert.That(Get<int>(session, "TotalRecords"), Is.EqualTo(5));
            var start = player.transform.position;
            var doors = Components("Interactable").Where(x => x.name == "Corridor sliding door").ToArray();
            Assert.That(doors.Length, Is.GreaterThan(0));
            // Topology paths are checked with all player-operable doors open.
            // Closed-door traversal and pursuit have their own physical tests.
            foreach (var door in doors) Call(door, "OpenForPursuer");
            yield return Delay(1.8f);
            Assert.That(NavMesh.SamplePosition(start, out var startHit, .5f, NavMesh.AllAreas), Is.True, "Generated entrance has no navigation: " + start);
            foreach (var item in memories)
            {
                var path = new NavMeshPath();
                var approach = item.transform.position + Vector3.forward * 1.1f; approach.y = startHit.position.y;
                Assert.That(NavMesh.SamplePosition(approach, out var hit, .8f, NavMesh.AllAreas), Is.True, "No floor beside " + item.name);
                Assert.That(NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path), Is.True, "No route from " + startHit.position + " to " + hit.position + "; agentType=" + NavMesh.GetSettingsByIndex(0).agentTypeID);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Unreachable generated memory " + item.name);
            }
            var threats = Components("StalkerBrain").Where(x => x.name.EndsWith("— corridor")).ToArray();
            Assert.That(threats.Length, Is.EqualTo(4));
            Assert.That(threats.Count(x => x.gameObject.activeSelf), Is.EqualTo(2));
            Assert.That(threats.All(x => x.GetComponentInChildren<SkinnedMeshRenderer>(true)), Is.True, "Original models missing");
            foreach (var actor in threats) foreach (var marker in Get<Transform[]>(actor, "patrol"))
            {
                Assert.That(NavMesh.SamplePosition(marker.position, out var destination, .15f, NavMesh.AllAreas), Is.True,
                    "Patrol waypoint remained inside furniture");
                Assert.That(Vector3.Distance(marker.position, destination.position), Is.LessThan(.1f));
            }
            var stock = Get<Component>(player, "Firecrackers"); Assert.That(Get<int>(stock, "Count"), Is.EqualTo(1));
            var supplies = Components("Interactable").Where(x => Get<object>(x, "kind").ToString() == "FirecrackerSupply").ToArray();
            Assert.That(supplies.Length, Is.EqualTo(8));
            for (int i = 0; i < 4; i++) Call(supplies[i], "Use", player);
            Assert.That(Get<int>(stock, "Count"), Is.EqualTo(5));
            Call(supplies[4], "Use", player); Assert.That(supplies[4].gameObject.activeSelf, Is.True, "Full stock consumed a pickup");
            Call(session, "TryEscape"); Assert.That(Get<bool>(session, "Finished"), Is.False);
            // Any-order progression fixture; this does not claim a full input survival route.
            foreach (var item in memories.Reverse()) Call(item, "Use", player);
            Assert.That(Get<int>(session, "RecordsRecovered"), Is.EqualTo(5));
            yield return null;
            Assert.That(memories.All(x => !x.GetComponent<AudioSource>().isPlaying), Is.True, "Collected memories continued sounding");
            Assert.That(threats.Count(x => x.gameObject.activeSelf), Is.EqualTo(4));
            // Genuine keyboard movement at the generated entrance; no teleport/path injection.
            float distance = 0; var previous = player.transform.position;
            yield return KeysObserved(Key.W);
            float deadline = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < deadline) { yield return null; distance += Vector3.Distance(previous, player.transform.position); previous = player.transform.position; }
            Keys(); Assert.That(distance, Is.GreaterThan(.4f));
            Call(shell, "Pause"); var paused = player.transform.position; yield return Delay(.2f);
            Assert.That(player.transform.position, Is.EqualTo(paused)); Call(shell, "Resume");
            var camera = Get<Camera>(player, "eyes");
            var capture = new RenderTexture(1280, 720, 24); capture.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = capture });
            var previousTarget = RenderTexture.active; RenderTexture.active = capture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("Verification/corridor"); System.IO.File.WriteAllBytes("Verification/corridor/entrance.png", image.EncodeToPNG());
            RenderTexture.active = previousTarget; camera.targetTexture = null; capture.Release(); Object.Destroy(capture); Object.Destroy(image);
            Call(session, "TryEscape"); Assert.That(Get<bool>(session, "Escaped"), Is.True);
        }
    }
}
