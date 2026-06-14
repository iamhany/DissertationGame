using UnityEngine;

/// <summary>
/// Attach this to any object (rock, marker, etc.).
/// When the player walks within <proximityRadius> units, the zone is cleared and
/// ExplorationSceneManager advances the narrative.
/// No collider setup required — uses a simple distance check every frame.
/// </summary>
public class JumpPassZone : MonoBehaviour
{
    [Tooltip("Display name shown in the HUD progress counter.")]
    public string zoneName = "Landmark";

    [Tooltip("How close the player must get to clear this zone.")]
    public float proximityRadius = 3f;

    public bool IsCleared { get; private set; }

    private Transform _player;

    void Start()
    {
        var pg = GameObject.FindGameObjectWithTag("Player");
        if (pg != null) _player = pg.transform;
    }

    void Update()
    {
        if (IsCleared || _player == null) return;

        if (Vector3.Distance(transform.position, _player.position) <= proximityRadius)
        {
            IsCleared = true;
            ExplorationSceneManager.Instance?.OnJumpPassZoneCleared(this);
        }
    }

    /// <summary>Called by ExplorationSceneManager on retry to reset this zone.</summary>
    public void ResetZone() => IsCleared = false;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = IsCleared ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0.8f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, proximityRadius);
    }
#endif
}
