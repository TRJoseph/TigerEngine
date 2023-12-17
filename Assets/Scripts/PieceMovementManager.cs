using System;
using System.Collections;
using System.Data.SqlTypes;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

namespace Chess
{
    public class PieceMovementManager : MonoBehaviour
    {

        // this holds the overlay that is placed on top of tiles to represent a legal move in the position
        [SerializeField] private GameObject highlightOverlayPrefab;

        private GameObject selectedPiece;

        private bool isDragging;
        private Vector3 offset;

        private Vector3 originalPosition;

        void Start()
        {
            /* uncommenting this line (and commenting out the Board.CalculateAllLegalMoves here) might fix issues with 
            castling using FEN Strings with a starting position that includes an attack of the opponents potential castling moves */
            //Board.AfterMove(GridManager.whiteToMove);

            // no need to calculate opponent moves here as the game just started, no possible checks on first move
            Board.CalculateAllLegalMoves(GridManager.whiteToMove);
        }

        void Update()
        {
            if (isDragging && selectedPiece != null)
            {
                Vector3 newPosition = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z)) + offset;
                selectedPiece.transform.position = new Vector3(newPosition.x, newPosition.y, selectedPiece.transform.position.z);
            }

            // Detect mouse down events
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                // this is detecting if the user actually selects a piece game object
                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.collider.gameObject.GetComponent<PieceRender>() != null)
                    {
                        OnPieceMouseDown(hit.collider.gameObject);
                    }
                }
            }

            // Detect mouse up events
            if (Input.GetMouseButtonUp(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                bool legalMove = false;

                // this segment intiates a 2d raycast in game to check if the user lets go of the mouse over a legal move for any given piece
                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.collider.tag == "Highlight")
                    {
                        legalMove = true;
                    }
                    OnPieceMouseUp(legalMove);
                }

            }
        }


        void OnPieceMouseDown(GameObject piece)
        {
            selectedPiece = piece;
            originalPosition = selectedPiece.transform.position;

            // Implement your conditions and logic
            if (GridManager.whiteToMove)
            {
                if (!selectedPiece.GetComponent<PieceRender>().isWhitePiece)
                {
                    return;
                }
            }
            else
            {
                if (selectedPiece.GetComponent<PieceRender>().isWhitePiece)
                {
                    return;
                }
            }


            DisplayLegalMoves(selectedPiece.transform.position.x, selectedPiece.transform.position.y);

            isDragging = true;
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
            offset = selectedPiece.transform.position - mousePos;
        }

        private void OnPieceMouseUp(bool legalMove)
        {
            isDragging = false;

            /* this might be moved to after SnapToNearestSquare, checking if a the 
             * move attempted was actually valid, determining whether or not to remove the highlights*/
            RemoveLegalMoveHighlights();

            SnapToNearestSquare(legalMove);

            selectedPiece = null;
        }

        private void SnapToNearestSquare(bool legalMove)
        {
            float closestDistance = float.MaxValue;
            Transform closestSquare = null;

            // Loop through all the square GameObjects in the scene
            foreach (var square in GameObject.FindGameObjectsWithTag("ChessTile"))
            {
                float distance = Vector3.Distance(selectedPiece.transform.position, square.transform.position);
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


                // if user attempts to move a piece to the same tile or move is not legal, return
                // (TODO: checking for piece on tile might not actually be necessary anymore with legal move checks, needs testing)
                if (pieceOnTile == selectedPiece.transform.gameObject || !legalMove)
                {
                    selectedPiece.transform.position = originalPosition;
                    selectedPiece = null;
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
                            selectedPiece.transform.position = originalPosition;
                            return;
                        }
                        else
                        {
                            // if occupied by enemy piece, destroy enemy piece and snap to tile
                            Destroy(pieceOnTile);

                            selectedPiece.transform.position = new Vector3(closestSquare.position.x, closestSquare.position.y, transform.position.z);
                        }
                    }
                    else
                    {
                        // must be blacks move, if occupied by friendly piece, snap back to original position
                        if (!pieceOnTile.GetComponent<PieceRender>().isWhitePiece)
                        {
                            selectedPiece.transform.position = originalPosition;
                            return;
                        }
                        else
                        {
                            // if occupied by enemy piece, destroy enemy piece and snap to tile
                            Destroy(pieceOnTile);

                            selectedPiece.transform.position = new Vector3(closestSquare.position.x, closestSquare.position.y, transform.position.z);
                        }


                    }
                }

                // this is reached when a move is made
                HandleMoveExecution(selectedPiece, closestSquare);

            }
        }

        private void HandleMoveExecution(GameObject selectedPiece, Transform closestSquare)
        {

            /* 
            PSA: Piece GameObjects have an associated Tile and Tile GameObjects have an associated piece,
            these are updated here when a move is made

            */

            // sets the new tile's occupying piece to the piece that was moved
            closestSquare.GetComponent<Tile>().OccupyingPiece = selectedPiece.transform.GameObject();

            // removes the old tile's occupied piece because the piece has moved squares
            selectedPiece.transform.GetComponent<PieceRender>().occupiedTile.OccupyingPiece = null;

            // sets the current new occupied tile (attached to the piece) to the new tile
            selectedPiece.transform.GetComponent<PieceRender>().occupiedTile = closestSquare.GetComponent<Tile>();

            // snaps to closest square
            selectedPiece.transform.position = new Vector3(closestSquare.position.x, closestSquare.position.y, transform.position.z);


            // update who's move it is
            GridManager.whiteToMove = !GridManager.whiteToMove;

            // update the internal board state when a move is made
            Board.UpdateInternalState(originalPosition.x, originalPosition.y, selectedPiece.transform.position.x, selectedPiece.transform.position.y);

            // wipe the available moves once a move is executed
            Board.ClearListMoves();

            Board.AfterMove(GridManager.whiteToMove);

            // TODO COME UP WITH BETTER WAY TO DO THIS
            // there will only be one instance of the UI controller so this is okay to do (for now)
            UIController.Instance.UpdateMoveStatusText(GridManager.whiteToMove);
        }

        private static void DoCastle(int oldRookXPos, int oldRookYPos, bool doKingSideCastle)
        {

            int newRookXpos = doKingSideCastle ? oldRookXPos - 2 : oldRookXPos + 3;

            // grab piece, if null, break expression
            PieceRender Rook = FindChessPieceGameObject(oldRookXPos, oldRookYPos) ?? throw new Exception();

            // grab tile, if null, break expression
            Tile newTile = FindTileGameObject(newRookXpos, oldRookYPos) ?? throw new Exception();

            // set current rook tile (which will now be the old tile in the corner) to null (empty)
            Rook.occupiedTile.OccupyingPiece = null;

            // set rook piece's occupied tile to the new tile that it is now on
            Rook.occupiedTile = newTile;
            Rook.transform.position = new Vector3(newRookXpos, oldRookYPos);

            // set new tile's currently occupied piece to the kingside rook
            newTile.OccupyingPiece = Rook.gameObject;
        }

        public static void UpdateFrontEndSpecialMove(int oldRookXPos, int oldRookYPos, bool doKingSideCastle, bool doQueenSideCastle)
        {
            /* This function will control updating the front end board with any special moves that are played (castling, en passant) */
            if (doKingSideCastle)
            {
                DoCastle(oldRookXPos, oldRookYPos, true);
            }

            if (doQueenSideCastle)
            {
                DoCastle(oldRookXPos, oldRookYPos, false);
            }

        }

        private static PieceRender FindChessPieceGameObject(int xPos, int yPos)
        {
            // Assuming each piece has a script that holds its board position and type
            foreach (var piece in GameObject.FindObjectsOfType<PieceRender>())
            {
                if (piece.transform.position.x == xPos && piece.transform.position.y == yPos)
                {
                    return piece;
                }
            }
            return null;
        }

        private static Tile FindTileGameObject(int xPos, int yPos)
        {
            foreach (var tile in GameObject.FindObjectsOfType<Tile>())
            {
                if (tile.transform.position.x == xPos && tile.transform.position.y == yPos)
                    return tile;
            }

            return null;
        }

        private void DisplayLegalMoves(float xPos, float yPos)
        {

            var selectedPiece = Board.Squares[(int)yPos * 8 + (int)xPos];

            int selectedPieceSquare = (int)yPos * 8 + (int)xPos;

            foreach (var move in Board.legalMoves)
            {
                if (selectedPieceSquare == move.startSquare)
                {
                    Vector2 newPos2 = new Vector2(move.endSquare % 8, move.endSquare / 8);
                    Instantiate(highlightOverlayPrefab, newPos2, Quaternion.identity);
                }
            }
        }

        private void RemoveLegalMoveHighlights()
        {
            GameObject[] highlights = GameObject.FindGameObjectsWithTag("Highlight");

            foreach (GameObject highlight in highlights)
            {
                Destroy(highlight);
            }
        }
    }
}
