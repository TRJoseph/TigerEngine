using static Chess.PieceValues;
using static Chess.Board;
using static Chess.MoveGen;

namespace Chess
{
    public class Evaluation
    {

        //https://www.chessprogramming.org/Simplified_Evaluation_Function
        // starting from square 0 to 63, prioritizing pawn center control

        readonly int[] whitePawnPushBias = {
             0, 0,  0,  0,  0,  0,  0,  0,
             5, 10, 10,-20,-20, 10, 10,  5,
             5, -5,-10,  0,  0,-10, -5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5,  5, 10, 25, 25, 10,  5,  5,
            10, 10, 20, 30, 30, 20, 10, 10,
            50, 50, 50, 50, 50, 50, 50, 50,
             0,  0,  0,  0,  0,  0,  0,  0,
        };

        readonly int[] blackPawnPushBias = {
             0,  0,  0,  0,  0,  0,  0,  0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
             5,  5, 10, 25, 25, 10,  5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5, -5,-10,  0,  0,-10, -5,  5,
             5, 10, 10,-20,-20, 10, 10,  5,
             0,  0,  0,  0,  0,  0,  0,  0
        };

        readonly int[] whiteKnightBias =
        {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50,
        };

        readonly int[] blackKnightBias =
        {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50,
        };

        readonly int[] whiteBishopBias =
        {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -20,-10,-10,-10,-10,-10,-10,-20,
        };

        readonly int[] blackBishopBias =
        {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -20,-10,-10,-10,-10,-10,-10,-20,
        };


        // we want the black rooks ideally on the second rank, doubled
        readonly int[] blackRookBias =
        {
              0,  0,  0,  0,  0,  0,  0,  0,
              5, 10, 10, 10, 10, 10, 10,  5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
              0,  0,  0,  5,  5,  0,  0,  0
        };

        // likewise for the white rooks, we want them on the 7th rank where they can inflict the damage to the black position
        readonly int[] whiteRookBias =
        {
              0,  0,  0,  0,  0,  0,  0,  0,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
              5, 10, 10, 10, 10, 10, 10,  5,
              0,  0,  0,  5,  5,  0,  0,  0
        };

        readonly int[] whiteQueenBias =
        {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5,  5,  5,  5,  0,-10,
              0,  0,  5,  5,  5,  5,  0, -5,
             -5,  0,  5,  5,  5,  5,  0, -5,
            -10,  5,  5,  5,  5,  5,  0,-10,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20
        };

        readonly int[] blackQueenBias =
        {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5,  5,  5,  5,  0,-10,
             -5,  0,  5,  5,  5,  5,  0, -5,
              0,  0,  5,  5,  5,  5,  0, -5,
            -10,  5,  5,  5,  5,  5,  0,-10,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20
        };

        // more coming up for king, this requires weights for the beginning of the game, middlegame, and endgame

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

        private int EvaluatePiecePositions(ulong pieces, int[] biasArray)
        {
            int score = 0;
            while (pieces != 0)
            {
                int index = BitBoardHelper.GetLSB(ref pieces);
                score += biasArray[index];
                pieces &= pieces - 1; // Clears the least significant bit
            }
            return score;
        }

        int ConsiderPiecePositions(bool whiteToMove)
        {
            /* Set initial position score, this will change based on various planned future factors.
            For example, passed pawns will be worth more, pawns on the 7th rank (from whites perspective) about to
            promote will be worth more, king safety, etc.
            */
            int piecePositionScore = 0;

            if (whiteToMove)
            {
                // Evaluate white piece positions
                piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn], whitePawnPushBias);
                piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Knight], whiteKnightBias);
                piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Bishop], whiteBishopBias);
                piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook], whiteRookBias);
                piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Queen], whiteQueenBias);
            }
            else
            {
                // Evaluate black piece positions
                piecePositionScore -= EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn], blackPawnPushBias);
                piecePositionScore -= EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Knight], blackKnightBias);
                piecePositionScore -= EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Bishop], blackBishopBias);
                piecePositionScore -= EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook], blackRookBias);
                piecePositionScore -= EvaluatePiecePositions(InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Queen], blackQueenBias);
            }

            return piecePositionScore; // Positive values favor white, negative values favor black
        }

        public int EvaluatePosition()
        {
            int whiteEvaluation = CountMaterial(ChessBoard.White);
            int blackEvaluation = CountMaterial(ChessBoard.Black);

            bool whiteToMove = PositionInformation.whiteToMove;

            // large values favor white, small values favor black
            int evaluation = whiteEvaluation - blackEvaluation;

            evaluation += ConsiderPiecePositions(whiteToMove);

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