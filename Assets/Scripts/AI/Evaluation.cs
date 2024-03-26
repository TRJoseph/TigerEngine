
using static Chess.Board;
using UnityEditor;

namespace Chess
{


    public static class Evaluation
    {

        public struct MoveEvaluation
        {
            public Move BestMove;
            public int Evaluation;

            public MoveEvaluation(Move bestMove, int evaluation)
            {
                BestMove = bestMove;
                Evaluation = evaluation;
            }
        }


        // in centipawn values
        const int PawnValue = 100;
        const int KnightValue = 300;
        const int BishopValue = 300;
        const int RookValue = 500;
        const int QueenValue = 900;

        public static int SimpleEval()
        {
            int whiteEvaluation = CountMaterial(ChessBoard.White);
            int blackEvaluation = CountMaterial(ChessBoard.Black);

            // large values favor white, small values favor black
            int evaluation = whiteEvaluation - blackEvaluation;

            int perspective = PositionInformation.whiteToMove ? 1 : -1;
            return evaluation * perspective;
        }

        static int CountMaterial(int colorIndex)
        {
            int materialCount = 0;
            materialCount += CountBits(InternalBoard.Pieces[colorIndex, ChessBoard.Pawn]) * PawnValue;
            materialCount += CountBits(InternalBoard.Pieces[colorIndex, ChessBoard.Knight]) * KnightValue;
            materialCount += CountBits(InternalBoard.Pieces[colorIndex, ChessBoard.Bishop]) * BishopValue;
            materialCount += CountBits(InternalBoard.Pieces[colorIndex, ChessBoard.Rook]) * RookValue;
            materialCount += CountBits(InternalBoard.Pieces[colorIndex, ChessBoard.Queen]) * QueenValue;
            return materialCount;
        }
    }
}