using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using static Chess.Board;
using static Chess.Arbiter;

namespace Chess
{
    public class PositionInformation
    {

        public enum CastlingRightsFlags
        {
            WhiteKingSide = 0b0001,
            WhiteQueenSide = 0b0010,
            BlackKingSide = 0b0100,
            BlackQueenSide = 0b1000
        }

        public int CastlingRights;

        public int EnPassantFile;


        /* Game ending conditions */

        public bool kingInCheck;
        public int halfMoveAccumulator;

        public int fullMoveAccumulator;

        /* Game State information */
        public GameState CurrentGameState;

        public ulong ZobristHashKey => CurrentGameState.zobristHashKey;

        public string GameStartFENString;

        public GameResult currentStatus;

        public ulong[] pinMasks = new ulong[64];

        /* Side to move information */
        public bool whiteToMove;
        public int MoveColorIndex => whiteToMove ? Board.ChessBoard.White : Board.ChessBoard.Black;
        public int OpponentColorIndex => whiteToMove ? Board.ChessBoard.Black : Board.ChessBoard.White;

        public ulong MoveColorPieces => whiteToMove ? InternalBoard.AllWhitePieces : InternalBoard.AllBlackPieces;
        public ulong OpponentColorPieces => whiteToMove ? InternalBoard.AllBlackPieces : InternalBoard.AllWhitePieces;

        /* Move history information */

        // PositionHashes is for efficient checking of repeated positions (three fold repetition)
        public Stack<ulong> PositionHashes = new();

        public Stack<GameState> GameStateHistory = new();

    }

}