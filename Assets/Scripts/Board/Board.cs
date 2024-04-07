using System;
using System.Linq;
using System.Collections.Generic;
using static Chess.PositionInformation;
using static Chess.MoveGen;
//using static Chess.ZobristHashing;

namespace Chess
{
    public static class Board
    {
        public static BoardManager boardManager;
        public struct ChessBoard
        {
            // https://www.chessprogramming.org/Bitboard_Board-Definition
            public const int None = -1;
            public const int Pawn = 0;
            public const int Bishop = 1;
            public const int Knight = 2;
            public const int Rook = 3;
            public const int Queen = 4;
            public const int King = 5;

            public const int White = 0;
            public const int Black = 1;

            public ulong[,] Pieces;
            public static ChessBoard Create()
            {
                var board = new ChessBoard
                {
                    Pieces = new ulong[2, 6]
                };
                return board;
            }

            public void UpdateCompositeBitboards()
            {
                AllWhitePieces = 0;
                AllBlackPieces = 0;
                for (int pieceType = Pawn; pieceType <= King; pieceType++)
                {
                    AllWhitePieces |= Pieces[White, pieceType];
                    AllBlackPieces |= Pieces[Black, pieceType];
                }
                AllPieces = AllWhitePieces | AllBlackPieces;
            }
            public ulong AllWhitePieces;
            public ulong AllBlackPieces;
            public ulong AllPieces;

            public readonly ulong NorthOne(ulong bitboard) { return bitboard << 8; }
            public readonly ulong EastOne(ulong bitboard) { return (bitboard << 1) & HFileMask; }
            public readonly ulong NorthEastOne(ulong bitboard) { return (bitboard << 9) & HFileMask; }
            public readonly ulong SouthEastOne(ulong bitboard) { return (bitboard >> 7) & HFileMask; }
            public readonly ulong WestOne(ulong bitboard) { return (bitboard >> 1) & AFileMask; }
            public readonly ulong SouthWestOne(ulong bitboard) { return (bitboard >> 9) & AFileMask; }
            public readonly ulong NorthWestOne(ulong bitboard) { return (bitboard << 7) & AFileMask; }
            public readonly ulong SouthOne(ulong bitboard) { return bitboard >> 8; }

            /* knight move offsets
             * Northeasteast, represents a knight move in an L shape that is one square up and two squares right */
            public readonly ulong NorthNorthEast(ulong bitboard) { return (bitboard << 17) & HFileMask; }
            public readonly ulong NorthEastEast(ulong bitboard) { return (bitboard << 10) & GHFileMask; }
            public readonly ulong SouthEastEast(ulong bitboard) { return (bitboard >> 6) & GHFileMask; }
            public readonly ulong SouthSouthEast(ulong bitboard) { return (bitboard >> 15) & HFileMask; }
            public readonly ulong NorthNorthWest(ulong bitboard) { return (bitboard << 15) & AFileMask; }
            public readonly ulong NorthWestWest(ulong bitboard) { return (bitboard << 6) & ABFileMask; }
            public readonly ulong SouthWestWest(ulong bitboard) { return (bitboard >> 10) & ABFileMask; }
            public readonly ulong SouthSouthWest(ulong bitboard) { return (bitboard >> 17) & AFileMask; }

        }

        public static ChessBoard InternalBoard;

        public const int BoardSize = 64;

        // masks to prevent A file and H file wrapping for legal move calculations
        public const ulong AFileMask = 0x7f7f7f7f7f7f7f7f;
        public const ulong HFileMask = 0xfefefefefefefefe;

        // masks to prevent knight jumps from wrapping 
        public const ulong ABFileMask = 0x3F3F3F3F3F3F3F3F;
        public const ulong GHFileMask = 0xFCFCFCFCFCFCFCFC;

        private const ulong deBruijn64 = 0x37E84A99DAE458F;
        private static readonly int[] deBruijnTable =
        {
            0, 1, 17, 2, 18, 50, 3, 57,
            47, 19, 22, 51, 29, 4, 33, 58,
            15, 48, 20, 27, 25, 23, 52, 41,
            54, 30, 38, 5, 43, 34, 59, 8,
            63, 16, 49, 56, 46, 21, 28, 32,
            14, 26, 24, 40, 53, 37, 42, 7,
            62, 55, 45, 31, 13, 39, 36, 6,
            61, 44, 12, 35, 60, 11, 10, 9
        };

