namespace Chess
{
    public static class BitBoardHelper
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

        // if only one piece is present in a bitboard array
        public static bool IsSoloPiece(ulong bitboard)
        {
            return (bitboard & (bitboard - 1)) == 0;
        }

    }

}