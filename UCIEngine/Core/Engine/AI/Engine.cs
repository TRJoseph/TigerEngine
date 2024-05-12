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

        readonly SearchInformation searchInformation;
        readonly SearchSettings searchSettings;

        readonly TranspositionTable transpositionTable;

        public Engine(SearchSettings searchSettings)
        {
            evaluation = new();
            searchInformation = new();
            moveSorter = new();
            transpositionTable = new();

            // initialize the engine's search settings
            this.searchSettings = searchSettings;
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

        public void StartSearch()
        {
            searchCancelled = false;

            var searchTask = Task.Run(() => ChooseSearchType());

            var timerTask = Task.Delay(searchSettings.SearchTime);

            // wait for either task to complete
            int completedTaskIndex = Task.WaitAny(searchTask, timerTask);

            // index 1 here is the timer task, checking if its completed before moving on
            if (completedTaskIndex == 1)
            {
                searchCancelled = true; // cancels the search
            }

            // ensure the search task is completed if it wasn't cancelled or if still running
            if (!searchTask.IsCompleted)
                searchTask.Wait();

            DoTurn(bestMove);
            return;
        }

        public void ChooseSearchType()
        {

            if (searchSettings.SearchType == SearchType.IterativeDeepening)
            {
                IterativeDeepeningSearch();
            }
            else // fixed depth
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

            while (!searchCancelled)
            {
                searchInformation.PositionsEvaluated = 0;
                searchInformation.NumOfCheckMates = 0;
                searchInformation.DepthSearched = depth;
                NegaMax(depth, depthFromRoot: 0, negativeInfinity, infinity);

                //searchInformation.MoveEvaluationInformation.BestMove = bestMove;
                //searchInformation.MoveEvaluationInformation.Evaluation = bestEval;

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

            //searchInformation.MoveEvaluationInformation.BestMove = bestMove;
            //searchInformation.MoveEvaluationInformation.Evaluation = bestEval;
            bestMove = bestMoveThisIteration;
            bestEval = bestEvalThisIteration;
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

                if (searchCancelled)
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
                    if (depthFromRoot == 0)
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