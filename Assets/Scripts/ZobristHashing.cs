using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Chess.Board;


namespace Chess
{
    public static class ZobristHashing
    {
        // https://www.chessprogramming.org/Zobrist_Hashing

        // one number for each piece (12) at each square (64)
        public static readonly ulong[,] pieceAtEachSquareArray = new ulong[12, 64];

        public static readonly ulong sideToMove;

        // no castling, kingside, queenside, both
        public static readonly ulong[] castlingRightsArray = new ulong[16];

        public static readonly ulong[] enPassantFile = new ulong[8];

    }
}