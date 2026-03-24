using System.Collections.Generic;
using UnityEngine;

namespace exo_01.Scripts
{
    public class Vaisseau : MonoBehaviour
    {
        public List<Transform> planetes; // Liste des planètes
        public float vitesseDeplacement = 10f; // Vitesse du vaisseau
        public float distanceMin = 10f; // Distance minimale pour changer de planète

        private Transform planeteCible; // La planète vers laquelle le vaisseau se dirige
        private float distance;

        void Start()
        {
            // Choisir deux planètes aléatoires au début
            ChoisirNouvelleDestination();
        }

        void Update()
        {
            // Se déplacer vers la planète cible
            if (planeteCible != null)
            {
                transform.position = Vector3.MoveTowards(transform.position, planeteCible.position, Time.deltaTime * vitesseDeplacement);
                transform.LookAt(planeteCible);

                // Calculer la distance entre le vaisseau et la planète cible
                distance = Vector3.Distance(transform.position, planeteCible.position);

                // Si le vaisseau est suffisamment proche, choisir une nouvelle destination
                if (distance < distanceMin)
                {
                    ChoisirNouvelleDestination();
                }
            }
        }

        void ChoisirNouvelleDestination()
        {
            // Sélectionner une nouvelle planète cible aléatoire différente de la précédente
            if (planetes.Count > 1)
            {
                Transform anciennePlanete = planeteCible; // Sauvegarder la planète actuelle pour éviter de la sélectionner à nouveau

                do
                {
                    planeteCible = planetes[Random.Range(0, planetes.Count)];
                } while (planeteCible == anciennePlanete); // Réessayer si la planète est la même que la précédente
            }
            else
            {
                Debug.LogError("Il doit y avoir au moins deux planètes dans la liste.");
            }
        }
    }
}