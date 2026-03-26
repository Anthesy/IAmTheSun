using UnityEngine;

public class AlignParticleSystemShapeToVRHead : MonoBehaviour
{
    [SerializeField] private ParticleSystem particleSystem;
    [SerializeField] private Transform targetToFollow;

    private void Start()
    {
        // Si aucun ParticleSystem n'est assigné, essayer de le trouver sur cet objet
        if (particleSystem == null)
        {
            particleSystem = GetComponent<ParticleSystem>();
        }

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
        if (particleSystem == null || targetToFollow == null)
        {
            return;
        }

        // Orienter seulement la shape du système de particules
        var shape = particleSystem.shape;
        shape.rotation = targetToFollow.eulerAngles;
    }
}
