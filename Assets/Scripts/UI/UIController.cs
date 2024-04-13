using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;
using static Chess.Board;
using static Chess.PositionInformation;
using static Chess.MoveGen;
using static Chess.Arbiter;

namespace Chess
{
    public class UIController : MonoBehaviour
    {
        public static UIController Instance;

        // this holds the UI elements for the scene
        public Canvas Canvas;

        // reference to game tile prefab
        [SerializeField] private Tile tilePrefab;

        [SerializeField] private Transform _cam;


        // Panel for useful display informatio
        public TextMeshProUGUI WhichPlayerMoveText;
        public TextMeshProUGUI SearchDepthText;
        public TextMeshProUGUI PositionsEvaluatedText;
        public TextMeshProUGUI NumCheckMatesText;
        public TextMeshProUGUI EvalText;

        public PieceMovementManager MovementManager;

        public GameObject PromotionPanel;

        [SerializeField] public GameObject chessPiecePrefab;

        // holds all the rank and file labels
        public List<TextMeshProUGUI> tileFileLabels = new List<TextMeshProUGUI>();
        public List<TextMeshProUGUI> tileRankLabels = new List<TextMeshProUGUI>();

        public Image[] pieceButtons; // References to the Image components of the buttons
        public Button[] promotionButtons;
        // this game object holds all the sprites for each chess piece

        public List<Sprite> whitePieceSprites; // Sprites for the white promotion pieces (queen, rook, bishop, knight)
        public List<Sprite> blackPieceSprites; // Sprites for the black promotion pieces

        public delegate void PromotionSelectedHandler(Move move);
        public static event PromotionSelectedHandler OnPromotionSelected;

        List<Move> currentPromotionMoves = new();

        void Start()
        {
            promotionButtons[0].onClick.AddListener(() => HandlePromotion(GetCurrentPromotionMove(PromotionFlags.PromoteToQueenFlag)));
            promotionButtons[1].onClick.AddListener(() => HandlePromotion(GetCurrentPromotionMove(PromotionFlags.PromoteToRookFlag)));
            promotionButtons[2].onClick.AddListener(() => HandlePromotion(GetCurrentPromotionMove(PromotionFlags.PromoteToBishopFlag)));
            promotionButtons[3].onClick.AddListener(() => HandlePromotion(GetCurrentPromotionMove(PromotionFlags.PromoteToKnightFlag)));

            // Subscribe to the promotion selected event
            OnPromotionSelected += HandlePromotionSelected;
        }

        private Move GetCurrentPromotionMove(PromotionFlags flag)
        {
            return currentPromotionMoves.Single(move => move.promotionFlag == flag);
        }
        private void HandlePromotionSelected(Move move)
        {
            Arbiter.DoTurn(move);
        }

        // This method is called when the user selects a promotion option
        public void PromotionSelected(Move move)
        {
            // Hide the promotion panel
            PromotionPanel.gameObject.SetActive(false);

            // Fire the promotion selected event
            OnPromotionSelected?.Invoke(move);
        }


        private void HandlePromotion(Move move)
        {
            PromotionSelected(move);
        }

