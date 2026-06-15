using UnityEngine;

/// <summary>
/// Place on a GameObject to mark the escape goal for the player.
/// Creates a blue pillar of light (particle system + point light) at runtime
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
    public Color  pillarColour   = new Color(0.3f, 0.6f, 1f, 1f);
    public float  pillarHeight   = 8f;
    public float  lightIntensity = 2.5f;
    public float  lightRange     = 10f;

    private Transform _player;
    private bool      _triggered;

    // ── Built at runtime ──────────────────────────────────────────────────────
    private ParticleSystem _particles;
    private Light          _light;

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
        // ── Point light ───────────────────────────────────────────────────────
        var lightGO = new GameObject("GateLight");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = Vector3.up * 1f;
        _light           = lightGO.AddComponent<Light>();
        _light.type      = LightType.Point;
        _light.color     = pillarColour;
        _light.intensity = lightIntensity;
        _light.range     = lightRange;

        // ── Particle pillar ───────────────────────────────────────────────────
        var psGO = new GameObject("GatePillar");
        psGO.transform.SetParent(transform, false);
        psGO.transform.localPosition = Vector3.zero;

        _particles = psGO.AddComponent<ParticleSystem>();

        // Main module — upward stream
        var main          = _particles.main;
        main.loop         = true;
        main.startLifetime = pillarHeight / 3f;
        main.startSpeed   = 3f;
        main.startSize    = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
        main.startColor   = new ParticleSystem.MinMaxGradient(
            new Color(pillarColour.r, pillarColour.g, pillarColour.b, 0.4f),
            new Color(pillarColour.r, pillarColour.g, pillarColour.b, 0.8f));
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Emission
        var emission     = _particles.emission;
        emission.rateOverTime = 80f;

        // Shape — disk at base, upward emission
        var shape        = _particles.shape;
        shape.shapeType  = ParticleSystemShapeType.Circle;
        shape.radius     = radius * 0.4f;
        shape.rotation   = new Vector3(-90f, 0f, 0f);   // emit upward

        // Velocity over lifetime — keep going up
        var vel          = _particles.velocityOverLifetime;
        vel.enabled      = true;
        vel.space        = ParticleSystemSimulationSpace.Local;
        vel.x            = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y            = new ParticleSystem.MinMaxCurve(2f, 4f);
        vel.z            = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Colour over lifetime — fade out at top
        var col          = _particles.colorOverLifetime;
        col.enabled      = true;
        var grad         = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f),        new GradientAlphaKey(0f, 1f) });
        col.color        = new ParticleSystem.MinMaxGradient(grad);

        // Size over lifetime — shrink toward top
        var size         = _particles.sizeOverLifetime;
        size.enabled     = true;
        var sizeCurve    = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.1f));
        size.size        = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Renderer — additive blend for glow (URP-compatible)
        var rend              = _particles.GetComponent<ParticleSystemRenderer>();
        rend.renderMode       = ParticleSystemRenderMode.Billboard;
        var pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                      Shader.Find("Particles/Standard Unlit") ??
                      Shader.Find("Legacy Shaders/Particles/Additive");
        if (pShader != null)
        {
            rend.material       = new Material(pShader);
            rend.material.color = pillarColour;
        }

        _particles.Play();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, radius);
    }
}
