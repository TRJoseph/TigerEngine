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
        public Board board;
        public BoardManager(Board board)
        {
            this.board = board;
        }
        public void InitializeLookupTables()
        {
            // initializes bishop and rook move lookup tables (every possible permutation of blocking pieces)
            MoveTables.InitBishopLookup();
            MoveTables.InitRookLookup();
        }

        public void InitializeBoard(string FENString)
        {
            InitializeLookupTables();
            LoadFENString(FENString);
        }


        public void LoadFENString(string FENString)
        {
            // ensure bitboards are cleared when loading in the FEN string
            InternalBoard.ResetBitboards();

            board.posInfo.GameStartFENString = FENString;

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
                board.posInfo.whiteToMove = true;
            }
            else
            {
                board.posInfo.whiteToMove = false;
            }

            // parse castling rights
            for (int i = 0; i < castlingRightsField.Length; i++)
            {
                switch (castlingRightsField[i])
                {
                    case 'K':
                        board.posInfo.CastlingRights |= (int)CastlingRightsFlags.WhiteKingSide;
                        break;
                    case 'Q':
                        board.posInfo.CastlingRights |= (int)CastlingRightsFlags.WhiteQueenSide;
                        break;
                    case 'k':
                        board.posInfo.CastlingRights |= (int)CastlingRightsFlags.BlackKingSide;
                        break;
                    case 'q':
                        board.posInfo.CastlingRights |= (int)CastlingRightsFlags.BlackQueenSide;
                        break;
                    default:
                        // case where there are no castling rights ('-')
                        break;
                }
            }
            if (enPassantTargetsField[0] == '-')
            {
                board.posInfo.EnPassantFile = 0;
            }
            else
            {
                board.posInfo.EnPassantFile = enPassantTargetsField[0] - 'a' + 1;
            }

            if (halfMoveClockField[0] == '-')
            {
                // do nothing
            }
            else
            {
                board.posInfo.halfMoveAccumulator = int.Parse(halfMoveClockField[0].ToString());
            }

            if (fullMoveNumberField[0] == '-')
            {
                // do nothing
            }
            else
            {
                board.posInfo.fullMoveAccumulator = int.Parse(halfMoveClockField[0].ToString());
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
