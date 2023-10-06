using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Chess
{
    public class PieceMovementManager : MonoBehaviour
    {

        private bool isDragging;
        private Vector3 offset;

        private Vector3 originalPosition;


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
            if (GridManager.whiteToMove)
            {
                if (!transform.GetComponent<PieceRender>().isWhitePiece)
                {
                    return;
                }
            }
            else
            {
                if (transform.GetComponent<PieceRender>().isWhitePiece)
                {
                    return;
                }
            }

            isDragging = true;
            originalPosition = transform.position;
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

                // if occupied by friendly piece, snap back to original position


                GameObject pieceOnTile = closestSquare.GetComponent<Tile>().OccupyingPiece;


                // if user attempts to move a piece to the same tile, return
                if (pieceOnTile == transform.gameObject)
                {
                    return;
                }

                // if there is a piece on the tile
                if (pieceOnTile != null)
                {
                    if (GridManager.whiteToMove)
                    {
                        if (pieceOnTile.GetComponent<PieceRender>().isWhitePiece)
                        {
                            transform.position = originalPosition;
                            return;
                        }
                        else
                        {
                            // if occupied by enemy piece, destroy enemy piece and snap to tile
                            Destroy(pieceOnTile);

                            transform.position = new Vector3(closestSquare.position.x, closestSquare.position.y, transform.position.z);
                        }
                    }
                    else
                    {
                        // must be blacks move
                        if (!pieceOnTile.GetComponent<PieceRender>().isWhitePiece)
                        {
                            transform.position = originalPosition;
                            return;
                        }
                        else
                        {
                            // if occupied by enemy piece, destroy enemy piece and snap to tile
                            Destroy(pieceOnTile);

                            transform.position = new Vector3(closestSquare.position.x, closestSquare.position.y, transform.position.z);
                        }


                    }
                }

                // if occupied by enemy piece, destroy enemy piece and snap to tile

                // if there is no piece on the tile



                /* 
                PSA: Piece GameObjects have an associated Tile and Tile GameObjects have an associated piece,
                these are updated here when a move is made
                
                */

                // sets the new tile's occupying piece to the piece that was moved
                closestSquare.GetComponent<Tile>().OccupyingPiece = transform.GameObject();

                // removes the old tile's occupied piece because the piece has moved squares
                transform.GetComponent<PieceRender>().occupiedTile.OccupyingPiece = null;

                // sets the current new occupied tile (attached to the piece) to the new tile
                transform.GetComponent<PieceRender>().occupiedTile = closestSquare.GetComponent<Tile>();

                // snaps to closest square
                transform.position = new Vector3(closestSquare.position.x, closestSquare.position.y, transform.position.z);

                GridManager.whiteToMove = !GridManager.whiteToMove;
            }
        }
    }
}
