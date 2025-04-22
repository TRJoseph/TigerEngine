namespace Chess
{
    public readonly struct GameState
    {
        public readonly sbyte capturedPieceType;
        public readonly byte enPassantFile;
        public readonly byte castlingRights;
        public readonly byte fiftyMoveCounter;
        public readonly ulong zobristHashKey;

        public const int ClearWhiteKingsideMask = 0b1110;
        public const int ClearWhiteQueensideMask = 0b1101;
        public const int ClearBlackKingsideMask = 0b1011;
        public const int ClearBlackQueensideMask = 0b0111;
       
        public GameState(sbyte capturedPieceType, byte enPassantFile, byte castlingRights, byte fiftyMoveCounter, ulong zobristHashKey)
        {
            this.capturedPieceType = capturedPieceType;
            this.enPassantFile = enPassantFile;
            this.castlingRights = castlingRights;
            this.fiftyMoveCounter = fiftyMoveCounter;
            this.zobristHashKey = zobristHashKey;
        }

        public bool HasKingsideCastleRight(bool white)
        {
            int mask = white ? 1 : 4;
            return (castlingRights & mask) != 0;
        }

        public bool HasQueensideCastleRight(bool white)
        {
            int mask = white ? 2 : 8;
            return (castlingRights & mask) != 0;
        }
    }
}

