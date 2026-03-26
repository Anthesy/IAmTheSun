using UnityEngine;

public class ChangeTemperature : MonoBehaviour
{
    [SerializeField] private float rayDistance = 100f;
    [SerializeField] private bool debugEnabled = true;
    [SerializeField] private Color debugHitColor = Color.green;
    [SerializeField] private Color debugMissColor = Color.red;
    [SerializeField] private float debugLineWidth = 0.003f;
    
    [SerializeField] private AudioSource backgroundAudioSource;
    [SerializeField] private float heatedPitch = 1.5f;
    [SerializeField] private float pitchLerpSpeed = 2f;

    private TransitionMaterials currentPlanetTransition;
    private LineRenderer debugLine;
    private Transform debugVisualRoot;
    
    private float originalPitch;
    private float targetPitch;

    private void Start()
    {
        EnsureDebugVisualsInitialized();
        CaptureOriginalAudioPitch();
    }
    
    private void CaptureOriginalAudioPitch()
    {
        if (backgroundAudioSource != null)
        {
            originalPitch = backgroundAudioSource.pitch;
            targetPitch = originalPitch;
        }
    }

    private void Update()
    {
        var ray = new Ray(transform.position, transform.forward);
        TransitionMaterials hitPlanetTransition = null;
        RaycastHit hitInfo = default;
        bool hasHit = Physics.Raycast(ray, out hitInfo, rayDistance);

        if (hasHit)
        {
            // Try to get TransitionMaterials from hit collider or its parents
            hitPlanetTransition = hitInfo.collider.GetComponent<TransitionMaterials>();
            if (hitPlanetTransition == null)
            {
                hitPlanetTransition = hitInfo.collider.GetComponentInParent<TransitionMaterials>();
            }
        }

        UpdateDebugVisuals(ray, hasHit, hitInfo, hitPlanetTransition != null);
        UpdateAudioPitch();

        if (hitPlanetTransition != currentPlanetTransition)
        {
            if (currentPlanetTransition != null)
            {
                currentPlanetTransition.ApplyOutside();
                ApplyAudioOutside();
            }

            if (hitPlanetTransition != null)
            {
                hitPlanetTransition.ApplyInside();
                ApplyAudioInside();
            }

            currentPlanetTransition = hitPlanetTransition;
        }
    }

    private void UpdateDebugVisuals(Ray ray, bool hasHit, RaycastHit hitInfo, bool isPlanetHit)
    {
        if (!debugEnabled)
        {
            if (debugLine != null)
            {
                debugLine.enabled = false;
            }

            return;
        }

        if (debugLine == null)
        {
            return;
        }

        debugLine.enabled = true;

        Vector3 endPoint = hasHit ? hitInfo.point : (ray.origin + ray.direction * rayDistance);
        debugLine.SetPosition(0, ray.origin);
        debugLine.SetPosition(1, endPoint);

        Color rayColor = isPlanetHit ? debugHitColor : debugMissColor;
        debugLine.startColor = rayColor;
        debugLine.endColor = rayColor;
    }
    
    private void ApplyAudioInside()
    {
        targetPitch = heatedPitch;
    }
    
    private void ApplyAudioOutside()
    {
        targetPitch = originalPitch;
    }
    
    private void UpdateAudioPitch()
    {
        if (backgroundAudioSource != null)
        {
            backgroundAudioSource.pitch = Mathf.Lerp(backgroundAudioSource.pitch, targetPitch, Time.deltaTime * pitchLerpSpeed);
        }
    }

    private void EnsureDebugVisualsInitialized()
    {
        if (debugVisualRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("TemperatureDebugVisuals");
        debugVisualRoot = root.transform;
        debugVisualRoot.SetParent(transform, false);
        debugVisualRoot.localPosition = Vector3.zero;
        debugVisualRoot.localRotation = Quaternion.identity;

        GameObject lineObject = new GameObject("RayLine");
        lineObject.transform.SetParent(debugVisualRoot, false);
        debugLine = lineObject.AddComponent<LineRenderer>();
        debugLine.positionCount = 2;
        debugLine.useWorldSpace = true;
        debugLine.startWidth = debugLineWidth;
        debugLine.endWidth = debugLineWidth;
        
        Material lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineMaterial.renderQueue = 2999;
        debugLine.material = lineMaterial;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugEnabled)
        {
            return;
        }

        var ray = new Ray(transform.position, transform.forward);
        Gizmos.color = debugMissColor;
        Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * rayDistance);
    }
}