        // Get index of least significant set bit in given 64bit value. Also clears the bit to zero.
        public static int GetLSB(ref ulong b)
        {
            int i = deBruijnTable[((ulong)((long)b & -(long)b) * deBruijn64) >> 58];
            return i;
        }

        public enum SpecialMove
        {
            None = 0,
            KingSideCastleMove = 1,
            QueenSideCastleMove = 2,
            EnPassant = 3,
            TwoSquarePawnMove = 4,
        }

        public enum PromotionFlags
        {
            None = 0,
            PromoteToQueenFlag = 1,
            PromoteToRookFlag = 2,
            PromoteToBishopFlag = 3,
            PromoteToKnightFlag = 4
        }

        // this structure will hold a move that can be executed
        public struct Move
        {
            // 'startSquare' and 'endSquare' holds the internal board start square and end square 
            public ulong fromSquare;

            public ulong toSquare;

            public int movedPiece;

            // special move flags
            public SpecialMove specialMove;

            public bool IsPawnPromotion;

            public PromotionFlags? promotionFlag;

            public readonly bool IsDefault()
            {
                return fromSquare == 0 && movedPiece == 0 && toSquare == 0 && specialMove == SpecialMove.None;
            }
        }

        public static Move[] legalMoves = new Move[256];

        // this holds the current move index for the move list actively being computed.
        // Allows for more efficient move list computations using statically allocated memory on the stack
        public static int currentMoveIndex;

        public static int legalMoveCount;

        public static PromotionFlags promotionSelection;

        public enum GameResult
        {
            InProgress,
            Stalemate,
            CheckMate,
            ThreeFold,
            FiftyMoveRule,
            InsufficientMaterial
        }

        private static void RemoveAndAddPieceBitboards(int movedPiece, int pieceColor, ulong fromSquareMask, ulong toSquareMask)
        {
            InternalBoard.Pieces[pieceColor, movedPiece] &= ~fromSquareMask;
            InternalBoard.Pieces[pieceColor, movedPiece] |= toSquareMask;

            // ZobristHashKey ^= pieceAtEachSquareArray[pieceColor, movedPiece, fromSquareIndex];
            // ZobristHashKey ^= pieceAtEachSquareArray[pieceColor, movedPiece, toSquareIndex];
            return;
        }

        public static int CountBits(ulong bits)
        {
            int count = 0;
            while (bits != 0)
            {
                count += 1;
                bits &= bits - 1; // Remove the lowest set bit
            }
            return count;
        }


        public static int GetPieceAtSquare(int pieceColorIndex, ulong square)
        {

            // Check if a piece is captured
            for (int pieceType = ChessBoard.Pawn; pieceType <= ChessBoard.King; pieceType++)
            {
                if ((InternalBoard.Pieces[pieceColorIndex, pieceType] & square) != 0)
                {
                    return pieceType;
                }
            }

            return ChessBoard.None;
        }

