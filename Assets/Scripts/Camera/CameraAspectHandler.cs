using UnityEngine;

/// <summary>
/// Locks horizontal extent by adjusting the camera's orthographic size when the
/// screen aspect ratio is narrower than the reference aspect ratio.
///
/// Reference behaviour (16:9 built):
///   - Narrower ratio (e.g. 16:10): orthographic size increases so the same
///     horizontal units remain visible and the player gains vertical space.
///   - Wider ratio (e.g. 21:9): orthographic size stays at referenceOrthoSize
///     so the player simply gains horizontal space (default Unity behaviour).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CameraAspectHandler : MonoBehaviour
{
    [Tooltip("Aspect ratio the game was designed for (width / height). Default: 16/9.")]
    [SerializeField] private float referenceAspect = 16f / 9f;

    [Tooltip("Orthographic size at the reference aspect ratio.")]
    [SerializeField] private float referenceOrthoSize = 3.5f;

    private Camera _cam;
    private float _lastAspect;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void Start()
    {
        UpdateOrthoSize();
    }

    private void Update()
    {
        float current = (float)Screen.width / Screen.height;
        if (!Mathf.Approximately(current, _lastAspect))
            UpdateOrthoSize();
    }

    private void UpdateOrthoSize()
    {
        if (_cam == null)
            _cam = GetComponent<Camera>();

        float current = (float)Screen.width / Screen.height;
        _lastAspect = current;

        if (current < referenceAspect)
        {
            // Narrower than reference: scale up ortho size to preserve horizontal extent.
            _cam.orthographicSize = referenceOrthoSize * (referenceAspect / current);
        }
        else
        {
            // Wider than or equal to reference: keep ortho size fixed, extra width is a bonus.
            _cam.orthographicSize = referenceOrthoSize;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateOrthoSize();
    }
#endif
}
