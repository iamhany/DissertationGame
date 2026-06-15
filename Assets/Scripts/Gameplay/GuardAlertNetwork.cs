using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry that lets guards alert one another.
/// When one guard enters Chase it calls BroadcastAlert(); all other registered
/// guards that are NOT already chasing are sent to the last-known position.
/// No MonoBehaviour needed — guards register/unregister themselves.
/// </summary>
public static class GuardAlertNetwork
{
    private static readonly List<GuardController> _guards = new List<GuardController>();

    public static void Register(GuardController g)
    {
        if (!_guards.Contains(g)) _guards.Add(g);
    }

    public static void Unregister(GuardController g)
    {
        _guards.Remove(g);
    }

    /// <summary>
    /// Called by the guard that first spots the player.
    /// All other guards are alerted to move to <paramref name="lastKnownPos"/>.
    /// </summary>
    public static void BroadcastAlert(GuardController source, Vector3 lastKnownPos)
    {
        foreach (var g in _guards)
        {
            if (g == source) continue;
            if (g == null)   continue;
            g.EnterAlerted(lastKnownPos);
        }
    }

    /// <summary>Clear the registry (called when the scene unloads).</summary>
    public static void Clear() => _guards.Clear();
}
