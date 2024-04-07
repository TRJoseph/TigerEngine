using static Chess.Board;
using static Chess.PieceValues;
using System;
using Unity.VisualScripting;

namespace Chess
{
    public static class MoveSorting
    {

        const int million = 1000000;
        const int winningCaptureBias = 8 * million;
        const int promoteBias = 6 * million;
        const int killerBias = 4 * million;
        const int losingCaptureBias = 2 * million;
        const int regularBias = 0;

        public struct MoveHeuristic
        {
            public int Score;
        }


        public static void OrderMoveList(ref Span<Move> moves)
        {
            MoveHeuristic[] moveHeuristicList = new MoveHeuristic[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                int currentScore = regularBias; // Start with a base score for all moves

                int capturedPieceType = GetPieceAtSquare(PositionInformation.OpponentColorIndex, moves[i].toSquare);
                int movePieceType = GetPieceAtSquare(PositionInformation.MoveColorIndex, moves[i].fromSquare);

                bool isCapture = capturedPieceType != ChessBoard.None;
                bool isPromotion = moves[i].IsPawnPromotion;

                if (isCapture)
                {
                    int capturedPieceValue = GetPieceValue(capturedPieceType);
                    int movedPieceValue = GetPieceValue(movePieceType);
                    int materialDelta = capturedPieceValue - movedPieceValue;

                    currentScore += winningCaptureBias + materialDelta;
                }

                if (isPromotion)
                {
                    currentScore += promoteBias + ConvertPromotionFlagToPieceValue(moves[i].promotionFlag);
                }

                // if (IsKillerMove(moves[i])) currentScore += killerBias;

                moveHeuristicList[i].Score = currentScore;
            }

            // Sort moves based on their heuristic score
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