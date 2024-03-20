using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using TMPro;
using static Chess.Board;
using static Chess.PositionInformation;

namespace Chess
{
    public class BoardManager : MonoBehaviour
    {
        [SerializeField] public Engine engine;

        // Forsyth-Edwards Notation representing positions in a chess game
        private readonly string FENString = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"; // starting position in chess

        // FEN string for testing draw rules
        //private readonly string FENString = "n1n5/PPPk4/8/8/8/8/4Kppp/5N1N b - - 0 1"; // starting position in chess

        //private readonly string FENString = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

        // Start is called before the first frame update
        void Start()
        {
            InitializeLookupTables();
            // loads position
            LoadPosition();
        }

        public void InitializeLookupTables()
        {
            // initializes bishop and rook move lookup tables (every possible permutation of blocking pieces)
            InitBishopLookup();
            InitRookLookup();
        }

        public void LoadPosition()
        {
            // load in fen string and set position information
            LoadFENString();

            UIController.Instance.GenerateGrid();
            UIController.Instance.RenderPiecesOnBoard();
            UIController.Instance.UpdateMoveStatusUIInformation();

            // generates zobrist hash key
            ZobristHashing.GenerateZobristHashes();

            // Set game state (note: calculating zobrist key relies on current game state)
            CurrentGameState = new GameState(0, PositionInformation.EnPassantFile, PositionInformation.CastlingRights, PositionInformation.halfMoveAccumulator, 0);
            ulong zobristHashKey = ZobristHashing.InitializeHashKey();
            CurrentGameState = new GameState(0, PositionInformation.EnPassantFile, PositionInformation.CastlingRights, PositionInformation.halfMoveAccumulator, zobristHashKey);

            GameStateHistory.Push(CurrentGameState);

            // run performance tests
            //Verification.RunPerformanceTests(6);

            Arbiter.StartGame();
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

        private void InitializeBitBoards(int pieceType, int pieceColor, int currentPosition)
        {
            InternalBoard.Pieces[pieceColor, pieceType] |= 1UL << currentPosition;
        }

    }

}
