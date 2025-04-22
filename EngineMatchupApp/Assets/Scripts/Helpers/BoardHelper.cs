using System;

namespace Chess
{
    public static class BoardHelper
    {
        // masks to prevent A file and H file wrapping for legal move calculations
        public const ulong AFileMask = 0x7f7f7f7f7f7f7f7f;
        public const ulong HFileMask = 0xfefefefefefefefe;

        private const ulong deBruijn64 = 0x37E84A99DAE458F;
        private static readonly int[] deBruijnTable =
        {
            0, 1, 17, 2, 18, 50, 3, 57,
            47, 19, 22, 51, 29, 4, 33, 58,
            15, 48, 20, 27, 25, 23, 52, 41,
            54, 30, 38, 5, 43, 34, 59, 8,
            63, 16, 49, 56, 46, 21, 28, 32,
            14, 26, 24, 40, 53, 37, 42, 7,
            62, 55, 45, 31, 13, 39, 36, 6,
            61, 44, 12, 35, 60, 11, 10, 9
        };

        // Get index of least significant set bit in given 64bit value. Also clears the bit to zero.
        public static int GetLSB(ref ulong b)
        {
            int i = deBruijnTable[((ulong)((long)b & -(long)b) * deBruijn64) >> 58];
            return i;
        }

        public static bool ContainsSquare(ulong bitboard, ulong targetSquare)
        {
            return (bitboard & targetSquare) != 0;
        }

        public const int a1 = 0;
        public const int b1 = 1;
        public const int c1 = 2;
        public const int d1 = 3;
        public const int e1 = 4;
        public const int f1 = 5;
        public const int g1 = 6;
        public const int h1 = 7;

        public const int a2 = 8;
        public const int b2 = 9;
        public const int c2 = 10;
        public const int d2 = 11;
        public const int e2 = 12;
        public const int f2 = 13;
        public const int g2 = 14;
        public const int h2 = 15;

        public const int a3 = 16;
        public const int b3 = 17;
        public const int c3 = 18;
        public const int d3 = 19;
        public const int e3 = 20;
        public const int f3 = 21;
        public const int g3 = 22;
        public const int h3 = 23;

        public const int a4 = 24;
        public const int b4 = 25;
        public const int c4 = 26;
        public const int d4 = 27;
        public const int e4 = 28;
        public const int f4 = 29;
        public const int g4 = 30;
        public const int h4 = 31;

        public const int a5 = 32;
        public const int b5 = 33;
        public const int c5 = 34;
        public const int d5 = 35;
        public const int e5 = 36;
        public const int f5 = 37;
        public const int g5 = 38;
        public const int h5 = 39;

        public const int a6 = 40;
        public const int b6 = 41;
        public const int c6 = 42;
        public const int d6 = 43;
        public const int e6 = 44;
        public const int f6 = 45;
        public const int g6 = 46;
        public const int h6 = 47;

        public const int a7 = 48;
        public const int b7 = 49;
        public const int c7 = 50;
        public const int d7 = 51;
        public const int e7 = 52;
        public const int f7 = 53;
        public const int g7 = 54;
        public const int h7 = 55;

        public const int a8 = 56;
        public const int b8 = 57;
        public const int c8 = 58;
        public const int d8 = 59;
        public const int e8 = 60;
        public const int f8 = 61;
        public const int g8 = 62;
        public const int h8 = 63;

        public static int GetSquareIndex(string square)
        {
            return square switch
            {
                "a1" => a1,
                "b1" => b1,
                "c1" => c1,
                "d1" => d1,
                "e1" => e1,
                "f1" => f1,
                "g1" => g1,
                "h1" => h1,
                "a2" => a2,
                "b2" => b2,
                "c2" => c2,
                "d2" => d2,
                "e2" => e2,
                "f2" => f2,
                "g2" => g2,
                "h2" => h2,
                "a3" => a3,
                "b3" => b3,
                "c3" => c3,
                "d3" => d3,
                "e3" => e3,
                "f3" => f3,
                "g3" => g3,
                "h3" => h3,
                "a4" => a4,
                "b4" => b4,
                "c4" => c4,
                "d4" => d4,
                "e4" => e4,
                "f4" => f4,
                "g4" => g4,
                "h4" => h4,
                "a5" => a5,
                "b5" => b5,
                "c5" => c5,
                "d5" => d5,
                "e5" => e5,
                "f5" => f5,
                "g5" => g5,
                "h5" => h5,
                "a6" => a6,
                "b6" => b6,
                "c6" => c6,
                "d6" => d6,
                "e6" => e6,
                "f6" => f6,
                "g6" => g6,
                "h6" => h6,
                "a7" => a7,
                "b7" => b7,
                "c7" => c7,
                "d7" => d7,
                "e7" => e7,
                "f7" => f7,
                "g7" => g7,
                "h7" => h7,
                "a8" => a8,
                "b8" => b8,
                "c8" => c8,
                "d8" => d8,
                "e8" => e8,
                "f8" => f8,
                "g8" => g8,
                "h8" => h8,
                _ => throw new ArgumentException("Invalid board square"),
            };
        }
    }

}