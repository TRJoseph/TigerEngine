using static Chess.Board;
using static Chess.PieceValues;
using System;
using Unity.VisualScripting;
using System.Text.RegularExpressions;

namespace Chess
{
    public class MoveSorting
    {

        const int maxDepth = 10;

        const int million = 1000000;
        const int winningCaptureBias = 8 * million;
        const int promoteBias = 6 * million;
        const int killerBias = 4 * million;
        const int losingCaptureBias = 2 * million;
        const int regularBias = 0;


        public Move[,] killerMoves;


        public struct MoveHeuristic
        {
            public int Score;
        }

        public MoveSorting()
        {
            // max depth x number of moves stored
            killerMoves = new Move[maxDepth, 2];
        }

        public bool IsKillerMove(Move move, int currentDepth)
        {
            if (MatchingMove(move, killerMoves[currentDepth, 0]) || MatchingMove(move, killerMoves[currentDepth, 1]))
            {
                return true;
            }
            return false;
        }

        public void OrderMoveList(ref Span<Move> moves, int currentDepth)
        {
            MoveHeuristic[] moveHeuristicList = new MoveHeuristic[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                int currentScore = regularBias; // Start with a base score for all moves

                int pieceMoveToSquare = BitBoardHelper.GetLSB(ref moves[i].toSquare);

                int capturedPieceType = GetPieceAtSquare(PositionInformation.OpponentColorIndex, moves[i].toSquare);
                int movePieceType = GetPieceAtSquare(PositionInformation.MoveColorIndex, moves[i].fromSquare);

                bool isCapture = capturedPieceType != ChessBoard.None;
                bool isPromotion = moves[i].IsPawnPromotion;

                if (isCapture)
                {
                    int capturedPieceValue = GetPieceValue(capturedPieceType);
                    int movedPieceValue = GetPieceValue(movePieceType);
                    int materialDelta = capturedPieceValue - movedPieceValue;
                    bool opponentCanRecapture = MoveGen.SquareAttackedBy(pieceMoveToSquare);

                    if (opponentCanRecapture)
                    {
                        currentScore += (materialDelta > 0 ? winningCaptureBias : losingCaptureBias) + materialDelta;
                    }
                    else
                    {
                        currentScore += winningCaptureBias + materialDelta;
                    }
                }

                if (isPromotion)
                {
                    currentScore += promoteBias + ConvertPromotionFlagToPieceValue(moves[i].promotionFlag);
                }

                if (IsKillerMove(moves[i], currentDepth)) currentScore += killerBias;

                //penalize a move for moving a piece where it can be attacked by an opponent pawn
                if (MoveGen.SquareAttackedByPawn(pieceMoveToSquare))
                {
                    currentScore -= GetPieceValue(movePieceType);
                }

                moveHeuristicList[i].Score = currentScore;
            }

            // Sort moves based on their heuristic score, quicksort is O(nlogn)
            QuickSort(ref moveHeuristicList, ref moves, 0, moves.Length - 1);
        }


        public void QuickSort(ref MoveHeuristic[] moveHeuristicList, ref Span<Move> moves, int low, int high)
        {
            if (low < high)
            {
                int pivotIndex = Partition(ref moveHeuristicList, ref moves, low, high);

                QuickSort(ref moveHeuristicList, ref moves, low, pivotIndex - 1);
                QuickSort(ref moveHeuristicList, ref moves, pivotIndex + 1, high);
            }
        }

        private int Partition(ref MoveHeuristic[] moveHeuristicList, ref Span<Move> moves, int low, int high)
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