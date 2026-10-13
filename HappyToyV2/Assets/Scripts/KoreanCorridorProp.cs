using UnityEngine;

namespace HappyToy.V2
{
    public sealed class KoreanCorridorProp : MonoBehaviour
    {
        public string Key { get; private set; }
        public int Cell { get; private set; }
        public void Configure(string key, int cell) { Key=key; Cell=cell; }
    }
}
