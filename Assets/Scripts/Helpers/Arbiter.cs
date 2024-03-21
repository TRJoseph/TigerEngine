using System.Linq;
using static Chess.Board;
using static Chess.PositionInformation;
using static Chess.MoveGen;
using System.Diagnostics;

namespace Chess
{

    public static class Arbiter
    {
        public enum Sides
        {
            White = 0,
            Black = 1
        }

        public enum GameType
        {
            HumanVersusHuman,
            HumanVersusComputer,
            ComputerVersusComputer
        }

        // these will be updated and selected based on UI elements in some sort of main menu before the game is started
        public static Sides humanPlayer = Sides.White;

        public static Sides ComputerPlayer1 = Sides.White;

        public static Sides ComputerPlayer2 = Sides.Black;

        public static Sides CurrentTurn = Sides.White;

        public static GameType gameType = GameType.HumanVersusComputer;

        public static RandomMoveEngine randomMoveEngine = new();
        public static MiniMaxEngineV0 miniMaxEngineV0 = new();


        public static void MatchUpConfiguration()
        {
            StartGame(gameType);
        }


        public static void InitializeGame()
        {
            // load in fen string and set position information
            BoardManager.LoadFENString();

            // reset the game state and position hashes
            GameStateHistory.Clear();
            PositionHashes.Clear();

            UIController.Instance.GenerateGrid();
            UIController.Instance.RenderPiecesOnBoard();
            UIController.Instance.UpdateMoveStatusUIInformation();

            // generates zobrist hash key
            ZobristHashing.GenerateZobristHashes();

            // Set game state (note: calculating zobrist key relies on current game state)
            CurrentGameState = new GameState(0, PositionInformation.EnPassantFile, PositionInformation.CastlingRights, PositionInformation.halfMoveAccumulator, 0);
            ulong zobristHashKey = ZobristHashing.InitializeHashKey();
            CurrentGameState = new GameState(0, PositionInformation.EnPassantFile, PositionInformation.CastlingRights, PositionInformation.halfMoveAccumulator, zobristHashKey);

            GameStateHistory.Push(CurrentGameState);

            // run performance tests
            //Verification.RunPerformanceTests(5);
        }

        public static void StartGame(GameType gameType)
        {
            switch (gameType)
            {
                case GameType.HumanVersusHuman:
                    InitializeGame();
                    legalMoves = GenerateMoves();
                    currentStatus = GameResult.InProgress;
                    break;
                case GameType.HumanVersusComputer:

                    humanPlayer = Sides.White;
                    ComputerPlayer1 = Sides.Black;

                    InitializeGame();
                    legalMoves = GenerateMoves();
                    HvsCGame();
                    currentStatus = GameResult.InProgress;
                    break;

                case GameType.ComputerVersusComputer:
                    ComputerPlayer1 = Sides.White;
                    ComputerPlayer2 = Sides.Black;
                    break;
                default:
                    break;
            }


            // RandomMoveEngine randomMoveEngine = new();
            // MiniMaxEngineV0 miniMaxEngineV0 = new();
        }

        private static void HvsCGame()
        {
            //SwapTurn();

            if (ComputerPlayer1 == Sides.White && whiteToMove)
            {
                DoTurn(miniMaxEngineV0.FindBestMove(4).BestMove);
            }

            if (ComputerPlayer1 == Sides.Black && !whiteToMove)
            {
                DoTurn(miniMaxEngineV0.FindBestMove(4).BestMove);
            }
        }

        public static void DoTurn(Move move)
        {
            ExecuteMove(move);

            UIController.Instance.ClearExistingPieces();
            UIController.Instance.RenderPiecesOnBoard();

            legalMoves = GenerateMoves();

            // check for game over rules
            currentStatus = CheckForGameOverRules();

            if (currentStatus != GameResult.InProgress)
            {
                UnityEngine.Debug.Log("Game Over");
            }

            if (gameType == GameType.HumanVersusComputer)
            {
                HvsCGame();
            }
        }

