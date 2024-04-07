using static Chess.Board;
using static Chess.PieceValues;
using System;
using Unity.VisualScripting;

namespace Chess
{
    public static class MoveSorting
    {
        // tier 1, 2, 3, 4; tier 1 contains moves that are most likely to be good, t4, contains moves that are less likely to be as good
        public enum MoveHeuristicType
        {
            // MVV LVA - most valuable victim, least valuable attacker
            Captures = 1,
            PromotionsAndChecks = 2,
            PositionalMoves = 3,
            Other = 4
        }

        public struct MoveHeuristic
        {
            public int Score;
        }


        public static void OrderMoveList(ref Span<Move> moves)
        {
            MoveHeuristic[] moveHeuristicList = new MoveHeuristic[256];
            for (int i = 0; i < moves.Length; i++)
            {
                int currentScore = 0;

                int capturedPieceType = GetPieceAtSquare(PositionInformation.OpponentColorIndex, moves[i].toSquare);
                int movePieceType = GetPieceAtSquare(PositionInformation.MoveColorIndex, moves[i].fromSquare);

                // if move was a capture
                if (capturedPieceType != ChessBoard.None)
                {
                    int capturedPieceValue = GetPieceValue(capturedPieceType);
                    int movedPieceValue = GetPieceValue(movePieceType);
                    // captures are tier 1, automatically prioritized over the rest
                    currentScore = 10 * capturedPieceValue - movedPieceValue;
                }

                if (moves[i].IsPawnPromotion)
                {
                    // pawn promotions are tier 2

                    currentScore += ConvertPromotionFlagToPieceValue(moves[i].promotionFlag);

                    // further score additions for promotion pieces that are likely a good choice (queen over rook for example)
                }

                moveHeuristicList[i].Score = currentScore;

            }

            // sorts the list in O(nlogn)
            QuickSort(ref moveHeuristicList, ref moves, 0, moves.Length - 1);

        }

        public static void QuickSort(ref MoveHeuristic[] moveHeuristicList, ref Span<Move> moves, int low, int high)
        {
            if (low < high)
            {
                int pivotIndex = Partition(ref moveHeuristicList, ref moves, low, high);

                QuickSort(ref moveHeuristicList, ref moves, low, pivotIndex - 1);
                QuickSort(ref moveHeuristicList, ref moves, pivotIndex + 1, high);
            }
        }

        private static int Partition(ref MoveHeuristic[] moveHeuristicList, ref Span<Move> moves, int low, int high)
        {
            // Choosing the last element in the segment as the pivot
            int pivot = moveHeuristicList[high].Score;
            int i = (low - 1); // Index of smaller element

            for (int j = low; j < high; j++)
            {
                // If current element's score is greater or equal to the pivot
                if (moveHeuristicList[j].Score >= pivot)
                {
                    i++;

                    // Swap moveHeuristicList[i] and moveHeuristicList[j]
                    (moveHeuristicList[j], moveHeuristicList[i]) = (moveHeuristicList[i], moveHeuristicList[j]);

                    // Perform the same swap on the moves Span to keep them in sync
                    (moves[j], moves[i]) = (moves[i], moves[j]);
                }
            }

            // Swap moveHeuristicList[i + 1] and moveHeuristicList[high] (or pivot)
            (moveHeuristicList[high], moveHeuristicList[i + 1]) = (moveHeuristicList[i + 1], moveHeuristicList[high]);

            // Do the same for the moves Span
            (moves[high], moves[i + 1]) = (moves[i + 1], moves[high]);
            return i + 1; // Return the partitioning index
        }


    }
}