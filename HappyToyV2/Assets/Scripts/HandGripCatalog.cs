using System;
using UnityEngine;

namespace HappyToy.V2
{
    public enum HandGripKind { Power, Precision, Slender, PaperPinch, Support }
    public enum HandGripSide { Left, Right }
    public enum HandGripAnchor { Cylinder, Pinch, SlenderPinch, PaperLowerCorner, Palm }

    // Presentation data only: the game inventory, release markers and effects
    // continue to belong to their existing owners.
    [CreateAssetMenu(menuName = "HappyToy/Hand Grip Catalog")]
    public sealed class HandGripCatalog : ScriptableObject
    {
        [Serializable] public sealed class Profile
        {
            public PlayerItem item;
            public HandGripKind gripType;
            public HandGripSide hand;
            public HandGripAnchor anchor;
            public string fingerPose;
            // anchorOffset is in the prop's local frame and is applied after
            // the geometric anchor has been placed on the hand.
            public Vector3 socketPosition, wristEuler, propEuler, anchorOffset;
            [Min(.01f)] public float propScale = 1;
            [Min(.01f)] public float transitionDuration = .44f;
        }

        public Profile[] profiles;
        public static HandGripCatalog Load(HandAnimationLibrary animations)
        {
            var catalog = Resources.Load<HandGripCatalog>("PlayerHandAnimation/GripCatalog");
            if (!catalog || catalog.profiles == null || catalog.profiles.Length != Enum.GetValues(typeof(PlayerItem)).Length)
                throw new InvalidOperationException("Missing or incomplete item grip catalog.");
            foreach (PlayerItem item in Enum.GetValues(typeof(PlayerItem)))
            {
                Profile found = null;
                foreach (var profile in catalog.profiles)
                    if (profile != null && profile.item == item)
                    { if (found != null) throw new InvalidOperationException("Duplicate grip profile: " + item); found = profile; }
                if (found == null || string.IsNullOrEmpty(found.fingerPose) ||
                    float.IsNaN(found.transitionDuration) || float.IsInfinity(found.transitionDuration) || found.transitionDuration <= 0 ||
                    float.IsNaN(found.propScale) || float.IsInfinity(found.propScale) || found.propScale <= 0 ||
                    !Finite(found.socketPosition) || !Finite(found.wristEuler) || !Finite(found.propEuler) || !Finite(found.anchorOffset) ||
                    !Enum.IsDefined(typeof(HandGripKind), found.gripType) || !Enum.IsDefined(typeof(HandGripAnchor), found.anchor))
                    throw new InvalidOperationException("Invalid grip profile: " + item);
                if (found.hand != (item == PlayerItem.Flashlight ? HandGripSide.Left : HandGripSide.Right))
                    throw new InvalidOperationException("Unsupported hand assignment for grip: " + item);
                HandGripAnchor required = item == PlayerItem.Flashlight ? HandGripAnchor.Cylinder :
                    item == PlayerItem.Snapper ? HandGripAnchor.Pinch : item == PlayerItem.Incense ? HandGripAnchor.SlenderPinch :
                    item == PlayerItem.Talisman ? HandGripAnchor.PaperLowerCorner : HandGripAnchor.Palm;
                if (found.anchor != required) throw new InvalidOperationException("Unsupported anchor for grip: " + item);
                if (animations.GetPose(found.fingerPose).hand != found.hand.ToString())
                    throw new InvalidOperationException("Finger pose is for the wrong hand: " + item);
            }
            return catalog;
        }
        public Profile Get(PlayerItem item)
        {
            foreach (var profile in profiles) if (profile != null && profile.item == item) return profile;
            throw new InvalidOperationException("Missing grip profile: " + item);
        }
        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
