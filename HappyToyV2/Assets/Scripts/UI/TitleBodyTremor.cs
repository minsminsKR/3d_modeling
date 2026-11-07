using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Visual-only motion for the title clone. The original animation and game
    // actor are never edited; each sample starts from the captured planted pose.
    internal sealed class TitleBodyTremor
    {
        readonly Transform pivot;
        readonly Joint[] torso, arms;
        readonly Contact[] contacts;

        readonly struct Joint
        {
            public readonly Transform transform;
            public readonly Quaternion rotation;
            public Joint(Transform value) { transform = value; rotation = value.localRotation; }
        }
        readonly struct Contact
        {
            public readonly Transform wrist;
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public Contact(Transform value, Transform pivot)
            {
                wrist = value; position = pivot.InverseTransformPoint(value.position);
                rotation = Quaternion.Inverse(pivot.rotation) * value.rotation;
            }
        }

        public TitleBodyTremor(Transform pivot, Transform creature)
        {
            this.pivot = pivot;
            var nodes = creature.GetComponentsInChildren<Transform>(true);
            Transform Find(string name) => nodes.Single(node => node.name == name);
            torso = new[] { "BodyRoot", "SpineRear", "SpineBack", "SpineFront", "Chest" }
                .Select(name => new Joint(Find(name))).ToArray();
            var armJoints = new System.Collections.Generic.List<Joint>();
            var supportContacts = new System.Collections.Generic.List<Contact>();
            for (int limb = 1; limb <= 6; limb++)
            foreach (string side in new[] { "L", "R" })
            {
                string prefix = "Arm" + limb + side;
                armJoints.Add(new Joint(Find(prefix + "_Shoulder")));
                armJoints.Add(new Joint(Find(prefix + "_Elbow")));
                supportContacts.Add(new Contact(Find(prefix + "_Wrist"), pivot));
            }
            arms = armJoints.ToArray(); contacts = supportContacts.ToArray();
        }

        // Several asymmetric waves avoid a metronomic synchronized shake.
        // Applying from captured rotations prevents drift over long title stays.
        static float Shiver(float time, float phase)
        {
            float envelope = .72f + .28f * Mathf.Sin(time * 1.13f + phase * .37f);
            return envelope * (.70f * Mathf.Sin(time * 30.2f + phase) +
                .30f * Mathf.Sin(time * 47.1f + phase * 1.73f));
        }
        public void Apply(float time, bool motionEnabled)
        {
            for (int i = 0; i < torso.Length; i++)
            {
                float amount = motionEnabled ? Shiver(time, i * 1.27f) : 0;
                float strength = i == 0 ? .55f : .95f;
                torso[i].transform.localRotation = torso[i].rotation *
                    Quaternion.Euler(amount * strength, amount * .28f, amount * strength * .55f);
            }
            for (int i = 0; i < arms.Length; i++)
            {
                float amount = motionEnabled ? Shiver(time, 1.1f + i * .83f) : 0;
                float strength = i % 2 == 0 ? .65f : 1.15f;
                arms[i].transform.localRotation = arms[i].rotation *
                    Quaternion.Euler(amount * strength, amount * strength * .35f, amount * strength * .5f);
            }
            // Keep each planted wrist and its original contact surface fixed
            // while the proximal joints and body shiver above it.
            foreach (var contact in contacts)
            {
                contact.wrist.position = pivot.TransformPoint(contact.position);
                contact.wrist.rotation = pivot.rotation * contact.rotation;
            }
        }
    }
}
