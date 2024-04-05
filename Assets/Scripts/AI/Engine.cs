using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using static Chess.Board;
using System.Diagnostics.Tracing;
using UnityEditor.Experimental.GraphView;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;

namespace Chess
{

    public interface IChessEngine
    {
        Evaluation.MoveEvaluation FindBestMove(int depth);

        SearchInformation FixedDepthSearch(int searchDepth);
    }

    public class RandomMoveEngine : IChessEngine
    {
        readonly SearchInformation SearchInformation = new();
        public SearchInformation FixedDepthSearch(int searchDepth)
        {
            SearchInformation.MoveEvaluationInformation = FindBestMove(searchDepth);

            return SearchInformation;
        }
        public Evaluation.MoveEvaluation FindBestMove(int depth)
        {

            var random = new System.Random();

            return new Evaluation.MoveEvaluation(legalMoves[random.Next(Board.legalMoves.Count())], 0);
        }
    }

    public class MiniMaxEngineV0 : IChessEngine
    {
        const int infinity = 9999999;
        const int negativeInfinity = -infinity;

        readonly SearchInformation SearchInformation = new();

        public void IterativeDeepeningSearch()
        {

        }

        public SearchInformation FixedDepthSearch(int searchDepth)
        {
            SearchInformation.PositionsEvaluated = 0;
            SearchInformation.NumOfCheckMates = 0;
            SearchInformation.DepthSearched = searchDepth;

            SearchInformation.searchDiagnostics.stopwatch = Stopwatch.StartNew();

            SearchInformation.MoveEvaluationInformation = FindBestMove(searchDepth);

            SearchInformation.searchDiagnostics.stopwatch.Stop();
            SearchInformation.searchDiagnostics.timeSpan = SearchInformation.searchDiagnostics.stopwatch.Elapsed;
            SearchInformation.searchDiagnostics.FormatElapsedTime();

            // this logs how long the fixed depth search took
            SearchInformation.searchDiagnostics.LogElapsedTime();

            return SearchInformation;
        }

        public Evaluation.MoveEvaluation FindBestMove(int depth)
        {
            int bestEval = negativeInfinity;
            Move bestMove = new();

            Span<Move> moves = MoveGen.GenerateMoves();

            foreach (Move move in moves)
            {
                ExecuteMove(move);
                int eval = -NegaMax(depth - 1, negativeInfinity, infinity); // Switch to the other player's perspective for the next depth.
                UndoMove(move);

                if (eval > bestEval)
                {
                    bestEval = eval;
                    bestMove = move;
                }
            }

            // in the rare case that a move is not available pick a random move
            if (bestMove.IsDefault() && PositionInformation.currentStatus == GameResult.InProgress)
            {
                var random = new System.Random();
                bestMove = moves[random.Next(moves.Length)];
            }

            return new Evaluation.MoveEvaluation(bestMove, bestEval);
        }

        public int NegaMax(int depth, int alpha, int beta)
        {

            if (depth == 0)
            {
                SearchInformation.PositionsEvaluated++;
                return Evaluation.SimpleEval();
            }

            Span<Move> moves = MoveGen.GenerateMoves();

            // order move list to place good moves at top of list
            OrderMoveList(ref moves);

            GameResult gameResult = Arbiter.CheckForGameOverRules();
            if (gameResult == GameResult.Stalemate || gameResult == GameResult.ThreeFold || gameResult == GameResult.FiftyMoveRule || gameResult == GameResult.InsufficientMaterial)
            {
                return 0;
            }

            if (gameResult == GameResult.CheckMate)
            {
                SearchInformation.NumOfCheckMates++;
                // prioritize the fastest mate
                return negativeInfinity - depth;
            }


            foreach (Move move in moves)
            {
                ExecuteMove(move);
                // maintains symmetry; -beta is new alpha value for swapped perspective and likewise with -alpha; (upper and lower score safeguards)
                int eval = -NegaMax(depth - 1, -beta, -alpha);
                UndoMove(move);

                if (eval >= beta)
                {
                    // prune branch, black or white had a better path earlier on in the tree
                    return beta;
                }
                if (eval > alpha)
                {
                    alpha = eval;
                }
            }
            return alpha;
        }


        public void OrderMoveList(ref Span<Move> moves)
        {
            for (int i = 0; i < currentMoveIndex; i++)
            {


            }

        }

    }
}