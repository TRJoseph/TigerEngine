using System.Linq;
using static Chess.Board;
using static Chess.PositionInformation;
using static Chess.MoveGen;
using System.Diagnostics;
using System.Collections;
using System.Xml.Serialization;

namespace Chess
{

    public class Arbiter
    {
        public static readonly string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        // this struct holds what engine version the computer will play using as well as the side it will play on (white or black)
        public struct ComputerPlayer
        {
            public Engine Engine;
        }

        // these will be the default search settings
        public static SearchSettings SearchSettings = new()
        {
            Depth = 4,
            SearchTime = TimeSpan.FromMilliseconds(1000),
            SearchType = SearchType.IterativeDeepening
        };

        /* when setting multiple computer players for a computer versus computer matchup,
         make sure that they are playing as opposite sides!! */
        public static ComputerPlayer ComputerPlayer1 = new()
        {
            Engine = new Engine(SearchSettings),
        };

        public static bool positionLoaded = false;


        public static void InitializeGame(string FENString)
        {
            // reset game status
            currentStatus = GameResult.InProgress;

            // reset the game state and position hashes
            GameStateHistory.Clear();
            PositionHashes.Clear();
            EnPassantFile = 0;
            CastlingRights = 0;

            BoardManager.InitializeBoard(FENString);

            // generates zobrist hash key
            ZobristHashing.GenerateZobristHashes();

            // Set game state (note: calculating zobrist key relies on current game state)
            CurrentGameState = new GameState(0, PositionInformation.EnPassantFile, PositionInformation.CastlingRights, PositionInformation.halfMoveAccumulator, 0);
            ulong zobristHashKey = ZobristHashing.InitializeHashKey();
            CurrentGameState = new GameState(0, PositionInformation.EnPassantFile, PositionInformation.CastlingRights, PositionInformation.halfMoveAccumulator, zobristHashKey);
            GameStateHistory.Push(CurrentGameState);

            legalMoves = GenerateMoves();

            //UICLI.PrintBoard(InternalBoard, whiteToMove);

            positionLoaded = true;
        }

        public static void DoTurn(Move move)
        {
            ExecuteMove(move);

            legalMoves = GenerateMoves();

            bool playerInCheck = IsPlayerInCheck();
            // check for game over rules
            currentStatus = CheckForGameOverRules(playerInCheck);

            //UICLI.PrintBoard(InternalBoard, whiteToMove);

            // Handle game over scenarios, if necessary
            if (currentStatus != GameResult.InProgress)
            {
                HandleGameOver();
                Array.Clear(legalMoves, 0, legalMoves.Length);
            }
        }

        public static void HandleGameOver()
        {
            switch(currentStatus)
            {
                case GameResult.Stalemate:
                    Console.WriteLine("Stalemate!");
                    break;
                case GameResult.Checkmate:
                    Console.WriteLine("Checkmate, " + (whiteToMove ? "black wins." : "white wins"));
                    break;
                case GameResult.ThreeFold:
                    Console.WriteLine("Draw by Threefold Repititon!");
                    break;
                case GameResult.FiftyMoveRule:
                    Console.WriteLine("Draw by 50-Move Rule.");
                    break;
                case GameResult.InsufficientMaterial:
                    Console.WriteLine("Draw by Insufficient Material.");
                    break;
                default:
                    return;
            }
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

            if(inSearch)
            {
                // if engine search, consider a single repeat of the position to be a draw for simplicity, this helps to avoid repeating positions over and over
                if (PositionHashes.Count(x => x == ZobristHashKey) >= 2)
                {
                    return GameResult.ThreeFold;
                }

            } else
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