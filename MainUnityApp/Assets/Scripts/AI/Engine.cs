using System;
using System.Linq;
using System.Diagnostics;
using static Chess.Board;
using static Chess.MoveGen;
using System.Threading.Tasks;
using static Chess.Arbiter;

namespace Chess
{

    public class Engine
    {

        // engine references so these can be easily swapped our for testing different versions
        readonly Evaluation evaluation;
        readonly MoveSorting moveSorter;

        SearchInformation searchInformation;
        SearchSettings searchSettings;

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
        const int maxExtensions = 256;


        public Move bestMoveThisIteration;
        public int bestEvalThisIteration;

        public Move bestMove;
        public int bestEval;

        public bool searchCancelled = false;

        public async Task StartSearchAsync(SearchSettings searchSettings)
        {
            this.searchSettings = searchSettings;

            searchCancelled = false;
            var searchTask = Task.Run(() => StartSearch());
            var timerTask = Task.Delay(searchSettings.SearchTime);

            await Task.WhenAny(searchTask, timerTask);

            if (timerTask.IsCompleted)
            {
                searchCancelled = true;
            }

            await searchTask; // ensure the search task completes after cancellation if it hasn't already.

            DoTurn(bestMove);

            return;
        }

        public async void StartSearch()
        {

            if (searchSettings.SearchType == SearchType.IterativeDeepening)
            {
                IterativeDeepeningSearch();
            } else // fixed depth
            {
                FixedDepthSearch(searchSettings.Depth);
            }
        }

        public void IterativeDeepeningSearch()
        {

            // start depth at 1 then increase until time runs out
            int depth = 1;

            bestMove = new Move();
            bestEval = 0;

            while(!searchCancelled)
            {
                searchInformation.PositionsEvaluated = 0;
                searchInformation.NumOfCheckMates = 0;
                searchInformation.DepthSearched = depth;
                NegaMax(depth, depthFromRoot: 0, negativeInfinity, infinity);
                searchInformation.MoveEvaluationInformation.BestMove = bestMove;
                searchInformation.MoveEvaluationInformation.Evaluation = bestEval;

                MainThreadDispatcher.Enqueue(() =>
                {
                    UIController.Instance.UpdateSearchUIInfo(ref searchInformation);
                });

                if (!searchCancelled)
                {
                    bestMove = bestMoveThisIteration;
                    bestEval = bestEvalThisIteration;
                }
                depth++;
            }

            return;
        }

        public void FixedDepthSearch(int searchDepth)
        {
            searchInformation.PositionsEvaluated = 0;
            searchInformation.NumOfCheckMates = 0;
            searchInformation.DepthSearched = searchDepth;
            NegaMax(searchDepth, depthFromRoot: 0, negativeInfinity, infinity);
            searchInformation.MoveEvaluationInformation.BestMove = bestMove;
            searchInformation.MoveEvaluationInformation.Evaluation = bestEval;


            bestMove = bestMoveThisIteration;
            bestEval = bestEvalThisIteration;

            MainThreadDispatcher.Enqueue(() =>
            {
                UIController.Instance.UpdateSearchUIInfo(ref searchInformation);
            });
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
                int eval = -NegaMax(depth - 1, 0, -beta, -alpha); // Note the switch and use of alpha-beta here
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


        public int NegaMax(int depth, int depthFromRoot, int alpha, int beta, int searchExtensions = 0)
        {
            if (searchCancelled)
            {
                return alpha;
            }

            if (depth == 0)
            {
                return QuiescenceSearch(alpha, beta);
            }

            Span<Move> moves = GenerateMoves();

            // order move list to place good moves at top of list
            moveSorter.OrderMoveList(ref moves, depth);

            bool playerInCheck = IsPlayerInCheck();
            GameResult gameResult = CheckForGameOverRules(playerInCheck, inSearch: true);
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

            int searchExtension = 0;

            // extend the search if the player is in check
            if (playerInCheck && searchExtensions < maxExtensions)
            {
                searchExtensions++;
                searchExtension = 1;
            }

            foreach (Move move in moves)
            {
                ExecuteMove(move);
                // maintains symmetry; -beta is new alpha value for swapped perspective and likewise with -alpha; (upper and lower score safeguards)
                int eval = -NegaMax(depth - 1 + searchExtension, depthFromRoot + 1, -beta, -alpha, searchExtensions);
                UndoMove(move);

                if(searchCancelled)
                {
                    return alpha;
                }

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
                    if(depthFromRoot == 0)
                    {
                        bestMoveThisIteration = move;
                        bestEvalThisIteration = alpha;
                    }
                }
            }
            return alpha;
        }

        // https://www.chessprogramming.org/Quiescence_Search
        public int QuiescenceSearch(int alpha, int beta)
        {
            if (searchCancelled)
            {
                return alpha;
            }

            int eval = evaluation.EvaluatePosition();
            searchInformation.PositionsEvaluated++;

            if (eval >= beta)
            {
                return beta;
            }

            if (eval > alpha)
            {
                alpha = eval;
            }

            Span<Move> captureMoves = MoveGen.GenerateMoves(true);

            moveSorter.OrderMoveList(ref captureMoves, 0);

            foreach (Move captureMove in captureMoves)
            {
                ExecuteMove(captureMove);
                eval = -QuiescenceSearch(-beta, -alpha);
                UndoMove(captureMove);

                if (searchCancelled)
                {
                    return alpha;
                }

                if (eval >= beta)
                {
                    return beta;
                }
                if (eval > alpha)
                {
                    alpha = eval;
                }
            }
            return alpha;
        }
    }

}