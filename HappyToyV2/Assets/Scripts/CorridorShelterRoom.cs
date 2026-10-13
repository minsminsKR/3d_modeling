using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    [DisallowMultipleComponent]
    public sealed class CorridorShelterRoom : MonoBehaviour
    {
        public int Cell { get; private set; }
        Interactable[] doors = Array.Empty<Interactable>();
        public Interactable EntranceDoor => doors.Length > 0 ? doors[0] : null;
        public IReadOnlyList<Interactable> Doors => doors;
        public bool HasSeparateExit => doors.Length == 2;
        public Vector3 Centre => transform.position;
        public void Configure(int cell, params Interactable[] entrances)
        {
            if (entrances == null || entrances.Length < 1 || entrances.Length > 2)
                throw new ArgumentException("Shelter requires one or two physical operable doors");
            foreach (var door in entrances)
                if (!door || !door.DoorOperable) throw new ArgumentException("Shelter requires physical operable doors");
            if (entrances.Length == 2 && entrances[0] == entrances[1]) throw new ArgumentException("Separate exits require distinct doors");
            Cell = cell; doors = (Interactable[])entrances.Clone();
        }
    }
}
