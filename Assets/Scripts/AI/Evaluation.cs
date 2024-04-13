using static Chess.PieceValues;
using static Chess.Board;
using static Chess.MoveGen;

namespace Chess
{
    public class Evaluation
    {

        //https://www.chessprogramming.org/Simplified_Evaluation_Function
        // starting from square 0 to 63, prioritizing pawn center control

        readonly int[] WhitePawnPushBias = {
             0, 0,  0,  0,  0,  0,  0,  0,
             5, 10, 10,-20,-20, 10, 10,  5,
             5, -5,-10,  0,  0,-10, -5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5,  5, 10, 25, 25, 10,  5,  5,
            10, 10, 20, 30, 30, 20, 10, 10,
            50, 50, 50, 50, 50, 50, 50, 50,
             0,  0,  0,  0,  0,  0,  0,  0,
        };

        readonly int[] BlackPawnPushBias = {
             0,  0,  0,  0,  0,  0,  0,  0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
             5,  5, 10, 25, 25, 10,  5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5, -5,-10,  0,  0,-10, -5,  5,
             5, 10, 10,-20,-20, 10, 10,  5,
             0,  0,  0,  0,  0,  0,  0,  0
        };


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

        int ConsiderPawnPositions()
        {
            /* Set initial position score, this will change based on various planned future factors.
            For example, passed pawns will be worth more, pawns on the 7th rank (from whites perspective) about to
            promote will be worth more, etc.
            */
            int pawnPosScore = 0;

            // Evaluate white pawn positions
            ulong whitePawns = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn];
            while (whitePawns != 0)
            {
                int index = BitBoardHelper.GetLSB(ref whitePawns);
                pawnPosScore += WhitePawnPushBias[index]; // Apply bias
                whitePawns &= whitePawns - 1; // Clears the LSB
            }

            // Evaluate black pawn positions
            ulong blackPawns = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn];
            while (blackPawns != 0)
            {
                int index = BitBoardHelper.GetLSB(ref blackPawns);
                pawnPosScore -= BlackPawnPushBias[index];
                blackPawns &= blackPawns - 1; // Clears the LSB
            }

            return pawnPosScore; // Positive values favor white, negative values favor black
        }

        public int EvaluatePosition()
        {
            int whiteEvaluation = CountMaterial(ChessBoard.White);
            int blackEvaluation = CountMaterial(ChessBoard.Black);

            bool whiteToMove = PositionInformation.whiteToMove;

            // large values favor white, small values favor black
            int evaluation = whiteEvaluation - blackEvaluation;

            evaluation += ConsiderPawnPositions();

            int perspective = whiteToMove ? 1 : -1;
            return evaluation * perspective;
        }

        int CountMaterial(int colorIndex)
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