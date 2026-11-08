using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Authored surface samples measured separately on the two retained
        // source FBXs. These are review candidates, not shipping assets.
        static readonly Vector3[] Pine11NearBackPoints = {
            new Vector3(7.94916f, 2.54823f, 9.74232f),
            new Vector3(8.01156f, 2.56780f, 9.78749f),
            new Vector3(8.05164f, 2.58005f, 9.83863f),
            new Vector3(8.08285f, 2.60060f, 9.89394f),
            new Vector3(8.09945f, 2.61771f, 9.95608f),
            new Vector3(8.11296f, 2.63356f, 10.02146f),
            new Vector3(8.11308f, 2.64926f, 10.08103f),
            new Vector3(8.10822f, 2.66462f, 10.15631f),
            new Vector3(8.05430f, 2.68418f, 10.22698f),
            new Vector3(7.99332f, 2.69513f, 10.28393f),
            new Vector3(7.93870f, 2.71436f, 10.32757f),
            new Vector3(7.91944f, 2.73510f, 10.35710f),
            new Vector3(7.91023f, 2.75115f, 10.38884f),
            new Vector3(7.91019f, 2.76773f, 10.43810f),
            new Vector3(7.90717f, 2.78832f, 10.49124f),
            new Vector3(7.86666f, 2.78559f, 10.56395f),
            new Vector3(7.79715f, 2.80151f, 10.57553f),
            new Vector3(7.76143f, 2.82056f, 10.59605f),
            new Vector3(7.72744f, 2.83248f, 10.62451f),
            new Vector3(7.69514f, 2.84906f, 10.65454f),
            new Vector3(7.66322f, 2.86069f, 10.69011f),
            new Vector3(7.62903f, 2.87727f, 10.72103f),
            new Vector3(7.58070f, 2.89714f, 10.74840f),
            new Vector3(7.54080f, 2.91965f, 10.76392f),
            new Vector3(7.49193f, 2.93328f, 10.77623f),
            new Vector3(7.44856f, 2.94986f, 10.78469f),
            new Vector3(7.40023f, 2.96480f, 10.79058f),
            new Vector3(7.35716f, 2.98138f, 10.79243f),
            new Vector3(7.31224f, 3.00212f, 10.78892f),
            new Vector3(7.27033f, 3.02004f, 10.78036f),
            new Vector3(7.23745f, 3.03608f, 10.77605f),
            new Vector3(7.20818f, 3.05497f, 10.78629f),
            new Vector3(7.11575f, 3.07118f, 10.82018f),
            new Vector3(7.07129f, 3.08915f, 10.80205f),
            new Vector3(7.02978f, 3.10675f, 10.77392f),
            new Vector3(7.00646f, 3.12757f, 10.71025f),
            new Vector3(6.99044f, 3.13218f, 10.70492f),
            new Vector3(6.93543f, 3.14545f, 10.68843f),
            new Vector3(6.89629f, 3.15653f, 10.66617f),
            new Vector3(6.84456f, 3.17971f, 10.64077f),
            new Vector3(6.82009f, 3.21479f, 10.59259f),
            new Vector3(6.81106f, 3.23137f, 10.55157f),
            new Vector3(6.80823f, 3.25094f, 10.50848f),
            new Vector3(6.84009f, 3.27192f, 10.44848f),
            new Vector3(6.87222f, 3.28572f, 10.40171f),
            new Vector3(6.85791f, 3.28549f, 10.40290f),
            new Vector3(6.83715f, 3.29611f, 10.37500f),
            new Vector3(6.78137f, 3.31987f, 10.32405f),
            new Vector3(6.77908f, 3.34154f, 10.27805f)
        };
        static readonly Vector3[] Pine11NearBackNormals = {
            new Vector3(-0.00397f, 0.21510f, -0.97658f),
            new Vector3(0.63267f, 0.31458f, -0.70765f),
            new Vector3(0.81767f, 0.17050f, -0.54986f),
            new Vector3(0.86576f, 0.30278f, -0.39848f),
            new Vector3(0.90558f, 0.32053f, -0.27783f),
            new Vector3(0.95300f, 0.29628f, -0.06333f),
            new Vector3(0.95503f, 0.26721f, -0.12849f),
            new Vector3(0.90205f, 0.22643f, 0.36746f),
            new Vector3(0.68295f, 0.32594f, 0.65371f),
            new Vector3(0.63422f, 0.13813f, 0.76072f),
            new Vector3(0.65191f, 0.22672f, 0.72361f),
            new Vector3(0.88151f, 0.36544f, 0.29899f),
            new Vector3(0.93034f, 0.34778f, -0.11628f),
            new Vector3(0.93034f, 0.34778f, -0.11628f),
            new Vector3(0.87078f, 0.48171f, -0.09841f),
            new Vector3(0.39081f, -0.16192f, 0.90612f),
            new Vector3(0.38093f, -0.18383f, 0.90615f),
            new Vector3(0.60046f, -0.10149f, 0.79319f),
            new Vector3(0.58403f, -0.25684f, 0.77003f),
            new Vector3(0.58403f, -0.25684f, 0.77003f),
            new Vector3(0.48733f, -0.42152f, 0.76474f),
            new Vector3(0.48733f, -0.42152f, 0.76474f),
            new Vector3(0.20027f, -0.31182f, 0.92880f),
            new Vector3(0.29637f, -0.11393f, 0.94825f),
            new Vector3(0.10823f, -0.21235f, 0.97118f),
            new Vector3(0.10823f, -0.21235f, 0.97118f),
            new Vector3(-0.06133f, -0.26675f, 0.96181f),
            new Vector3(-0.06133f, -0.26675f, 0.96181f),
            new Vector3(-0.15193f, -0.12796f, 0.98007f),
            new Vector3(-0.19456f, -0.08333f, 0.97734f),
            new Vector3(0.06521f, -0.10140f, 0.99271f),
            new Vector3(0.78400f, -0.02408f, 0.62029f),
            new Vector3(-0.34542f, -0.03629f, 0.93774f),
            new Vector3(-0.41638f, 0.01000f, 0.90913f),
            new Vector3(-0.57509f, 0.04399f, 0.81691f),
            new Vector3(-0.89940f, 0.18535f, 0.39588f),
            new Vector3(-0.27421f, -0.21364f, 0.93764f),
            new Vector3(-0.52448f, -0.32392f, 0.78740f),
            new Vector3(-0.44086f, -0.50710f, 0.74060f),
            new Vector3(-0.69847f, -0.28709f, 0.65552f),
            new Vector3(-0.88526f, 0.32975f, 0.32800f),
            new Vector3(-0.88526f, 0.32975f, 0.32800f),
            new Vector3(-0.86931f, 0.42950f, 0.24460f),
            new Vector3(-0.76182f, 0.57634f, -0.29576f),
            new Vector3(-0.60376f, 0.48363f, -0.63370f),
            new Vector3(-0.89849f, -0.07662f, 0.43226f),
            new Vector3(-0.75294f, -0.27504f, 0.59785f),
            new Vector3(-0.95736f, -0.03569f, 0.28668f),
            new Vector3(-0.99091f, 0.13385f, 0.01320f)
        };
        static readonly Vector3[] Pine11MidBackPoints = {
            new Vector3(7.91503f, 2.53934f, 9.73514f),
            new Vector3(7.99852f, 2.55781f, 9.78776f),
            new Vector3(8.02151f, 2.57449f, 9.84109f),
            new Vector3(8.03892f, 2.59559f, 9.89072f),
            new Vector3(8.06746f, 2.61228f, 9.93965f),
            new Vector3(8.09702f, 2.62897f, 9.99014f),
            new Vector3(8.12107f, 2.65478f, 10.05166f),
            new Vector3(8.11511f, 2.66888f, 10.13652f),
            new Vector3(8.07397f, 2.68549f, 10.20082f),
            new Vector3(8.00283f, 2.69115f, 10.27036f),
            new Vector3(7.93164f, 2.70784f, 10.31643f),
            new Vector3(7.89257f, 2.73078f, 10.33395f),
            new Vector3(7.85957f, 2.74507f, 10.35166f),
            new Vector3(7.92541f, 2.76472f, 10.41938f),
            new Vector3(7.91520f, 2.78693f, 10.48734f),
            new Vector3(7.87829f, 2.78267f, 10.55942f),
            new Vector3(7.81131f, 2.79936f, 10.57571f),
            new Vector3(7.76241f, 2.81684f, 10.59315f),
            new Vector3(7.72526f, 2.83381f, 10.61438f),
            new Vector3(7.69670f, 2.85029f, 10.63620f),
            new Vector3(7.66972f, 2.86201f, 10.66247f),
            new Vector3(7.61815f, 2.88403f, 10.69456f),
            new Vector3(7.57949f, 2.90047f, 10.71134f),
            new Vector3(7.54071f, 2.91716f, 10.72879f),
            new Vector3(7.50185f, 2.93385f, 10.74628f),
            new Vector3(7.46236f, 2.95054f, 10.76398f),
            new Vector3(7.42168f, 2.96723f, 10.78210f),
            new Vector3(7.37140f, 2.98284f, 10.79605f),
            new Vector3(7.32779f, 2.99953f, 10.80334f),
            new Vector3(7.27281f, 3.02483f, 10.80193f),
            new Vector3(7.24326f, 3.03593f, 10.79747f),
            new Vector3(7.18704f, 3.05444f, 10.79617f),
            new Vector3(7.14577f, 3.07113f, 10.78523f),
            new Vector3(7.10424f, 3.08781f, 10.77421f),
            new Vector3(7.06201f, 3.10405f, 10.76376f),
            new Vector3(7.00507f, 3.12654f, 10.73580f),
            new Vector3(6.98099f, 3.14366f, 10.68051f),
            new Vector3(6.96237f, 3.14580f, 10.68381f),
            new Vector3(6.91525f, 3.17407f, 10.66883f),
            new Vector3(6.87439f, 3.19075f, 10.64633f),
            new Vector3(6.84716f, 3.19793f, 10.60418f),
            new Vector3(6.80983f, 3.23236f, 10.55906f),
            new Vector3(6.83015f, 3.25683f, 10.50354f),
            new Vector3(6.83968f, 3.25941f, 10.47167f),
            new Vector3(6.83668f, 3.28265f, 10.43642f),
            new Vector3(6.83631f, 3.29934f, 10.40330f),
            new Vector3(6.83607f, 3.31603f, 10.37100f),
            new Vector3(6.80136f, 3.32630f, 10.33577f),
            new Vector3(6.79367f, 3.34298f, 10.29847f)
        };
        static readonly Vector3[] Pine11MidBackNormals = {
            new Vector3(-0.60703f, -0.02082f, -0.79440f),
            new Vector3(0.91318f, 0.03856f, -0.40572f),
            new Vector3(0.91318f, 0.03856f, -0.40572f),
            new Vector3(0.81997f, 0.18549f, -0.54152f),
            new Vector3(0.81997f, 0.18549f, -0.54152f),
            new Vector3(0.81997f, 0.18549f, -0.54152f),
            new Vector3(0.81254f, 0.48946f, -0.31655f),
            new Vector3(0.81563f, 0.40343f, 0.41472f),
            new Vector3(0.81507f, 0.40061f, 0.41854f),
            new Vector3(0.54849f, 0.03327f, 0.83550f),
            new Vector3(0.54849f, 0.03327f, 0.83550f),
            new Vector3(0.95925f, 0.24170f, 0.14634f),
            new Vector3(0.86556f, 0.16161f, -0.47401f),
            new Vector3(0.85065f, 0.26026f, -0.45680f),
            new Vector3(0.87810f, 0.44421f, 0.17782f),
            new Vector3(0.16833f, -0.25403f, 0.95243f),
            new Vector3(0.16833f, -0.25403f, 0.95243f),
            new Vector3(0.28888f, -0.22734f, 0.92998f),
            new Vector3(0.41399f, -0.21807f, 0.88378f),
            new Vector3(0.62936f, -0.22514f, 0.74379f),
            new Vector3(0.73407f, -0.39082f, 0.55534f),
            new Vector3(0.29111f, -0.21284f, 0.93271f),
            new Vector3(0.31961f, -0.22112f, 0.92139f),
            new Vector3(0.31961f, -0.22112f, 0.92139f),
            new Vector3(0.31961f, -0.22112f, 0.92139f),
            new Vector3(0.31961f, -0.22112f, 0.92139f),
            new Vector3(0.31961f, -0.22112f, 0.92139f),
            new Vector3(0.06269f, -0.25707f, 0.96436f),
            new Vector3(0.06269f, -0.25707f, 0.96436f),
            new Vector3(-0.32403f, 0.03022f, 0.94557f),
            new Vector3(0.10649f, -0.15624f, 0.98196f),
            new Vector3(-0.29091f, -0.09561f, 0.95196f),
            new Vector3(-0.29091f, -0.09561f, 0.95196f),
            new Vector3(-0.29091f, -0.09561f, 0.95196f),
            new Vector3(-0.26870f, -0.11058f, 0.95685f),
            new Vector3(-0.81940f, 0.08257f, 0.56724f),
            new Vector3(-0.99338f, 0.09716f, 0.06126f),
            new Vector3(-0.39227f, -0.38780f, 0.83411f),
            new Vector3(-0.48305f, -0.00194f, 0.87559f),
            new Vector3(-0.48305f, -0.00194f, 0.87559f),
            new Vector3(-0.68766f, -0.31896f, 0.65221f),
            new Vector3(-0.93963f, 0.27233f, 0.20720f),
            new Vector3(-0.84345f, 0.53185f, -0.07572f),
            new Vector3(-0.97811f, 0.06145f, 0.19880f),
            new Vector3(-0.94797f, 0.27989f, 0.15170f),
            new Vector3(-0.94797f, 0.27989f, 0.15170f),
            new Vector3(-0.94797f, 0.27989f, 0.15170f),
            new Vector3(-0.97106f, 0.06579f, 0.22961f),
            new Vector3(-0.97106f, 0.06579f, 0.22961f)
        };

        [Serializable] sealed class Pine11VariantSurfaceStats
        {
            public string part, note;
            public int samples, hits, misses, negative, underFiveMillimetres, overEightCentimetres;
            public float minGapMetres, maxGapMetres;
        }
        [Serializable] sealed class Pine11VariantViewStats
        {
            public string lod, view, variant, note;
            public bool flashlightEnabled;
            public float baseRed, baseGreen, baseBlue, widthMultiplier, offsetMetres;
            public CandleReviewRegion targetRegion, restoredBaseline;
        }
        [Serializable] sealed class Pine11VariantContactStats
        {
            public string lod, variant, note;
            public Pine11RopeSurfaceReport centreline;
            public Pine11VariantSurfaceStats ropeVertices;
            public Pine11VariantSurfaceStats[] papers;
        }

        // Explicitly selected review only. Each candidate borrows the original
        // shrine rope/paper meshes. Material tints are private copies and no
        // candidate is attached to shipping gameplay or written to disk.
        [UnityTest, Timeout(240000)]
        public IEnumerator TemplePine11WrappedRopeVariantOffscreenReview()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-pine11-wrap-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh review folder");
            Directory.CreateDirectory(output);
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            Call(shell, "RestoreDefaultSettings"); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var tree = world.transform.Find("Pine 11");
            var visual = tree ? tree.Find("Reviewed forest pine") : null;
            var group = visual ? visual.GetComponent<LODGroup>() : null;
            var ropeSource = world.transform.Find("Temple exterior weathering/Intruding shrine cord");
            var sealSource = world.transform.Find("Temple exterior weathering/Intruding shrine seal");
            Assert.That(tree && visual && group && group.lodCount == 3 && ropeSource && sealSource, Is.True);
            var colliderBefore = tree.GetComponent<CapsuleCollider>(); Assert.That(colliderBefore, Is.Not.Null);
            var cordFilter = ropeSource.GetComponent<MeshFilter>(); var cordRenderer = ropeSource.GetComponent<MeshRenderer>();
            Assert.That(cordFilter && cordFilter.sharedMesh && cordFilter.sharedMesh.isReadable && cordRenderer &&
                cordRenderer.sharedMaterial && cordRenderer.sharedMaterial.HasProperty("_BaseColor"), Is.True);
            var originalMaterial = cordRenderer.sharedMaterial;
            var originalBaseColor = originalMaterial.GetColor("_BaseColor");
            var sourceLods = group.GetLODs(); var eyes = Get<Camera>(player, "eyes");
            var flashlight = Get<Light>(player, "flashlight"); var motor = (Behaviour)player;
            var oldPosition = player.transform.position; var oldRotation = player.transform.rotation;
            var oldEyes = eyes.transform.rotation; float oldFov = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldMotor = motor.enabled, oldLight = flashlight.enabled, paused = false;
            GameObject owner = null; var ownedMeshes = new List<Mesh>(); var ownedMaterials = new List<Material>();
            const int width = 1280, height = 720;
            try
            {
                motor.enabled = false; eyes.fieldOfView = 72; eyes.aspect = (float)width / height;
                Call(shell, "Pause"); paused = true; yield return null; Assert.That(Time.timeScale, Is.Zero);
                owner = new GameObject("Diagnostic pine 11 full wrap variants"); owner.layer = 8;
                owner.transform.SetParent(world.transform, false);
                var materials = new Material[3]; materials[0] = originalMaterial;
                materials[1] = new Material(originalMaterial) { name = "Diagnostic rope warmer subtle" }; ownedMaterials.Add(materials[1]);
                materials[1].SetColor("_BaseColor", new Color(.38f, .30f, .23f, 1));
                materials[2] = new Material(originalMaterial) { name = "Diagnostic rope warmer visible" }; ownedMaterials.Add(materials[2]);
                materials[2].SetColor("_BaseColor", new Color(.57f, .43f, .31f, 1));
                var roots = new GameObject[2, 3]; var ropes = new Mesh[2, 3];
                var paths = new Vector3[2, 3][]; var directions = new Vector3[2, 3][];
                var paperObjects = new GameObject[2, 3][]; var paperDirections = new Vector3[2, 3][];
                var fronts = new[] { Pine11NearPoints, Pine11MidPoints };
                var frontNormals = new[] { Pine11NearNormals, Pine11MidNormals };
                var backs = new[] { Pine11NearBackPoints, Pine11MidBackPoints };
                var backNormals = new[] { Pine11NearBackNormals, Pine11MidBackNormals };
                float[] thickness = { 1f, 1.15f, 1.30f }; float[] offsets = { 0f, .02f, .03f };
                string[] labels = { "original-partial", "wrapped-subtle", "wrapped-visible" };
                for (int level = 0; level < 2; level++)
                    for (int variant = 0; variant < 3; variant++)
                    {
                        var fullPoints = variant == 0 ? fronts[level] : fronts[level].Concat(backs[level].Skip(1)).ToArray();
                        var fullNormals = variant == 0 ? frontNormals[level] : frontNormals[level].Concat(backNormals[level].Skip(1)).ToArray();
                        Assert.That(fullPoints.Length, Is.EqualTo(fullNormals.Length));
                        paths[level, variant] = fullPoints.Select((p, i) => p + fullNormals[i] * offsets[variant]).ToArray();
                        directions[level, variant] = fullNormals;
                        var root = new GameObject("Diagnostic " + labels[variant] + " LOD " + level);
                        root.layer = 8; root.transform.SetParent(owner.transform, false); roots[level, variant] = root;
                        var mesh = DeformPine11ExistingCord(cordFilter.sharedMesh, paths[level, variant], thickness[variant], variant != 0);
                        ownedMeshes.Add(mesh); ropes[level, variant] = mesh;
                        var rope = new GameObject("Reused shrine cord"); rope.layer = 8; rope.transform.SetParent(root.transform, false);
                        rope.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var rendered = rope.AddComponent<MeshRenderer>(); rendered.sharedMaterial = materials[variant];
                        rendered.shadowCastingMode = ShadowCastingMode.Off;
                        int[] indices = variant == 0 ? new[] { 7, 22 } : new[] { 7, 22, fronts[level].Length + 24 };
                        paperObjects[level, variant] = new GameObject[indices.Length]; paperDirections[level, variant] = new Vector3[indices.Length];
                        for (int i = 0; i < indices.Length; i++)
                        {
                            int index = indices[i]; var n = fullNormals[index];
                            var paper = Object.Instantiate(sealSource.gameObject, root.transform, false); paper.name = "Existing shrine paper " + i;
                            paper.transform.localPosition = paths[level, variant][index] + n * (variant == 0 ? .025f : .01f);
                            paper.transform.localRotation = Quaternion.LookRotation(-n, Vector3.up);
                            paper.transform.localScale = new Vector3(i == 0 ? .18f : .16f, i == 0 ? .40f : .34f, 1);
                            foreach (var renderer in paper.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
                            paperObjects[level, variant][i] = paper; paperDirections[level, variant][i] = n;
                        }
                        root.SetActive(false);
                    }
                Assert.That(owner.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(owner.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(owner.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                for (int level = 0; level < 2; level++)
                {
                    var filter = sourceLods[level].renderers[0].GetComponent<MeshFilter>(); Assert.That(filter && filter.sharedMesh, Is.True);
                    group.ForceLOD(level);
                    for (int variant = 0; variant < 3; variant++)
                    {
                        var centre = ProbePine11Candidate(world.transform, filter, paths[level, variant], directions[level, variant], level);
                        var contact = ProbePine11VariantMesh(world.transform, filter, cordFilter.sharedMesh,
                            ropes[level, variant], paths[level, variant], directions[level, variant],
                            paperObjects[level, variant], paperDirections[level, variant]);
                        var report = new Pine11VariantContactStats { lod = level == 0 ? "near" : "middle", variant = labels[variant],
                            centreline = centre, ropeVertices = contact.Item1, papers = contact.Item2,
                            note = "Isolated imported tree collider, normal rays. Backfaces or missing hits are not successes. Paper may legitimately hang away; inspect negatives, missed rays, views and thickness together." };
                        File.WriteAllText(Path.Combine(output, "lod" + level + "-" + labels[variant] + "-contact.json"), JsonUtility.ToJson(report, true));
                    }
                    Vector3[] feet = { new Vector3(0, .04f, 3.5f), new Vector3(6f, .04f, 5.5f), new Vector3(5.5f, .04f, 9.5f), new Vector3(8.7f, .04f, 12.5f) };
                    Vector3[] targets = { new Vector3(0, 1.9f, 9), new Vector3(7.2f, 2.8f, 9.9f), new Vector3(7.2f, 2.8f, 9.9f), new Vector3(7.5f, 2.9f, 10.4f) };
                    string[] names = { "front", "side", "close-light", "back" };
                    for (int view = 0; view < feet.Length; view++)
                    {
                        Call(player, "RelocateWorld", world.transform.TransformPoint(feet[view]), Quaternion.identity);
                        eyes.transform.rotation = Quaternion.LookRotation(world.transform.TransformPoint(targets[view]) - eyes.transform.position);
                        flashlight.enabled = view == 2; Physics.SyncTransforms(); yield return null;
                        eyes.transform.rotation = Quaternion.LookRotation(world.transform.TransformPoint(targets[view]) - eyes.transform.position);
                        if (view == 2) Assert.That(flashlight.isActiveAndEnabled, Is.True);
                        var baseline = CapturePine11Variant(eyes, output, "lod" + level + "-" + names[view] + "-baseline.png");
                        for (int variant = 0; variant < 3; variant++)
                        {
                            roots[level, variant].SetActive(true);
                            var frame = CapturePine11Variant(eyes, output, "lod" + level + "-" + names[view] + "-" + labels[variant] + ".png");
                            var bounds = CandleReviewBounds(eyes, roots[level, variant].GetComponentsInChildren<Renderer>(true), width, height);
                            var color = materials[variant].GetColor("_BaseColor");
                            var metrics = new Pine11VariantViewStats { lod = level == 0 ? "near" : "middle", view = names[view], variant = labels[variant],
                                flashlightEnabled = flashlight.isActiveAndEnabled, baseRed = color.r, baseGreen = color.g, baseBlue = color.b,
                                widthMultiplier = thickness[variant], offsetMetres = offsets[variant],
                                targetRegion = CandleReviewRegionMetrics("rope and paper bounds", baseline, frame, width, bounds),
                                note = "Target rectangle includes background and may be clipped by the viewport; clipping counts are not proof of visual quality or HDR light contribution." };
                            File.WriteAllText(Path.Combine(output, "lod" + level + "-" + names[view] + "-" + labels[variant] + "-visual.json"), JsonUtility.ToJson(metrics, true));
                            roots[level, variant].SetActive(false);
                        }
                        var restored = CapturePine11Variant(eyes, output, "lod" + level + "-" + names[view] + "-restored.png");
                        var full = new RectInt(0, 0, width, height);
                        var stability = CandleReviewRegionMetrics("restored frame", baseline, restored, width, full);
                        File.WriteAllText(Path.Combine(output, "lod" + level + "-" + names[view] + "-stability.json"), JsonUtility.ToJson(stability, true));
                    }
                }
                Assert.That(tree.GetComponent<CapsuleCollider>(), Is.SameAs(colliderBefore));
                Assert.That(cordRenderer.sharedMaterial, Is.SameAs(originalMaterial));
                Assert.That(originalMaterial.GetColor("_BaseColor"), Is.EqualTo(originalBaseColor));
            }
            finally
            {
                if (group) group.ForceLOD(-1);
                if (owner) Object.DestroyImmediate(owner);
                foreach (var mesh in ownedMeshes) if (mesh) Object.DestroyImmediate(mesh);
                foreach (var material in ownedMaterials) if (material) Object.DestroyImmediate(material);
                eyes.fieldOfView = oldFov; eyes.aspect = oldAspect;
                Call(player, "RelocateWorld", oldPosition, oldRotation); eyes.transform.rotation = oldEyes;
                flashlight.enabled = oldLight; motor.enabled = oldMotor;
                if (paused) Call(shell, "Resume"); Physics.SyncTransforms();
            }
        }

        static Color32[] CapturePine11Variant(Camera eyes, string output, string filename)
        {
            var frame = SchoolHorrorFrame(eyes, 1280, 720);
            try { var pixels = frame.GetPixels32(); File.WriteAllBytes(Path.Combine(output, filename), frame.EncodeToPNG()); return pixels; }
            finally { Object.Destroy(frame); }
        }

        static Vector3 Pine11VariantNormal(Vector3[] path, Vector3[] normals, float t)
        {
            float total = 0; for (int i = 1; i < path.Length; i++) total += Vector3.Distance(path[i-1], path[i]);
            float distance = Mathf.Clamp01(t) * total;
            for (int i = 1; i < path.Length; i++)
            {
                float length = Vector3.Distance(path[i-1], path[i]);
                if (distance <= length || i == path.Length-1)
                    return Vector3.Slerp(normals[i-1], normals[i], length > .00001f ? distance / length : 0).normalized;
                distance -= length;
            }
            return normals[normals.Length-1];
        }

        static Tuple<Pine11VariantSurfaceStats, Pine11VariantSurfaceStats[]> ProbePine11VariantMesh(
            Transform world, MeshFilter tree, Mesh sourceCord, Mesh warpedCord, Vector3[] path, Vector3[] normals,
            GameObject[] papers, Vector3[] paperNormals)
        {
            var scene = SceneManager.CreateScene("Pine 11 full wrap probe " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var probe = new GameObject("Isolated temporary imported tree collider"); SceneManager.MoveGameObjectToScene(probe, scene);
            try
            {
                var collider = probe.AddComponent<MeshCollider>(); collider.sharedMesh = tree.sharedMesh;
                var toMesh = tree.transform.worldToLocalMatrix * world.localToWorldMatrix;
                var toWorld = world.worldToLocalMatrix * tree.transform.localToWorldMatrix;
                Pine11VariantSurfaceStats NewStats(string part) => new Pine11VariantSurfaceStats { part = part,
                    minGapMetres = float.PositiveInfinity, maxGapMetres = float.NegativeInfinity,
                    note = "Normal-ray samples on imported triangles, not full physical containment; missed rays are recorded separately. Paper may hang freely; views and attachment points also require review." };
                void Sample(Pine11VariantSurfaceStats stats, Vector3 p, Vector3 n)
                {
                    n.Normalize(); var start = toMesh.MultiplyPoint3x4(p + n * .5f); var end = toMesh.MultiplyPoint3x4(p - n * .5f);
                    var ray = end - start; stats.samples++;
                    if (!collider.Raycast(new Ray(start, ray.normalized), out var hit, ray.magnitude)) { stats.misses++; return; }
                    float gap = Vector3.Dot(p - toWorld.MultiplyPoint3x4(hit.point), n);
                    stats.hits++; stats.minGapMetres = Mathf.Min(stats.minGapMetres, gap); stats.maxGapMetres = Mathf.Max(stats.maxGapMetres, gap);
                    if (gap < 0) stats.negative++; if (gap < .005f) stats.underFiveMillimetres++; if (gap > .08f) stats.overEightCentimetres++;
                }
                void Finish(Pine11VariantSurfaceStats stats) { if (stats.hits == 0) { stats.minGapMetres = 0; stats.maxGapMetres = 0; } }
                var rope = NewStats("rope vertices"); var original = sourceCord.vertices; var actual = warpedCord.vertices;
                Assert.That(original.Length, Is.EqualTo(actual.Length)); var bounds = sourceCord.bounds;
                int stride = Math.Max(1, original.Length / 2048);
                for (int i = 0; i < original.Length; i += stride)
                {
                    float t = Mathf.Clamp01((original[i].x - bounds.min.x) / bounds.size.x);
                    Sample(rope, actual[i], Pine11VariantNormal(path, normals, t));
                }
                Finish(rope); var result = new Pine11VariantSurfaceStats[papers.Length];
                for (int i = 0; i < papers.Length; i++)
                {
                    var stats = NewStats("paper " + i);
                    foreach (var filter in papers[i].GetComponentsInChildren<MeshFilter>(true))
                    {
                        Assert.That(filter.sharedMesh && filter.sharedMesh.isReadable, Is.True);
                        var points = filter.sharedMesh.vertices;
                        var toPrologue = world.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        int paperStride = Math.Max(1, points.Length / 256);
                        for (int n = 0; n < points.Length; n += paperStride)
                            Sample(stats, toPrologue.MultiplyPoint3x4(points[n]), paperNormals[i]);
                    }
                    Finish(stats); result[i] = stats;
                }
                return Tuple.Create(rope, result);
            }
            finally { probe.SetActive(false); Object.DestroyImmediate(probe); SceneManager.UnloadSceneAsync(scene); }
        }
    }
}
