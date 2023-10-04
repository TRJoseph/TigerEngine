using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chess
{
    public class GridManager : MonoBehaviour
    {
        // reference to game tile prefab
        [SerializeField] private Tile tilePrefab;

        [SerializeField] private Transform _cam;
        private string FENString = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"; // starting position in chess

        // Start is called before the first frame update
        void Start()
        {
            GenerateGrid();
        }

        void GenerateGrid()
        {
            int file = 0;
            int rank = 0;
            for (file = 0; file < 8; file++)
            {
                for (rank = 0; rank < 8; rank++)
                {
                    var tile = Instantiate(tilePrefab, new Vector3(file, rank, 0), Quaternion.identity);

                    bool isLightSquare = (file + rank) % 2 != 0;

                    tile.SetTileColor(isLightSquare);

                    tile.name = $"Tile {file} {rank}";
                }
            }

            _cam.transform.position = new Vector3((float)file / 2 - 0.5f, (float)rank / 2 - 0.5f, -10);

        }

        void LoadFENString()
        {
            // start at 7th rank and 0th file (top left of board)
            int file = 0;
            int rank = 7;

            // dictionary to hold the piece types
            var pieceType = new Dictionary<char, int>()
            {
                ['k'] = Chess.Piece.King,
                ['q'] = Chess.Piece.Queen,
                ['r'] = Chess.Piece.Rook,
                ['b'] = Chess.Piece.Bishop,
                ['n'] = Chess.Piece.Knight,
                ['p'] = Chess.Piece.Pawn
            };

            // loop through the FEN string
            for (int i = 0; i < FENString.Length; i++)
            {

                // if the character is a number
                if (Char.IsDigit(FENString[i]))
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
                else if (Char.IsLetter(FENString[i]))
                {
                    // get the piece type
                    int piece = pieceType[Char.ToLower(FENString[i])];
                    // get the piece color
                    int pieceColor = Char.IsUpper(FENString[i]) ? Chess.Piece.White : Chess.Piece.Black;

                    // represented in binary with or operator
                    Chess.Board.Squares[rank * 8 + file] = piece | pieceColor;

                    file++;
                }
            }

        }

        // render sprites onto board
        void RenderPiecesOnBoard()
        {

        }

    }

}
