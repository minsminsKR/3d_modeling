using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Presentation only. A preserved authored sheet supplies the entire paper
    // geometry; the two opt-in ink states share a decal projected to its top.
    [DisallowMultipleComponent]
    public sealed class TempleStoryEvidence : MonoBehaviour
    {
        const int InkWidth = 256, InkHeight = 384;
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        Transform paper;
        GameObject inkObject;
        Mesh inkMesh;
        Material inkMaterial;
        Texture2D blankInk, houseInk;
        bool prepared;
        public bool SecondDay { get; private set; }
        public Transform Paper => paper;
        public Renderer InkRenderer => inkObject ? inkObject.GetComponent<Renderer>() : null;
        public Texture2D InkTexture => SecondDay ? houseInk : blankInk;

        public void Prepare()
        {
            if (prepared || paper) throw new InvalidOperationException("A fresh temple evidence sheet is required");
            var shader = Resources.Load<Shader>("SchoolCleaningBlood/BloodDecal");
            if (!shader) throw new InvalidOperationException("The retained transparent ink shader is missing");
            try
            {
                paper = CorridorDetailLibrary.Attach("exercise-leaf", transform, ResolvePaper);
                paper.name = "Authored temple evidence sheet";
                // Keep a complete 3D paper, its bends and its PBR surface. Its
                // envelope avoids the central incense vessel and both candles.
                GraphicsPropLibrary.Fit(paper, new Bounds(new Vector3(.65f, .850f, 7.46f), new Vector3(.20f, .009f, .25f)));
                foreach (var renderer in paper.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                inkMesh = ProjectPaperTop(paper);
                blankInk = MakeInk(false); houseInk = MakeInk(true);
                inkMaterial = new Material(shader) { name = "Owned quiet ink on temple sheet", renderQueue = (int)RenderQueue.Transparent };
                inkMaterial.SetColor("_BaseColor", new Color(.10f, .075f, .06f, .86f));
                inkObject = new GameObject("Ink on authored evidence paper"); inkObject.layer = 8;
                inkObject.transform.SetParent(transform, false);
                inkObject.AddComponent<MeshFilter>().sharedMesh = inkMesh;
                var inkRenderer = inkObject.AddComponent<MeshRenderer>();
                inkRenderer.sharedMaterial = inkMaterial;
                inkRenderer.shadowCastingMode = ShadowCastingMode.Off; inkRenderer.receiveShadows = false;
                inkRenderer.lightProbeUsage = LightProbeUsage.Off; inkRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                prepared = true;
                SetSecondDay(false);
            }
            catch { Release(); throw; }
        }

        Material ResolvePaper(string slot)
        {
            if (slot != "CD_paper") throw new InvalidOperationException("Evidence sheet contains an unexpected material slot");
            return surfaces.Get("paper-aged", new Color(.64f, .59f, .49f));
        }

        Mesh ProjectPaperTop(Transform source)
        {
            var positions = new List<Vector3>();
            var top = new List<int>();
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var sourceMesh = filter.sharedMesh;
                if (!sourceMesh || !sourceMesh.isReadable) throw new InvalidOperationException("Readable authored paper is required");
                var vertices = sourceMesh.vertices;
                var triangles = sourceMesh.triangles;
                for (int k = 0; k + 2 < triangles.Length; k += 3)
                {
                    Vector3 a = transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[k]]));
                    Vector3 b = transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[k + 1]]));
                    Vector3 c = transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[k + 2]]));
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 1e-12f || normal.normalized.y < .65f) continue;
                    int start = positions.Count;
                    positions.Add(a + Vector3.up * .0015f); positions.Add(b + Vector3.up * .0015f); positions.Add(c + Vector3.up * .0015f);
                    top.Add(start); top.Add(start + 1); top.Add(start + 2);
                }
            }
            if (positions.Count < 3) throw new InvalidOperationException("The authored sheet has no upward surface for ink");
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (var p in positions) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            if (maxX - minX < .1f || maxZ - minZ < .1f) throw new InvalidOperationException("The paper ink area is too small");
            var uv = new Vector2[positions.Count];
            for (int i = 0; i < positions.Count; i++) uv[i] = new Vector2(Mathf.InverseLerp(minX, maxX, positions[i].x), Mathf.InverseLerp(minZ, maxZ, positions[i].z));
            var result = new Mesh { name = "Owned ink following authored paper surface" };
            result.SetVertices(positions); result.SetUVs(0, uv); result.SetTriangles(top, 0); result.RecalculateBounds();
            return result;
        }

        static Texture2D MakeInk(bool house)
        {
            var pixels = new Color32[InkWidth * InkHeight];
            // A blank field stays legible as a mark, without baking Korean
            // typography into the model; the reading view owns the text.
            Stroke(pixels, 40, 315, 215, 315, 1.5f, 1);
            Stroke(pixels, 40, 314, 40, 337, 1.3f, 2);
            Stroke(pixels, 215, 314, 215, 337, 1.3f, 3);
            // Subtle irregular pen traces suggest sentences without pretending
            // to render tiny Korean glyphs. The reading UI carries the actual
            // text; these traces avoid both the blank field and house image.
            Handwriting(pixels);
            if (house)
            {
                // Uneven, overlapping pencil strokes form a small child's
                // house; it appears only on the second day.
                Stroke(pixels, 74, 143, 78, 228, 2.1f, 4);
                Stroke(pixels, 78, 228, 129, 267, 2.1f, 5);
                Stroke(pixels, 129, 267, 181, 226, 2.2f, 6);
                Stroke(pixels, 181, 226, 184, 142, 2.0f, 7);
                Stroke(pixels, 74, 143, 184, 142, 1.9f, 8);
                Stroke(pixels, 115, 143, 115, 191, 1.8f, 9);
                Stroke(pixels, 115, 191, 143, 189, 1.8f, 10);
                Stroke(pixels, 143, 189, 143, 143, 1.9f, 11);
                Stroke(pixels, 83, 218, 128, 253, 1.0f, 12);
                Stroke(pixels, 128, 253, 174, 220, 1.0f, 13);
                Stroke(pixels, 156, 208, 175, 209, 1.3f, 14);
                Stroke(pixels, 175, 209, 175, 191, 1.3f, 15);
                Stroke(pixels, 175, 191, 156, 191, 1.3f, 16);
                Stroke(pixels, 156, 191, 156, 208, 1.3f, 17);
            }
            var texture = new Texture2D(InkWidth, InkHeight, TextureFormat.RGBA32, false) {
                name = house ? "Owned child's house ink" : "Owned blank name field ink",
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        static void Handwriting(Color32[] pixels)
        {
            float[] rows = { 298f, 280f, 116f, 96f, 76f };
            for (int line = 0; line < rows.Length; line++)
            {
                float cursor = 42f + (line % 2) * 4f;
                int groups = line == rows.Length - 1 ? 7 : 11;
                for (int j = 0; j < groups; j++)
                {
                    int seed = 101 + line * 29 + j * 7;
                    float baseline = rows[line] + .7f * Mathf.Sin(j * 1.7f + line);
                    float width = 7f + (j * 3 + line) % 5;
                    Stroke(pixels, cursor, baseline, cursor + width, baseline + 1.2f * Mathf.Sin(seed), .65f, seed, .38f);
                    Stroke(pixels, cursor + width * .25f, baseline + 2f, cursor + width * .67f,
                        baseline + 3.5f + (j % 2), .52f, seed + 1, .30f);
                    if ((j + line) % 3 == 1)
                        Stroke(pixels, cursor + width * .42f, baseline - .8f, cursor + width * .55f,
                            baseline + 4.2f, .45f, seed + 2, .25f);
                    cursor += width + 4f + (j + line) % 3;
                }
            }
        }

        static void Stroke(Color32[] pixels, float x0, float y0, float x1, float y1, float radius, int seed, float opacity = 1f)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2);
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)Mathf.Max(1, steps);
                float x = Mathf.Lerp(x0, x1, t) + .38f * Mathf.Sin(k * .27f + seed);
                float y = Mathf.Lerp(y0, y1, t) + .34f * Mathf.Sin(k * .19f + seed * 2);
                int left = Mathf.Max(0, Mathf.FloorToInt(x - radius - 1)); int right = Mathf.Min(InkWidth - 1, Mathf.CeilToInt(x + radius + 1));
                int bottom = Mathf.Max(0, Mathf.FloorToInt(y - radius - 1)); int top = Mathf.Min(InkHeight - 1, Mathf.CeilToInt(y + radius + 1));
                for (int py = bottom; py <= top; py++) for (int px = left; px <= right; px++)
                {
                    float distance = Vector2.Distance(new Vector2(px + .5f, py + .5f), new Vector2(x, y));
                    byte alpha = (byte)Mathf.RoundToInt(205 * opacity * Mathf.Clamp01(radius + .8f - distance));
                    int index = py * InkWidth + px;
                    if (alpha > pixels[index].a) pixels[index] = new Color32(255, 255, 255, alpha);
                }
            }
        }

        public void SetSecondDay(bool secondDay)
        {
            if (!prepared || !inkMaterial) throw new InvalidOperationException("Prepare the evidence before changing its day");
            SecondDay = secondDay;
            inkMaterial.SetTexture("_BaseMap", secondDay ? houseInk : blankInk);
        }

        void Release()
        {
            if (paper) GraphicsSurfaceLibrary.DestroyOwned(paper.gameObject);
            if (inkObject) GraphicsSurfaceLibrary.DestroyOwned(inkObject);
            GraphicsSurfaceLibrary.DestroyOwned(inkMesh); GraphicsSurfaceLibrary.DestroyOwned(inkMaterial);
            GraphicsSurfaceLibrary.DestroyOwned(blankInk); GraphicsSurfaceLibrary.DestroyOwned(houseInk);
            paper = null; inkObject = null; inkMesh = null; inkMaterial = null; blankInk = null; houseInk = null;
            surfaces.Dispose(); prepared = false;
        }
        void OnDestroy() => Release();
    }
}
