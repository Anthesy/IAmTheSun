using UnityEngine;

public class FollowVRHead : MonoBehaviour
{
    [SerializeField] private Transform targetToFollow;
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = true;

    private void Start()
    {
        // Si aucune cible n'est assignée, essayer de trouver le CenterEyeAnchor
        if (targetToFollow == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                targetToFollow = mainCamera.transform;
            }
        }
    }

    private void Update()
    {
        if (targetToFollow == null)
        {
            return;
        }

        if (followPosition)
        {
            transform.position = targetToFollow.position;
        }

        if (followRotation)
        {
            transform.rotation = targetToFollow.rotation;
        }
    }
}
