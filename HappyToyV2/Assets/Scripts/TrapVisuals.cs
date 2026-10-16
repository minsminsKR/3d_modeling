using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed class TrapVisuals : MonoBehaviour
    {
        Transform front, back, plate;
        Quaternion frontRest, backRest;
        Vector3 plateRest, frontAxis, backAxis, plateTravel;
        float frontSign, backSign;
        public float ClosedAmount { get; private set; }
        public static TrapVisuals Attach(Transform parent, float scale = 1)
        {
            var prefab = Resources.Load<GameObject>("Traps/Trap");
            if (!prefab) throw new InvalidOperationException("Missing authored webapp trap model");
            var root = Instantiate(prefab,parent,false); root.transform.localScale *= scale;
            var visual = root.AddComponent<TrapVisuals>();
            var nodes = root.GetComponentsInChildren<Transform>(true);
            visual.front = nodes.Single(t=>t.name=="JawFront"); visual.back = nodes.Single(t=>t.name=="JawBack");
            visual.plate = nodes.Single(t=>t.name=="PressurePlate");
            visual.frontRest = visual.front.localRotation; visual.backRest = visual.back.localRotation; visual.plateRest = visual.plate.localPosition;
            visual.frontAxis = visual.front.InverseTransformDirection(root.transform.right).normalized;
            visual.backAxis = visual.back.InverseTransformDirection(root.transform.right).normalized;
            visual.frontSign = LiftSign(visual.front, root.transform);
            visual.backSign = LiftSign(visual.back, root.transform);
            visual.plateTravel = visual.plate.parent.InverseTransformVector(root.transform.TransformVector(Vector3.up*.014f));
            foreach (var node in nodes) node.gameObject.layer = parent.gameObject.layer;
            return visual;
        }
        static float LiftSign(Transform jaw, Transform root)
        {
            var bounds = jaw.GetComponent<Renderer>().bounds;
            float lift = Vector3.Dot(Vector3.Cross(root.right,bounds.center-jaw.position),root.up);
            return lift>=0?1:-1;
        }
        public void SetClosed(float amount)
        {
            ClosedAmount = Mathf.Clamp01(amount);
            front.localRotation = frontRest*Quaternion.AngleAxis(78*frontSign*ClosedAmount,frontAxis);
            back.localRotation = backRest*Quaternion.AngleAxis(78*backSign*ClosedAmount,backAxis);
            plate.localPosition = plateRest-plateTravel*ClosedAmount;
        }
        public void Consume() => gameObject.SetActive(false);
    }
}
