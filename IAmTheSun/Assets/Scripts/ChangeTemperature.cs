using UnityEngine;

public class ChangeTemperature : MonoBehaviour
{
    [SerializeField] private float rayDistance = 100f;
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private bool debugRay;
    [SerializeField] private bool debugOverlayInBuild;
    [SerializeField] private Vector3 debugOverlayLocalOffset = new Vector3(0f, -0.08f, 0.6f);
    [SerializeField] private bool followVRHead = true;

    private TransitionMaterials currentPlanetTransition;
    private Camera mainCamera;
    private LineRenderer debugLine;
    private TextMesh debugText;
    private Transform debugVisualRoot;

    private void Start()
    {
        TryResolveRayOrigin();
    }

    private void Update()
    {
        TryResolveRayOrigin();

        if (rayOrigin == null)
        {
            return;
        }

        // Suivre la tête du joueur en VR
        if (followVRHead && mainCamera != null)
        {
            transform.position = mainCamera.transform.position;
            transform.rotation = mainCamera.transform.rotation;
        }

        var ray = new Ray(rayOrigin.position, rayOrigin.forward);
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

        if (debugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * rayDistance, hitPlanetTransition != null ? Color.green : Color.red);
        }

        UpdateBuildDebugVisuals(ray, hasHit, hitInfo, hitPlanetTransition != null);

        if (hitPlanetTransition != currentPlanetTransition)
        {
            if (currentPlanetTransition != null)
            {
                currentPlanetTransition.ApplyOutside();
            }

            if (hitPlanetTransition != null)
            {
                hitPlanetTransition.ApplyInside();
            }

            currentPlanetTransition = hitPlanetTransition;
        }
    }

    private void TryResolveRayOrigin()
    {
        if (rayOrigin != null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null)
        {
            rayOrigin = mainCamera.transform;
        }
    }

    private void UpdateBuildDebugVisuals(Ray ray, bool hasHit, RaycastHit hitInfo, bool isPlanetHit)
    {
        if (!debugOverlayInBuild)
        {
            if (debugLine != null)
            {
                debugLine.enabled = false;
            }

            if (debugText != null)
            {
                debugText.gameObject.SetActive(false);
            }

            return;
        }

        EnsureDebugVisualsInitialized();
        if (debugLine == null || debugText == null)
        {
            return;
        }

        if (debugVisualRoot.parent != rayOrigin)
        {
            debugVisualRoot.SetParent(rayOrigin, false);
        }

        debugLine.enabled = true;
        debugText.gameObject.SetActive(true);

        Vector3 endPoint = hasHit ? hitInfo.point : (ray.origin + ray.direction * rayDistance);
        debugLine.SetPosition(0, ray.origin);
        debugLine.SetPosition(1, endPoint);

        Color rayColor = isPlanetHit ? Color.green : (hasHit ? Color.yellow : Color.red);
        debugLine.startColor = rayColor;
        debugLine.endColor = rayColor;

        string hitName = hasHit ? hitInfo.collider.name : "None";
        float distance = hasHit ? hitInfo.distance : rayDistance;
        debugText.text = $"Ray: {(isPlanetHit ? "PLANET" : (hasHit ? "HIT" : "MISS"))}\nHit: {hitName}\nDist: {distance:F2}m";
    }

    private void EnsureDebugVisualsInitialized()
    {
        if (rayOrigin == null || debugVisualRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("TemperatureDebugVisuals");
        debugVisualRoot = root.transform;
        debugVisualRoot.SetParent(rayOrigin, false);

        GameObject lineObject = new GameObject("RayLine");
        lineObject.transform.SetParent(debugVisualRoot, false);
        debugLine = lineObject.AddComponent<LineRenderer>();
        debugLine.positionCount = 2;
        debugLine.useWorldSpace = true;
        debugLine.startWidth = 0.003f;
        debugLine.endWidth = 0.003f;
        debugLine.material = new Material(Shader.Find("Sprites/Default"));

        GameObject textObject = new GameObject("RayText");
        textObject.transform.SetParent(debugVisualRoot, false);
        textObject.transform.localPosition = debugOverlayLocalOffset;
        textObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        debugText = textObject.AddComponent<TextMesh>();
        debugText.fontSize = 42;
        debugText.characterSize = 0.0018f;
        debugText.anchor = TextAnchor.MiddleCenter;
        debugText.alignment = TextAlignment.Center;
        debugText.color = Color.white;
    }
}
