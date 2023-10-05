using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chess
{
    public class PieceMovementManager : MonoBehaviour
    {

        private bool isDragging;
        private Vector3 offset;


        // Start is called before the first frame update
        private void Update()
        {
            if (isDragging)
            {
                Vector3 newPosition = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z)) + offset;
                transform.position = new Vector3(newPosition.x, newPosition.y, transform.position.z);
            }
        }


        private void OnMouseDown()
        {
            isDragging = true;
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
            offset = transform.position - mousePos;
        }

        private void OnMouseUp()
        {
            isDragging = false;

            SnapToNearestSquare();
        }
        private void SnapToNearestSquare()
        {
            float closestDistance = float.MaxValue;
            Transform closestSquare = null;

            // Loop through all the square GameObjects in the scene
            foreach (var square in GameObject.FindGameObjectsWithTag("ChessTile"))
            {
                float distance = Vector3.Distance(transform.position, square.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestSquare = square.transform;
                }
            }

            // 
            if (closestSquare != null)
            {
                // check if tile is occupied by friendly piece or by enemy piece



                transform.position = new Vector3(closestSquare.position.x, closestSquare.position.y, transform.position.z);
                
            }
        }
    }
}
