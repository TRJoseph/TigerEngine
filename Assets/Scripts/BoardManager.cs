using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using TMPro;
using System.Linq;
using UnityEditor.PackageManager;

namespace Chess
{
    public class BoardManager : MonoBehaviour
    {
        // reference to game tile prefab
        [SerializeField] private Tile tilePrefab;

        [SerializeField] private Transform _cam;

        [SerializeField] public Engine engine;

        // this holds the UI elements for the scene
        public Canvas Canvas;

        // holds all the rank and file labels
        public List<TextMeshProUGUI> tileFileLabels = new List<TextMeshProUGUI>();
        public List<TextMeshProUGUI> tileRankLabels = new List<TextMeshProUGUI>();


        // this game object holds all the sprites for each chess piece
        [SerializeField] public GameObject chessPiecePrefab;

        // Forsyth-Edwards Notation representing positions in a chess game
        private readonly string FENString = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"; // starting position in chess

        // FEN string for testing pawn promotions
        //private readonly string FENString = "8/8/8/6P1/6p1/8/8/8";

        // second FEN string for testings king interactivity
        //private readonly string FENString = "4k3/8/7P/2b5/8/8/8/4K3";

        // another FEN string for testing king castling
        //private readonly string FENString = "r3k2r/p7/6N1/8/8/8/8/4K2R";

        // another FEN string for testing checkmate 
        //private readonly string FENString = "rnbqkbnr/p1pp1ppp/1p6/4p3/2B1P3/5Q2/PPPP1PPP/RNB1K1NR";

        // this will control the turn based movement, white moves first
        public static bool whiteToMove = true;

        public enum Sides
        {
            White = 0,
            Black = 1
        }

        // these will be updated and selected based on UI elements in some sort of main menu before the game is started
        public static Sides humanPlayer = Sides.White;

        public static Sides ComputerSide = Sides.White;

        public static Sides CurrentTurn = Sides.White;

        // Start is called before the first frame update
        void Start()
        {
            GenerateGrid();
            LoadFENString();
            CalculateDistanceToEdge();
            RenderPiecesOnBoard();

            Board.legalMoves = Board.GenerateLegalMoves();

            /* ChooseSide controls what side the player will play 
            For example, if Sides.White is passed in, the player will be able to control the white pieces
            and the engine will move the black pieces.
            If the goal is to have the engine play itself, comment out this ChooseSide function call below and
            comment out the 'SwapTurns' call from inside the 'AfterMove' method.

            If the goal is to let the human player make both white and black moves, just comment out the 
            'SwapTurns' call from inside the 'AfterMove' method.
            */
            ChooseSide(Sides.White);

            // TODO: this will likely get moved to some sort of button trigger on a UI main menu (starting the game)
            Board.currentState = Board.GameState.Normal;

            // The engine should be analyzing the position constantly whether or not its the engine's turn
            engine.StartThinking();
        }

        public void ChooseSide(Sides playerSide)
        {
            humanPlayer = playerSide;
            ComputerSide = (playerSide == Sides.White) ? Sides.Black : Sides.White; ;
        }

        void GenerateGrid()
        {
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

                    tile.SetTileColor(isLightSquare);

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

            _cam.transform.position = new Vector3((float)file / 2 - 0.5f, (float)rank / 2 - 0.5f, -10);

        }

        void LoadFENString()
        {
            // start at 7th rank and 0th file (top left of board)
            // (7th rank is actually 8th rank on board, 0th file is the a file)
            int file = 0;
            int rank = 7;

            // dictionary to hold the piece types
            var pieceType = new Dictionary<char, int>()
            {
                ['k'] = Piece.King,
                ['q'] = Piece.Queen,
                ['r'] = Piece.Rook,
                ['b'] = Piece.Bishop,
                ['n'] = Piece.Knight,
                ['p'] = Piece.Pawn
            };

            // loop through the FEN string
            for (int i = 0; i < FENString.Length; i++)
            {

                // if the character is a number
                if (char.IsDigit(FENString[i]))
                {
                    // skip that many files
                    file += int.Parse(FENString[i].ToString());
                }
                // if the character is a slash
                else if (FENString[i] == '/')
                {
                    // go to the next rank
                    rank--;
                    // reset the file
                    file = 0;
                }
                else if (char.IsLetter(FENString[i]))
                {
                    // get the piece type
                    int piece = pieceType[char.ToLower(FENString[i])];
                    // get the piece color
                    int pieceColor = char.IsUpper(FENString[i]) ? Piece.White : Piece.Black;

                    // represented in binary with or operator
                    Board.Squares[rank * 8 + file].encodedPiece = pieceColor | piece;

                    file++;
                }
            }

        }


