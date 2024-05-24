using System;
using System.Linq;
using System.Diagnostics;
using static Chess.Board;
using static Chess.MoveGen;
using System.Threading.Tasks;
using static Chess.Arbiter;
using static Chess.MoveSorting;

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
        const int maxExtensions = 10;


        public Move bestMoveThisIteration;
        public int bestEvalThisIteration;

        public Move bestMove;
        public int bestEval;

        public bool searchCancelled = false;
        public bool searchedOneDepth = false;

        public void StartTimer(int timeout)
        {
            // create a new thread that will wait for a specified timeout and then cancel the search
            new Thread(() =>
            {
                Thread.Sleep(timeout); // wait for the timeout period
                searchCancelled = true;
            })
            { IsBackground = true }.Start();
        }


        public void StartSearch()
        {
            searchCancelled = false;
            searchedOneDepth = false;

            // if we are using iterative deepening then we are concerned about the time
            if(searchSettings.SearchType == SearchType.IterativeDeepening)
            {
                StartTimer((int)searchSettings.SearchTime.TotalMilliseconds);
            }

            ChooseSearchType();

            //DoTurn(bestMove);
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

            // in the rare case that the move does not get updated choose a random move
            if (bestMove.IsDefault() || bestMove.toSquare == 0 || bestMove.fromSquare == 0)
            {
                Random rand = new();
                int moveIndex = rand.Next(legalMoves.Length); // Select a random index from the list
                bestMove = legalMoves[moveIndex];
                bestEval = 0;
            }

        }

        public void IterativeDeepeningSearch()
        {

            // start depth at 1 then increase until time runs out
            int depth = 1;

            bestMove = new Move();
            bestEval = 0;

            while (!searchCancelled && depth <= maxDepth)
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
            if (searchedOneDepth && searchCancelled)
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

            if(depthFromRoot > 1)
            {
                if(gameResult == GameResult.ThreeFold || gameResult == GameResult.FiftyMoveRule)
                {
                    return 0;
                }
            }

            if (gameResult == GameResult.Checkmate)
            {
                searchInformation.NumOfCheckMates++;

                // prioritize the fastest mate
                return -mateScore - depth;
            }

            if (gameResult == GameResult.Stalemate || gameResult == GameResult.InsufficientMaterial)
            {
                return 0;
            }
            //if (gameResult == GameResult.Stalemate || gameResult == GameResult.InsufficientMaterial)
            //{
            //    return 0;
            //}

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

                if (searchedOneDepth && searchCancelled)
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
                    searchedOneDepth = true;
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
            if (searchedOneDepth && searchCancelled)
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

                if (searchedOneDepth && searchCancelled)
                {
                    return alpha;
                }

                if (eval >= beta)
                {
                    return beta;
                }
                if (eval > alpha)
                {
                    searchedOneDepth = true;
                    alpha = eval;
                }
            }
            return alpha;
        }
    }

}