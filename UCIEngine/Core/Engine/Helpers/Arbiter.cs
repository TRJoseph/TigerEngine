using System.Linq;
using System.Diagnostics;
using System.Collections;
using System.Xml.Serialization;
using static Chess.Board;

namespace Chess
{

    public class Arbiter
    {
        // Forsyth-Edwards Notation representing the starting position in a chess game
        public static readonly string StartFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public bool positionLoaded = false;

        public MoveGen moveGenerator;
        public Board board;

        public Arbiter(MoveGen moveGenerator, Board board) {
            this.moveGenerator = moveGenerator;
            this.board = board;
        }


        // represents the current status of the game
        public enum GameResult
        {
            InProgress,
            Stalemate,
            Checkmate,
            ThreeFold,
            FiftyMoveRule,
            InsufficientMaterial
        }
       
        public void InitializeGame(string FENString)
        {
            // reset game status
            board.posInfo.currentStatus = GameResult.InProgress;

            // reset the game state and position hashes
            board.posInfo.GameStateHistory.Clear();
            board.posInfo.PositionHashes.Clear();
            board.posInfo.EnPassantFile = 0;
            board.posInfo.CastlingRights = 0;

            board.boardManager.InitializeBoard(FENString);

            // generates zobrist hash key
            board.zobristHashing.GenerateZobristHashes();

            // Set game state (note: calculating zobrist key relies on current game state)
            board.posInfo.CurrentGameState = new GameState(0, (byte)board.posInfo.EnPassantFile, (byte)board.posInfo.CastlingRights, (byte)board.posInfo.halfMoveAccumulator, 0);
            ulong zobristHashKey = board.zobristHashing.InitializeHashKey();
            board.posInfo.CurrentGameState = new GameState(0, (byte)board.posInfo.EnPassantFile, (byte)board.posInfo.CastlingRights, (byte)board.posInfo.halfMoveAccumulator, zobristHashKey);
            board.posInfo.GameStateHistory.Push(board.posInfo.CurrentGameState);

            moveGenerator.legalMoves = moveGenerator.GenerateMoves();

            //UICLI.PrintBoard(InternalBoard, whiteToMove);

            positionLoaded = true;
        }

        public void DoTurn(MoveGen.Move move)
        {
            board.ExecuteMove(ref move);

            moveGenerator.legalMoves = moveGenerator.GenerateMoves();

            // check for game over rules
            bool playerInCheck = IsPlayerInCheck(board.posInfo);
            board.posInfo.currentStatus = CheckForGameOverRules(moveGenerator, board.posInfo, playerInCheck);

            //UICLI.PrintBoard(InternalBoard, whiteToMove);

            // Handle game over scenarios, if necessary
            if (board.posInfo.currentStatus != GameResult.InProgress)
            {
                HandleGameOver(board.posInfo);
                Array.Clear(moveGenerator.legalMoves, 0, moveGenerator.legalMoves.Length);
            }
        }

        public static void HandleGameOver(PositionInformation posInfo)
        {
            switch(posInfo.currentStatus)
            {
                case GameResult.Stalemate:
                    Console.WriteLine("Stalemate!");
                    break;
                case GameResult.Checkmate:
                    Console.WriteLine("Checkmate, " + (posInfo.whiteToMove ? "black wins." : "white wins"));
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

        public static bool IsPlayerInCheck(PositionInformation posInfo)
        {
            int currentKingSquare = posInfo.whiteToMove ? BitBoardHelper.GetLSB(ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.King]) : BitBoardHelper.GetLSB(ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.King]);
            // Check for pawn attacks
            ulong pawnAttacks = posInfo.whiteToMove ?
                MoveTables.PrecomputedWhitePawnCaptures[currentKingSquare] :
                MoveTables.PrecomputedBlackPawnCaptures[currentKingSquare];
            if ((pawnAttacks & InternalBoard.Pieces[posInfo.OpponentColorIndex, ChessBoard.Pawn]) != 0) return true;

            // Check for knight attacks
            ulong knightAttacks = MoveTables.PrecomputedKnightMoves[currentKingSquare];
            if ((knightAttacks & InternalBoard.Pieces[posInfo.OpponentColorIndex, ChessBoard.Knight]) != 0) return true;

            // Check for sliding pieces (bishops, rooks, queens)
            ulong bishopQueenAttacks = MoveGen.GetBishopAttacks(InternalBoard.AllPieces, currentKingSquare);
            ulong rookQueenAttacks = MoveGen.GetRookAttacks(InternalBoard.AllPieces, currentKingSquare);

            ulong bishopsQueens = InternalBoard.Pieces[posInfo.OpponentColorIndex, ChessBoard.Bishop] |
                                  InternalBoard.Pieces[posInfo.OpponentColorIndex, ChessBoard.Queen];
            ulong rooksQueens = InternalBoard.Pieces[posInfo.OpponentColorIndex, ChessBoard.Rook] |
                                InternalBoard.Pieces[posInfo.OpponentColorIndex, ChessBoard.Queen];

            if ((bishopQueenAttacks & bishopsQueens) != 0) return true;
            if ((rookQueenAttacks & rooksQueens) != 0) return true;

            // Check for king attacks (useful in edge cases and avoids self-check scenarios)
            ulong kingAttacks = MoveTables.PrecomputedKingMoves[currentKingSquare];
            if ((kingAttacks & InternalBoard.Pieces[posInfo.OpponentColorIndex, ChessBoard.King]) != 0) return true;

            return false;
        }

        private static bool CheckForInsufficientMaterial()
        {
            // if any pawns on the board, not insufficient material
            if (Board.CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn]) > 0)
            {
                return false;
            }

            // if rooks on the board, not insufficient material
            if (Board.CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook]) > 0)
            {
                return false;
            }

            // if queens on the board, not insufficient material
            if (Board.CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Queen]) > 0)
            {
                return false;
            }

            ulong whiteBishops = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Bishop];
            ulong blackBishops = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Bishop];

            // count knights and bishops
            int numOfWhiteKnights = Board.CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Knight]);
            int numOfBlackKnights = Board.CountBits(InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Knight]);
            int numOfWhiteBishops = Board.CountBits(whiteBishops);
            int numOfBlackBishops = Board.CountBits(blackBishops);

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


        public static GameResult CheckForGameOverRules(MoveGen moveGenerator, PositionInformation posInfo, bool playerInCheck, bool inSearch = false)
        {

            if (moveGenerator.currentMoveIndex == 0 && playerInCheck)
            {
                return GameResult.Checkmate;
            }

            if (moveGenerator.currentMoveIndex == 0 && !playerInCheck)
            {
                return GameResult.Stalemate;
            }

            // a "move" consists of a player completing a turn followed by the opponent completing a turn, hence checking when this reaches 100, 50 moves have been made
            if (posInfo.CurrentGameState.fiftyMoveCounter >= 100)
            {
                return GameResult.FiftyMoveRule;
            }

            if(inSearch)
            {
                // if engine search, consider a single repeat of the position to be a draw for simplicity, this helps to avoid repeating positions over and over
                if (posInfo.PositionHashes.Count(x => x == posInfo.ZobristHashKey) >= 2)
                {
                    return GameResult.ThreeFold;
                }

            } else
            {
                // threefold repetition rule (position repeats three times is a draw)
                if (posInfo.PositionHashes.Count(x => x == posInfo.ZobristHashKey) >= 3)
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