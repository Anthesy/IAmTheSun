using UnityEngine;

namespace exo_01.Scripts
{
    public class Asteroids : MonoBehaviour
    {
        public float vitesseRotationAsteroïde = 10f;
        public GameObject asteroidPrefab;
        public int numberOfAsteroids = 10;

        void Start()
        {
            for (int i = 0; i < numberOfAsteroids; i++)
            {
                // Rotation, position et scale aléatoire
                Vector3 axe = new Vector3(Random.Range(-90f, 90f), Random.Range(-90f, 90f), Random.Range(-90f, 90f));
                Vector3 randomPosition = new Vector3(Random.Range(-500f, 500f), Random.Range(-20f, 20f), Random.Range(-500f, 500f));
                float s = Random.Range(3f, 10f) ;
                Vector3 randomScale = new Vector3(s,s,s);
            
                // Instancie l'astéroïde
                GameObject asteroid = Instantiate(asteroidPrefab, randomPosition, Quaternion.identity);

                asteroid.transform.SetParent(gameObject.transform);

                // Ajout d'un script de rotation
                AsteroidRotator rotator = asteroid.AddComponent<AsteroidRotator>();
                rotator.SetRotationAxis(axe);
                rotator.SetScale(randomScale);
            
                rotator.SetRotationSpeed(vitesseRotationAsteroïde);
            }
        }
    }

    public class AsteroidRotator : MonoBehaviour
    {
        private Vector3 rotationAxis;
    
        private float rotationSpeed;
    
        // Angle de rotation
        public void SetRotationAxis(Vector3 axis)
        {
            rotationAxis = axis;
        }
    
        // Scale
        public void SetScale(Vector3 randomScale)
        {
            transform.localScale = randomScale ;
        }
    
    
        // Vitesse de rotation
        public void SetRotationSpeed(float speed)
        {
            rotationSpeed = speed;
        }
    

        void Update()
        {
            transform.Rotate(rotationAxis * Time.deltaTime * rotationSpeed);
        }
    }
}