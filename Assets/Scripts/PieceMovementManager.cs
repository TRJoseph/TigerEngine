using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Chess
{
    public class PieceMovementManager : MonoBehaviour
    {

        // this holds the overlay that is placed on top of tiles to represent a legal move in the position
        [SerializeField] private GameObject highlightOverlayPrefab;

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
            // sets the original position of the piece for resetting if a move was not made
            originalPosition = transform.position;


            // this prevents white from attempting to make a move on blacks turn and vice versa
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

            List<Board.LegalMove> legalMoves = Board.CalculateLegalMoves(transform);

            DisplayLegalMoves(legalMoves);


            isDragging = true;
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
            offset = transform.position - mousePos;
        }

        private void OnMouseUp()
        {
            isDragging = false;

            Board.ClearListMoves();

            /* this might be moved to after SnapToNearestSquare, checking if a the 
             * move attempted was actually valid, determining whether or not to remove the highlights*/
            RemoveLegalMoveHighlights();

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


                GameObject pieceOnTile = closestSquare.GetComponent<Tile>().OccupyingPiece;


                // if user attempts to move a piece to the same tile, return
                if (pieceOnTile == transform.gameObject)
                {
                    transform.position = originalPosition;
                    return;
                }

                // if there is a piece on the tile
                if (pieceOnTile != null)
                {
                    if (GridManager.whiteToMove)
                    {
                        // if occupied by friendly piece, snap back to original position
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
                        // must be blacks move, if occupied by friendly piece, snap back to original position
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

                // update the internal board state when a move is made
                Board.UpdateInternalState(originalPosition.x, originalPosition.y, transform.position.x, transform.position.y);


                // TODO COME UP WITH BETTER WAY TO DO THIS
                // there will only be one instance of the UI controller so this is okay to do (for now)
                UIController.Instance.UpdateMoveStatusText(GridManager.whiteToMove);
            }
        }


        private void DisplayLegalMoves(List<Board.LegalMove> legalMoves)
        {
            foreach(Board.LegalMove move in legalMoves)
            {
                GameObject overlay = Instantiate(highlightOverlayPrefab, move.endTile.transform.position, Quaternion.identity);
                overlay.transform.SetParent(move.endTile.transform);
            }

        }


        private void RemoveLegalMoveHighlights() {
            GameObject[] highlights = GameObject.FindGameObjectsWithTag("Highlight");

            foreach(GameObject highlight in highlights)
            {
                Destroy(highlight);
            }
}
    }
}
