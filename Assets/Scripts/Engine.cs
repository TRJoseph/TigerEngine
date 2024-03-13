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

    public class Engine : MonoBehaviour
    {
        public BoardManager boardManager;

        public PieceMovementManager pieceMovementManager;

        private Thread _engineThread;

        public void StartThinking()
        {
            _engineThread = new Thread(Think);
            _engineThread.Start();
        }

        private void Think()
        {
            while (Board.currentStatus != GameStatus.Ended)
            {
                // engine should be analyzing the position constantly while the gamestate is active
                if (BoardManager.ComputerSide == BoardManager.CurrentTurn)
                {
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        pieceMovementManager.HandleEngineMoveExecution(SimpleEval());
                    });

                }
                Thread.Sleep(100); // Prevents tight looping, adjust as needed
            }
        }

        public static PromotionFlags EvaluateBestPromotionPiece()
        {
            // Later, add more sophisticated logic
            return PromotionFlags.PromoteToQueenFlag;
        }

        private static Board.Move SimpleEval()
        {
            // capture piece when available

            // make random move, for now
            var random = new System.Random();
            return Board.legalMoves[random.Next(Board.legalMoves.Count)];
        }
    }

}