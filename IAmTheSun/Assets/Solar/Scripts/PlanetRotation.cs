using UnityEngine;

namespace exo_01.Scripts
{
    public class PlanetRotation : MonoBehaviour
    {
        public Transform centre;

        [Space(10)]
    
        [Header("Vitesse")]
        public float minVitesseRotation = 10f, minVitesseTournerAutour = 10f, maxVitesseRotation = 10f, maxVitesseTournerAutour = 30f;

        [Space(10)]
    
        [Header("Direction random de rotation")]
        private Vector3 rotationDirection = Vector3.forward;
    
        private float vitesseRotation;
        private float vitesseTournerAutour;

        void Start()
        {
            vitesseRotation = Random.Range(minVitesseRotation, maxVitesseRotation);
            vitesseTournerAutour = Random.Range(minVitesseTournerAutour, maxVitesseTournerAutour);
        
            float randomAngle = Random.Range(0f, 360f);
            transform.RotateAround(centre.position, Vector3.up, randomAngle);
            if (GetComponent<TrailRenderer>() != null)
            {
                GetComponent<TrailRenderer>().enabled = true;
            }
        }

        void Update()
        {
            transform.Rotate(rotationDirection * Time.deltaTime * vitesseRotation);
        
            transform.RotateAround(centre.position, Vector3.up, vitesseTournerAutour * Time.deltaTime);
        }
    }
}