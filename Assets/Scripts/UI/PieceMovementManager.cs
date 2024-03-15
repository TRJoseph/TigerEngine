using System;
using System.Collections;
using System.Data.SqlTypes;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using static Chess.Board;
using static Chess.PositionInformation;

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
            if (whiteToMove)
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

            if (selectedPiece != null) { SnapToNearestSquare(legalMove); }

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
                HandleMovePlayed(selectedPiece, closestSquare);
            }
        }

        private static bool IsPawnPromotion(ulong toSquare, int movedPiece)
        {
            if (movedPiece != ChessBoard.Pawn)
            {
                return false;
            }

            if (toSquare >> 8 == 0 || toSquare << 8 == 0)
            {
                return true;
            }

            return false;
        }

        private void HandleMovePlayed(GameObject selectedPiece, Transform closestSquare)
        {
            int fromSquareIndex = (int)originalPosition.y * 8 + (int)originalPosition.x;
            int toSquareIndex = (int)selectedPiece.transform.position.y * 8 + (int)selectedPiece.transform.position.x;

            ulong fromSquare = 1UL << fromSquareIndex;
            ulong toSquare = 1UL << toSquareIndex;

            // Get all moves that match the from and to squares
            var matchingMoves = legalMoves.Where(move => move.fromSquare == fromSquare && move.toSquare == toSquare).ToList();

            if (matchingMoves.Count == 0)
            {
                Debug.Log("Error: No matching moves found for this played move!");
                return;
            }

            Move move;
            if (matchingMoves.Count > 1)
            {
                // This implies promotion moves are possible, handle accordingly
                if (BoardManager.CurrentTurn == BoardManager.ComputerSide)
                {
                    var promotionFlag = UpdatePromotedPawnEngine();
                    move = matchingMoves.First(m => m.promotionFlag == promotionFlag);
                    DoMove(move);
                }
                else
                {
                    var savedPromotionMoves = legalMoves.Where(move => move.fromSquare == fromSquare && move.toSquare == toSquare && move.IsPawnPromotion).ToList();
                    // SavedMoveForPromotionBase = new MoveBase { fromSquare = fromSquare, toSquare = toSquare };
                    UIController.Instance.ShowPromotionDropdown(toSquare, savedPromotionMoves);
                    // The actual selection of the promotionFlag is handled by the promotion dropdown, after the user move input
                }
            }
            else
            {
                // Only one matching move, so it's not a promotion or it's a non-pawn move
                move = matchingMoves.Single();
                DoMove(move);
            }
        }


        public void DoMove(Move move)
        {
            ExecuteMove(move);

            UIController.Instance.ClearExistingPieces();
            UIController.Instance.RenderPiecesOnBoard();

            legalMoves = GenerateMoves();
        }


        public void HandleEngineMoveExecution(Move move)
        {
            // update the internal board state when a move is made by the computer
            ExecuteMove(move);

            UIController.Instance.ClearExistingPieces();
            UIController.Instance.RenderPiecesOnBoard();

            //HandleGameStateAfterMove();
            legalMoves = GenerateMoves();
        }

        public static void UpdateFrontEndPromotion(int pieceType, int xPos, int yPos)
        {

            PieceRender renderScript = FindChessPieceGameObject(xPos, yPos);

            Sprite pieceSprite = UIController.GetSpriteForPiece(pieceType, whiteToMove ? ChessBoard.White : ChessBoard.Black, renderScript);

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

            int selectedPieceSquare = (int)yPos * 8 + (int)xPos;

            foreach (var move in legalMoves)
            {
                int legalmoveSquare = (int)Math.Log(move.fromSquare, 2);
                if (selectedPieceSquare == legalmoveSquare)
                {
                    Vector2 newPos2 = new Vector2((int)Math.Log(move.toSquare, 2) % 8, (int)Math.Log(move.toSquare, 2) / 8);
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
