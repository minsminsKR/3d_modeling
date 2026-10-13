using UnityEngine;

namespace HappyToy.V2
{
    internal static class EnemyRouteGeometry
    {
        // A wall-mounted candle remains a solid ray target for E, but its small
        // tray is not a door or an obstruction to a creature's walking body.
        public static bool IsSmallRouteMarker(Collider collider) => collider && collider.GetComponentInParent<WaymarkCandle>();
    }
}
