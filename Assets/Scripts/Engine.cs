using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

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
            while (Board.currentState != Board.GameState.Ended)
            {
                // engine should be analyzing the position constantly while the gamestate is active
                if (BoardManager.ComputerSide == BoardManager.CurrentTurn)
                {
                    // make random moves
                    var random = new System.Random();
                    int index = random.Next(Board.legalMoves.Count);
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        pieceMovementManager.HandleEngineMoveExecution(Board.legalMoves[index]);
                    });

                }
                Thread.Sleep(100); // Prevents tight looping, adjust as needed
            }
        }

        public static int EvaluateBestPromotionPiece()
        {
            // Later, add more sophisticated logic
            return Piece.Queen;
        }
    }

}