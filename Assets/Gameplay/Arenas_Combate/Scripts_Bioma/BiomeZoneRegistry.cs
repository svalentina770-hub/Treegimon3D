using System.Collections.Generic;
using UnityEngine;

public class BiomeZoneRegistry : MonoBehaviour
{
    private static readonly List<BiomeZone> zones = new();

    public static void Register(BiomeZone zone)
    {
        if (zone == null || zones.Contains(zone))
            return;

        zones.Add(zone);
    }

    public static void Unregister(BiomeZone zone)
    {
        if (zone == null)
            return;

        zones.Remove(zone);
    }

    public static PlantBiomeType GetBiomeAtPosition(Vector3 position)
    {
        BiomeZone best = null;
        float bestDistance = float.MaxValue;

        foreach (BiomeZone zone in zones)
        {
            if (zone == null || zone.ZoneCollider == null)
                continue;

            if (!zone.ContainsPoint(position))
                continue;

            float distance = Vector3.Distance(zone.transform.position, position);

            if (best == null ||
                zone.Priority > best.Priority ||
                (zone.Priority == best.Priority && distance < bestDistance))
            {
                best = zone;
                bestDistance = distance;
            }
        }

        return best != null ? best.BiomeType : PlantBiomeType.Templado;
    }
}