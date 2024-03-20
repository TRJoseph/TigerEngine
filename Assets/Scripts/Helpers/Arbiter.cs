using static Chess.Board;

namespace Chess
{

    public static class Arbiter
    {

        public enum Sides
        {
            White = 0,
            Black = 1
        }

        // these will be updated and selected based on UI elements in some sort of main menu before the game is started
        public static Sides humanPlayer = Sides.White;

        public static Sides ComputerSide = Sides.White;

        public static Sides CurrentTurn = Sides.White;

        public static void StartGame()
        {
            /* ChooseSide controls what side the player will play 
            For example, if Sides.White is passed in, the player will be able to control the white pieces
            and the engine will move the black pieces.
            If the goal is to have the engine play itself, comment out this ChooseSide function call below and
            comment out the 'SwapTurns' call from inside the 'AfterMove' method.

            If the goal is to let the human player make both white and black moves, just comment out the 
            'SwapTurns' call from inside the 'AfterMove' method.
            */
            ChooseSide(Sides.White);

            legalMoves = GenerateMoves();

            //engine.StartThinking();
        }

        public static void DoTurn()
        {


            // check for game over rules

        }

        //private static void CheckForGameOverRules()
        //{
        //    if (legalMoves.Count == 0 && kingInCheck)
        //    {
        //        UnityEngine.Debug.Log("CheckMate!");
        //        currentState = GameState.Ended;
        //    }

        //    if (legalMoves.Count == 0 && !kingInCheck)
        //    {
        //        UnityEngine.Debug.Log("Stalemate!");
        //        currentState = GameState.Ended;
        //    }

        //    // a "move" consists of a player completing a turn followed by the opponent completing a turn, hence
        //    if (halfMoveAccumulator == 100)
        //    {
        //        UnityEngine.Debug.Log("Draw by 50 move rule!");
        //        currentState = GameState.Ended;
        //    }

        //    // threefold repetition rule (position repeats three times is a draw)
        //    if (PositionHashes[ZobristHashKey] >= 3)
        //    {
        //        UnityEngine.Debug.Log("Draw by Threefold Repetition!");
        //        currentState = GameState.Ended;
        //    }

        //    // draw by insufficient material rule, for example: knight and king cannot deliver checkmate

        //    if (CheckForInsufficientMaterial())
        //    {
        //        UnityEngine.Debug.Log("Draw by Insufficient Material!");
        //        currentState = GameState.Ended;
        //    }
        //}


        private static void SwapTurn()
        {
            CurrentTurn = CurrentTurn == Sides.White
                                    ? Sides.Black
                                    : Sides.White;
        }

        public static void ChooseSide(Sides playerSide)
        {
            humanPlayer = playerSide;
            ComputerSide = (playerSide == Sides.White) ? Sides.Black : Sides.White; ;
        }
    }

}