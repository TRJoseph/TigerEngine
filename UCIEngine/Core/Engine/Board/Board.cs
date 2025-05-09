using System;
using System.Linq;
using System.Collections.Generic;
using static Chess.PositionInformation;
using static Chess.MoveGen;
using System.Drawing;
//using static Chess.ZobristHashing;

namespace Chess
{
    public class Board
    {
        // this class contains all information about the current board position
        public PositionInformation posInfo;
        public BoardManager boardManager;
        public ZobristHashing zobristHashing;

        public Board()
        {
            this.posInfo = new PositionInformation();
            this.boardManager = new BoardManager(this);
            this.zobristHashing = new ZobristHashing(this);
        }

        public static ChessBoard InternalBoard = ChessBoard.Create();

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

        public void ExecuteMove(ref Move move, bool inSearch = false)
        {

            int movedPiece = move.movedPiece;
            ulong toSquare = move.toSquare;
            ulong fromSquare = move.fromSquare;

            int toSquareIndex = BitBoardHelper.GetLSB(ref toSquare);
            int fromSquareIndex = BitBoardHelper.GetLSB(ref fromSquare);

            bool isEnPassant = move.specialMove is SpecialMove.EnPassant;

            bool isPromotion = move.IsPawnPromotion;

            int friendlyPieceColor = posInfo.MoveColorIndex;
            int opponentPieceColor = posInfo.OpponentColorIndex;

            // ChessBoard.None (-1) indicates that no piece was captured
            int capturedPieceType = move.capturedPieceType;

            int prevCastleState = posInfo.CurrentGameState.castlingRights;
            int prevEnPassantFile = posInfo.CurrentGameState.enPassantFile;
            ulong newZobristKey = posInfo.CurrentGameState.zobristHashKey;
            int newCastlingRights = posInfo.CurrentGameState.castlingRights;
            int newEnPassantFile = 0;

            // this moves the piece from its old position to its new position
            InternalBoard.Pieces[friendlyPieceColor, movedPiece] &= ~fromSquare;
            InternalBoard.Pieces[friendlyPieceColor, movedPiece] |= toSquare;

            if (friendlyPieceColor == ChessBoard.White)
            {
                InternalBoard.AllWhitePieces &= ~fromSquare;
                InternalBoard.AllWhitePieces |= toSquare;
            }
            else
            {
                InternalBoard.AllBlackPieces &= ~fromSquare;
                InternalBoard.AllBlackPieces |= toSquare;
            }

            // a piece was captured
            if (capturedPieceType != ChessBoard.None)
            {
                ulong captureSquare = toSquare;

                if (isEnPassant)
                {
                    captureSquare = posInfo.whiteToMove ? captureSquare >> 8 : captureSquare << 8;
                    int captureSquareIndex = BitBoardHelper.GetLSB(ref captureSquare);
                    captureSquareIndex += posInfo.whiteToMove ? -8 : 8;

                    // remove en passant captured piece
                    newZobristKey ^= zobristHashing.pieceAtEachSquareArray[opponentPieceColor, capturedPieceType, captureSquareIndex];
                    InternalBoard.Pieces[opponentPieceColor, capturedPieceType] &= ~captureSquare;

                    // Incremental update for en passant capture
                    InternalBoard.AllPieces &= ~captureSquare;
                    if (opponentPieceColor == ChessBoard.White)
                        InternalBoard.AllWhitePieces &= ~captureSquare;
                    else
                        InternalBoard.AllBlackPieces &= ~captureSquare;
                }
                else
                {
                    // remove captured piece from zobrist hash
                    newZobristKey ^= zobristHashing.pieceAtEachSquareArray[opponentPieceColor, capturedPieceType, toSquareIndex];
                    InternalBoard.Pieces[opponentPieceColor, capturedPieceType] &= ~captureSquare;

                    if (opponentPieceColor == ChessBoard.White)
                        InternalBoard.AllWhitePieces &= ~captureSquare;
                    else
                        InternalBoard.AllBlackPieces &= ~captureSquare;
                }
            }

            // Handle king
            if (movedPiece is ChessBoard.King)
            {
                newCastlingRights &= posInfo.whiteToMove ? 0b1100 : 0b0011;

                // Handle castling
                if (move.specialMove == SpecialMove.KingSideCastleMove)
                {
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] &= ~(toSquare << 1);
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] |= toSquare >> 1;

                    newZobristKey ^= zobristHashing.pieceAtEachSquareArray[friendlyPieceColor, ChessBoard.Rook, toSquareIndex + 1];
                    newZobristKey ^= zobristHashing.pieceAtEachSquareArray[friendlyPieceColor, ChessBoard.Rook, toSquareIndex - 1];

                    if (friendlyPieceColor == ChessBoard.White)
                    {
                        InternalBoard.AllWhitePieces &= ~(toSquare << 1);
                        InternalBoard.AllWhitePieces |= toSquare >> 1;
                    }
                    else
                    {
                        InternalBoard.AllBlackPieces &= ~(toSquare << 1);
                        InternalBoard.AllBlackPieces |= toSquare >> 1;
                    }
                }