        public static void ExecuteMove(Move move, bool inSearch = false)
        {
            int movedPiece = move.movedPiece;
            ulong toSquare = move.toSquare;
            ulong fromSquare = move.fromSquare;

            int toSquareIndex = GetLSB(ref toSquare);
            int fromSquareIndex = GetLSB(ref fromSquare);

            bool isEnPassant = move.specialMove is SpecialMove.EnPassant;

            bool isPromotion = move.IsPawnPromotion;

            int friendlyPieceColor = PositionInformation.MoveColorIndex;
            int opponentPieceColor = PositionInformation.OpponentColorIndex;

            // -1 signifies no piece was captured
            int capturedPieceType = isEnPassant ? ChessBoard.Pawn : GetPieceAtSquare(opponentPieceColor, toSquare);

            int prevCastleState = CurrentGameState.castlingRights;
            int prevEnPassantFile = CurrentGameState.enPassantFile;
            ulong newZobristKey = CurrentGameState.zobristHashKey;
            int newCastlingRights = CurrentGameState.castlingRights;
            int newEnPassantFile = 0;


            // this moves the piece from its old position to its new position
            RemoveAndAddPieceBitboards(movedPiece, friendlyPieceColor, fromSquare, toSquare);

            // a piece was captured
            if (capturedPieceType != ChessBoard.None)
            {
                ulong captureSquare = toSquare;

                if (isEnPassant)
                {
                    captureSquare = whiteToMove ? captureSquare >> 8 : captureSquare << 8;
                    int captureSquareIndex = GetLSB(ref captureSquare);
                    captureSquareIndex += whiteToMove ? -8 : 8;

                    // remove en passant captured piece
                    newZobristKey ^= ZobristHashing.pieceAtEachSquareArray[opponentPieceColor, capturedPieceType, captureSquareIndex];
                }
                else
                {
                    // remove captured piece from zobrist hash
                    newZobristKey ^= ZobristHashing.pieceAtEachSquareArray[opponentPieceColor, capturedPieceType, toSquareIndex];
                }

                InternalBoard.Pieces[opponentPieceColor, capturedPieceType] &= ~captureSquare;

            }

            // Handle king
            if (movedPiece is ChessBoard.King)
            {
                newCastlingRights &= whiteToMove ? 0b1100 : 0b0011;

                // Handle castling
                if (move.specialMove == SpecialMove.KingSideCastleMove)
                {
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] &= ~(toSquare << 1);
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] |= toSquare >> 1;

                    newZobristKey ^= ZobristHashing.pieceAtEachSquareArray[friendlyPieceColor, ChessBoard.Rook, toSquareIndex + 1];
                    newZobristKey ^= ZobristHashing.pieceAtEachSquareArray[friendlyPieceColor, ChessBoard.Rook, toSquareIndex - 1];
                }

                if (move.specialMove == SpecialMove.QueenSideCastleMove)
                {
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] &= ~(toSquare >> 2);
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] |= toSquare << 1;

