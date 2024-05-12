using System.Linq;
using static Chess.Board;
using static Chess.PositionInformation;
using static Chess.MoveGen;
using System.Diagnostics;
using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

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

        // this struct holds what engine version the computer will play using as well as the side it will play on (white or black)
        public struct ComputerPlayer
        {
            public Sides Side;
            public ChessEngineProcess ChessEngineProcess;
            public string enginePath;
        }

        /* when setting multiple computer players for a computer versus computer matchup,
         make sure that they are playing as opposite sides!! */
        public static ComputerPlayer ComputerPlayer1 = new()
        {
            Side = Sides.White,
            ChessEngineProcess = new()
        };

        public static ComputerPlayer ComputerPlayer2 = new()
        {
            Side = Sides.Black,
            ChessEngineProcess = new()
        };

        // set for the desired game type
        public static GameType gameType = GameType.ComputerVersusComputer;

        public static void InitializeGame(string FENString, bool isLogging = false)
        {
            // reset game status
            currentStatus = GameResult.InProgress;

            MoveHistory.Clear();
            GameStateHistory.Clear();
            PositionHashes.Clear();
            EnPassantFile = 0;
            CastlingRights = 0;

            // load in fen string, set position information, and fill bitboards
            BoardManager.InitializeBoard(FENString);

            if(!isLogging)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    UIController.Instance.ClearExistingPieces();
                    UIController.Instance.RenderPiecesOnBoard();
                    UIController.Instance.UpdateToMoveText();
                });
            }

            // generates zobrist hash key
            ZobristHashing.GenerateZobristHashes();

            // Set game state (note: calculating zobrist key relies on current game state)
            CurrentGameState = new GameState(0, PositionInformation.EnPassantFile, PositionInformation.CastlingRights, PositionInformation.halfMoveAccumulator, 0);
            ulong zobristHashKey = ZobristHashing.InitializeHashKey();
            CurrentGameState = new GameState(0, PositionInformation.EnPassantFile, PositionInformation.CastlingRights, PositionInformation.halfMoveAccumulator, zobristHashKey);
            GameStateHistory.Push(CurrentGameState);

            // run performance tests
            //Verification.RunPerformanceTests(5);

            legalMoves = GenerateMoves();
        }

        public static void StartGame(string FENString, bool isLogging = false)
        {
            // Unsubscribe previous event handlers to ensure a clean state
            ComputerPlayer1.ChessEngineProcess.OnMoveReady -= HandleMoveReadyForLogging;
            ComputerPlayer1.ChessEngineProcess.OnMoveReady -= HandleMoveReadyWithUI;
            ComputerPlayer2.ChessEngineProcess.OnMoveReady -= HandleMoveReadyForLogging;
            ComputerPlayer2.ChessEngineProcess.OnMoveReady -= HandleMoveReadyWithUI;

            InitializeGame(FENString, isLogging);

            if (isLogging)
            {
                // Subscribe to a simplified version of event handling that does not involve UI updates
                ComputerPlayer1.ChessEngineProcess.OnMoveReady += HandleMoveReadyForLogging;
                ComputerPlayer2.ChessEngineProcess.OnMoveReady += HandleMoveReadyForLogging;

                // Start the game in a logging or automated mode
                StartComputerVersusComputerGame();
            }
            else
            {
                // Subscribe to the full event handling that updates the UI
                ComputerPlayer1.ChessEngineProcess.OnMoveReady += HandleMoveReadyWithUI;
                ComputerPlayer2.ChessEngineProcess.OnMoveReady += HandleMoveReadyWithUI;

                // Start the game with UI updates and interactions
                UIController.Instance.StartComputerVersusComputerGame();
            }
        }

        private static void HandleMoveReadyForLogging(string move)
        {
            MoveHistory.Add(move);
            if (currentStatus == GameResult.InProgress) // Make sure the game is still ongoing
            {
                ApplyMove(move, isLogging: true);
                StartTurn();
            }
        }

        public static void HandleMoveReadyWithUI(string move)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                MoveHistory.Add(move);
                if (currentStatus == GameResult.InProgress) // Make sure the game is still ongoing
                {
                    ApplyMove(move, isLogging: false);
                    StartTurn();
                }
            });
        }

        private static void StartComputerVersusComputerGame()
        {
            // replace these directories with the desired engines
            ComputerPlayer1.ChessEngineProcess.StartEngine(ComputerPlayer1.enginePath);
            ComputerPlayer2.ChessEngineProcess.StartEngine(ComputerPlayer2.enginePath);

            StartTurn();
        }

        public static void StartTurn()
        {
            string moveString = FormatAppliedMoves();
            ChessEngineProcess targetEngine = GetTargetEngine();

            targetEngine.SendCommand("position fen " + GameStartFENString + " " + moveString);
            targetEngine.SendCommand("go");

            if(currentStatus != GameResult.InProgress) {
                GameManagement.TriggerCheckGameCompletion();
            }
        }
        private static ChessEngineProcess GetTargetEngine()
        {
            return (whiteToMove ? ComputerPlayer1.Side == Sides.White : ComputerPlayer1.Side == Sides.Black)
                ? ComputerPlayer1.ChessEngineProcess
                : ComputerPlayer2.ChessEngineProcess;
        }

        public static string FormatAppliedMoves()
        {
            if(MoveHistory.Count > 0)
            {
                string currentString = "moves";

                foreach (var move in MoveHistory)
                {
                    currentString += " ";
                    currentString += move;
                }

                return currentString;
            } else
            {
                return "";
            }
        }

        public static void ApplyMove(string move, bool isLogging = false)
        {
            // Validate the move format: e.g., "e2e4" or "e7e8q" for promotion
            if (move.Length < 4)
            {
                Console.WriteLine("Invalid move format, moves were not applied: " + move);
                return; // Exit the function if the format is incorrect
            }

            string fromSquare = move[..2];
            string toSquare = move[2..4]; // Extract the 'to' square from the move
            char? promotionChar = move.Length > 4 ? move[4] : null; // Check for promotion character

            // Convert square notation to bitboard positions
            ulong fromBitboard = BoardHelper.GetSquareBitboard(fromSquare);
            ulong toBitboard = BoardHelper.GetSquareBitboard(toSquare);

            // Find a matching move, considering promotion if applicable
            Move selectedMove = FindMatchingMove(fromBitboard, toBitboard, promotionChar);

            // Perform the move
            DoTurn(selectedMove, isLogging);
        }

        private static Move FindMatchingMove(ulong fromBitboard, ulong toBitboard, char? promotionChar)
        {
            IEnumerable<Move> candidateMoves = legalMoves.Where(x =>
                x.fromSquare == fromBitboard && x.toSquare == toBitboard);

            if (promotionChar.HasValue)
            {
                // Map the promotion character to a specific promotion flag
                PromotionFlags? flag = ConvertCharToPromotionFlag(promotionChar.Value);
                if (flag.HasValue)
                {
                    candidateMoves = candidateMoves.Where(x => x.promotionFlag == flag.Value);
                }
                else
                {
                    Console.WriteLine("Invalid promotion character: " + promotionChar.Value);
                }
            }

            return candidateMoves.SingleOrDefault(); // Expect exactly one matching move
        }

        private static PromotionFlags? ConvertCharToPromotionFlag(char promotionChar)
        {
            switch (promotionChar)
            {
                case 'q': return PromotionFlags.PromoteToQueenFlag;
                case 'r': return PromotionFlags.PromoteToRookFlag;
                case 'k': return PromotionFlags.PromoteToKnightFlag;
                case 'b': return PromotionFlags.PromoteToBishopFlag;
                default:
                    Console.WriteLine($"Unknown promotion character: {promotionChar}");
                    return null;
            }
        }


        public static void DoTurn(Move move, bool isLogging = false)
        {
            ExecuteMove(move);

            // if we are not logging, update the UI so user can watch the game
            if(!isLogging)
            {
                UIController.Instance.UpdatePieceRenders();
                UIController.Instance.UpdateToMoveText();
            }

            legalMoves = GenerateMoves();
            // check for game over rules
            currentStatus = CheckForGameOverRules();
        }

        public static bool IsPlayerInCheck()
        {
            int currentKingSquare = whiteToMove ? BitBoardHelper.GetLSB(ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.King]) : BitBoardHelper.GetLSB(ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.King]);
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

            if (legalMoveCount == 0 && playerInCheck)
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
    }

}