                if (move.specialMove == SpecialMove.QueenSideCastleMove)
                {
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] &= ~(toSquare >> 2);
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] |= toSquare << 1;

                    newZobristKey ^= zobristHashing.pieceAtEachSquareArray[friendlyPieceColor, ChessBoard.Rook, toSquareIndex - 2];
                    newZobristKey ^= zobristHashing.pieceAtEachSquareArray[friendlyPieceColor, ChessBoard.Rook, toSquareIndex + 1];

                    if (friendlyPieceColor == ChessBoard.White)
                    {
                        InternalBoard.AllWhitePieces &= ~(toSquare >> 2);
                        InternalBoard.AllWhitePieces |= toSquare << 1;
                    }
                    else
                    {
                        InternalBoard.AllBlackPieces &= ~(toSquare >> 2);
                        InternalBoard.AllBlackPieces |= toSquare << 1;
                    }
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
                // No incremental update needed: toSquare remains set in composites
            }

            // Pawn has moved two forwards, mark file with en-passant flag
            if (move.specialMove == SpecialMove.TwoSquarePawnMove)
            {
                int file = GetFile(BitBoardHelper.GetLSB(ref fromSquare));
                newEnPassantFile = file + 1;
                newZobristKey ^= zobristHashing.enPassantFile[file];
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
            newZobristKey ^= zobristHashing.sideToMove;
            newZobristKey ^= zobristHashing.pieceAtEachSquareArray[friendlyPieceColor, movedPiece, fromSquareIndex];
            newZobristKey ^= zobristHashing.pieceAtEachSquareArray[friendlyPieceColor, movedPiece, toSquareIndex];
            newZobristKey ^= zobristHashing.enPassantFile[prevEnPassantFile];

            if (newCastlingRights != prevCastleState)
            {
                newZobristKey ^= zobristHashing.castlingRightsArray[prevCastleState]; // remove old castling rights
                newZobristKey ^= zobristHashing.castlingRightsArray[newCastlingRights]; // add new castling rights
            }

            posInfo.whiteToMove = !posInfo.whiteToMove;

            posInfo.halfMoveAccumulator++;

            int newFiftyMoveCounter = posInfo.CurrentGameState.fiftyMoveCounter + 1;

            // Pawn moves and captures reset the fifty move counter
            if (movedPiece == ChessBoard.Pawn || capturedPieceType != ChessBoard.None)
            {
                newFiftyMoveCounter = 0;
            }
            InternalBoard.AllPieces = InternalBoard.AllBlackPieces | InternalBoard.AllWhitePieces;

            GameState newState = new((sbyte)capturedPieceType, (byte)newEnPassantFile, (byte)newCastlingRights, (byte)newFiftyMoveCounter, newZobristKey);

            posInfo.GameStateHistory.Push(newState);
            posInfo.CurrentGameState = newState;
            if (!inSearch)
            {
                posInfo.PositionHashes.Push(newState.zobristHashKey);
            }
        }

        public void UndoMove(ref Move move, bool inSearch=false)
        {
            int movedPiece = move.movedPiece;
            ulong toSquare = move.toSquare;
            ulong fromSquare = move.fromSquare;
            int toSquareIndex = BitBoardHelper.GetLSB(ref toSquare);
            int fromSquareIndex = BitBoardHelper.GetLSB(ref fromSquare);

            bool isEnPassant = move.specialMove is SpecialMove.EnPassant;
            bool isPromotion = move.IsPawnPromotion;

            int friendlyPieceColor = 1 - posInfo.MoveColorIndex; // Opposite of current
            int opponentPieceColor = posInfo.MoveColorIndex;
        
            // ChessBoard.None (-1) indicates that no piece was captured
            int capturedPieceType = move.capturedPieceType;

            // this moves the piece from its old position to its new position
            InternalBoard.Pieces[friendlyPieceColor, movedPiece] |= fromSquare;
            InternalBoard.Pieces[friendlyPieceColor, movedPiece] &= ~toSquare;

            if (friendlyPieceColor == ChessBoard.White)
            {
                InternalBoard.AllWhitePieces |= fromSquare;
                InternalBoard.AllWhitePieces &= ~toSquare;
            }
            else
            {
                InternalBoard.AllBlackPieces |= fromSquare;
                InternalBoard.AllBlackPieces &= ~toSquare;
            }

            // a piece was captured
            if (capturedPieceType != ChessBoard.None)
            {
                ulong captureSquare = toSquare;
                if (isEnPassant)
                    captureSquare = posInfo.whiteToMove ? captureSquare << 8 : captureSquare >> 8;

                InternalBoard.Pieces[opponentPieceColor, capturedPieceType] |= captureSquare;

                if (opponentPieceColor == ChessBoard.White)
                    InternalBoard.AllWhitePieces |= captureSquare;
                else
                    InternalBoard.AllBlackPieces |= captureSquare;
            }

            // Handle king
            if (movedPiece is ChessBoard.King)
            {
                // Handle castling
                if (move.specialMove == SpecialMove.KingSideCastleMove)
                {
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] |= toSquare << 1;
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] &= ~(toSquare >> 1);

                    if (friendlyPieceColor == ChessBoard.White)
                    {
                        InternalBoard.AllWhitePieces |= toSquare << 1;
                        InternalBoard.AllWhitePieces &= ~(toSquare >> 1);
                    }
                    else
                    {
                        InternalBoard.AllBlackPieces |= toSquare << 1;
                        InternalBoard.AllBlackPieces &= ~(toSquare >> 1);
                    }
                }
                if (move.specialMove == SpecialMove.QueenSideCastleMove)
                {
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] |= toSquare >> 2;
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] &= ~(toSquare << 1);

   
                    if (friendlyPieceColor == ChessBoard.White)
                    {
                        InternalBoard.AllWhitePieces |= toSquare >> 2;
                        InternalBoard.AllWhitePieces &= ~(toSquare << 1);
                    }
                    else
                    {
                        InternalBoard.AllBlackPieces |= toSquare >> 2;
                        InternalBoard.AllBlackPieces &= ~(toSquare << 1);
                    }
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
                InternalBoard.Pieces[friendlyPieceColor, promotionPieceType] &= ~toSquare;
                //InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Pawn] |= toSquare;
                // No incremental update needed: toSquare remains set in composites
            }
            InternalBoard.AllPieces = InternalBoard.AllBlackPieces | InternalBoard.AllWhitePieces;
            posInfo.whiteToMove = !posInfo.whiteToMove;

            posInfo.halfMoveAccumulator--;

            posInfo.GameStateHistory.Pop();
            posInfo.CurrentGameState = posInfo.GameStateHistory.Peek();
            if (!inSearch)
                posInfo.PositionHashes.Pop();
        }
    }
}