                    newZobristKey ^= ZobristHashing.pieceAtEachSquareArray[friendlyPieceColor, ChessBoard.Rook, toSquareIndex - 2];
                    newZobristKey ^= ZobristHashing.pieceAtEachSquareArray[friendlyPieceColor, ChessBoard.Rook, toSquareIndex + 1];
                }
            }

            // Handle promotion
            if (isPromotion)
            {
                int promotionPieceType = move.promotionFlag switch
                {
                    PromotionFlags.PromoteToQueenFlag => ChessBoard.Queen,
                    PromotionFlags.PromoteToRookFlag => ChessBoard.Rook,
                    PromotionFlags.PromoteToBishopFlag => ChessBoard.Bishop,
                    PromotionFlags.PromoteToKnightFlag => ChessBoard.Knight,
                    _ => 0
                };

                // Remove pawn from promotion square and add promoted piece instead
                InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Pawn] &= ~toSquare;
                InternalBoard.Pieces[friendlyPieceColor, promotionPieceType] |= toSquare;
            }

            // Pawn has moved two forwards, mark file with en-passant flag
            if (move.specialMove == SpecialMove.TwoSquarePawnMove)
            {
                int file = GetFile(GetLSB(ref fromSquare));
                newEnPassantFile = file + 1;
                newZobristKey ^= ZobristHashing.enPassantFile[file];
            }

            if (prevCastleState != 0)
            {
                // Any piece moving to/from rook square removes castling right for that side
                if (toSquare == BoardHelper.h1 || fromSquare == BoardHelper.h1)
                {
                    newCastlingRights &= GameState.ClearWhiteKingsideMask;
                }
                else if (toSquare == BoardHelper.a1 || fromSquare == BoardHelper.a1)
                {
                    newCastlingRights &= GameState.ClearWhiteQueensideMask;
                }
                if (toSquare == BoardHelper.h8 || fromSquare == BoardHelper.h8)
                {
                    newCastlingRights &= GameState.ClearBlackKingsideMask;
                }
                else if (toSquare == BoardHelper.a8 || fromSquare == BoardHelper.a8)
                {
                    newCastlingRights &= GameState.ClearBlackQueensideMask;
                }
            }

            // Update zobrist key with new piece position and side to move
            newZobristKey ^= ZobristHashing.sideToMove;
            newZobristKey ^= ZobristHashing.pieceAtEachSquareArray[friendlyPieceColor, movedPiece, fromSquareIndex];
            newZobristKey ^= ZobristHashing.pieceAtEachSquareArray[friendlyPieceColor, movedPiece, toSquareIndex];
            newZobristKey ^= ZobristHashing.enPassantFile[prevEnPassantFile];

            if (newCastlingRights != prevCastleState)
            {
                newZobristKey ^= ZobristHashing.castlingRightsArray[prevCastleState]; // remove old castling rights
                newZobristKey ^= ZobristHashing.castlingRightsArray[newCastlingRights]; // add new castling rights
            }

            whiteToMove = !whiteToMove;

            halfMoveAccumulator++;

            int newFiftyMoveCounter = CurrentGameState.fiftyMoveCounter + 1;


            InternalBoard.UpdateCompositeBitboards();

            // Pawn moves and captures reset the fifty move counter
            if (movedPiece == ChessBoard.Pawn || capturedPieceType != ChessBoard.None)
            {
                newFiftyMoveCounter = 0;
            }

            GameState newState = new(capturedPieceType, newEnPassantFile, newCastlingRights, newFiftyMoveCounter, newZobristKey);

            GameStateHistory.Push(newState);
            CurrentGameState = newState;

            if (!inSearch)
            {
                PositionHashes.Push(newState.zobristHashKey);
            }
        }

        public static void UndoMove(Move move, bool inSearch = false)
        {
            whiteToMove = !whiteToMove;

            ulong toSquare = move.toSquare;
            ulong fromSquare = move.fromSquare;
            int movedPiece = move.movedPiece;

            bool undoingEnPassant = move.specialMove == SpecialMove.EnPassant;
            bool undoingPromotion = move.IsPawnPromotion;
            bool undoingCapture = CurrentGameState.capturedPieceType != ChessBoard.None;

            int capturedPieceType = CurrentGameState.capturedPieceType;

            //int movedPiece = undoingPromotion ? ChessBoard.Pawn : InternalBoard.Pieces[];

            int friendlyPieceColor = PositionInformation.MoveColorIndex;
            int opponentPieceColor = PositionInformation.OpponentColorIndex;

            // undo promotion

            if (undoingPromotion)
            {
                int promotedPiece = GetPieceAtSquare(friendlyPieceColor, toSquare);

                // places pawn back at promotion square on either 8th or 1st rank and removes promoted piece
                InternalBoard.Pieces[friendlyPieceColor, promotedPiece] &= ~toSquare;
                InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Pawn] |= toSquare;
            }

            RemoveAndAddPieceBitboards(movedPiece, friendlyPieceColor, toSquare, fromSquare);

            // undo any capture move (en passant or normal)
            if (undoingCapture)
            {
                ulong captureSquare = toSquare;

                if (undoingEnPassant)
                {
                    captureSquare = whiteToMove ? captureSquare >> 8 : captureSquare << 8;
                }

                InternalBoard.Pieces[opponentPieceColor, capturedPieceType] |= captureSquare;

            }

            if (movedPiece is ChessBoard.King)
            {
                if (move.specialMove == SpecialMove.KingSideCastleMove)
                {
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] &= ~(toSquare >> 1);

                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] |= toSquare << 1;
                }

                if (move.specialMove == SpecialMove.QueenSideCastleMove)
                {
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] &= ~(toSquare << 1);
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] |= toSquare >> 2;
                }
            }

            InternalBoard.UpdateCompositeBitboards();

            if (!inSearch && PositionHashes.Count > 0)
            {
                PositionHashes.Pop();
            }

            // go back to previous game state
            GameStateHistory.Pop();
            CurrentGameState = GameStateHistory.Peek();
            halfMoveAccumulator--;
        }
    }
}