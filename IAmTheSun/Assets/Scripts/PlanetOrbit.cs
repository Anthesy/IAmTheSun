using UnityEngine;

public class PlanetOrbit : MonoBehaviour
{
    [SerializeField] private Vector3 orbitCenter = Vector3.zero;
    [SerializeField] private float orbitSpeed = 10f; // degrees per second
    [SerializeField] private Vector3 orbitPlaneNormal = Vector3.up; // Axis around which planet orbits (determines orbit plane orientation)
    [SerializeField] private bool debugDrawOrbit = false;
    [SerializeField] private int debugCircleSegments = 32;

    private float initialAngle;
    private float orbitRadius;
    private Vector3 orbitAxis1; // First axis in the orbit plane
    private Vector3 orbitAxis2; // Second axis in the orbit plane

    private void Start()
    {
        CalculateInitialOrbitValues();
    }

    private void CalculateInitialOrbitValues()
    {
        // Normalize orbit plane normal
        Vector3 normalizedPlaneNormal = orbitPlaneNormal.normalized;
        if (normalizedPlaneNormal.magnitude < 0.001f)
        {
            normalizedPlaneNormal = Vector3.up;
        }

        // Calculate two orthogonal axes in the orbit plane
        orbitAxis1 = Vector3.Cross(normalizedPlaneNormal, Vector3.right).normalized;
        if (orbitAxis1.magnitude < 0.001f)
        {
            orbitAxis1 = Vector3.Cross(normalizedPlaneNormal, Vector3.forward).normalized;
        }
        orbitAxis2 = Vector3.Cross(normalizedPlaneNormal, orbitAxis1).normalized;

        Vector3 directionFromCenter = transform.position - orbitCenter;
        orbitRadius = directionFromCenter.magnitude;

        if (orbitRadius > 0.001f)
        {
            // Calculate initial angle in the orbit plane
            float dotAxis1 = Vector3.Dot(directionFromCenter, orbitAxis1);
            float dotAxis2 = Vector3.Dot(directionFromCenter, orbitAxis2);
            initialAngle = Mathf.Atan2(dotAxis1, dotAxis2) * Mathf.Rad2Deg;
        }
        else
        {
            initialAngle = 0f;
            Debug.LogWarning($"PlanetOrbit on '{gameObject.name}': already at orbit center, using angle 0.", gameObject);
        }
    }

    private void Update()
    {
        if (orbitRadius < 0.001f)
        {
            return;
        }

        float currentAngle = initialAngle + orbitSpeed * Time.time;
        float angleRad = currentAngle * Mathf.Deg2Rad;

        // Position calculated using the two axes of the orbit plane
        Vector3 newPosition = orbitCenter 
            + Mathf.Cos(angleRad) * orbitAxis2 * orbitRadius
            + Mathf.Sin(angleRad) * orbitAxis1 * orbitRadius;

        transform.position = newPosition;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawOrbit)
        {
            return;
        }

        // Normalize orbit plane normal
        Vector3 normalizedPlaneNormal = orbitPlaneNormal.normalized;
        if (normalizedPlaneNormal.magnitude < 0.001f)
        {
            normalizedPlaneNormal = Vector3.up;
        }

        // Calculate two orthogonal axes in the orbit plane (same as in CalculateInitialOrbitValues)
        Vector3 axis1 = Vector3.Cross(normalizedPlaneNormal, Vector3.right).normalized;
        if (axis1.magnitude < 0.001f)
        {
            axis1 = Vector3.Cross(normalizedPlaneNormal, Vector3.forward).normalized;
        }
        Vector3 axis2 = Vector3.Cross(normalizedPlaneNormal, axis1).normalized;

        // Draw orbit circle
        Gizmos.color = Color.green;
        Vector3 center = orbitCenter;

        float distance = Vector3.Distance(transform.position, center);
        if (distance > 0.001f)
        {
            for (int i = 0; i < debugCircleSegments; i++)
            {
                float angle1 = (i / (float)debugCircleSegments) * 360f * Mathf.Deg2Rad;
                float angle2 = ((i + 1) / (float)debugCircleSegments) * 360f * Mathf.Deg2Rad;

                Vector3 point1 = center + Mathf.Cos(angle1) * axis2 * distance + Mathf.Sin(angle1) * axis1 * distance;
                Vector3 point2 = center + Mathf.Cos(angle2) * axis2 * distance + Mathf.Sin(angle2) * axis1 * distance;

                Gizmos.DrawLine(point1, point2);
            }

            // Draw center point
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(center, 0.5f);

            // Draw direction to planet
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(center, transform.position);

            // Draw orbit plane normal
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(center, center + normalizedPlaneNormal * distance * 0.3f);
        }
    }

    public void SetOrbitSpeed(float newSpeed)
    {
        orbitSpeed = newSpeed;
    }

    public void SetOrbitCenter(Vector3 newCenter)
    {
        orbitCenter = newCenter;
        CalculateInitialOrbitValues();
    }

    public void SetOrbitPlaneNormal(Vector3 newNormal)
    {
        orbitPlaneNormal = newNormal;
        CalculateInitialOrbitValues();
    }
}
