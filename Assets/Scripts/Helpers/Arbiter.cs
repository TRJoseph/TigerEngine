using System.Linq;
using static Chess.Board;
using static Chess.PositionInformation;
using static Chess.MoveGen;
using System.Diagnostics;
using UnityEngine;
using System.Collections;

namespace Chess
{

    public class Arbiter : MonoBehaviour
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

        public static GameType gameType = GameType.ComputerVersusComputer;

        public static RandomMoveEngine randomMoveEngine = new();
        public static MiniMaxEngineV0 miniMaxEngineV0 = new();


        public static void MatchUpConfiguration()
        {
            //StartGame(gameType, 4);
            Verification.ComputerVsComputerMatches(10, 4);
        }


        public static void InitializeGame()
        {
            // create empty bitboards
            InternalBoard = ChessBoard.Create();

            // load in fen string, set position information, and fill bitboards
            BoardManager.LoadFENString();

            // reset game status
            currentStatus = GameResult.InProgress;

            // reset the game state and position hashes
            GameStateHistory.Clear();
            PositionHashes.Clear();

            UIController.Instance.ClearExistingPieces();
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

        public static void StartGame(GameType gameType, int searchDepth = 1, bool isLogging = false)
        {
            switch (gameType)
            {
                case GameType.HumanVersusHuman:
                    InitializeGame();
                    legalMoves = GenerateMoves();
                    break;
                case GameType.HumanVersusComputer:

                    humanPlayer = Sides.White;
                    ComputerPlayer1 = Sides.Black;

                    InitializeGame();
                    legalMoves = GenerateMoves();
                    HvsCGame(searchDepth);
                    break;

                case GameType.ComputerVersusComputer:
                    ComputerPlayer1 = Sides.White;
                    ComputerPlayer2 = Sides.Black;
                    InitializeGame();
                    if (isLogging)
                    {
                        // run this line to simply executer a computer versus computer game
                        ComputerVsComputerGame(searchDepth);
                    }
                    else
                    {
                        // run this to watch a game between two computers be played
                        UIController.Instance.StartComputerVsComputerGame(searchDepth);
                    }

                    break;
                default:
                    break;
            }
        }

        // human versus computer game 
        private static void HvsCGame(int opponentSearchDepth)
        {
            //SwapTurn();

            if (ComputerPlayer1 == Sides.White && whiteToMove)
            {
                DoTurn(miniMaxEngineV0.FindBestMove(opponentSearchDepth).BestMove);
            }

            if (ComputerPlayer1 == Sides.Black && !whiteToMove)
            {
                DoTurn(miniMaxEngineV0.FindBestMove(opponentSearchDepth).BestMove);
            }
        }

        // Verification.cs essentially calls this over and over to log engine performance versus previous iterations
        private static void ComputerVsComputerGame(int searchDepth)
        {
            while (currentStatus == GameResult.InProgress)
            {
                if (CurrentTurn == Sides.White)
                {
                    DoTurn(miniMaxEngineV0.FindBestMove(searchDepth).BestMove);
                }
                else
                {
                    DoTurn(randomMoveEngine.FindBestMove(searchDepth).BestMove);
                }

                if (currentStatus != GameResult.InProgress)
                {
                    return; // game is over
                }
                SwapTurn();
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

            if (gameType == GameType.HumanVersusComputer)
            {
                HvsCGame(4);
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

        public static void SwapTurn()
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