using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    /// <summary>
    /// Bounded presentation repairs for the existing annex, not a scene generator.
    /// Furniture silhouettes stay within the authored occupied areas. New trim has
    /// no collision. One midpoint seat row copies existing physical furniture and
    /// carves its small footprints from the existing NavMesh; no scene is regenerated.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnnexRoomPresentation : MonoBehaviour
    {
        public bool Applied { get; private set; }
        public int ChairCount { get; private set; }
        public int AddedSeatCount { get; private set; }
        public int AddedColliderCount { get; private set; }
        public int CarvedObstacleCount { get; private set; }
        public int WhiteKeyCount { get; private set; }
        public int BlackKeyCount { get; private set; }
        public int MountedSignCount { get; private set; }
        public int InitialColliderCount { get; private set; }
        public int FinalColliderCount { get; private set; }
        public string Failure { get; private set; } = string.Empty;

        const string ScenePath = "Assets/Annex/SchoolAnnex.unity";
        Transform refresh;
        Transform[] originals;
        Material wood, darkWood, iron, ivory, graphite, brass, paper;
        readonly GraphicsSurfaceLibrary.Pool graphicsSurfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<FittedText> fitted = new List<FittedText>();
        int fitFrames;

        struct FittedText
        {
            public TextMesh text;
            public Vector2 limit;
        }

        void Start()
        {
            if (gameObject.scene.path != ScenePath) return;
            try { Apply(); }
            catch (Exception error)
            {
                Failure = error.Message;
                Debug.LogError("HappyToy annex presentation failed: " + error, this);
            }
        }

        // Public so a repeat-initialization regression can verify idempotence.
        public void Apply()
        {
            if (Applied || refresh || gameObject.scene.path != ScenePath) return;
            originals = gameObject.scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<Transform>(true)).ToArray();
            var school = Exactly("School — authored V2 first floor");
            var piano = Exactly("Music upright piano");
            var bench = Exactly("Piano bench");
            var board = Exactly("Choir rehearsal board");
            RequirePosition(piano, new Vector3(23.4f, .75f, 13.4f));
            RequirePosition(bench, new Vector3(23.4f, .4f, 12.25f));
            RequirePosition(board, new Vector3(28.06f, 1.8f, 11.1f));
            var chairs = originals.Where(item => item.name == "Abandoned choir chair").ToArray();
            var stands = originals.Where(item => item.name == "Choir music stand").ToArray();
            if (chairs.Length != 4 || stands.Length != 4)
                throw new InvalidOperationException("Expected the four authored choir seats and stands.");
            var labels = new[] { "MUSIC", "ARCHIVE", "ANNEX / EAST WING", "NURSERY", "2층 · 액자실", "지하 · 침수된 인형방" }
                .Select(name => Exactly(name).GetComponent<TextMesh>()).ToArray();
            if (labels.Any(label => !label)) throw new InvalidOperationException("Expected the six annex TextMesh signs.");
            InitialColliderCount = CountEnabledColliders();
            MakeMaterials();
            refresh = new GameObject("Annex presentation refresh").transform;
            refresh.SetParent(school, false);
            RemoveDetachedClassroomLabel();
            DressPiano(piano, bench);
            var addedChairs = new List<Transform>();
            var addedStands = new List<Transform>();
            AddMiddleRow(chairs[0], stands[0], addedChairs, addedStands);
            foreach (var chair in chairs) DressChair(chair);
            foreach (var stand in stands) DressStand(stand);
            foreach (var chair in addedChairs) DressChair(chair);
            foreach (var stand in addedStands) DressStand(stand);
            DressRoster();
            MakePercussionRack();

            // This board formerly overhung the open east passage by 1.25 metres.
            // The existing east wall spans z=11.2..14.4; the moved frame fits inside it.
            board.position = new Vector3(28.06f, 1.8f, 12.8f);
            var boardText = board.GetComponentsInChildren<TextMesh>(true).Single();
            FitLater(boardText, new Vector2(1.97f, .86f));
            MountedSign(labels[0], labels[0].transform.position, 0, new Vector2(1.55f, .40f));
            MountedSign(labels[1], labels[1].transform.position, 0, new Vector2(1.55f, .40f));
            MountedSign(labels[2], labels[2].transform.position, 0, new Vector2(2.20f, .40f));
            MountedSign(labels[3], new Vector3(42.6f, 2.4f, 4.675f), 0, new Vector2(2.40f, .42f));
            MountedSign(labels[4], new Vector3(33, 2.5f, 11.075f), 0, new Vector2(2.70f, .42f));
            MountedSign(labels[5], new Vector3(17, 2.5f, -11.075f), 180, new Vector2(2.70f, .42f));
            FinalColliderCount = CountEnabledColliders();
            if (FinalColliderCount != InitialColliderCount + AddedColliderCount || AddedColliderCount != 6)
                throw new InvalidOperationException("Presentation changed collision outside the two added seats and stands.");
            Font.textureRebuilt += FontRebuilt;
            fitFrames = 3;
            Applied = true;
            var surfaces=GetComponent<GraphicsSchoolSurfaces>();
            if(!surfaces)surfaces=gameObject.AddComponent<GraphicsSchoolSurfaces>();
            surfaces.Prepare(GetComponent<GameSession>());
        }

        Transform Exactly(string name)
        {
            var matches = originals.Where(item => item.name == name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected one named presentation target: " + name);
            return matches[0];
        }

        static void RequirePosition(Transform item, Vector3 expected)
        {
            if (Vector3.Distance(item.position, expected) > .06f)
                throw new InvalidOperationException("Authored presentation target moved: " + item.name);
        }

        void RemoveDetachedClassroomLabel()
        {
            // The old reverse face is a lone text plane beyond the classroom lintel.
            // Opening 1-2's door exposes it hanging inside the room. Keep the backed
            // corridor sign and all door/collision geometry; remove only this face.
            var interior = Exactly("CLASSROOM sign interior");
            RequirePosition(interior, new Vector3(-4.5f, 2.7f, 1.74f));
            var renderer = interior.GetComponent<MeshRenderer>();
            if (!renderer || !interior.GetComponent<TextMesh>())
                throw new InvalidOperationException("Expected the detached classroom TextMesh face.");
            renderer.enabled = false;
        }

        int CountEnabledColliders() => gameObject.scene.GetRootGameObjects()
            .SelectMany(item => item.GetComponentsInChildren<Collider>(true))
            .Count(item => item.enabled && item.gameObject.activeInHierarchy);

        void MakeMaterials()
        {
            wood = Material("Music warm plywood", new Color(.29f, .17f, .085f));
            darkWood = Material("Music dark cabinet", new Color(.072f, .044f, .025f));
            iron = Material("Music enamel steel", new Color(.12f, .16f, .15f), .25f);
            ivory = Material("Music worn ivory", new Color(.77f, .73f, .60f));
            graphite = Material("Music graphite", new Color(.018f, .024f, .022f));
            brass = Material("Music aged brass", new Color(.47f, .34f, .12f), .48f);
            paper = Material("Music score paper", new Color(.66f, .62f, .47f));
        }

        Material Material(string name,Color color,float metallic=0)
        {
            string key=name.Contains("brass")?"brass-tarnished":name.Contains("steel")?"painted-metal":
                name.Contains("ivory")?"wax-tallow":name.Contains("graphite")?"cloth-charred":
                name.Contains("paper")?"paper-aged":"wood-aged";
            Color tint=key=="wood-aged"?(name.Contains("dark")?new Color(.61f,.57f,.48f):new Color(.97f,.94f,.86f)):
                key=="paper-aged"?new Color(.97f,.94f,.86f):key=="brass-tarnished"||key=="wax-tallow"?Color.white:color;
            var material=graphicsSurfaces.Get(key,tint);material.name=name+" — measured PBR";return material;
        }

        Transform Group(string name, Vector3 at, Quaternion rotation)
        {
            var group = new GameObject(name).transform;
            group.SetParent(refresh, false);
            group.SetPositionAndRotation(at, rotation);
            return group;
        }

        GameObject Part(Transform parent, string name, Vector3 at, Vector3 size, Material material,
            PrimitiveType type = PrimitiveType.Cube, Quaternion? rotation = null)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localRotation = rotation ?? Quaternion.identity;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if(type==PrimitiveType.Cube)
            {
                float bevel=Mathf.Min(.003f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.12f);
                part.GetComponent<MeshFilter>().sharedMesh=graphicsSurfaces.MetreBoxMesh(size,bevel,GraphicsSurfaceLibrary.TileSpan(material));
            }
            var collision = part.GetComponent<Collider>();
            if (collision) { collision.enabled = false; Destroy(collision); }
            return part;
        }

        void DressPiano(Transform piano, Transform bench)
        {
            piano.GetComponent<Renderer>().sharedMaterial = darkWood;
            bench.GetComponent<Renderer>().sharedMaterial = wood;
            var keys = originals.Where(item => item.name == "Piano key").ToArray();
            if (keys.Length != 22) throw new InvalidOperationException("Expected the original 22 piano-key colliders.");
            foreach (var key in keys) key.GetComponent<Renderer>().enabled = false;
            var group = Group("Upright piano detail", new Vector3(23.4f, 0, 13.4f), Quaternion.identity);
            Part(group, "Keyboard bed", new Vector3(0, .88f, -.405f), new Vector3(2.13f, .055f, .35f), wood);
            for (int key = 0; key < 28; key++)
            {
                float x = (key - 13.5f) * .068f;
                Part(group, "Ivory piano key", new Vector3(x, .921f, -.447f), new Vector3(.065f, .027f, .275f), ivory);
                WhiteKeyCount++;
                int note = key % 7;
                if (key < 27 && note != 2 && note != 6)
                {
                    Part(group, "Raised black piano key", new Vector3(x + .034f, .948f, -.388f), new Vector3(.040f, .029f, .157f), graphite);
                    BlackKeyCount++;
                }
            }
            Part(group, "Piano upper lid", new Vector3(0, 1.505f, 0), new Vector3(2.20f, .035f, .65f), wood);
            Part(group, "Keyboard front lip", new Vector3(0, .873f, -.58f), new Vector3(2.16f, .06f, .045f), darkWood);
            foreach (float x in new[] { -.97f, .97f })
                Part(group, "Piano front stile", new Vector3(x, .45f, -.333f), new Vector3(.055f, .83f, .022f), wood);
            Part(group, "Lower cabinet panel", new Vector3(0, .47f, -.332f), new Vector3(1.76f, .55f, .018f), wood);
            foreach (float x in new[] { -.13f, 0, .13f })
                Part(group, "Piano pedal", new Vector3(x, .09f, -.40f), new Vector3(.055f, .035f, .18f), brass);
            var supports = Group("Piano bench supports", new Vector3(23.4f, 0, 12.25f), Quaternion.identity);
            foreach (float x in new[] { -.47f, .47f }) foreach (float z in new[] { -.13f, .13f })
                Part(supports, "Grounded bench leg", new Vector3(x, .16f, z), new Vector3(.06f, .32f, .06f), darkWood);
            Part(supports, "Bench stretcher", new Vector3(0, .13f, 0), new Vector3(.96f, .045f, .045f), wood);
        }

        void DressChair(Transform chair)
        {
            chair.Find("Worn wooden seat").GetComponent<Renderer>().sharedMaterial = wood;
            chair.Find("Chair back").GetComponent<Renderer>().enabled = false;
            foreach (var renderer in chair.GetComponentsInChildren<Renderer>())
                if (renderer.name == "Steel leg") renderer.sharedMaterial = iron;
            var group = Group("Choir chair back detail", chair.position, chair.rotation);
            foreach (float x in new[] { -.18f, -.06f, .06f, .18f })
                Part(group, "Plywood chair back slat", new Vector3(x, .75f, -.23f), new Vector3(.105f, .32f, .045f), wood);
            Part(group, "Chair back top rail", new Vector3(0, .935f, -.23f), new Vector3(.52f, .03f, .052f), darkWood);
            ChairCount++;
        }

        void AddMiddleRow(Transform sourceChair, Transform sourceStand, List<Transform> chairs, List<Transform> stands)
        {
            // Fill the gap between the existing rows at z=6.7 and z=9.4. The
            // central aisle x=23.4, crossing z=9.6 and cabinet/piano area stay open.
            // Verify the actual authored space, not merely assumed floor coordinates.
            Physics.SyncTransforms();
            foreach (float x in new[] { 21.1f, 25.7f })
            {
                CheckFurnitureSpace(new Vector3(x, 0, 8.05f), new Vector3(.35f, .50f, .35f));
                CheckFurnitureSpace(new Vector3(x + .7f, 0, 8.70f), new Vector3(.30f, .60f, .27f));
            }
            foreach (float x in new[] { 21.1f, 25.7f })
            {
                var chair = Instantiate(sourceChair.gameObject, refresh).transform;
                chair.name = "Additional choir chair";
                chair.SetPositionAndRotation(new Vector3(x, 0, 8.05f), Quaternion.identity);
                AddedColliderCount += chair.GetComponentsInChildren<Collider>().Count(item => item.enabled);
                Carve(chair, new Vector3(.60f, 1.05f, .60f));
                chairs.Add(chair); AddedSeatCount++;
                var stand = Instantiate(sourceStand.gameObject, refresh).transform;
                stand.name = "Additional choir music stand";
                stand.SetPositionAndRotation(new Vector3(x + .7f, 0, 8.70f), Quaternion.identity);
                AddedColliderCount += stand.GetComponentsInChildren<Collider>().Count(item => item.enabled);
                Carve(stand, new Vector3(.50f, 1.25f, .43f));
                stands.Add(stand);
            }
            Physics.SyncTransforms();
        }

        static void CheckFurnitureSpace(Vector3 floor, Vector3 halfSize)
        {
            // Bottom at eight centimetres avoids counting the supporting floor.
            if (Physics.CheckBox(floor + Vector3.up * (halfSize.y + .08f), halfSize,
                    Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Additional music seating footprint is obstructed at " + floor);
        }

        void Carve(Transform furniture, Vector3 size)
        {
            var obstacle = furniture.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0, size.y * .5f, 0);
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            CarvedObstacleCount++;
        }

        void DressStand(Transform stand)
        {
            foreach (var renderer in stand.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name == "Stand base" || renderer.name == "Stand upright") renderer.sharedMaterial = iron;
                else renderer.enabled = false;
            }
            // Only the non-colliding music-rest head turns towards its authored singer.
            // Its original base and upright (and the base collider) do not move.
            var group = Group("Angled choir score", stand.position + Vector3.up * 1.2f, Quaternion.Euler(-12, 47, 0));
            Part(group, "Music-rest rim", Vector3.zero, new Vector3(.48f, .33f, .035f), iron);
            Part(group, "Readable score sheet", new Vector3(0, 0, -.025f), new Vector3(.415f, .278f, .008f), paper);
            ScoreLines(group, new Vector3(0, 0, -.031f), .34f, .15f);
            Part(group, "Music-rest lower ledge", new Vector3(0, -.17f, -.035f), new Vector3(.49f, .022f, .08f), iron);
        }

        void DressRoster()
        {
            var item = originals.Select(item => item.GetComponent<Interactable>()).FirstOrDefault(item => item && item.stableId == "music-roster");
            if (!item) throw new InvalidOperationException("Missing original music roster interaction.");
            RequirePosition(item.transform, new Vector3(23.4f, 1.35f, 13.02f));
            var group = Group("Mounted original choir roster", item.transform.position, Quaternion.identity);
            group.SetParent(item.transform,true);
            Part(group, "Roster wooden border", new Vector3(0, 0, -.043f), new Vector3(.60f, .45f, .012f), wood);
            Part(group, "Roster paper", new Vector3(0, 0, -.052f), new Vector3(.53f, .38f, .006f), paper);
            ScoreLines(group, new Vector3(0, .015f, -.057f), .44f, .23f);
            Part(group, "Erased fourth singer", new Vector3(.10f, -.142f, -.06f), new Vector3(.21f, .018f, .003f), graphite);
            Part(group, "Roster brass clip", new Vector3(0, .19f, -.061f), new Vector3(.105f, .035f, .008f), brass);
        }

        void ScoreLines(Transform group, Vector3 center, float width, float height)
        {
            for (int row = 0; row < 2; row++) for (int line = 0; line < 5; line++)
                Part(group, "Printed musical staff", center + new Vector3(0, height * (.39f - row * .63f - line * .063f), 0),
                    new Vector3(width, .002f, .0015f), graphite);
            for (int note = 0; note < 5; note++)
            {
                var at = center + new Vector3((note - 2) * width * .16f, height * (.24f - (note % 3) * .063f), -.0015f);
                Part(group, "Printed note head", at, new Vector3(.019f, .012f, .0015f), graphite);
                Part(group, "Printed note stem", at + new Vector3(.008f, .023f, 0), new Vector3(.002f, .046f, .0015f), graphite);
            }
        }

        void MakePercussionRack()
        {
            // Shallow hanging instruments, supported by the existing west wall.
            // They add no floor furniture or collision into the cabinet approach.
            var rack = Group("Wall mounted classroom percussion", new Vector3(18.71f, 1.75f, 12.05f), Quaternion.Euler(0, -90, 0));
            Part(rack, "Percussion storage backboard", Vector3.zero, new Vector3(1.70f, 1.00f, .04f), wood);
            for (int drum = 0; drum < 3; drum++)
            {
                float x = -.55f + drum * .55f;
                Part(rack, "Hanging hand-drum rim", new Vector3(x, .05f, -.043f), new Vector3(.38f, .018f, .38f), brass,
                    PrimitiveType.Cylinder, Quaternion.Euler(90, 0, 0));
                Part(rack, "Hand-drum skin", new Vector3(x, .05f, -.064f), new Vector3(.325f, .004f, .325f), ivory,
                    PrimitiveType.Cylinder, Quaternion.Euler(90, 0, 0));
                Part(rack, "Instrument hanging hook", new Vector3(x, .32f, -.037f), new Vector3(.02f, .10f, .03f), iron);
                Part(rack, "Drum beater", new Vector3(x + .11f, -.30f, -.04f), new Vector3(.016f, .22f, .016f), darkWood);
                Part(rack, "Drum beater head", new Vector3(x + .11f, -.195f, -.04f), new Vector3(.038f, .038f, .035f), ivory, PrimitiveType.Sphere);
            }
        }

        void MountedSign(TextMesh label, Vector3 position, float yaw, Vector2 size)
        {
            label.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var support = Group("Mounted sign " + label.name, position, label.transform.rotation);
            Part(support, "Sign oak backing", new Vector3(0, 0, .025f), new Vector3(size.x, size.y, .032f), darkWood);
            foreach (float x in new[] { -.5f, .5f })
                Part(support, "Sign fixing", new Vector3(x * (size.x - .075f), 0, .005f), new Vector3(.022f, .022f, .008f), brass);
            FitLater(label, size - new Vector2(.20f, .10f));
            MountedSignCount++;
        }

        void FitLater(TextMesh text, Vector2 bounds)
        {
            var font = text.GetComponent<AnnexSignFont>();
            if (!font) font = text.gameObject.AddComponent<AnnexSignFont>();
            font.Apply();
            fitted.Add(new FittedText { text = text, limit = bounds });
            fitFrames = 3;
        }

        void FontRebuilt(Font font)
        {
            if (fitted.Any(item => item.text && item.text.font == font)) fitFrames = 3;
        }

        void LateUpdate()
        {
            if (fitFrames <= 0) return;
            fitFrames--;
            foreach (var item in fitted)
            {
                if (!item.text) continue;
                var renderer = item.text.GetComponent<Renderer>();
                Vector3 size = renderer.localBounds.size;
                if (size.x <= .0001f || size.y <= .0001f) continue;
                float scale = Mathf.Min(1, item.limit.x / size.x, item.limit.y / size.y);
                item.text.transform.localScale = Vector3.one * scale;
            }
        }

        void OnDestroy()
        {
            Font.textureRebuilt -= FontRebuilt;
            graphicsSurfaces.Dispose();
        }
    }
}
