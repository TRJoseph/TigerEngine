using System.Linq;
using System.Diagnostics;
using static Chess.Board;
using static Chess.MoveGen;
using System.Threading.Tasks;
using static Chess.Arbiter;
using static Chess.MoveSorting;
using static Chess.TranspositionTable;

namespace Chess
{

    public class Engine
    {
        // engine references so these can be easily swapped our for testing different versions
        readonly Evaluator evaluator;
        readonly MoveGen moveGenerator;
        readonly MoveSorting moveSorter;

        // TODO: implement NNUE evaluator
        readonly NNUE nnueEvaluator = new();

        readonly Board board;

        readonly SearchInformation searchInformation;
        public readonly SearchSettings searchSettings;

        readonly TranspositionTable transpositionTable;

        public Engine(MoveGen moveGenerator, Board board)
        {
            evaluator = new Evaluator(board.posInfo);
            searchInformation = new();
            moveSorter = new MoveSorting(board, moveGenerator);
            transpositionTable = new();

            // sets move generator and board references
            this.moveGenerator = moveGenerator;
            this.board = board;

            // initialize the engine's search settings
            this.searchSettings = new SearchSettings(4, TimeSpan.FromMilliseconds(1000), SearchType.IterativeDeepening);
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
            transpositionTable.Reset();

            // if we are using iterative deepening then we are concerned about the time
            if (searchSettings.SearchType == SearchType.IterativeDeepening)
            {
                StartTimer((int)searchSettings.SearchTime.TotalMilliseconds);
            }

            ChooseSearchType();
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
                if(moveGenerator.legalMoves.Length > 0)
                {
                    int moveIndex = rand.Next(moveGenerator.legalMoves.Length); // Select a random index from the list
                    bestMove = moveGenerator.legalMoves[moveIndex];
                    bestEval = 0;
                }
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

            // base cases: cancellation due to lack of computation time remaining or depth == 0
            if (searchedOneDepth && searchCancelled)
            {
                return alpha;
            }

            if (depth == 0)
            {
                return QuiescenceSearch(alpha, beta);
            }
            
            if (transpositionTable.TryGetValue(board.posInfo.CurrentGameState.zobristHashKey, out TranspositionEntry entry) && entry.DepthOfSearch >= depth)
            {
                if (entry.TypeOfNode == NodeType.PVNode)
                    return entry.EvaluationScore;
                if (entry.TypeOfNode == NodeType.CutNode && entry.EvaluationScore >= beta)
                    return entry.EvaluationScore;
                if (entry.TypeOfNode == NodeType.AllNode && entry.EvaluationScore <= alpha)
                    return entry.EvaluationScore;
            }

            Span<Move> moves = moveGenerator.GenerateMoves();

            // order move list to place good moves at top of list
            moveSorter.OrderMoveList(ref moves, depth);

            bool playerInCheck = IsPlayerInCheck(board.posInfo);
            GameResult gameResult = CheckForGameOverRules(moveGenerator, board.posInfo, playerInCheck, inSearch: true);

            if (depthFromRoot >= 1)
            {
                if (gameResult == GameResult.ThreeFold || gameResult == GameResult.FiftyMoveRule)
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

            int searchExtension = 0;

            // extend the search if the player is in check
            if (playerInCheck && searchExtensions < maxExtensions)
            {
                searchExtensions++;
                searchExtension = 1;
            }

            // keeps track of boundaries for fail-low nodes
            int originalAlpha = alpha;
            Move tempBestMove = default;

            for(int i = 0; i < moves.Length; i ++)
            {
                board.ExecuteMove(ref moves[i]);
                // maintains symmetry; -beta is new alpha value for swapped perspective and likewise with -alpha; (upper and lower score safeguards)
                int eval = -NegaMax(depth - 1 + searchExtension, depthFromRoot + 1, -beta, -alpha, searchExtensions);
                board.UndoMove(ref moves[i]);

                if (searchedOneDepth && searchCancelled)
                {
                    return alpha;
                }

                if (eval >= beta)
                {
                    int capturedPieceType = GetPieceAtSquare(board.posInfo.OpponentColorIndex, moves[i].toSquare);
                    bool isCapture = capturedPieceType != ChessBoard.None;
                    // for quiet moves, we have a potential killer move

                    if (!isCapture)
                    {
                        moveSorter.killerMoves[depth, 1] = moveSorter.killerMoves[depth, 0];
                        moveSorter.killerMoves[depth, 0] = moves[i];
                    }

                    // prune branch, black or white had a better path earlier on in the tree
                    transpositionTable.Store(board.posInfo.CurrentGameState.zobristHashKey, moves[i], depth, beta, NodeType.CutNode);
                    return beta;
                }
                if (eval > alpha)
                {
                    searchedOneDepth = true;
                    alpha = eval;
                    tempBestMove = moves[i];
                    if (depthFromRoot == 0)
                    {
                        bestMoveThisIteration = moves[i];
                        bestEvalThisIteration = alpha;
                    }
                }
            }
            foreach (Move move in moves)
            {
                
            }

            if (alpha > originalAlpha)
            {
                transpositionTable.Store(board.posInfo.CurrentGameState.zobristHashKey, tempBestMove, depth, alpha, NodeType.PVNode);
            }
            else
            {
                transpositionTable.Store(board.posInfo.CurrentGameState.zobristHashKey, tempBestMove, depth, alpha, NodeType.AllNode);
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

            if (transpositionTable.TryGetValue(board.posInfo.CurrentGameState.zobristHashKey, out TranspositionTable.TranspositionEntry entry) && entry.DepthOfSearch >= 0)
            {
                if (entry.TypeOfNode == NodeType.PVNode)
                    return entry.EvaluationScore;
                if (entry.TypeOfNode == NodeType.CutNode && entry.EvaluationScore >= beta)
                    return entry.EvaluationScore;
                if (entry.TypeOfNode == NodeType.AllNode && entry.EvaluationScore <= alpha)
                    return entry.EvaluationScore;
            }

            int eval = evaluator.EvaluatePosition();
            searchInformation.PositionsEvaluated++;


            if (eval >= beta)
            {
                transpositionTable.Store(board.posInfo.CurrentGameState.zobristHashKey, default(Move), 0, beta, NodeType.CutNode);
                return beta;
            }

            if (eval > alpha)
            {
                alpha = eval;
            }

            Span<Move> captureMoves = moveGenerator.GenerateMoves(true);

            moveSorter.OrderMoveList(ref captureMoves, 0);
            Move tempBestMove = default;
            int originalAlpha = alpha;

            for (int i = 0; i < captureMoves.Length; i ++)
            {
                board.ExecuteMove(ref captureMoves[i]);
                eval = -QuiescenceSearch(-beta, -alpha);
                board.UndoMove(ref captureMoves[i]);

                if (searchedOneDepth && searchCancelled)
                {
                    return alpha;
                }

                if (eval >= beta)
                {
                    transpositionTable.Store(board.posInfo.CurrentGameState.zobristHashKey, captureMoves[i], 0, beta, NodeType.CutNode);
                    return beta;
                }
                if (eval > alpha)
                {
                    searchedOneDepth = true;
                    alpha = eval;
                    tempBestMove = captureMoves[i];
                }
            }

            if (alpha > originalAlpha)
            {
                transpositionTable.Store(board.posInfo.CurrentGameState.zobristHashKey, tempBestMove, 0, alpha, NodeType.PVNode);
            }
            else
            {
                transpositionTable.Store(board.posInfo.CurrentGameState.zobristHashKey, tempBestMove, 0, alpha, NodeType.AllNode);
            }
            return alpha;
        }
    }

}