using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Place on a GameObject to mark the escape goal for the player.
/// Creates a blue pillar of light at runtime
/// and triggers level success when the player steps within <see cref="radius"/>.
///
/// Replaces JumpPassZone as the win condition for the escape level.
/// </summary>
public class EscapeGate : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("How close the player must get to trigger success.")]
    public float radius = 3f;

    [Header("Visual")]
    public Color pillarColour = new Color(0.3f, 0.6f, 1f, 1f);
    public float pillarHeight = 80f;
    public float pillarRadius = 1.1f;
    [Range(0.05f, 0.6f)]
    public float pillarAlpha = 0.32f;

    private Transform _player;
    private bool      _triggered;

    // ── Built at runtime ──────────────────────────────────────────────────────
    private Renderer _pillarRenderer;

    void Start()
    {
        var pg = GameObject.FindGameObjectWithTag("Player");
        if (pg != null) _player = pg.transform;

        BuildVisual();
    }

    void Update()
    {
        if (_triggered || _player == null) return;

        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(_player.position.x,   0f, _player.position.z));

        if (dist <= radius)
        {
            _triggered = true;
            ExplorationSceneManager.Instance?.OnEscapeGateReached();
        }
    }

    // ── Visual construction ───────────────────────────────────────────────────

    private void BuildVisual()
    {
        // ── 3D pillar mesh ────────────────────────────────────────────────────
        var pillarGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillarGO.name = "GatePillar";
        pillarGO.transform.SetParent(transform, false);
        pillarGO.transform.localPosition = Vector3.up * (pillarHeight * 0.5f);
        pillarGO.transform.localScale = new Vector3(pillarRadius * 2f, pillarHeight * 0.5f, pillarRadius * 2f);

        var collider = pillarGO.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        _pillarRenderer = pillarGO.GetComponent<Renderer>();
        if (_pillarRenderer != null)
        {
            _pillarRenderer.material = CreatePillarMaterial();
            _pillarRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _pillarRenderer.receiveShadows = false;
        }

    }

    private Material CreatePillarMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                     Shader.Find("Unlit/Color") ??
                     Shader.Find("Legacy Shaders/Transparent/Diffuse");
        var material = new Material(shader);
        var colour = new Color(pillarColour.r, pillarColour.g, pillarColour.b, pillarAlpha);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", colour);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.One);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, radius);
    }
}
