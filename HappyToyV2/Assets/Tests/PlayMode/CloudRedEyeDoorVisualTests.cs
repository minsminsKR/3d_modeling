using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Mesh EyeOriginalMesh(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;

        void EyeSameOriginalMaterial(Material actual, Material original, string context)
        {
            Assert.That(actual.shader, Is.SameAs(original.shader), context);
            Assert.That(actual.shaderKeywords.Where(keyword => keyword != "_EMISSION").OrderBy(keyword => keyword),
                Is.EqualTo(original.shaderKeywords.Where(keyword => keyword != "_EMISSION").OrderBy(keyword => keyword)),
                context + ": inherited shader flags changed");
            for (int index = 0; index < original.shader.GetPropertyCount(); index++)
            {
                string property = original.shader.GetPropertyName(index);
                if (property == "_EmissionColor" || property == "_EmissionMap") continue;
                switch (original.shader.GetPropertyType(index))
                {
                    case ShaderPropertyType.Color: Assert.That(actual.GetColor(property), Is.EqualTo(original.GetColor(property)), context + property); break;
                    case ShaderPropertyType.Vector: Assert.That(actual.GetVector(property), Is.EqualTo(original.GetVector(property)), context + property); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: Assert.That(actual.GetFloat(property), Is.EqualTo(original.GetFloat(property)), context + property); break;
                    case ShaderPropertyType.Texture:
                        Assert.That(actual.GetTexture(property), Is.SameAs(original.GetTexture(property)), context + property);
                        Assert.That(actual.GetTextureScale(property), Is.EqualTo(original.GetTextureScale(property)), context + property + " scale");
                        Assert.That(actual.GetTextureOffset(property), Is.EqualTo(original.GetTextureOffset(property)), context + property + " offset"); break;
                }
            }
        }

        Color EyeBlockEmission(Renderer renderer, int slot)
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, slot);
            return block.GetColor("_EmissionColor");
        }

        float EyeEmissionRgb(Color colour) => Mathf.Max(Mathf.Abs(colour.r), Mathf.Max(Mathf.Abs(colour.g), Mathf.Abs(colour.b)));

        IEnumerator EyeActivateProductionVisuals()
        {
            foreach (var motion in Components("V1MonsterMotion"))
            {
                var brain = motion.GetComponent(RequireType("StalkerBrain")) as Behaviour;
                if (!brain) continue;
                brain.enabled = false;
                var agent = motion.GetComponent<NavMeshAgent>(); if (agent) agent.enabled = false;
                motion.gameObject.SetActive(true);
            }
            foreach (string type in new[] { "LanternMaskEncounter", "WeepingAngelEncounter" }) foreach (var actor in Components(type))
            {
                ((Behaviour)actor).enabled = false;
                var agent = actor.GetComponent<NavMeshAgent>(); if (agent) agent.enabled = false;
                actor.gameObject.SetActive(true);
                // Appearance gates can keep the authored visual child hidden
                // after the actor root becomes active. This render fixture
                // explicitly activates that child with all encounter AI off.
                Get<Transform>(actor, type == "LanternMaskEncounter" ? "mask" : "visual").gameObject.SetActive(true);
            }
            yield return null;
        }

        string EyeRelativePath(Transform root, Transform node)
        {
            var names = new Stack<string>();
            while (node != root)
            {
                Assert.That(node, Is.Not.Null, "Original eye renderer left the source visual");
                names.Push(node.name); node = node.parent;
            }
            return string.Join("/", names);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NaturalEyeLifecycleRestoresCallerMaterialAndIndexedPropertiesAndHonoursLaterMaterialOwner()
        {
            Call(shell, "BeginChapter"); yield return null; yield return null;
            yield return EyeActivateProductionVisuals();
            var sourceEye = Components("MonsterRedEyes").First(eye => Get<string>(eye, "ProfileKey") == "Cyclopse");
            var motion = sourceEye.GetComponentInParent(RequireType("V1MonsterMotion")) as Component;
            var visual = Get<Transform>(motion, "model");
            var sourceRenderer = Get<IReadOnlyList<Renderer>>(sourceEye, "EmissionRenderers").Single();
            int slot = ((int[])Call(sourceEye, "GetAffectedSlots", sourceRenderer)).Single();
            var baseline = (Material)Call(sourceEye, "GetOriginalMaterial", sourceRenderer, slot);
            GameObject clone = null; Material laterOwner = null;
            try
            {
                // A disposable production visual supplies the real imported
                // mesh/UV/bones. Seed caller-owned properties before a fresh
                // production Attach, so restoration has an independent oracle.
                clone = UnityEngine.Object.Instantiate(visual.gameObject); clone.name = "Isolated original-eye lifecycle fixture";
                clone.SetActive(true);
                foreach (var behaviour in clone.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                foreach (var old in clone.GetComponentsInChildren(RequireType("MonsterRedEyes"), true).Cast<Component>())
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                string rendererPath = EyeRelativePath(visual, sourceRenderer.transform);
                var renderer = (rendererPath.Length == 0 ? clone.transform : clone.transform.Find(rendererPath)).GetComponent<Renderer>();
                var assigned = renderer.sharedMaterials; assigned[slot] = baseline; renderer.sharedMaterials = assigned;
                var mesh = EyeOriginalMesh(renderer); bool rendererEnabled = renderer.enabled;
                int colliderCount = clone.GetComponentsInChildren<Collider>(true).Length;
                var sentinel = new Vector4(.13f, .25f, .37f, .49f);
                var originalEmission = new Color(.07f, .04f, .01f, .4f);
                var originalTexture = baseline.GetTexture("_BaseMap");
                var callerBlock = new MaterialPropertyBlock();
                callerBlock.SetVector("_EyePreservationSentinel", sentinel);
                callerBlock.SetTexture("_EyePreservationTexture", originalTexture);
                callerBlock.SetColor("_EmissionColor", originalEmission);
                renderer.SetPropertyBlock(callerBlock, slot);
                var effect = (Component)Call(RequireType("MonsterRedEyes"), "Attach", clone.transform, "Cyclopse");
                Assert.That(Get<bool>(effect, "Prepared"), Is.True);
                var applied = renderer.sharedMaterials[slot];
                Assert.That(applied, Is.Not.SameAs(baseline)); EyeSameOriginalMaterial(applied, baseline, "Lifecycle attach: ");
                Assert.That(EyeBlockEmission(renderer, slot).r, Is.GreaterThan(.1f));
                ((Behaviour)effect).enabled = false;
                var restoredBlock = new MaterialPropertyBlock(); renderer.GetPropertyBlock(restoredBlock, slot);
                Assert.That(renderer.sharedMaterials[slot], Is.SameAs(baseline), "Disable did not return the caller's original eye material");
                Assert.That(Vector4.Distance(restoredBlock.GetColor("_EmissionColor"), originalEmission), Is.LessThan(.000001f));
                Assert.That(restoredBlock.GetVector("_EyePreservationSentinel"), Is.EqualTo(sentinel));
                Assert.That(restoredBlock.GetTexture("_EyePreservationTexture"), Is.SameAs(originalTexture));
                ((Behaviour)effect).enabled = true;
                renderer.GetPropertyBlock(restoredBlock, slot);
                Assert.That(renderer.sharedMaterials[slot], Is.SameAs(applied), "Enable created a different clone or failed to rebind its own slot");
                Assert.That(restoredBlock.GetVector("_EyePreservationSentinel"), Is.EqualTo(sentinel));
                Assert.That(restoredBlock.GetTexture("_EyePreservationTexture"), Is.SameAs(originalTexture));
                Assert.That(restoredBlock.GetColor("_EmissionColor").r, Is.GreaterThan(.1f));
                Assert.That(EyeOriginalMesh(renderer), Is.SameAs(mesh)); Assert.That(renderer.enabled, Is.EqualTo(rendererEnabled));
                Assert.That(clone.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(colliderCount));

                // Another presentation owner legitimately replaces that slot.
                // The old eye owner must neither overwrite its material nor
                // keep pulsing emission into its indexed property block.
                laterOwner = new Material(baseline) { name = "Later-owner eye preservation material" };
                assigned = renderer.sharedMaterials; assigned[slot] = laterOwner; renderer.sharedMaterials = assigned;
                var laterSentinel = new Vector4(.71f, .63f, .55f, .47f);
                var laterEmission = new Color(.19f, .11f, .06f, .3f);
                var laterBlock = new MaterialPropertyBlock(); laterBlock.SetVector("_EyePreservationSentinel", laterSentinel);
                laterBlock.SetColor("_EmissionColor", laterEmission); renderer.SetPropertyBlock(laterBlock, slot);
                yield return null; yield return null; // Real production LateUpdate.
                Assert.That(renderer.sharedMaterials[slot], Is.SameAs(laterOwner));
                renderer.GetPropertyBlock(restoredBlock, slot);
                Assert.That(Vector4.Distance(restoredBlock.GetColor("_EmissionColor"), laterEmission), Is.LessThan(.000001f), "Pulse overwrote a later material owner's block");
                Assert.That(restoredBlock.GetVector("_EyePreservationSentinel"), Is.EqualTo(laterSentinel));
                ((Behaviour)effect).enabled = false; ((Behaviour)effect).enabled = true;
                Call(effect, "SetEmissionEnabled", false); yield return null;
                Assert.That(renderer.sharedMaterials[slot], Is.SameAs(laterOwner));
                renderer.GetPropertyBlock(restoredBlock, slot);
                Assert.That(Vector4.Distance(restoredBlock.GetColor("_EmissionColor"), laterEmission), Is.LessThan(.000001f));
                Assert.That(restoredBlock.GetVector("_EyePreservationSentinel"), Is.EqualTo(laterSentinel));
                UnityEngine.Object.DestroyImmediate(effect.gameObject);
                Assert.That(renderer.sharedMaterials[slot], Is.SameAs(laterOwner), "Teardown restored a stale material over its new owner");
                renderer.GetPropertyBlock(restoredBlock, slot);
                Assert.That(Vector4.Distance(restoredBlock.GetColor("_EmissionColor"), laterEmission), Is.LessThan(.000001f));
                Assert.That(restoredBlock.GetVector("_EyePreservationSentinel"), Is.EqualTo(laterSentinel));
                Assert.That(baseline, Is.Not.Null, "Eye teardown destroyed its borrowed original material");
                Assert.That(originalTexture, Is.Not.Null, "Eye teardown destroyed the original pupil texture");
            }
            finally
            {
                if (clone) UnityEngine.Object.DestroyImmediate(clone);
                if (laterOwner) UnityEngine.Object.DestroyImmediate(laterOwner);
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator EveryHostileFacePreservesOriginalEyeSurfaceAndPupilTexturesWithOneCyclopseEyeAndNoAddedGeometry()
        {
            Call(shell, "BeginChapter"); yield return null; yield return null;
            // Controlled appearance fixture: activate otherwise unreleased
            // actors with AI/agents disabled to initialize Baby's real visual.
            yield return EyeActivateProductionVisuals();
            var eyes = Components("MonsterRedEyes");
            foreach (string key in new[] { "Cyclopse", "Uncat", "Hwacat_angry", "Baby", "LanternMask", "Mannequin" })
                Assert.That(eyes.Any(item => Get<string>(item, "ProfileKey") == key), Is.True, "Missing natural-eye face: " + key);
            var retained = Resources.LoadAll<Material>("FeedbackShaderVariants").Concat(Resources.LoadAll<Material>("GraphicsPbr")).ToArray();
            foreach (var eye in eyes)
            {
                string key = Get<string>(eye, "ProfileKey");
                Assert.That(Get<bool>(eye, "Prepared"), Is.True);
                var head = Get<Transform>(eye, "Head");
                var anchors = Get<IReadOnlyList<Transform>>(eye, "EyeAnchors");
                var radii = Get<IReadOnlyList<float>>(eye, "EyeRadii");
                var emitters = Get<IReadOnlyList<Renderer>>(eye, "EmissionRenderers");
                Assert.That(anchors.Count, Is.EqualTo(key == "Cyclopse" ? 1 : 2), key + " anatomical eye count");
                Assert.That(radii.Count, Is.EqualTo(anchors.Count));
                Assert.That(radii.All(radius => radius > .002f && radius < .15f), Is.True, key + " actual eye extent");
                Assert.That(emitters, Is.Not.Empty);
                Assert.That(eye.GetComponentsInChildren<Renderer>(true), Is.Empty, "An overlay covers the original iris/pupil");
                Assert.That(eye.GetComponentsInChildren<MeshFilter>(true), Is.Empty, "Eyes added opaque dome/disc geometry");
                Assert.That(eye.GetComponentsInChildren<Light>(true), Is.Empty, "Eyes added real illumination");
                Assert.That(eye.GetComponentsInChildren<Collider>(true), Is.Empty, "Eyes altered actor physics");
                Assert.That(eye.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
                var meshes = emitters.Select(EyeOriginalMesh).ToArray();
                var materials = emitters.Select(renderer => renderer.sharedMaterials).ToArray();
                var enabled = emitters.Select(renderer => renderer.enabled).ToArray();
                var shadows = emitters.Select(renderer => renderer.shadowCastingMode).ToArray();
                var motion = eye.GetComponentInParent(RequireType("V1MonsterMotion")) as Component;
                foreach (var emitter in emitters)
                {
                    Assert.That(emitter.transform.IsChildOf(eye.transform), Is.False, "Emission must use the original face renderer");
                    int[] slots = (int[])Call(eye, "GetAffectedSlots", emitter); Assert.That(slots, Is.Not.Empty);
                    foreach (int slot in slots)
                    {
                        var material = emitter.sharedMaterials[slot];
                        EyeSameOriginalMaterial(material, (Material)Call(eye, "GetOriginalMaterial", emitter, slot), key + " original surface slot: ");
                        Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, key + " original eye atlas was removed");
                        Assert.That(material.GetTexture("_EmissionMap"), Is.Not.Null, key + " original-surface mask is missing");
                        Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True);
                        Assert.That(retained.Any(template => template.shader == material.shader &&
                            template.shaderKeywords.OrderBy(value => value).SequenceEqual(material.shaderKeywords.OrderBy(value => value))),
                            Is.True, key + " emission combination is absent from release shader anchors");
                        Assert.That(material.GetFloat("_Surface"), Is.Zero, "Eye surface became transparent");
                        Assert.That(material.GetFloat("_Cull"), Is.EqualTo(2), "Eye emission can show through the back of the head");
                        if (motion)
                        {
                            var skins = Get<Transform>(motion, "model").GetComponentsInChildren<SkinnedMeshRenderer>();
                            var source = Resources.Load<GameObject>("EnemyRefinement/" + key + "-v2");
                            Assert.That(emitter is SkinnedMeshRenderer, Is.True);
                            int skinIndex = System.Array.IndexOf(skins, (SkinnedMeshRenderer)emitter);
                            Assert.That(EyeOriginalMesh(emitter), Is.SameAs(source.GetComponentsInChildren<SkinnedMeshRenderer>()[skinIndex].sharedMesh),
                                key + " imported eye topology was replaced");
                        }
                    }
                }
                Call(eye, "SetEmissionEnabled", false);
                foreach (var emitter in emitters) foreach (int slot in (int[])Call(eye, "GetAffectedSlots", emitter))
                    Assert.That(EyeEmissionRgb(EyeBlockEmission(emitter, slot)), Is.LessThan(.00001f), key + " emission-off leaves a red overlay");
                Call(eye, "SetEmissionEnabled", true);
                for (int index = 0; index < emitters.Count; index++)
                {
                    Assert.That(EyeOriginalMesh(emitters[index]), Is.SameAs(meshes[index]));
                    Assert.That(emitters[index].sharedMaterials, Is.EqualTo(materials[index]));
                    Assert.That(emitters[index].enabled, Is.EqualTo(enabled[index]), "Toggling emission hid the original eyeball");
                    Assert.That(emitters[index].shadowCastingMode, Is.EqualTo(shadows[index]));
                    foreach (int slot in (int[])Call(eye, "GetAffectedSlots", emitters[index]))
                    {
                        var colour = EyeBlockEmission(emitters[index], slot);
                        Assert.That(colour.r, Is.GreaterThan(.1f)); Assert.That(colour.g, Is.LessThan(colour.r * .1f));
                    }
                }
                var localPoints = anchors.Select(anchor => head.InverseTransformPoint(anchor.position)).ToArray();
                var initial = head.localRotation;
                head.localRotation = initial * Quaternion.Euler(0, 22, -13);
                for (int index = 0; index < anchors.Count; index++)
                {
                    Assert.That(anchors[index].IsChildOf(head), Is.True);
                    Assert.That(Vector3.Distance(anchors[index].position, head.TransformPoint(localPoints[index])), Is.LessThan(.00001f),
                        "A red core did not follow the animated face transform");
                }
                head.localRotation = initial;
            }
            Call(shell, "Pause");
            foreach (var eye in eyes) Call(eye, "SetEmissionEnabled", false);
            yield return new WaitForSecondsRealtime(.08f);
            foreach (var eye in eyes) foreach (var emitter in Get<IReadOnlyList<Renderer>>(eye, "EmissionRenderers"))
                foreach (int slot in (int[])Call(eye, "GetAffectedSlots", emitter))
                    Assert.That(EyeEmissionRgb(EyeBlockEmission(emitter, slot)), Is.LessThan(.00001f), "Paused LateUpdate re-enabled emission");
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator NaturalEyeEmissionTogglePreservesPausedChapterCheckpointPhysicsAndSourceAssetsAcrossReload()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            Call(shell, "BeginChapter"); yield return null; yield return null; Call(shell, "Pause");
            var before = SchoolCheckpointCopy(Call(session, "CaptureChapterCheckpoint"));
            var effects = Components("MonsterRedEyes").Where(eye => Get<bool>(eye, "Prepared")).ToArray();
            Assert.That(effects, Is.Not.Empty);
            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var navigation = NavMesh.CalculateTriangulation();
            var sourceMaps = effects.Select(eye => Resources.Load<Texture2D>("ThreatEyes/" + Get<string>(eye, "ProfileKey") + "-emission")).Distinct().ToArray();
            Assert.That(sourceMaps.All(map => map), Is.True);
            foreach (var eye in effects) Call(eye, "SetEmissionEnabled", false);
            yield return new WaitForSecondsRealtime(.08f);
            foreach (var eye in effects) foreach (var renderer in Get<IReadOnlyList<Renderer>>(eye, "EmissionRenderers"))
                foreach (int slot in (int[])Call(eye, "GetAffectedSlots", renderer))
                    Assert.That(EyeEmissionRgb(EyeBlockEmission(renderer, slot)), Is.LessThan(.00001f), "Paused LateUpdate re-enabled emission");
            var after = SchoolCheckpointCopy(Call(session, "CaptureChapterCheckpoint")); Set(after, "token", Get<string>(before, "token"));
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(JsonUtility.ToJson(before)), "Cosmetic eye toggle changed saved gameplay");
            Assert.That(UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.EquivalentTo(colliders));
            var afterNavigation = NavMesh.CalculateTriangulation();
            Assert.That(afterNavigation.vertices, Is.EqualTo(navigation.vertices)); Assert.That(afterNavigation.indices, Is.EqualTo(navigation.indices));
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            Assert.That(sourceMaps.All(map => map), Is.True, "Eye owner destroyed imported emission masks during scene reload");
            Call(session, "ConfigureRecordDirectory", directory); Call(session, "CreateChapterForCheckpoint", before);
            var chapter = Get<Component>(session, "Chapter"); Call(chapter, "PrepareCheckpointNavigation", before); yield return null; yield return null;
            Call(session, "ApplyChapterCheckpoint", before);
            Assert.That(Get<int>(chapter, "Recovered"), Is.EqualTo(Get<int>(before, "recovered")));
            Assert.That(Vector3.Distance(player.transform.position, Get<Vector3>(Get<object>(before, "player"), "position")), Is.LessThan(.001f));
            foreach (var eye in Components("MonsterRedEyes").Where(item => Get<bool>(item, "Prepared")))
            {
                Assert.That(Get<IReadOnlyList<Transform>>(eye, "EyeAnchors").Count,
                    Is.EqualTo(Get<string>(eye, "ProfileKey") == "Cyclopse" ? 1 : 2));
                Assert.That(eye.GetComponentsInChildren<Renderer>(true), Is.Empty);
                foreach (var renderer in Get<IReadOnlyList<Renderer>>(eye, "EmissionRenderers")) foreach (int slot in (int[])Call(eye, "GetAffectedSlots", renderer))
                {
                    var original = (Material)Call(eye, "GetOriginalMaterial", renderer, slot);
                    Assert.That(original, Is.Not.Null);
                    EyeSameOriginalMaterial(renderer.sharedMaterials[slot], original, "Restored inactive school actor eye: ");
                }
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator RaisedDoorPullsStayOnBothMovingFacesDuringOpeningAndCheckpointPoseRestore()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin(); yield return null; yield return null;
            var run = Get<Component>(session, "Corridor");
            var doors = run.GetComponentsInChildren(RequireType("Interactable"), true).Cast<Component>()
                .Where(item => Get<object>(item, "kind").ToString() == "Door" && Get<Transform>(item, "movingLeaf")).ToArray();
            Assert.That(doors, Is.Not.Empty);
            var navigation = HauntedNavigationHash();
            foreach (var door in doors.Take(3))
            {
                var leaf = Get<Transform>(door, "movingLeaf");
                var hardware = leaf.GetComponentInChildren(RequireType("CorridorDoorHardware"), true);
                Assert.That(hardware, Is.Not.Null); Assert.That(Get<bool>(hardware, "Prepared"), Is.True);
                Assert.That(Get<Component>(hardware, "Owner"), Is.SameAs(door));
                Assert.That(Get<Transform>(hardware, "Leaf"), Is.SameAs(leaf));
                var faces = Get<IReadOnlyList<Transform>>(hardware, "FaceRoots");
                Assert.That(faces.Count, Is.EqualTo(2));
                Assert.That(hardware.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(hardware.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
                Assert.That(hardware.GetComponentsInChildren(RequireType("Interactable"), true), Is.Empty);
                Assert.That(faces[0].localPosition.z, Is.LessThan(0)); Assert.That(faces[1].localPosition.z, Is.GreaterThan(0));
                Assert.That(Vector3.Dot(faces[0].forward, faces[1].forward), Is.LessThan(-.99f));
                var travel=Get<Vector3>(hardware,"OpeningWorldDelta");
                foreach(var face in faces)
                    Assert.That(Vector3.Dot(face.position-leaf.position,travel.normalized),Is.LessThan(-.9f),
                        "Actual corridor pull occupies the leading disappearing edge instead of the trailing edge");
                foreach (var face in faces)
                {
                    var renderers = face.GetComponentsInChildren<MeshRenderer>(true);
                    Assert.That(renderers.Length, Is.GreaterThan(1));
                    var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Assert.That(bounds.size.y, Is.GreaterThan(.28f), "Pull shrank into the leaf texture");
                }
                var closed = leaf.localPosition; var scale = leaf.localScale; var rotation = leaf.localRotation;
                var anchors = faces.Select(face => leaf.InverseTransformPoint(face.position)).ToArray();
                var stableId = Get<string>(door, "stableId"); var colliders = leaf.GetComponentsInChildren<Collider>(true);
                Assert.That((bool)Call(door, "OpenForPursuer"), Is.True);
                yield return Wait(() => Get<bool>(door, "AtRequestedDoorPose"), 3, "Actual door never reached its requested open pose");
                for (int index = 0; index < faces.Count; index++)
                    Assert.That(Vector3.Distance(faces[index].position, leaf.TransformPoint(anchors[index])), Is.LessThan(.0001f));
                Call(door, "RestoreDoor", false, closed); yield return null;
                Assert.That(leaf.localPosition, Is.EqualTo(closed)); Assert.That(leaf.localScale, Is.EqualTo(scale));
                Assert.That(leaf.localRotation, Is.EqualTo(rotation)); Assert.That(Get<string>(door, "stableId"), Is.EqualTo(stableId));
                Assert.That(leaf.GetComponentsInChildren<Collider>(true), Is.EqualTo(colliders));
                for (int index = 0; index < faces.Count; index++)
                    Assert.That(Vector3.Distance(faces[index].position, leaf.TransformPoint(anchors[index])), Is.LessThan(.0001f));
            }
            Assert.That(HauntedNavigationHash(), Is.EqualTo(navigation), "Render-only handles changed the corridor navigation");
        }
    }
}
