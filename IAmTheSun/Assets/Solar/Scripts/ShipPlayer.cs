using Cinemachine;
using UnityEngine;

namespace exo_01.Scripts
{
    public class ShipPlayer : MonoBehaviour
    {
        [Space(40)]
        [Header("Contrôles du vaisseau")]
        public float vitesseDeplacementVaisseau = 100f;
        public float accelerationMultiplier  = 100f;
        public float vitesseRotationVaisseau = 100f;
        [Space(20)]
        public float currentSpeed = 0f;
        public float maxSpeed = 300f;
    
        [Space(40)]
        [Header("Zoom caméra")]
    public CinemachineVirtualCamera gameCamera;
        [Space(20)]
        public float zoomSpeed = 10f;
        public float minY = 0.2f;
        public float maxY = 2f;
        [Space(20)]
        public float smoothSpeed = 0.1f;
        public float rotationSmoothSpeed = 0.1f;

        [Space(40)]
        private float targetY;
        private CinemachineTransposer transposer;

        void Start()
        {
            if (gameCamera == null)
            {
                Debug.LogWarning("ShipPlayer: gameCamera n'est pas assignée.");
                return;
            }

            transposer = gameCamera.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer == null)
            {
                Debug.LogWarning("ShipPlayer: aucun CinemachineTransposer trouvé sur la caméra.");
                return;
            }

            targetY = transposer.m_FollowOffset.y;
        }

        void Update()
        {
            // Accélération vaisseau
            if (Input.GetButton("Fire3"))
            {
                currentSpeed += accelerationMultiplier * Time.deltaTime;
            }
            else
            {
                currentSpeed -= accelerationMultiplier * Time.deltaTime;
            }
            currentSpeed = Mathf.Clamp(currentSpeed, vitesseDeplacementVaisseau, maxSpeed);
        
            float verticalInput = Input.GetAxis("Vertical");
            float horizontalInput = Input.GetAxis("Horizontal");
        
            // Vaisseau avance
            if (verticalInput > 0)
            {
                transform.Translate(Vector3.forward * Time.deltaTime * currentSpeed);
            }
        
            // Vaisseau recule
            if (verticalInput < 0)
            {
                transform.Translate(Vector3.back * Time.deltaTime * currentSpeed);
            
            }
            // Vaisseau tourne à droite
            if (horizontalInput > 0)
            {
                transform.Rotate(Vector3.up * Time.deltaTime * vitesseRotationVaisseau);
            }
            // Vaisseau tourne à gauche
            if (horizontalInput < 0)
            {
                transform.Rotate(Vector3.down * Time.deltaTime * vitesseRotationVaisseau);
            }

            // Zoom
            if (Input.GetKey(KeyCode.V))
            {
                targetY -= zoomSpeed * Time.deltaTime;
            }
        
            // Dé-zoom
            if (Input.GetKey(KeyCode.C))
            {    
                targetY += zoomSpeed * Time.deltaTime;
            }

            // Smoothness du suivi de la caméra 
            if (gameCamera != null && transposer != null)
            {
                // Zoom
                targetY = Mathf.Clamp(targetY, minY, maxY);
                Vector3 currentOffset = transposer.m_FollowOffset;
                float newY = Mathf.Lerp(currentOffset.y, targetY, smoothSpeed);
                transposer.m_FollowOffset = new Vector3(currentOffset.x, newY, currentOffset.z);

                // Rotation actuelle de la caméra
                Quaternion currentRotation = gameCamera.transform.rotation;
            
                // Rotation de la caméra sur l'axe Y
                float currentYRotation = transform.eulerAngles.y;
                Quaternion targetRotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentYRotation, currentRotation.eulerAngles.z);
                gameCamera.transform.rotation = Quaternion.Lerp(currentRotation, targetRotation, rotationSmoothSpeed);
            }
        }
    }
}
