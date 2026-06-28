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
    public float pillarAlpha = 0.42f;

    private Transform _player;
    private bool      _triggered;

    // ── Built at runtime ──────────────────────────────────────────────────────
    private Material _pillarMaterial;

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
        _pillarMaterial = CreatePillarMaterial();

        for (int i = 0; i < 3; i++)
        {
            var beamGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            beamGO.name = $"GateLightBeam_{i + 1}";
            beamGO.transform.SetParent(transform, false);
            beamGO.transform.localPosition = Vector3.up * (pillarHeight * 0.5f);
            beamGO.transform.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
            beamGO.transform.localScale = new Vector3(pillarRadius * 4.5f, pillarHeight, 1f);

            var collider = beamGO.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = beamGO.GetComponent<Renderer>();
            if (renderer == null) continue;

            renderer.sharedMaterial = _pillarMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

    }

    private Material CreatePillarMaterial()
    {
        var shader = Shader.Find("Sprites/Default") ??
                     Shader.Find("Universal Render Pipeline/Unlit") ??
                     Shader.Find("Unlit/Transparent") ??
                     Shader.Find("Legacy Shaders/Transparent/Diffuse") ??
                     Shader.Find("Standard");
        var material = new Material(shader);
        var colour = new Color(pillarColour.r, pillarColour.g, pillarColour.b, pillarAlpha);
        material.mainTexture = CreateBeamTexture();

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", colour);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.One);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private Texture2D CreateBeamTexture()
    {
        const int width = 64;
        const int height = 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "GateLightBeamTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < height; y++)
        {
            float vertical = Mathf.Sin((y / (height - 1f)) * Mathf.PI);
            for (int x = 0; x < width; x++)
            {
                float centeredX = Mathf.Abs((x / (width - 1f)) * 2f - 1f);
                float core = Mathf.Clamp01(1f - centeredX);
                float alpha = Mathf.Pow(core, 2.2f) * Mathf.Lerp(0.35f, 1f, vertical);
                texture.SetPixel(x, y, alpha > 0.01f ? new Color(1f, 1f, 1f, alpha) : clear);
            }
        }

        texture.Apply(false, true);
        return texture;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, radius);
    }
}
