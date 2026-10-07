using UnityEngine;

namespace HappyToy.V2
{
    // Placement evidence stays beside the real furniture, not in a separate scene.
    [DisallowMultipleComponent]
    public sealed class CorridorFurniturePlacement : MonoBehaviour
    {
        public string StableId { get; private set; }
        public string PropKey { get; private set; }
        public int Cell { get; private set; }
        public int WallDirection { get; private set; }
        public float WallOffset { get; private set; }
        public Vector3 Approach => transform.TransformPoint(new Vector3(0, 0, -.95f));
        public void Configure(string id, string prop, int cell, int direction, float offset)
        { StableId = id; PropKey = prop; Cell = cell; WallDirection = direction; WallOffset = offset; }
    }
}
