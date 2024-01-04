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

        public enum Sides
        {
            White = 0,
            Black = 0
        }

        // these will be updated and selected based on UI elements in some sort of main menu before the game is started
        public static Sides humanPlayer = Sides.White;

        public static Sides ComputerSide = Sides.Black;

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
                // if (BoardManager.ComputerSide == BoardManager.Sides.White)
                // {

                // }
                // else
                // {
                if (BoardManager.ComputerMove)
                {
                    // make random moves
                    var random = new System.Random();
                    int index = random.Next(Board.legalMoves.Count);
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        pieceMovementManager.HandleEngineMoveExecution(Board.legalMoves[index]);
                    });

                }

                //}
                Thread.Sleep(100); // Prevents tight looping, adjust as needed
            }
        }
    }

}