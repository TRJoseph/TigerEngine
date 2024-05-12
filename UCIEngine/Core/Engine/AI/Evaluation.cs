using static Chess.PieceValues;
using static Chess.Board;
using static Chess.MoveGen;
using System.Runtime.Serialization;
using System;

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

        readonly int[] whiteKingMiddleGameBias =
        {
            20, 30, 10,  0,  0, 10, 30, 20,
            20, 20,  0,  0,  0,  0, 20, 20,
            -10,-20,-20,-20,-20,-20,-20,-10,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
        };

        readonly int[] blackKingMiddleGameBias =
        {
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -10,-20,-20,-20,-20,-20,-20,-10,
             20, 20,  0,  0,  0,  0, 20, 20,
             20, 30, 10,  0,  0, 10, 30, 20
        };

        readonly int[] whiteKingEndGameBias =
        {
            -50,-30,-30,-30,-30,-30,-30,-50,
            -30,-30,  0,  0,  0,  0,-30,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-20,-10,  0,  0,-10,-20,-30,
            -50,-40,-30,-20,-20,-30,-40,-50,
        };

        readonly int[] blackKingEndGameBias =
        {
            -50,-40,-30,-20,-20,-30,-40,-50,
            -30,-20,-10,  0,  0,-10,-20,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-30,  0,  0,  0,  0,-30,-30,
            -50,-30,-30,-30,-30,-30,-30,-50
        };

        // more coming up for king, including helping it solve k and q versus k endgames
        const int EndgameThreshold = 10; // When x or fewer pieces remain, consider it the endgame

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
            // Set initial position score, this will change based on various planned future factors.
            // For example, passed pawns will be worth more, pawns on the 7th rank (from whites perspective) about to
            // promote will be worth more, king safety, etc.
            int piecePositionScore = 0;

            int colorIndex = PositionInformation.MoveColorIndex;
            int[] pawnBias = whiteToMove ? whitePawnPushBias : blackPawnPushBias;
            int[] knightBias = whiteToMove ? whiteKnightBias : blackKnightBias;
            int[] bishopBias = whiteToMove ? whiteBishopBias : blackBishopBias;
            int[] rookBias = whiteToMove ? whiteRookBias : blackRookBias;
            int[] queenBias = whiteToMove ? whiteQueenBias : blackQueenBias;

            // Evaluate piece positions
            piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[colorIndex, ChessBoard.Pawn], pawnBias);
            piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[colorIndex, ChessBoard.Knight], knightBias);
            piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[colorIndex, ChessBoard.Bishop], bishopBias);
            piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[colorIndex, ChessBoard.Rook], rookBias);
            piecePositionScore += EvaluatePiecePositions(InternalBoard.Pieces[colorIndex, ChessBoard.Queen], queenBias);

            return piecePositionScore; // Always return positive scores
        }

        int ConsiderKingPosition(bool whiteToMove, double middleGameWeight, double endGameWeight)
        {
            int[] middleGameBiasArray = whiteToMove ? whiteKingMiddleGameBias : blackKingMiddleGameBias;
            int[] endGameBiasArray = whiteToMove ? whiteKingEndGameBias : blackKingEndGameBias;

            int middleGameBias = EvaluatePiecePositions(InternalBoard.Pieces[PositionInformation.MoveColorIndex, ChessBoard.King], middleGameBiasArray);
            int endGameBias = EvaluatePiecePositions(InternalBoard.Pieces[PositionInformation.MoveColorIndex, ChessBoard.King], endGameBiasArray);

            return (int)(middleGameBias * middleGameWeight + endGameBias * endGameWeight);
        }

        int ChebyshevDistance(int file1, int rank1, int file2, int rank2)
        {
            int rankDistance = Math.Abs(rank2 - rank1);
            int fileDistance = Math.Abs(file2 - file1);

            return Math.Max(rankDistance, fileDistance);
        }

        int BiasKingPositionForEndgames(double endGameWeight)
        {
            int eval = 0;

            int friendlyKingSquare = BitBoardHelper.GetLSB(ref InternalBoard.Pieces[PositionInformation.MoveColorIndex, ChessBoard.King]);
            int oppKingSquare = BitBoardHelper.GetLSB(ref InternalBoard.Pieces[PositionInformation.OpponentColorIndex, ChessBoard.King]);

            // Calculate edge distance for opponent king to encourage pushing to the edge of the board
            int oppKingFile = GetFile(oppKingSquare);
            int oppKingRank = GetRank(oppKingSquare);

            int oppKingDstToCenterFile = Math.Max(3 - oppKingFile, oppKingFile - 4);
            int oppKingDstToCenterRank = Math.Max(3 - oppKingRank, oppKingRank - 4);

            int oppKingDstFromCenter = oppKingDstToCenterFile + oppKingDstToCenterRank;
            eval += oppKingDstFromCenter;

            // get friendly piece 
            int friendlyKingFile = GetFile(friendlyKingSquare);
            int friendlyKingRank = GetRank(friendlyKingSquare);

            // this prioritizes the friendly king moving closer to an enemy king, biases the distance between the king to favor short distances
            int chebyshevDistance = ChebyshevDistance(friendlyKingFile, friendlyKingRank, oppKingFile, oppKingRank);

            eval += 14 - chebyshevDistance;
            return (int)(10 * eval * endGameWeight);
        }

        int ConsiderKing()
        {
            int eval = 0;
            // piece count will be used for king endgame weight and to solve specific endgames
            int pieceCount = CountBits(InternalBoard.AllPieces);

            double middleGameWeight;
            double endGameWeight;

            if (pieceCount > EndgameThreshold)
            {
                // for the most part we are going to be in the early-middle game phase
                middleGameWeight = 1.0;
                endGameWeight = 0.0;

            }
            else
            {
                endGameWeight = (EndgameThreshold - pieceCount + 1) / (double)EndgameThreshold;
                middleGameWeight = 1.0 - endGameWeight;
            }

            eval += ConsiderKingPosition(PositionInformation.whiteToMove, middleGameWeight, endGameWeight);
            eval += BiasKingPositionForEndgames(endGameWeight);

            return eval;
        }


        public int EvaluatePosition()
        {
            int whiteEvaluation = CountMaterial(ChessBoard.White);
            int blackEvaluation = CountMaterial(ChessBoard.Black);

            bool whiteToMove = PositionInformation.whiteToMove;

            // large values favor white, small values favor black
            int evaluation = whiteEvaluation - blackEvaluation;

            if (whiteToMove)
            {
                evaluation += ConsiderPiecePositions(whiteToMove);
                evaluation += ConsiderKing();
            }
            else
            {
                evaluation -= ConsiderPiecePositions(whiteToMove);
                evaluation -= ConsiderKing();
            }

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