        void CalculateDistanceToEdge()
        {

            // calculates from bottom left across each file, then up each rank
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int currentSquareIndex = rank * 8 + file;

                    Board.Squares[currentSquareIndex].DistanceNorth = 7 - rank;
                    Board.Squares[currentSquareIndex].DistanceSouth = rank;
                    Board.Squares[currentSquareIndex].DistanceWest = file;
                    Board.Squares[currentSquareIndex].DistanceEast = 7 - file;

                    Board.Squares[currentSquareIndex].DistanceNorthWest = Math.Min(Board.Squares[currentSquareIndex].DistanceNorth, Board.Squares[currentSquareIndex].DistanceWest);
                    Board.Squares[currentSquareIndex].DistanceNorthEast = Math.Min(Board.Squares[currentSquareIndex].DistanceNorth, Board.Squares[currentSquareIndex].DistanceEast);
                    Board.Squares[currentSquareIndex].DistanceSouthWest = Math.Min(Board.Squares[currentSquareIndex].DistanceSouth, Board.Squares[currentSquareIndex].DistanceWest);
                    Board.Squares[currentSquareIndex].DistanceSouthEast = Math.Min(Board.Squares[currentSquareIndex].DistanceSouth, Board.Squares[currentSquareIndex].DistanceEast);

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

        // render sprites onto board
        public void RenderPiecesOnBoard()
        {
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int encodedPiece = Board.Squares[rank * 8 + file].encodedPiece;

                    int decodedPieceColor = encodedPiece & 24;
                    int decodedPiece = encodedPiece & 7;

                    if (decodedPiece != Piece.Empty)
                    {
                        GameObject piece = Instantiate(chessPiecePrefab, new Vector3(file, rank, -1), Quaternion.identity);
                        PieceRender renderScript = piece.GetComponent<PieceRender>();
                        Sprite pieceSprite = GetSpriteForPiece(decodedPiece, decodedPieceColor, renderScript);
                        piece.GetComponent<SpriteRenderer>().sprite = pieceSprite;
                        renderScript.isWhitePiece = decodedPieceColor == Piece.White;

                        // this may be removed for a better alternative for sizing the pieces
                        piece.transform.localScale = new Vector3(0.125f, 0.125f, 1f);
                    }
                }
            }
        }

        public static Sprite GetSpriteForPiece(int decodedPiece, int decodedPieceColor, PieceRender renderScript)
        {
            switch (decodedPiece)
            {
                case Piece.Pawn:
                    return (decodedPieceColor == Piece.White) ? renderScript.whitePawn : renderScript.blackPawn;
                case Piece.Knight:
                    return (decodedPieceColor == Piece.White) ? renderScript.whiteKnight : renderScript.blackKnight;
                case Piece.Bishop:
                    return (decodedPieceColor == Piece.White) ? renderScript.whiteBishop : renderScript.blackBishop;
                case Piece.Rook:
                    return (decodedPieceColor == Piece.White) ? renderScript.whiteRook : renderScript.blackRook;
                case Piece.Queen:
                    return (decodedPieceColor == Piece.White) ? renderScript.whiteQueen : renderScript.blackQueen;
                case Piece.King:
                    return (decodedPieceColor == Piece.White) ? renderScript.whiteKing : renderScript.blackKing;
                default:
                    return null;  // For the 'Empty' piece or any unexpected value
            }
        }
    }

}
