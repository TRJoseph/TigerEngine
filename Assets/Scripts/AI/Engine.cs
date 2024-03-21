using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using static Chess.Board;
using System.Diagnostics.Tracing;
using UnityEditor.Experimental.GraphView;

namespace Chess
{

    public interface IChessEngine
    {
        Evaluation.MoveEvaluation FindBestMove(int depth);
    }

    public class RandomMoveEngine : IChessEngine
    {

        public Evaluation.MoveEvaluation FindBestMove(int depth)
        {

            var random = new System.Random();

            return new Evaluation.MoveEvaluation(legalMoves[random.Next(Board.legalMoves.Count())], 0);
        }
    }

    public class MiniMaxEngineV0 : IChessEngine
    {

        public Evaluation.MoveEvaluation FindBestMove(int depth)
        {
            int bestEval = int.MinValue;
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

            return new Evaluation.MoveEvaluation(bestMove, bestEval);
        }

        public static int MiniMax(int depth)
        {
            if (depth == 0)
            {
                return Evaluation.SimpleEval();
            }

            int maxEval = int.MinValue;

            Span<Move> moves = MoveGen.GenerateMoves();

            GameResult gameResult = Arbiter.CheckForGameOverRules();
            if (gameResult == GameResult.Stalemate || gameResult == GameResult.ThreeFold || gameResult == GameResult.FiftyMoveRule || gameResult == GameResult.InsufficientMaterial)
            {
                return 0;
            }

            if (gameResult == GameResult.CheckMate)
            {
                return -100000 + depth;
            }

            foreach (Move move in moves)
            {
                ExecuteMove(move);
                int eval = -MiniMax(depth - 1);
                maxEval = Math.Max(maxEval, eval);
                UndoMove(move);
            }
            return maxEval;
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