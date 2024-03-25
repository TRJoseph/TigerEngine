using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using static Chess.Board;
using System.Diagnostics.Tracing;
using UnityEditor.Experimental.GraphView;
using System.Runtime.InteropServices.WindowsRuntime;

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
            SearchInformation.DepthSearched = searchDepth;
            SearchInformation.MoveEvaluationInformation = FindBestMove(searchDepth);

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
                int eval = -MiniMax(depth - 1); // Switch to the other player's perspective for the next depth.
                UndoMove(move);

                if (eval > bestEval)
                {
                    bestEval = eval;
                    bestMove = move;
                }
            }

            // in the rare case that a move is not available pick a random move
            if(bestMove.IsDefault() && PositionInformation.currentStatus == GameResult.InProgress)
            {
                var random = new System.Random();
                bestMove = moves[random.Next(moves.Length)];
            }

            return new Evaluation.MoveEvaluation(bestMove, bestEval);
        }

        public int MiniMax(int depth)
        {
            if (depth == 0)
            {
                SearchInformation.PositionsEvaluated++;
                return Evaluation.SimpleEval();
            }

            Span<Move> moves = MoveGen.GenerateMoves();

            GameResult gameResult = Arbiter.CheckForGameOverRules();
            if (gameResult == GameResult.Stalemate || gameResult == GameResult.ThreeFold || gameResult == GameResult.FiftyMoveRule || gameResult == GameResult.InsufficientMaterial)
            {
                return 0;
            }

            if (gameResult == GameResult.CheckMate)
            {
                // prioritize the fastest mate
                return negativeInfinity - depth;
            }

            int bestEval = negativeInfinity;

            foreach (Move move in moves)
            {
                ExecuteMove(move);
                int eval = -MiniMax(depth - 1);
                bestEval = Math.Max(bestEval, eval);
                UndoMove(move);
            }
            return bestEval;
        }

    }

    // public class Engine : MonoBehaviour
    // {
    //     public BoardManager boardManager;

    //     public PieceMovementManager pieceMovementManager;

    //     private static Thread _engineThread;

    //     public static void StartThinking()
    //     {
    //         _engineThread = new Thread(Think);
    //         _engineThread.Start();
    //     }

    //     private static void Think()
    //     {
    //         //MiniMax(4, PositionInformation.whiteToMove);
    //         while (currentStatus == GameStatus.Normal)
    //         {
    //             if (Arbiter.ComputerSide == Arbiter.CurrentTurn)
    //             {
    //                 MainThreadDispatcher.Enqueue(() =>
    //                 {
    //                     Arbiter.DoTurn(FindBestMoveV1(4).BestMove);
    //                 });

    //             }
    //             Thread.Sleep(100); // Prevents tight looping, adjust as needed
    //         }
    //     }

    //     random move V0
    // }

}