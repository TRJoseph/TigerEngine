using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using TMPro;

namespace Chess
{
    public class GridManager : MonoBehaviour
    {
        // reference to game tile prefab
        [SerializeField] private Tile tilePrefab;

        [SerializeField] private Transform _cam;


        // this holds the UI elements for the scene
        public Canvas Canvas;

        // holds all the rank and file labels
        public List<TextMeshProUGUI> tileFileLabels = new List<TextMeshProUGUI>();
        public List<TextMeshProUGUI> tileRankLabels = new List<TextMeshProUGUI>();


        // this game object holds all the sprites for each chess piece
        public GameObject chessPiecePrefab;

        // Forsyth-Edwards Notation representing positions in a chess game
        private readonly string FENString = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"; // starting position in chess


        // this array holds all the tiles in the game
        public static Tile[,] chessTiles = new Tile[8, 8];

        // this will control the turn based movement
        public static bool whiteToMove = true;

        // Start is called before the first frame update
        void Start()
        {
            GenerateGrid();
            LoadFENString();
            CalculateDistanceToEdge();
            RenderPiecesOnBoard();
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

                    chessTiles[file, rank] = tile;

                    if(file  == 0)
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
                    Board.Squares[rank * 8 + file] = pieceColor | piece;

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
                    // Board.Squares[rank * 8 + file];
                    chessTiles[file, rank].distances.DistanceNorth = 7 - rank;
                    chessTiles[file, rank].distances.DistanceSouth = rank;
                    chessTiles[file, rank].distances.DistanceWest = file;
                    chessTiles[file, rank].distances.DistanceEast = 7 - file;

                    chessTiles[file, rank].distances.DistanceNorthWest = Math.Min(chessTiles[file, rank].distances.DistanceNorth, chessTiles[file, rank].distances.DistanceWest);
                    chessTiles[file, rank].distances.DistanceNorthEast = Math.Min(chessTiles[file, rank].distances.DistanceNorth, chessTiles[file, rank].distances.DistanceEast);
                    chessTiles[file, rank].distances.DistanceSouthWest = Math.Min(chessTiles[file, rank].distances.DistanceSouth, chessTiles[file, rank].distances.DistanceWest);
                    chessTiles[file, rank].distances.DistanceSouthEast = Math.Min(chessTiles[file, rank].distances.DistanceSouth, chessTiles[file, rank].distances.DistanceEast);

                }
            }


        }

        // render sprites onto board
        void RenderPiecesOnBoard()
        {
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    int encodedPiece = Board.Squares[rank * 8 + file];

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

                        // set tile to occupied by piece
                        chessTiles[file, rank].OccupyingPiece = piece;

                        // set piece to occupy tile
                        renderScript.occupiedTile = chessTiles[file, rank];
                    }
                }
            }
        }

        private Sprite GetSpriteForPiece(int decodedPiece, int decodedPieceColor, PieceRender renderScript)
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
