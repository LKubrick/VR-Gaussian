using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float rotationSpeed = .2f;
    public float distance = 2.5f;
    public Transform target;
    public float zoomSpeed = 0.05f;
    private Vector3 lastMousePosition;
    private Vector3 lastDirection;
    private void Start()
    {
        lastDirection = (transform.position - target.position);
        lastDirection.Normalize();
    }

    private void Update()
    {
        if(Input.GetMouseButton(0))
        {
            //Récupérer la position actuelle de la souris
            Vector3 currentMousePosition = Input.mousePosition;

            if (lastMousePosition != Vector3.zero)
            {
                //Calculer la différence de la position 
                float diffx = currentMousePosition.x - lastMousePosition.x;
                float diffy = currentMousePosition.y - lastMousePosition.y;

                //Calcul l'angle de rotation 
                float rAX = diffx * rotationSpeed /** Time.deltaTime*/;
                float rAY = -diffy * rotationSpeed /** Time.deltaTime*/;

                //Appliquer la rotation
                transform.RotateAround(target.position, Vector3.up,rAX);
                transform.RotateAround(target.position, Vector3.right, rAY);

                //Camera regarde le target
                transform.LookAt(target);
                var direction = (transform.position - target.position);
                direction.Normalize();
                lastDirection = direction;
                
            }

            //Mettre à jour la dernière position de la souris
            lastMousePosition = currentMousePosition;
        }
        else
        {
            //Rénitialiser la dernière position de la souris
            lastMousePosition = Vector3.zero;
        }
        if(Input.mouseScrollDelta.y != 0)
        {
            distance -= Input.mouseScrollDelta.y * zoomSpeed;
            distance = Mathf.Clamp(distance, 0.8f, 8);
        }
        Vector3 newPos = target.position + lastDirection * distance;
        newPos = new Vector3(newPos.x, Mathf.Clamp(newPos.y, 0.05f, 7), newPos.z);
        transform.position = newPos;
    }
}
