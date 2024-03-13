using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

namespace Chess
{
    public static class PositionInformation
    {

        public enum CastlingRightsFlags
        {
            WhiteKingSide = 0b0010,
            WhiteQueenSide = 0b0001,
            BlackKingSide = 0b1000,
            BlackQueenSide = 0b0100
        }

        public static int CastlingRights = 0;

        public static int potentialEnPassantCaptureSquare;


        public static bool enPassantFilePreviouslySet = false;

        public static int EnPassantFile = 0;


        /* Game ending conditions */
        public static bool kingInCheck;

        public static int halfMoveAccumulator;

        public static int fullMoveAccumulator;

        public static int threeFoldAccumulator = 0;


        /* Game State information */
        public static GameState CurrentGameState;

        public static ulong ZobristHashKey => CurrentGameState.zobristHashKey;

        public static string GameStartFENString;


        /* Side to move information */
        public static bool whiteToMove;
        public static int MoveColorIndex => whiteToMove ? Board.ChessBoard.White : Board.ChessBoard.Black;
        public static int OpponentColorIndex => whiteToMove ? Board.ChessBoard.Black : Board.ChessBoard.White;


        /* Move history information */

        // PositionHashes is for efficient checking of repeated positions (three fold repetition)
        public static Dictionary<ulong, int> PositionHashes = new();

        public static Stack<GameState> GameStateHistory = new();
    }

}