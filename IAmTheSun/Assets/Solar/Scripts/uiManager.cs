using UnityEngine;

namespace exo_01.Scripts
{
    public class uiManager : MonoBehaviour
    {
        public CanvasGroup canvasGroup;
        public bool controlActivation = true;
        //public float speed = 1.0f;


        void Start()
        {

        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("QUIT");
                Application.Quit();
            }
            if (Input.GetKeyUp(KeyCode.Space))
            {
                if (controlActivation)
                {
                    canvasGroup.alpha = 0f;
                    //transform.localPosition = Vector3.left * Time.deltaTime * speed;
                    controlActivation = false;
                }
                else if (!controlActivation)
                {
                    canvasGroup.alpha = 1f;
                    //transform.localPosition = Vector3.right * Time.deltaTime * speed;
                    controlActivation = true;
                }

            }
        }
    }
}
