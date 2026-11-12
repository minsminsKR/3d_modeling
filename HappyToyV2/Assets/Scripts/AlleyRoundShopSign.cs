using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Replaces the projecting square sign only when the circular source and
    // cutout template are present. Existing shop brackets and gameplay remain.
    [DisallowMultipleComponent]
    public sealed class AlleyRoundShopSign : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        Material faceMaterial;
        public bool TryPrepare()
        {
            if (faceMaterial) return false;
            var texture = Resources.Load<Texture2D>("AlleyStorefrontInterior/round-icecream-sign");
            var template = Resources.Load<Material>("AlleyNotices/fire-notice");
            var oldFrame = transform.Find("Projecting shop sign frame");
            if (!texture || !template || !oldFrame) return false;
            faceMaterial = new Material(template) { name = "Aged round ice cream sign face" };
            faceMaterial.SetTexture("_BaseMap", texture);
            faceMaterial.SetColor("_BaseColor", new Color(.80f, .76f, .68f));
            var casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            casing.name = "Round ice cream sign casing"; casing.layer = 8;
            casing.transform.SetParent(transform, false);
            casing.transform.localPosition = new Vector3(-2.63f, 2.75f, -11.2f);
            casing.transform.localRotation = Quaternion.Euler(90, 0, 0);
            casing.transform.localScale = new Vector3(.94f, .05f, .94f);
            var caseRenderer = casing.GetComponent<Renderer>();
            caseRenderer.sharedMaterial = surfaces.Get("metal-rust", new Color(.38f, .33f, .29f));
            caseRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Destroy(casing.GetComponent<Collider>());
            for (int face = 0; face < 2; face++)
            {
                var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                panel.name = "Round ice cream sign face"; panel.layer = 8;
                panel.transform.SetParent(transform, false);
                panel.transform.localPosition = new Vector3(-2.63f, 2.75f, -11.2f + (face == 0 ? -.055f : .055f));
                panel.transform.localRotation = Quaternion.Euler(0, face * 180, 0);
                panel.transform.localScale = Vector3.one * .90f;
                var renderer = panel.GetComponent<Renderer>(); renderer.sharedMaterial = faceMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                Destroy(panel.GetComponent<Collider>());
            }
            var oldRenderer = oldFrame.GetComponent<Renderer>(); if (oldRenderer) oldRenderer.enabled = false;
            foreach (Transform child in transform)
                if (child.name == "Baked Korean shop sign face" && child.GetComponent<Renderer>())
                    child.GetComponent<Renderer>().enabled = false;
            return true;
        }
        void OnDestroy()
        {
            if (faceMaterial) GraphicsSurfaceLibrary.DestroyOwned(faceMaterial);
            surfaces.Dispose();
        }
    }
}
