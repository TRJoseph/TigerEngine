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

        public BoardManager boardManager;

        private GameObject selectedPiece;

        private bool isDragging;

        private Vector3 offset;
        private Vector3 originalPosition;

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
            if (BoardManager.whiteToMove)
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
            if (closestSquare != null)
            {
                // check if tile is occupied by friendly piece or by enemy piece
                GameObject pieceOnTile = closestSquare.GetComponent<Tile>().OccupyingPiece;


                // if user attempts an illegal move, return
                if (!legalMove)
                {
                    selectedPiece.transform.position = originalPosition;
                    selectedPiece = null;
                    return;
                }

                selectedPiece.transform.position = new Vector3(closestSquare.transform.position.x, closestSquare.transform.position.y, closestSquare.transform.position.z);

                if (pieceOnTile != null)
                {
                    Destroy(pieceOnTile);
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
            // update the internal board state when a move is made
            Board.UpdateInternalState((int)originalPosition.x, (int)originalPosition.y, (int)selectedPiece.transform.position.x, (int)selectedPiece.transform.position.y);

            boardManager.ClearExistingPieces();
            boardManager.RenderPiecesOnBoard();

            // update who's move it is
            BoardManager.whiteToMove = !BoardManager.whiteToMove;

            if (Board.currentState == Board.GameState.Normal)
            {
                // wipe the available moves once a move is executed
                Board.ClearListMoves();

                Board.legalMoves = Board.AfterMove(BoardManager.whiteToMove);

                BoardManager.ComputerMove = true;

                // TODO COME UP WITH BETTER WAY TO DO THIS
                // there will only be one instance of the UI controller so this is okay to do (for now)
                UIController.Instance.UpdateMoveStatusUIInformation();
            }
        }

        public void HandleEngineMoveExecution(Board.LegalMove legalMove)
        {

            int originalXPosition = legalMove.startSquare % 8;
            int originalYPosition = legalMove.startSquare / 8;

            int newXPosition = legalMove.endSquare % 8;
            int newYPosition = legalMove.endSquare / 8;

            // update the internal board state when a move is made
            Board.UpdateInternalState(originalXPosition, originalYPosition, newXPosition, newYPosition);

            boardManager.ClearExistingPieces();
            boardManager.RenderPiecesOnBoard();

            // update who's move it is
            BoardManager.whiteToMove = !BoardManager.whiteToMove;

            if (Board.currentState == Board.GameState.Normal)
            {
                // wipe the available moves once a move is executed
                Board.ClearListMoves();

                Board.legalMoves = Board.AfterMove(BoardManager.whiteToMove);

                BoardManager.ComputerMove = !BoardManager.ComputerMove;

                // TODO COME UP WITH BETTER WAY TO DO THIS
                // there will only be one instance of the UI controller so this is okay to do (for now)
                UIController.Instance.UpdateMoveStatusUIInformation();
            }
        }

        public static void UpdateFrontEndPromotion(int encodedPiece, int xPos, int yPos)
        {
            int decodedPieceColor = encodedPiece & 24;
            int decodedPiece = encodedPiece & 7;

            PieceRender renderScript = FindChessPieceGameObject(xPos, yPos);

            Sprite pieceSprite = BoardManager.GetSpriteForPiece(decodedPiece, decodedPieceColor, renderScript);

            renderScript.GetComponent<SpriteRenderer>().sprite = pieceSprite;
        }

        private static PieceRender FindChessPieceGameObject(int xPos, int yPos)
        {
            // Assuming each piece has a script that holds its board position and type
            foreach (var piece in FindObjectsOfType<PieceRender>())
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
            foreach (var tile in FindObjectsOfType<Tile>())
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
