using System.Linq;
using static Chess.Board;
using static Chess.PositionInformation;
using static Chess.MoveGen;
using System.Diagnostics;
using UnityEngine;
using System.Collections;
using System;
using System.Threading.Tasks;

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
            public Engine Engine;
            public SearchSettings SearchSettings;
        }

        /* when setting multiple computer players for a computer versus computer matchup,
         make sure that they are playing as opposite sides!! */
        public static ComputerPlayer ComputerPlayer1 = new()
        {
            Side = Sides.White,
            Engine = new Engine(),
            SearchSettings = new SearchSettings()
            {
                Depth = 4,
                SearchTime = TimeSpan.FromSeconds(3),
                SearchType = SearchType.IterativeDeepening,
            }
        };

        public static ComputerPlayer ComputerPlayer2 = new()
        {
            Side = Sides.Black,
            Engine = new Engine(),
            SearchSettings = new SearchSettings()
            {
                Depth = 4,
                SearchTime = TimeSpan.FromSeconds(3),
                SearchType = SearchType.IterativeDeepening,
            }
        };


        // set for the desired game type
        public static GameType gameType = GameType.HumanVersusComputer;

        public static Sides currentTurn = whiteToMove ? Sides.White : Sides.Black;

        public static void MatchUpConfiguration()
        {
            StartGame(gameType);

            // runs three matches of computer versus computer
            //Verification.ComputerVsComputerMatches(3);
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

            UIController.Instance.GenerateGrid();
            UIController.Instance.GenerateFileAndRankLabels();
            UIController.Instance.SetGamePerspective();
            UIController.Instance.RenderPiecesOnBoard();
            UIController.Instance.UpdateToMoveText();

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

        public static void StartGame(GameType gameType, bool isLogging = false)
        {
            switch (gameType)
            {
                case GameType.HumanVersusHuman:
                    InitializeGame();
                    break;
                case GameType.HumanVersusComputer:
                    InitializeGame();
                    HvsCGame();
                    break;

                case GameType.ComputerVersusComputer:
                    InitializeGame();
                    // run this to watch a game between two computers be played
                    ComputerVsComputerGame();

                    break;
                default:
                    break;
            }
        }

        // human versus computer game 
        private static void HvsCGame()
        {

            if (ComputerPlayer1.Side == Sides.White && whiteToMove)
            {
                ComputerPlayer1.Engine.StartSearchAsync(ComputerPlayer1.SearchSettings);
            }

            if (ComputerPlayer1.Side == Sides.Black && !whiteToMove)
            {
                ComputerPlayer1.Engine.StartSearchAsync(ComputerPlayer1.SearchSettings);
            }
        }

        // Verification.cs essentially calls this over and over to log engine performance versus previous iterations
        private static async Task ComputerVsComputerGame()
        {
            while (currentStatus == GameResult.InProgress)
            {

                Engine currentEngine = (currentTurn == ComputerPlayer1.Side) ? ComputerPlayer1.Engine : ComputerPlayer2.Engine;

                await currentEngine.StartSearchAsync(ComputerPlayer1.SearchSettings);

                // toggle the current turn to the other side
                ToggleTurn();
            }
        }

        public static void DoTurn(Move move)
        {
            ExecuteMove(move);

            UIController.Instance.UpdatePieceRenders();

            legalMoves = GenerateMoves();

            bool playerInCheck = IsPlayerInCheck();
            // check for game over rules
            currentStatus = CheckForGameOverRules(playerInCheck);

            UIController.Instance.UpdateToMoveText();

            if (gameType == GameType.HumanVersusComputer)
            {
                HvsCGame();
            }
        }

        public static void ToggleTurn()
        {
            currentTurn = currentTurn == Sides.White ? Sides.Black : Sides.White;
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


        public static GameResult CheckForGameOverRules(bool playerInCheck, bool inSearch = false)
        {

            if (legalMoveCount == 0 && playerInCheck)
            {
                return GameResult.Checkmate;
            }

            if (legalMoveCount == 0 && !playerInCheck)
            {
                return GameResult.Stalemate;
            }

            // a "move" consists of a player completing a turn followed by the opponent completing a turn, hence checking when this reaches 100, 50 moves have been made
            if (CurrentGameState.fiftyMoveCounter >= 100)
            {
                return GameResult.FiftyMoveRule;
            }

            if (inSearch)
            {
                // if engine search, consider a single repeat of the position to be a draw for simplicity, this helps to avoid repeating positions over and over
                if (PositionHashes.Count(x => x == ZobristHashKey) >= 2)
                {
                    return GameResult.ThreeFold;
                }

            }
            else
            {
                // threefold repetition rule (position repeats three times is a draw)
                if (PositionHashes.Count(x => x == ZobristHashKey) >= 3)
                {
                    return GameResult.ThreeFold;
                }
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