        public static bool IsPlayerInCheck()
        {
            int currentKingSquare = PositionInformation.whiteToMove ? GetLSB(ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.King]) : GetLSB(ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.King]);
            // Check for pawn attacks
            ulong pawnAttacks = whiteToMove ?
                MoveTables.PrecomputedWhitePawnCaptures[currentKingSquare] :
                MoveTables.PrecomputedBlackPawnCaptures[currentKingSquare];
            if ((pawnAttacks & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Pawn]) != 0) return true;

            // Check for knight attacks
            ulong knightAttacks = MoveTables.PrecomputedKnightMoves[currentKingSquare];
            if ((knightAttacks & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Knight]) != 0) return true;

            // Check for sliding pieces (bishops, rooks, queens)
            ulong bishopQueenAttacks = GetBishopAttacks(InternalBoard.AllPieces, currentKingSquare);
            ulong rookQueenAttacks = GetRookAttacks(InternalBoard.AllPieces, currentKingSquare);

            ulong bishopsQueens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Bishop] |
                                  InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen];
            ulong rooksQueens = InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Rook] |
                                InternalBoard.Pieces[OpponentColorIndex, ChessBoard.Queen];

            if ((bishopQueenAttacks & bishopsQueens) != 0) return true;
            if ((rookQueenAttacks & rooksQueens) != 0) return true;

            // Check for king attacks (useful in edge cases and avoids self-check scenarios)
            ulong kingAttacks = MoveTables.PrecomputedKingMoves[currentKingSquare];
            if ((kingAttacks & InternalBoard.Pieces[OpponentColorIndex, ChessBoard.King]) != 0) return true;

            return false;
        }

        private static bool CheckForInsufficientMaterial()
        {
            // if any pawns on the board, not insufficient material
            if (CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn]) > 0)
            {
                return false;
            }

            // if rooks on the board, not insufficient material
            if (CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook]) > 0)
            {
                return false;
            }

            // if queens on the board, not insufficient material
            if (CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Queen]) > 0)
            {
                return false;
            }

            ulong whiteBishops = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Bishop];
            ulong blackBishops = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Bishop];

            // count knights and bishops
            int numOfWhiteKnights = CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Knight]);
            int numOfBlackKnights = CountBits(InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Knight]);
            int numOfWhiteBishops = CountBits(whiteBishops);
            int numOfBlackBishops = CountBits(blackBishops);

            int whiteMinorPieces = numOfWhiteKnights + numOfWhiteBishops;
            int blackMinorPieces = numOfBlackKnights + numOfBlackBishops;

            int minorPieces = whiteMinorPieces + blackMinorPieces;

            // king vs king and minor piece is a draw and king vs king is a draw
            if (minorPieces <= 1)
            {
                return true;
            }

            if (minorPieces == 2 && blackBishops == 1 && whiteBishops == 1)
            {
                // Check if bishops are on the same color squares by checking their intersection with light/dark squares bitboards
                bool blackBishopOnLightSquare = (blackBishops & MoveTables.lightSquares) != 0;
                bool whiteBishopOnLightSquare = (whiteBishops & MoveTables.lightSquares) != 0;

                // If both bishops are on light squares or both are on dark squares, it's a draw due to insufficient material
                return blackBishopOnLightSquare == whiteBishopOnLightSquare;
            }

            return false;
        }


        public static GameResult CheckForGameOverRules()
        {
            bool playerInCheck = IsPlayerInCheck();

            if (Board.legalMoveCount == 0 && playerInCheck)
            {
                return GameResult.CheckMate;
            }

            if (legalMoveCount == 0 && !playerInCheck)
            {
                return GameResult.Stalemate;
            }

            // a "move" consists of a player completing a turn followed by the opponent completing a turn, hence checking when this reaches 100, 50 moves have been made
            if (CurrentGameState.fiftyMoveCounter == 100)
            {
                return GameResult.FiftyMoveRule;
            }

            // threefold repetition rule (position repeats three times is a draw)
            if (PositionInformation.PositionHashes.Count(x => x == ZobristHashKey) >= 3)
            {
                return GameResult.ThreeFold;
            }

            // draw by insufficient material rule, for example: knight and king cannot deliver checkmate
            if (CheckForInsufficientMaterial())
            {
                return GameResult.InsufficientMaterial;
            }
            return GameResult.InProgress;
        }

        private static void SwapTurn()
        {
            CurrentTurn = CurrentTurn == Sides.White
                                    ? Sides.Black
                                    : Sides.White;
        }

        public static void ChooseSide(Sides playerSide)
        {
            humanPlayer = playerSide;
            ComputerPlayer1 = (playerSide == Sides.White) ? Sides.Black : Sides.White;
        }
    }

}