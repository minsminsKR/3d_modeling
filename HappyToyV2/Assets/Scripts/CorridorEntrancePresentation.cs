using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>Owned visual assembly of the webapp-generated gate and cave rock.
    /// Chamber-local metres; the chamber owns its physical boundary and navigation.</summary>
    [DisallowMultipleComponent]
    public sealed class CorridorEntrancePresentation : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<Light> lanterns = new List<Light>();
        Transform root;
        CorridorEntranceChamber physicsOwner;
        public Transform TempleGateRoot { get; private set; }
        public int RockInstances { get; private set; }
        public IReadOnlyList<Light> Lanterns => lanterns;

        public static CorridorEntrancePresentation Attach(CorridorEntranceChamber chamber)
        {
            if (!chamber || !chamber.VisualRoot || !chamber.Gate)
                throw new ArgumentException("Prepared entrance chamber required");
            if (chamber.TryGetComponent<CorridorEntrancePresentation>(out var existing)) return existing;
            var presentation = chamber.gameObject.AddComponent<CorridorEntrancePresentation>();
            presentation.Build(chamber);
            return presentation;
        }

        void Build(CorridorEntranceChamber chamber)
        {
            root = chamber.VisualRoot;
            physicsOwner = chamber;
            BuildCavern();
            TempleGateRoot = Model("temple-gate", "Monumental sealed Korean temple gate", root,
                root.InverseTransformPoint(chamber.GatePosition), Quaternion.identity);
            // This measured door plane is refined from the exported front surface,
            // independently of the overhanging tiled roof's much wider envelope.
            chamber.ConfigureGateBody(new Vector3(0, 3.9f, 1.70f), new Vector3(9.2f, 7.8f, .55f));
            chamber.ConfigureGateGeometry(TempleGateRoot);
            BuildApproach();
            BuildLight();
        }

        Transform Model(string key, string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var source = Resources.Load<GameObject>("CorridorEntrance/" + key + "-visual");
            if (!source) throw new InvalidOperationException("Generated entrance visual prefab missing: " + key);
            if (source.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Entrance visual prefab unexpectedly owns physical geometry: " + key);
            var holder = Group(parent, name, position, rotation);
            var instance = Instantiate(source, holder, false);
            foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
            owned.Add(instance);
            return holder;
        }

        void Rock(string name, Vector3 centre, Vector3 size, Quaternion rotation)
        {
            var holder = Model("cave-rock", name, root, centre, rotation);
            var visual = holder.GetChild(0);
            var bounds = GraphicsPropLibrary.LocalBounds(visual);
            var scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            visual.localScale = scale;
            visual.localPosition = -Vector3.Scale(bounds.center, scale);
            physicsOwner.AddRockGeometry(holder);
            RockInstances++;
        }

        void BuildCavern()
        {
            // Generated rocks have natural silhouettes. A continuous stone skin
            // behind them closes their joints and excludes the outdoor sky.
            var backing = surfaces.Get("concrete-rough", new Color(.24f, .26f, .25f));
            Surface("Continuous cave rear stone", new Vector3(0, 6, -15.1f), new Vector3(28.4f, 12.4f, .4f), backing);
            Surface("Continuous cave vault stone", new Vector3(0, 12.15f, 0), new Vector3(28.4f, .3f, 30.4f), backing);
            foreach (int side in new[] { -1, 1 })
            {
                Surface("Continuous cave side stone", new Vector3(side * 14.1f, 6, 0), new Vector3(.4f, 12.4f, 30.4f), backing);
                Surface("Continuous cave portal shoulder", new Vector3(side * 8.2f, 6, 15), new Vector3(11.6f, 12.4f, .22f), backing);
                Surface("Continuous cave passage side", new Vector3(side * 2.55f, 1.9f, 17), new Vector3(.3f, 3.8f, 4.1f), backing);
            }
            Surface("Continuous cave portal crown", new Vector3(0, 7.9f, 15), new Vector3(4.8f, 8.2f, .22f), backing);
            Surface("Continuous cave passage roof", new Vector3(0, 3.95f, 17), new Vector3(4.8f, .3f, 4), backing);
            // Rock fronts project through the continuous backing. The lowest LOD
            // supplies matching static collision, while central routes stay clear.
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 3; row++)
                    Rock("Fractured cavern side wall", new Vector3(side * 14.35f, 6.05f, -10 + row * 10),
                        new Vector3(11.2f, 12.3f, 4.1f), Quaternion.Euler(0, -side * 90, 0));
            for (int side = -1; side <= 1; side += 2)
            {
                Rock("Cavern wall behind sealed sanctuary", new Vector3(side * 7.2f, 6.05f, -15.59f),
                    new Vector3(15, 12.3f, 4.5f), Quaternion.identity);
                Rock("Cavern shoulder beside the onward passage", new Vector3(side * 8.3f, 6.05f, 16.04f),
                    new Vector3(11.6f, 12.3f, 4.3f), Quaternion.Euler(0, 180, 0));
            }
            Rock("High cave arch over the corridor mouth", new Vector3(0, 8.15f, 16.04f),
                new Vector3(6.2f, 8.8f, 4.3f), Quaternion.Euler(0, 180, 0));
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
                Rock("Interlocking natural cavern vault", new Vector3(x * 9.5f, 12.75f, z * 10),
                    new Vector3(11.3f, 12, 3.1f), Quaternion.Euler(90, (x + z) * 5, 0));
            // The short link reaches the real cell-zero opening, rather than a
            // decorative black wall that hides an inaccessible corridor.
            for (int side = -1; side <= 1; side += 2)
                Rock("Low rock passage flank", new Vector3(side * 2.9f, 1.55f, 17.1f),
                    new Vector3(4.6f, 3.2f, 1.9f), Quaternion.Euler(0, -side * 90, 0));
            Rock("Low rock passage ceiling", new Vector3(0, 4.45f, 17.1f),
                new Vector3(4.8f, 4.7f, 1.4f), Quaternion.Euler(90, 0, 0));
        }

        void BuildApproach()
        {
            var stone = surfaces.Get("concrete-rough", new Color(.47f, .49f, .47f));
            Surface("Damp stone cavern ground", new Vector3(0, -.014f, 0), new Vector3(28, .03f, 30), stone);
            Surface("Stone floor through the rock passage", new Vector3(0, -.014f, 17), new Vector3(4.8f, .03f, 4), stone);
            var path = surfaces.Get("concrete-rough", new Color(.66f, .65f, .59f));
            for (int row = 0; row < 14; row++)
                Surface("Worn sanctuary approach slab", new Vector3((row % 3 - 1) * .018f, .007f, -10.6f + row * 1.77f),
                    new Vector3(3.45f, .014f, 1.72f), path);
            foreach (int side in new[] { -1, 1 })
            {
                var source = Resources.Load<GameObject>("KoreanCorridorProps/jangseung");
                if (!source) throw new InvalidOperationException("Generated guardian post missing");
                // Guardian posts sit inside the rock perimeter, well outside all
                // navigable approaches. These existing source-derived carvings are
                // enlarged only as visual landmarks and never narrow the route.
                var guardian = Instantiate(source, root, false);
                guardian.name = "Weathered guardian in the cavern wall";
                guardian.transform.localPosition = new Vector3(side * 13.95f, 0, -8.3f);
                guardian.transform.localRotation = Quaternion.Euler(0, -side * 19, 0);
                guardian.transform.localScale = Vector3.one * 1.8f;
                foreach (var child in guardian.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
            }
        }

        void BuildLight()
        {
            // Local lighting only: no global fog/exposure changes leak into the
            // maze, title or the restored school. The roof remains a looming silhouette.
            Lamp("Cavern cold vault bounce", new Vector3(0, 9.9f, 2.4f), new Color(.27f, .38f, .46f), 3.8f, 31, false);
            Lamp("Sealed gate dim reflected glow", new Vector3(0, 5.8f, -8.8f), new Color(.64f, .62f, .53f), 4.0f, 18, false);
            foreach (int side in new[] { -1, 1 })
            {
                Lantern(new Vector3(side * 6.35f, 3.65f, -10.8f), 11.5f, 3.2f);
                Lantern(new Vector3(side * 3.8f, 2.65f, 11.8f), 8.4f, 2.7f);
                Lamp("Rock face grazing ember", new Vector3(side * 11.8f, 1.0f, -3.1f), new Color(.72f, .30f, .12f), 1.4f, 9, false);
            }
        }

        void Lantern(Vector3 position, float range, float intensity)
        {
            var holder = Group(root, "Suspended temple paper lantern", position, Quaternion.identity);
            holder.localScale = Vector3.one * 1.45f;
            var paper = surfaces.Get("paper-aged", new Color(.63f, .38f, .22f), new Color(.57f, .23f, .055f));
            GraphicsPropLibrary.Attach("paper-lantern", holder,
                slot => slot == "GU_washi" ? paper : surfaces.Resolve(slot), owned);
            foreach (var renderer in holder.GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            var chain = surfaces.Get("metal-rust", new Color(.31f, .29f, .24f));
            float length = 11.75f - position.y;
            Surface("Lantern suspension into the cave vault", position + Vector3.up * (length * .5f),
                new Vector3(.022f, length, .022f), chain);
            Lamp("Cavern temple lantern", position, new Color(1f, .65f, .35f), intensity, range, true);
        }

        Light Lamp(string name, Vector3 position, Color colour, float intensity, float range, bool shadow)
        {
            var lamp = Group(root, name, position, Quaternion.identity).gameObject.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.color = colour; lamp.intensity = intensity; lamp.range = range;
            lamp.shadows = shadow ? LightShadows.Soft : LightShadows.None;
            lanterns.Add(lamp);
            return lamp;
        }

        void Surface(string name, Vector3 centre, Vector3 size, Material material)
        {
            var holder = Group(root, name, centre, Quaternion.identity);
            holder.localScale = size;
            holder.gameObject.AddComponent<MeshFilter>().sharedMesh = surfaces.MetreBoxMesh(size, .004f, GraphicsSurfaceLibrary.TileSpan(material));
            var renderer = holder.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.receiveShadows = true;
        }

        static Transform Group(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var result = new GameObject(name).transform; result.SetParent(parent, false);
            result.localPosition = position; result.localRotation = rotation; result.gameObject.layer = 8;
            return result;
        }

        void OnDestroy()
        {
            surfaces.Dispose();
            foreach (var resource in owned) GraphicsSurfaceLibrary.DestroyOwned(resource);
        }
    }
}
