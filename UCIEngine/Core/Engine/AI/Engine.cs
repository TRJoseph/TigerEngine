using System;
using System.Linq;
using System.Diagnostics;
using static Chess.Board;
using static Chess.MoveGen;

namespace Chess
{

    public class Engine
    {

        // engine references so these can be easily swapped our for testing different versions
        readonly Evaluation evaluation;
        readonly MoveSorting moveSorter;
        readonly SearchInformation searchInformation;

        readonly TranspositionTable transpositionTable;

        public Engine()
        {
            evaluation = new();
            searchInformation = new();
            moveSorter = new();
            transpositionTable = new();
        }

        const int infinity = 9999999;
        const int negativeInfinity = -infinity;
        const int mateScore = 100000;

        public void IterativeDeepeningSearch()
        {
            // this is for the future, lol
        }

        public SearchInformation FixedDepthSearch(int searchDepth)
        {
            searchInformation.PositionsEvaluated = 0;
            searchInformation.NumOfCheckMates = 0;
            searchInformation.DepthSearched = searchDepth;

            searchInformation.searchDiagnostics.stopwatch = Stopwatch.StartNew();

            searchInformation.MoveEvaluationInformation = FindBestMove(searchDepth);

            searchInformation.searchDiagnostics.stopwatch.Stop();
            searchInformation.searchDiagnostics.timeSpan = searchInformation.searchDiagnostics.stopwatch.Elapsed;
            searchInformation.searchDiagnostics.FormatElapsedTime();

            // this logs how long the fixed depth search took
            //searchInformation.searchDiagnostics.LogElapsedTime();

            return searchInformation;
        }

        public Evaluation.MoveEvaluation FindBestMove(int depth)
        {
            int alpha = negativeInfinity;
            int beta = infinity;
            Move bestMove = new();

            Span<Move> moves = MoveGen.GenerateMoves();
            moveSorter.OrderMoveList(ref moves, depth);

            foreach (Move move in moves)
            {
                ExecuteMove(move);
                int eval = -NegaMax(depth - 1, -beta, -alpha); // Note the switch and use of alpha-beta here
                UndoMove(move);

                // intially thought pruning here would be unnecessary (I was being a schmuck), turns out its incredible effective at reducing node search count, lol
                if (eval > alpha) // Only update alpha if we found a better move
                {
                    alpha = eval;
                    bestMove = move;
                }

                if (eval >= beta)
                {
                    break;
                }
            }

            // Handle the case of no valid moves
            if (bestMove.IsDefault() && PositionInformation.currentStatus == GameResult.InProgress)
            {
                var random = new System.Random();
                bestMove = moves[random.Next(moves.Length)];
            }

            return new Evaluation.MoveEvaluation(bestMove, alpha);
        }


        public int NegaMax(int depth, int alpha, int beta)
        {

            if (depth == 0)
            {
                return QuiescenceSearch(alpha, beta);
            }

            Span<Move> moves = MoveGen.GenerateMoves();

            // order move list to place good moves at top of list
            moveSorter.OrderMoveList(ref moves, depth);

            GameResult gameResult = Arbiter.CheckForGameOverRules();
            if (gameResult == GameResult.Stalemate || gameResult == GameResult.ThreeFold || gameResult == GameResult.FiftyMoveRule || gameResult == GameResult.InsufficientMaterial)
            {
                return 0;
            }

            if (gameResult == GameResult.Checkmate)
            {
                searchInformation.NumOfCheckMates++;

                // prioritize the fastest mate
                return -mateScore - depth;
            }

            foreach (Move move in moves)
            {
                ExecuteMove(move);
                // maintains symmetry; -beta is new alpha value for swapped perspective and likewise with -alpha; (upper and lower score safeguards)
                int eval = -NegaMax(depth - 1, -beta, -alpha);
                UndoMove(move);

                if (eval >= beta)
                {
                    int capturedPieceType = GetPieceAtSquare(PositionInformation.OpponentColorIndex, move.toSquare);
                    bool isCapture = capturedPieceType != ChessBoard.None;
                    // for quiet moves, we have a potential killer move

                    if (!isCapture)
                    {
                        moveSorter.killerMoves[depth, 1] = moveSorter.killerMoves[depth, 0];
                        moveSorter.killerMoves[depth, 0] = move;
                    }

                    // prune branch, black or white had a better path earlier on in the tree
                    return beta;
                }
                if (eval > alpha)
                {
                    alpha = eval;
                }
            }
            return alpha;
        }

        // https://www.chessprogramming.org/Quiescence_Search
        public int QuiescenceSearch(int alpha, int beta)
        {
            int eval = evaluation.EvaluatePosition();
            searchInformation.PositionsEvaluated++;

            if(eval >= beta)
            {
                return beta;
            }

            if(eval > alpha) {
                alpha = eval;
            }

            Span<Move> captureMoves = MoveGen.GenerateMoves(true);

            moveSorter.OrderMoveList(ref captureMoves, 0);

            foreach (Move captureMove in captureMoves) {
                ExecuteMove(captureMove);
                eval = -QuiescenceSearch(-beta, -alpha);
                UndoMove(captureMove);

                if(eval >= beta)
                {
                    return beta;
                }
                if(eval > alpha)
                {
                    alpha = eval;
                }
            }
            return alpha;
        }
    }

}