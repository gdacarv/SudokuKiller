using System.Collections;
using UnityEngine;

public class PixelFireworks : MonoBehaviour
{
    public static PixelFireworks Instance { get; private set; }

    [Header("Burst Settings")]
    [SerializeField] private int burstCount = 7;
    [SerializeField] private int particlesPerBurst = 38;
    [SerializeField] private float delayBetweenBursts = 0.17f;

    [Header("Confetti Settings")]
    [SerializeField] private int confettiPerWave = 14;
    [SerializeField] private int confettiWaves = 9;
    [SerializeField] private float confettiWaveDelay = 0.14f;

    private static readonly Color[] FireworkColors =
    {
        new Color(1f, 0.18f, 0.18f),   // red
        new Color(1f, 0.85f, 0.08f),   // gold
        new Color(0.08f, 0.94f, 0.9f), // cyan
        new Color(1f, 0.28f, 0.85f),   // magenta
        new Color(0.35f, 1f, 0.18f),   // lime
        Color.white,                    // white
        new Color(0.6f, 0.28f, 1f),    // purple
        new Color(1f, 0.55f, 0.08f),   // orange
        new Color(0.2f, 0.65f, 1f),    // sky blue
        new Color(1f, 0.4f, 0.4f),     // coral
    };

    private ParticleSystem _burstPs;
    private ParticleSystem _confettiPs;
    private Material _pixelMat;

    void Awake()
    {
        Instance = this;
        _pixelMat = BuildPixelMaterial();
        _burstPs = BuildBurstPS();
        _confettiPs = BuildConfettiPS();
    }

    private Material BuildPixelMaterial()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        return mat;
    }

    private ParticleSystem BuildBurstPS()
    {
        var go = new GameObject("BurstParticles");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier = 1.7f;
        main.maxParticles = burstCount * 3 * particlesPerBurst;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.01f;

        // Fade out in final 40% of lifetime
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.58f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        // Shrink at the end
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 0.9f), new Keyframe(1f, 0.2f)));

        // Spin each particle independently
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-4f * Mathf.PI, 4f * Mathf.PI);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = _pixelMat;
        rend.sortingOrder = 200;

        return ps;
    }

    private ParticleSystem BuildConfettiPS()
    {
        var go = new GameObject("ConfettiParticles");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 4.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier = 0.3f;
        main.maxParticles = confettiPerWave * confettiWaves + 20;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.01f;

        // Long hold, then fade
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.72f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        // Fast spin — makes flat squares look like tumbling confetti
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-6f * Mathf.PI, 6f * Mathf.PI);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = _pixelMat;
        rend.sortingOrder = 199;

        return ps;
    }

    public static void Play()
    {
        if (Instance == null) return;
        Instance.StartCoroutine(Instance.FireworksRoutine());
    }

    private IEnumerator FireworksRoutine()
    {
        var cam = Camera.main;
        if (cam == null) yield break;

        // Confetti starts immediately in parallel with the burst waves
        StartCoroutine(ConfettiRoutine(cam));

        // Wave 1: rapid-fire
        for (int i = 0; i < burstCount; i++)
        {
            EmitBurst(PickBurstPos(cam));
            yield return new WaitForSeconds(delayBetweenBursts);
        }

        yield return new WaitForSeconds(0.06f);

        // Wave 2: slightly slower, fills gaps
        for (int i = 0; i < burstCount; i++)
        {
            EmitBurst(PickBurstPos(cam));
            yield return new WaitForSeconds(delayBetweenBursts * 1.25f);
        }

        yield return new WaitForSeconds(0.06f);

        // Finale: 4 big center bursts in quick succession
        for (int i = 0; i < 4; i++)
        {
            EmitBurst(PickCenterPos(cam), particlesPerBurst * 2);
            yield return new WaitForSeconds(0.11f);
        }
    }

    private IEnumerator ConfettiRoutine(Camera cam)
    {
        float depth = CamDepth(cam);
        for (int wave = 0; wave < confettiWaves; wave++)
        {
            for (int i = 0; i < confettiPerWave; i++)
            {
                // Spawn confetti just above the visible screen, spread across its full width
                var pos = cam.ViewportToWorldPoint(new Vector3(
                    Random.Range(0f, 1f),
                    Random.Range(0.88f, 1.1f),
                    depth));
                _confettiPs.transform.position = pos;

                var ep = new ParticleSystem.EmitParams
                {
                    startColor = FireworkColors[Random.Range(0, FireworkColors.Length)],
                    startLifetime = Random.Range(2.4f, 4.2f),
                    startSize = Random.Range(0.05f, 0.14f)
                };
                _confettiPs.Emit(ep, 1);
            }
            yield return new WaitForSeconds(confettiWaveDelay);
        }
    }

    private void EmitBurst(Vector3 worldPos, int count = -1)
    {
        _burstPs.transform.position = worldPos;
        var ep = new ParticleSystem.EmitParams
        {
            startColor = FireworkColors[Random.Range(0, FireworkColors.Length)],
            startSize = Random.Range(0.04f, 0.11f),
            startLifetime = Random.Range(0.9f, 1.8f)
        };
        _burstPs.Emit(ep, count < 0 ? particlesPerBurst : count);
    }

    private static Vector3 PickBurstPos(Camera cam) =>
        cam.ViewportToWorldPoint(new Vector3(
            Random.Range(0.07f, 0.93f),
            Random.Range(0.38f, 0.93f),
            CamDepth(cam)));

    private static Vector3 PickCenterPos(Camera cam) =>
        cam.ViewportToWorldPoint(new Vector3(
            Random.Range(0.3f, 0.7f),
            Random.Range(0.5f, 0.85f),
            CamDepth(cam)));

    private static float CamDepth(Camera cam) =>
        cam.orthographic ? 10f : -cam.transform.position.z + 1f;
}
