using System;
using System.Linq;
using System.Collections.Generic;

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

        public static int potentialEnPassantCaptureFile = 0;

        // These flags are for game-ending conditions
        public static bool kingInCheck;

        public static int halfMoveAccumulator;

        public static int fullMoveAccumulator;

        public static int threeFoldAccumulator = 0;

        public static Stack<ulong> MoveHistory = new();

        // this will control the turn based movement, white moves first
        public static bool whiteToMove;


    }

}