using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class HandGripCatalogSetup
    {
        const string Path = "Assets/Resources/PlayerHandAnimation/GripCatalog.asset";
        static HandGripCatalog.Profile Profile(PlayerItem item, HandGripKind kind, HandGripSide side,
            HandGripAnchor anchor, Vector3 socket, Vector3 wrist, Vector3 prop, float scale = 1)
        {
            return new HandGripCatalog.Profile { item = item, gripType = kind, hand = side, anchor = anchor,
                fingerPose = item.ToString(), socketPosition = socket, wristEuler = wrist,
                propEuler = prop, propScale = scale, transitionDuration = .44f };
        }
        public static void CreateDefault()
        {
            if (AssetDatabase.LoadAssetAtPath<HandGripCatalog>(Path))
                throw new InvalidOperationException("The grip catalog already exists; do not overwrite authored profiles.");
            var catalog = ScriptableObject.CreateInstance<HandGripCatalog>();
            var right = new Vector3(.315f, -.245f, .50f);
            var usual = new Vector3(4, -24, 8);
            catalog.profiles = new[] {
                Profile(PlayerItem.Flashlight, HandGripKind.Power, HandGripSide.Left, HandGripAnchor.Cylinder,
                    new Vector3(-.260f, -.230f, .50f), new Vector3(0, 90, -6), new Vector3(-90, 0, 0)),
                Profile(PlayerItem.Snapper, HandGripKind.Precision, HandGripSide.Right, HandGripAnchor.Pinch,
                    right, usual, new Vector3(12, 0, -10), .85f),
                Profile(PlayerItem.Incense, HandGripKind.Slender, HandGripSide.Right, HandGripAnchor.SlenderPinch,
                    right, usual, new Vector3(-5, 0, -8)),
                Profile(PlayerItem.Talisman, HandGripKind.PaperPinch, HandGripSide.Right, HandGripAnchor.PaperLowerCorner,
                    right, new Vector3(4, -24, -27), new Vector3(0, 165, -8)),
                Profile(PlayerItem.Trap, HandGripKind.Support, HandGripSide.Right, HandGripAnchor.Palm,
                    new Vector3(.300f, -.305f, .62f), new Vector3(-65, 132, -8), new Vector3(0, 0, -8))
            };
            AssetDatabase.CreateAsset(catalog, Path);
            AssetDatabase.SaveAssets();
            Debug.Log("Created item grip catalog: " + Path);
        }
        public static void Reserialize()
        {
            if (!AssetDatabase.LoadAssetAtPath<HandGripCatalog>(Path))
                throw new InvalidOperationException("The grip catalog does not exist.");
            AssetDatabase.ForceReserializeAssets(new[] { Path });
        }
    }
}
