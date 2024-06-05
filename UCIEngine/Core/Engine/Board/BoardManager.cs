using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using static Chess.Board;
using static Chess.PositionInformation;
using static Chess.MoveGen;

namespace Chess
{
    public class BoardManager
    {

        // Forsyth-Edwards Notation representing positions in a chess game
        //private static readonly string FENString = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"; // starting position in chess

        // FEN string for testing draw rules
        //private static readonly string FENString = "3r1k2/8/8/8/8/3Q4/8/4K3 w - - 0 1";

        //private static readonly string FENString = "4kr2/8/8/8/5Q2/8/4K3/8 b - - 0 1";

        //private static readonly string FENString = "8/7k/5KR1/8/8/8/8/8 w - - 0 1";

        //private static readonly string FENString = "8/7K/5kr1/8/8/8/8/8 b - - 0 1";

        // test position from perft results page
        //private static readonly string FENString = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

        //private static readonly string FENString = "4k3/3p4/8/2K5/8/8/8/8 w - - 0 1";

        public static void InitializeLookupTables()
        {
            // initializes bishop and rook move lookup tables (every possible permutation of blocking pieces)
            InitBishopLookup();
            InitRookLookup();
        }

        public static void InitializeBoard(string FENString)
        {
            InitializeLookupTables();
            LoadFENString(FENString);
        }


        public static void LoadFENString(string FENString)
        {
            // ensure bitboards are cleared when loading in the FEN string
            InternalBoard.ResetBitboards();

            GameStartFENString = FENString;

            // start at 7th rank and 0th file (top left of board)
            // (7th rank is actually 8th rank on board, 0th file is the a file)
            int file = 0;
            int rank = 7;

            // dictionary to hold the piece types
            var pieceType = new Dictionary<char, int>()
            {
                ['k'] = ChessBoard.King,
                ['q'] = ChessBoard.Queen,
                ['r'] = ChessBoard.Rook,
                ['b'] = ChessBoard.Bishop,
                ['n'] = ChessBoard.Knight,
                ['p'] = ChessBoard.Pawn
            };

            string[] FENFields = FENString.Split(null);

            string pieceLocationField = FENFields[0];
            string activeColorField = FENFields[1];
            string castlingRightsField = FENFields[2];
            string enPassantTargetsField = FENFields[3];
            string halfMoveClockField = FENFields[4];
            string fullMoveNumberField = FENFields[5];

            // loop through the FEN string, parse piece location information
            for (int i = 0; i < pieceLocationField.Length; i++)
            {

                // if the character is a number
                if (char.IsDigit(pieceLocationField[i]))
                {
                    // skip that many files
                    file += int.Parse(pieceLocationField[i].ToString());
                }
                // if the character is a slash
                else if (pieceLocationField[i] == '/')
                {
                    // go to the next rank
                    rank--;
                    // reset the file
                    file = 0;
                }
                else if (char.IsLetter(pieceLocationField[i]))
                {
                    // get the piece type
                    int piece = pieceType[char.ToLower(pieceLocationField[i])];
                    // get the piece color
                    int pieceColor = char.IsUpper(pieceLocationField[i]) ? ChessBoard.White : ChessBoard.Black;

                    // places all pieces in appropriate bitboard locations
                    InitializeBitBoards(piece, pieceColor, rank * 8 + file);

                    file++;
                }
            }

            // parse active color
            if (activeColorField[0] == 'w')
            {
                whiteToMove = true;
            }
            else
            {
                whiteToMove = false;
            }

            // parse castling rights
            for (int i = 0; i < castlingRightsField.Length; i++)
            {
                switch (castlingRightsField[i])
                {
                    case 'K':
                        CastlingRights |= (int)CastlingRightsFlags.WhiteKingSide;
                        break;
                    case 'Q':
                        CastlingRights |= (int)CastlingRightsFlags.WhiteQueenSide;
                        break;
                    case 'k':
                        CastlingRights |= (int)CastlingRightsFlags.BlackKingSide;
                        break;
                    case 'q':
                        CastlingRights |= (int)CastlingRightsFlags.BlackQueenSide;
                        break;
                    default:
                        // case where there are no castling rights ('-')
                        break;
                }
            }
            if (enPassantTargetsField[0] == '-')
            {
                EnPassantFile = 0;
            }
            else
            {
                EnPassantFile = enPassantTargetsField[0] - 'a';
            }

            if (halfMoveClockField[0] == '-')
            {
                // do nothing
            }
            else
            {
                halfMoveAccumulator = int.Parse(halfMoveClockField[0].ToString());
            }

            if (fullMoveNumberField[0] == '-')
            {
                // do nothing
            }
            else
            {
                fullMoveAccumulator = int.Parse(halfMoveClockField[0].ToString());
            }

            // Initializes the initial physical locations of all the white pieces, black pieces, and every piece on the board
            InternalBoard.UpdateCompositeBitboards();
        }

        private static void InitializeBitBoards(int pieceType, int pieceColor, int currentPosition)
        {
            InternalBoard.Pieces[pieceColor, pieceType] |= 1UL << currentPosition;
        }

    }

}
