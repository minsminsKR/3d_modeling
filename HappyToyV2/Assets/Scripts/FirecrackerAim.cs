using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>Optional held preview of the first physical contact, never a landing or enemy-success forecast.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(150)]
    public sealed class FirecrackerAim : MonoBehaviour
    {
        readonly Vector3[] points = new Vector3[FirecrackerTrajectory.MaximumForecastPoints];
        readonly Vector3[] ring = new Vector3[33];
        FirecrackerInventory inventory;
        PlayerMotor player;
        GameObject visuals;
        Material material;
        LineRenderer trajectory, contact;
        bool held, requireRelease = true;
        FirecrackerTrajectory.ForecastResult forecast;

        public bool Visible { get; private set; }
        public bool HasContact => Visible && forecast.HasContact;
        public bool InitialOverlap => Visible && forecast.InitialOverlap;
        public int ForecastPointCount => Visible ? forecast.PointCount : 0;
        public Vector3 EndPosition => forecast.EndPosition;
        public Vector3 ContactPoint => forecast.ContactPoint;
        public Vector3 ContactNormal => forecast.ContactNormal;
        public float ForecastTime => forecast.Time;
        public LineRenderer TrajectoryRenderer => trajectory;
        public LineRenderer ContactRenderer => contact;
        public string Hint => InitialOverlap ? "던질 공간이 없습니다 · 조금 물러나세요" :
            HasContact ? PlayerControls.UsingGamepad ? "첫 충돌 예상 · RT 투척 · LT 놓기 취소" : "첫 충돌 예상 · Q 투척 · 우클릭 놓기 취소" :
            PlayerControls.UsingGamepad ? "도화선 범위 예상 · RT 투척 · LT 놓기 취소" : "도화선 범위 예상 · Q 투척 · 우클릭 놓기 취소";

        void Awake() { inventory = GetComponent<FirecrackerInventory>(); player = GetComponent<PlayerMotor>(); }
        void LateUpdate()
        {
            bool pressed = PlayerControls.AimHeld;
            if (!inventory || !inventory.CanAim || GameSession.Current.Shell.GameplayEntryFrame == Time.frameCount)
            { Cancel(); return; }
            // A held button from a menu/cabinet/previous life is not a fresh intent.
            if (!pressed) { held = false; requireRelease = false; Hide(); return; }
            if (requireRelease) { Hide(); return; }
            if (PlayerControls.AimPressed) held = true;
            if (!held) { Hide(); return; }
            var eye = player.eyes.transform;
            FirecrackerTrajectory.GetLaunch(eye.position, eye.forward, out var position, out var velocity);
            forecast = FirecrackerTrajectory.Forecast(position, velocity, points);
            EnsureVisuals();
            if (!visuals) { Hide(); return; }
            Visible = true;
            visuals.SetActive(true);
            var shell = GameSession.Current.Shell;
            bool contrast = shell && shell.HighContrast;
            // No blinking, moving dashes, camera motion or pulsing, including ReducedMotion.
            Color color = contrast ? new Color(1f, .86f, .48f, .96f) : new Color(.84f, .74f, .51f, .72f);
            trajectory.startColor = trajectory.endColor = color;
            contact.startColor = contact.endColor = color;
            // Perspective magnifies the near-eye segment: taper its world width
            // so it stays a fine line rather than an opaque wedge across the view.
            trajectory.startWidth = contrast ? .005f : .0035f;
            trajectory.endWidth = contrast ? .025f : .018f;
            contact.startWidth = contact.endWidth = contrast ? .021f : .014f;
            trajectory.enabled = !forecast.InitialOverlap && forecast.PointCount > 1;
            trajectory.positionCount = forecast.PointCount;
            for (int i = 0; i < forecast.PointCount; i++) trajectory.SetPosition(i, points[i]);
            contact.enabled = forecast.HasContact && !forecast.InitialOverlap;
            if (contact.enabled)
            {
                Vector3 normal = forecast.ContactNormal.normalized;
                Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.right).normalized;
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                Vector3 center = forecast.ContactPoint + normal * .012f;
                for (int i = 0; i < ring.Length; i++)
                {
                    float angle = i * (Mathf.PI * 2 / (ring.Length - 1));
                    ring[i] = center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * .12f;
                }
                contact.SetPositions(ring);
            }
        }
        void EnsureVisuals()
        {
            if (visuals) return;
            // Reuse the bundled depth-tested vertex-color shader. A white atlas
            // makes simple world lines; normal scene depth hides them behind walls.
            var shader = Resources.Load<Shader>(AnnexSignFont.ShaderResourcePath);
            if (!shader) { Debug.LogError("Missing bundled firecracker preview shader", this); enabled = false; return; }
            material = new Material(shader) { name = "Firecracker first-contact preview" };
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            visuals = new GameObject("Firecracker aim visuals");
            visuals.layer = 2;
            visuals.transform.SetParent(transform, false);
            trajectory = CreateLine("First-contact trajectory", 0);
            contact = CreateLine("First-contact ring", ring.Length);
        }
        LineRenderer CreateLine(string name, int count)
        {
            var root = new GameObject(name); root.layer = 2; root.transform.SetParent(visuals.transform, false);
            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = count;
            line.alignment = LineAlignment.View; line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.numCapVertices = 2; line.numCornerVertices = 2; line.enabled = false;
            return line;
        }
        void Hide() { Visible = false; if (visuals) visuals.SetActive(false); }
        public void Cancel() { held = false; requireRelease = true; Hide(); }
        void OnDisable() { Cancel(); }
        void OnDestroy() { if (visuals) Destroy(visuals); if (material) Destroy(material); }
    }
}
