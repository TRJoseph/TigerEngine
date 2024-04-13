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

        // engine references so these can be easily swapped our for testing different versions
        readonly Evaluation evaluation;
        readonly MoveSorting moveSorter;
        readonly SearchInformation searchInformation;

        readonly TranspositionTable transpositionTable;

        public MiniMaxEngineV0()
        {
            evaluation = new();
            searchInformation = new();
            moveSorter = new();
            transpositionTable = new();
        }

        const int infinity = 9999999;
        const int negativeInfinity = -infinity;

        public void IterativeDeepeningSearch()
        {

        }

        public SearchInformation FixedDepthSearch(int searchDepth)
        {
            searchInformation.PositionsEvaluated = 0;
            searchInformation.NumOfCheckMates = 0;
            searchInformation.DepthSearched = searchDepth;

            searchInformation.searchDiagnostics.stopwatch = Stopwatch.StartNew();

            searchInformation.MoveEvaluationInformation = FindBestMove(searchDepth);

            searchInformation.searchDiagnostics.stopwatch.Stop();
            searchInformation.searchDiagnostics.timeSpan = searchInformation.searchDiagnostics.stopwatch.Elapsed;
            searchInformation.searchDiagnostics.FormatElapsedTime();

            // this logs how long the fixed depth search took
            searchInformation.searchDiagnostics.LogElapsedTime();

            return searchInformation;
        }

        public Evaluation.MoveEvaluation FindBestMove(int depth)
        {
            int alpha = negativeInfinity;
            int beta = infinity;
            Move bestMove = new();

            Span<Move> moves = MoveGen.GenerateMoves();
            moveSorter.OrderMoveList(ref moves, depth);

            foreach (Move move in moves)
            {
                ExecuteMove(move);
                int eval = -NegaMax(depth - 1, -beta, -alpha); // Note the switch and use of alpha-beta here
                UndoMove(move);

                // intially thought pruning here would be unnecessary (I was being a schmuck), turns out its incredible effective at reducing node search count, lol
                if (eval > alpha) // Only update alpha if we found a better move
                {
                    alpha = eval;
                    bestMove = move;
                }

                if (eval >= beta)
                {
                    break;
                }
            }

            // Handle the case of no valid moves
            if (bestMove.IsDefault() && PositionInformation.currentStatus == GameResult.InProgress)
            {
                var random = new System.Random();
                bestMove = moves[random.Next(moves.Length)];
            }

            return new Evaluation.MoveEvaluation(bestMove, alpha);
        }


        public int NegaMax(int depth, int alpha, int beta)
        {

            if (depth == 0)
            {
                searchInformation.PositionsEvaluated++;
                return evaluation.EvaluatePosition();
            }

            Span<Move> moves = MoveGen.GenerateMoves();

            // order move list to place good moves at top of list
            moveSorter.OrderMoveList(ref moves, depth);

            GameResult gameResult = Arbiter.CheckForGameOverRules();
            if (gameResult == GameResult.Stalemate || gameResult == GameResult.ThreeFold || gameResult == GameResult.FiftyMoveRule || gameResult == GameResult.InsufficientMaterial)
            {
                return 0;
            }

            if (gameResult == GameResult.CheckMate)
            {
                searchInformation.NumOfCheckMates++;
                // prioritize the fastest mate
                return -100000 - depth;
            }

            foreach (Move move in moves)
            {
                ExecuteMove(move);
                // maintains symmetry; -beta is new alpha value for swapped perspective and likewise with -alpha; (upper and lower score safeguards)
                int eval = -NegaMax(depth - 1, -beta, -alpha);
                UndoMove(move);

                if (eval >= beta)
                {
                    int capturedPieceType = GetPieceAtSquare(PositionInformation.OpponentColorIndex, move.toSquare);
                    bool isCapture = capturedPieceType != ChessBoard.None;
                    // for quiet moves, we have a potential killer move

                    if (!isCapture)
                    {
                        moveSorter.killerMoves[depth, 1] = moveSorter.killerMoves[depth, 0];
                        moveSorter.killerMoves[depth, 0] = move;
                    }

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
    }

}