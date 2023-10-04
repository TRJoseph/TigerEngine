using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Chess
{
    public static class Piece
    {

        // from lowest to highest piece value
        public const int Empty = 0;
        public const int Pawn = 1;
        public const int Knight = 2;
        public const int Bishop = 3;
        public const int Rook = 4;
        public const int Queen = 5;
        public const int King = 6;

        public const int White = 8;
        public const int Black = 16;

    }
}