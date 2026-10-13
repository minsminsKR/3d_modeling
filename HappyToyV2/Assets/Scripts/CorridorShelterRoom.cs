using UnityEngine;

namespace HappyToy.V2
{
    [DisallowMultipleComponent]
    public sealed class CorridorShelterRoom : MonoBehaviour
    {
        public int Cell { get; private set; }
        public Interactable EntranceDoor { get; private set; }
        public Vector3 Centre => transform.position;
        public void Configure(int cell, Interactable entrance)
        {
            if (!entrance || !entrance.DoorOperable) throw new System.ArgumentException("Shelter requires a physical operable door");
            Cell = cell; EntranceDoor = entrance;
        }
    }
}
