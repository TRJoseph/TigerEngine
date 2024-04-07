using static Chess.Board;

namespace Chess
{
    public static class PieceValues
    {
        // in centipawn values
        public const int PawnValue = 100;
        public const int KnightValue = 300;
        public const int BishopValue = 300;
        public const int RookValue = 500;
        public const int QueenValue = 900;

        
        public static int GetPieceValue(int pieceType)
        {
            return pieceType switch
            {
                ChessBoard.Pawn => PawnValue,
                ChessBoard.Knight => KnightValue,
                ChessBoard.Bishop => BishopValue,
                ChessBoard.Rook => RookValue,
                ChessBoard.Queen => QueenValue,
                _ => 0,
            };
        }

        public static int ConvertPromotionFlagToPieceValue(PromotionFlags? promotionFlag)
        {
            return promotionFlag switch
            {
                PromotionFlags.PromoteToQueenFlag => QueenValue,
                PromotionFlags.PromoteToRookFlag => RookValue,
                PromotionFlags.PromoteToBishopFlag => BishopValue,
                PromotionFlags.PromoteToKnightFlag => KnightValue,
                _ => 0
            };
        }
    }

}