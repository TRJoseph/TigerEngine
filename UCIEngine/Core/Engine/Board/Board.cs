using System;
using System.Linq;
using System.Collections.Generic;
using static Chess.PositionInformation;
using static Chess.MoveGen;
using System.Drawing;
//using static Chess.ZobristHashing;

namespace Chess
{
    public static class Board
    {
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

            public void ResetBitboards()
            {
                for(int pieceColor = 0; pieceColor < 2; pieceColor++)
                {
                    for (int type = 0; type < 6; type++)  // Assuming types from 0 (Pawn) to 5 (King)
                    {
                        Pieces[pieceColor, type] = 0;
                    }
                }

                // Reset composite bitboards
                AllWhitePieces = 0;
                AllBlackPieces = 0;
                AllPieces = 0;
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

            // assisting functions to help with pawn push move gen
            public static ulong NorthOne(ulong bitboard) { return bitboard << 8; }
            public static ulong SouthOne(ulong bitboard) { return bitboard >> 8; }
        }

        public static ChessBoard InternalBoard = ChessBoard.Create();

        public const int BoardSize = 64;

        public enum GameResult
        {
            InProgress,
            Stalemate,
            Checkmate,
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

            int toSquareIndex = BitBoardHelper.GetLSB(ref toSquare);
            int fromSquareIndex = BitBoardHelper.GetLSB(ref fromSquare);

            bool isEnPassant = move.specialMove is SpecialMove.EnPassant;

            bool isPromotion = move.IsPawnPromotion;

            int friendlyPieceColor = PositionInformation.MoveColorIndex;
            int opponentPieceColor = PositionInformation.OpponentColorIndex;

            // ChessBoard.None (-1) indicates that no piece was captured
            int capturedPieceType = move.capturedPieceType;

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
                    int captureSquareIndex = BitBoardHelper.GetLSB(ref captureSquare);
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
                int file = GetFile(BitBoardHelper.GetLSB(ref fromSquare));
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