        public void ShowPromotionDropdown(ulong toSquare, List<Move> savedPromotionMoves)
        {
            // this is responsible for updating the dropdown list with the correct corresponding promotion move choice
            currentPromotionMoves = savedPromotionMoves;

            PromotionPanel.gameObject.SetActive(true);

            PromotionPanel.gameObject.transform.position = new Vector3(((int)Math.Log(toSquare, 2) % 8) - 1, ((int)Math.Log(toSquare, 2) / 8) - 2, -2);

            List<Sprite> pieceSprites = whiteToMove ? whitePieceSprites : blackPieceSprites;
            for (int i = 0; i < pieceButtons.Length; i++)
            {
                pieceButtons[i].sprite = pieceSprites[i];
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }


        public void UpdateToMoveText()
        {
            if (whiteToMove)
            {
                WhichPlayerMoveText.text = "White to move";
            }
            else
            {
                WhichPlayerMoveText.text = "Black to move";
            }

        }

        public void UpdateSearchUIInfo(ref SearchInformation searchInformation)
        {
            SearchDepthText.text = "Search Depth: " + searchInformation.DepthSearched;
            PositionsEvaluatedText.text = "Positions Evaluated: " + searchInformation.PositionsEvaluated;
            NumCheckMatesText.text = "CheckMates Found: " + searchInformation.NumOfCheckMates;
            EvalText.text = "Evaluation: " + searchInformation.MoveEvaluationInformation.Evaluation;
        }

        public void GenerateGrid()
        {
            Canvas.transform.position = new Vector3(3.5f, 3.5f, 0);
            _cam.transform.position = new Vector3(3.5f, 3.5f, -10);

            int file = 0;
            int rank = 0;
            for (file = 0; file < 8; file++)
            {
                // instantiates labels for each file
                TextMeshProUGUI fileLabel = Instantiate(tileFileLabels[file]);
                fileLabel.transform.position = new Vector3(file, -1, -1);
                fileLabel.transform.SetParent(Canvas.transform, true);
                fileLabel.transform.localScale = new Vector3(1f, 1f, 1f);

                for (rank = 0; rank < 8; rank++)
                {
                    var tile = Instantiate(tilePrefab, new Vector3(file, rank, 0), Quaternion.identity);

                    bool isLightSquare = (file + rank) % 2 != 0;

                    tile.SetTileColor(!isLightSquare);

                    tile.name = $"Tile file: {file} rank: {rank}";

                    if (file == 0)
                    {
                        TextMeshProUGUI rankLabel = Instantiate(tileRankLabels[rank]);
                        rankLabel.transform.position = new Vector3(-1, rank, -1);
                        rankLabel.transform.SetParent(Canvas.transform, true);
                        rankLabel.transform.localScale = new Vector3(1f, 1f, 1f);
                    }
                }
            }

        }


        public void ClearExistingPieces()
        {
            var pieces = GameObject.FindGameObjectsWithTag("ChessPiece");
            foreach (var piece in pieces)
            {
                Destroy(piece);
            }
        }
        public void RenderPiecesOnBoard()
        {
            PlacePieces(ChessBoard.Pawn, ChessBoard.White);
            PlacePieces(ChessBoard.Knight, ChessBoard.White);
            PlacePieces(ChessBoard.Bishop, ChessBoard.White);
            PlacePieces(ChessBoard.Rook, ChessBoard.White);
            PlacePieces(ChessBoard.Queen, ChessBoard.White);
            PlacePieces(ChessBoard.King, ChessBoard.White);

            PlacePieces(ChessBoard.Pawn, ChessBoard.Black);
            PlacePieces(ChessBoard.Knight, ChessBoard.Black);
            PlacePieces(ChessBoard.Bishop, ChessBoard.Black);
            PlacePieces(ChessBoard.Rook, ChessBoard.Black);
            PlacePieces(ChessBoard.Queen, ChessBoard.Black);
            PlacePieces(ChessBoard.King, ChessBoard.Black);

        }

        private static void PlacePieces(int pieceType, int pieceColor)
        {
            for (int i = 0; i < BoardSize; i++)
            {
                if ((InternalBoard.Pieces[pieceColor, pieceType] & (1UL << i)) != 0)
                {
                    GameObject piece = Instantiate(Instance.chessPiecePrefab, new Vector3(i % 8, i / 8, -1), Quaternion.identity);
                    PieceRender renderScript = piece.GetComponent<PieceRender>();
                    Sprite pieceSprite = GetSpriteForPiece(pieceType, pieceColor, renderScript);
                    piece.GetComponent<SpriteRenderer>().sprite = pieceSprite;
                    renderScript.isWhitePiece = pieceColor == ChessBoard.White;

                    // this may be removed for a better alternative for sizing the pieces
                    piece.transform.localScale = new Vector3(0.125f, 0.125f, 1f);
                }
            }
        }

        public static Sprite GetSpriteForPiece(int pieceType, int pieceColor, PieceRender renderScript)
        {
            switch (pieceType)
            {
                case ChessBoard.Pawn:
                    return (pieceColor == ChessBoard.White) ? renderScript.whitePawn : renderScript.blackPawn;
                case ChessBoard.Knight:
                    return (pieceColor == ChessBoard.White) ? renderScript.whiteKnight : renderScript.blackKnight;
                case ChessBoard.Bishop:
                    return (pieceColor == ChessBoard.White) ? renderScript.whiteBishop : renderScript.blackBishop;
                case ChessBoard.Rook:
                    return (pieceColor == ChessBoard.White) ? renderScript.whiteRook : renderScript.blackRook;
                case ChessBoard.Queen:
                    return (pieceColor == ChessBoard.White) ? renderScript.whiteQueen : renderScript.blackQueen;
                case ChessBoard.King:
                    return (pieceColor == ChessBoard.White) ? renderScript.whiteKing : renderScript.blackKing;
                default:
                    return null;  // For the 'Empty' piece or any unexpected value
            }
        }



        public void StartComputerVsComputerGame()
        {
            StartCoroutine(ComputerVsComputerGameCoroutine());
        }


        private IEnumerator ComputerVsComputerGameCoroutine()
        {
            while (currentStatus == GameResult.InProgress)
            {

                if (whiteToMove)
                {
                    if (ComputerPlayer1.Side == Sides.White)
                    {
                        // engine 1
                        SearchInformation searchInformation = ComputerPlayer1.Engine.FixedDepthSearch(ComputerPlayer1.SearchDepth);

                        DoTurn(searchInformation.MoveEvaluationInformation.BestMove);

                        Instance.UpdateSearchUIInfo(ref searchInformation);
                    }
                    else
                    {
                        // engine 2 
                        SearchInformation searchInformation = ComputerPlayer2.Engine.FixedDepthSearch(ComputerPlayer2.SearchDepth);

                        DoTurn(searchInformation.MoveEvaluationInformation.BestMove);

                        Instance.UpdateSearchUIInfo(ref searchInformation);
                    }
                }
                else
                {
                    if (ComputerPlayer1.Side == Sides.Black)
                    {
                        SearchInformation searchInformation = ComputerPlayer1.Engine.FixedDepthSearch(ComputerPlayer1.SearchDepth);

                        DoTurn(searchInformation.MoveEvaluationInformation.BestMove);

                        Instance.UpdateSearchUIInfo(ref searchInformation);
                    }
                    else
                    {
                        SearchInformation searchInformation = ComputerPlayer2.Engine.FixedDepthSearch(ComputerPlayer2.SearchDepth);

                        DoTurn(searchInformation.MoveEvaluationInformation.BestMove);

                        Instance.UpdateSearchUIInfo(ref searchInformation);
                    }
                }

                // Wait for a second (or any suitable duration) before making the next move
                yield return new WaitForSeconds(0.5f);
            }
            // Handle end of game (display result, restart, etc.)
        }

    }
}

