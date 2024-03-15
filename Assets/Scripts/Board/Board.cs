using System;
using System.Linq;
using System.Collections.Generic;
using static Chess.PositionInformation;
using static Chess.ZobristHashing;
using Unity.PlasticSCM.Editor.WebApi;
using UnityEngine;
using static Chess.Board;
using Unity.VisualScripting;

namespace Chess
{
    public static class Board
    {
        public static BoardManager boardManager;
        public struct ChessBoard
        {
            // https://www.chessprogramming.org/Bitboard_Board-Definition
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

        public static ChessBoard InternalBoard = ChessBoard.Create();

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

            public bool IsDefault()
            {
                return fromSquare == 0 && toSquare == 0 && movedPiece == 0 && specialMove == SpecialMove.None && !IsPawnPromotion && promotionFlag == null;
            }
        }

        public static Move[] legalMoves = new Move[256];

        private static int currentMoveIndex;


        private static ulong lastPawnDoubleMoveBitboard = 0;

        public static PromotionFlags promotionSelection;


        public enum GameStatus
        {
            Normal,
            AwaitingPromotion,
            Ended
        }

        public static GameStatus currentStatus = GameStatus.Normal;

        private static void RemoveAndAddPieceBitboards(int movedPiece, int pieceColor, ulong fromSquareMask, ulong toSquareMask)
        {
            InternalBoard.Pieces[pieceColor, movedPiece] &= ~fromSquareMask;
            InternalBoard.Pieces[pieceColor, movedPiece] |= toSquareMask;

            // ZobristHashKey ^= pieceAtEachSquareArray[pieceColor, movedPiece, fromSquareIndex];
            // ZobristHashKey ^= pieceAtEachSquareArray[pieceColor, movedPiece, toSquareIndex];
            return;
        }

        private static void UpdateLastPawnDoubleSquareMove(int oldPiecePosition, int newPiecePosition)
        {
            // update last pawn double square move
            // double pawn move
            if (Math.Abs(oldPiecePosition - newPiecePosition) == 16)
            {

                if (whiteToMove)
                {
                    lastPawnDoubleMoveBitboard = 1UL << newPiecePosition - 8;
                }
                else
                {
                    lastPawnDoubleMoveBitboard = 1UL << newPiecePosition + 8;
                }

                // if (enPassantFilePreviouslySet)
                // {
                //     ZobristHashKey ^= enPassantFile[previousEnPassantFile];
                // }

                // // new en passant file
                // ZobristHashKey ^= enPassantFile[(newPiecePosition / 8) + 1];
                // EnPassantFile = (newPiecePosition / 8) + 1;

            }
            else
            {
                lastPawnDoubleMoveBitboard = 0;
                // if (enPassantFilePreviouslySet)
                // {
                //     ZobristHashKey ^= enPassantFile[EnPassantFile];
                // }
            }

        }

        // private static void UpdateCastlingRights(LegalMove move)
        // {


        //     // TODO: potentially need to check here if rooks actually exist in each corner of the board (this is useful for custom positions
        //     // where castling might not be possible initially)
        //     // Alternatively modify the FEN string parsing method in 'BoardManager.cs'

        //     ///
        //     // Example: Moving the white king from e1 (square 4) or black king from e8 (square 60)
        //     if ((move.startSquare & (1UL << 4)) != 0)
        //     {
        //         // Check if white had kingside/ queenside rights before clearing
        //         if ((CastlingRights & (int)CastlingRightsFlags.WhiteKingSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.WhiteKingSide];
        //         if ((CastlingRights & (int)CastlingRightsFlags.WhiteQueenSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.WhiteQueenSide];

        //         CastlingRights &= 0b1100; // White king moves, clear white's rights
        //     }
        //     if ((move.startSquare & (1UL << 60)) != 0)
        //     {
        //         // Check if black had kingside/queenside rights before clearing
        //         if ((CastlingRights & (int)CastlingRightsFlags.BlackKingSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.BlackKingSide];
        //         if ((CastlingRights & (int)CastlingRightsFlags.BlackQueenSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.BlackQueenSide];

        //         CastlingRights &= 0b0011; // Black king moves, clear black's rights
        //     }

        //     // white queenside rook 
        //     if ((move.startSquare & 1UL) != 0)
        //     {
        //         if ((CastlingRights & (int)CastlingRightsFlags.WhiteQueenSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.WhiteQueenSide];
        //         CastlingRights &= 0b1101;
        //     }

        //     // white kingside rook
        //     if ((move.startSquare & (1UL << 7)) != 0)
        //     {
        //         if ((CastlingRights & (int)CastlingRightsFlags.WhiteKingSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.WhiteKingSide];
        //         CastlingRights &= 0b1110;
        //     }

        //     // black queenside rook
        //     if ((move.startSquare & (1UL << 56)) != 0)
        //     {
        //         if ((CastlingRights & (int)CastlingRightsFlags.BlackQueenSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.BlackQueenSide];
        //         CastlingRights &= 0b0111;
        //     }

        //     // black kingside rook
        //     if ((move.startSquare & (1UL << 63)) != 0)
        //     {
        //         if ((CastlingRights & (int)CastlingRightsFlags.BlackKingSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.BlackKingSide];
        //         CastlingRights &= 0b1011;
        //     }

        //     // if any piece moves to any of these individual squares it is safe to assume the rook is either captured or not there anymore, either way removing castling rights
        //     if ((move.endSquare & 1UL) != 0)
        //     {
        //         if ((CastlingRights & (int)CastlingRightsFlags.WhiteQueenSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.WhiteQueenSide];
        //         CastlingRights &= 0b1110; // White queenside rook captured
        //     }
        //     if ((move.endSquare & (1UL << 7)) != 0)
        //     {
        //         if ((CastlingRights & (int)CastlingRightsFlags.WhiteKingSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.WhiteKingSide];
        //         CastlingRights &= 0b1101; // White kingside rook captured
        //     }
        //     if ((move.endSquare & (1UL << 56)) != 0)
        //     {
        //         if ((CastlingRights & (int)CastlingRightsFlags.BlackQueenSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.BlackQueenSide];
        //         CastlingRights &= 0b1011; // Black queenside rook captured
        //     }
        //     if ((move.endSquare & (1UL << 63)) != 0)
        //     {
        //         if ((CastlingRights & (int)CastlingRightsFlags.BlackKingSide) != 0)
        //             ZobristHashKey ^= castlingRightsArray[(int)CastlingRightsFlags.BlackKingSide];
        //         CastlingRights &= 0b0111; // Black kingside rook captured
        //     }
        //     ///
        // }

        private static void HandleSpecialMoveInternal(Move move, ulong toSquare, ulong fromSquare)
        {

            if (whiteToMove)
            {
                switch (move.specialMove)
                {
                    case SpecialMove.EnPassant:
                        // shift down 8 squares to remove pawn captured via en passant a rank "below" the capturing pawn
                        InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn] &= ~(toSquare >> 8);
                        break;
                    case SpecialMove.KingSideCastleMove:
                        InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook] &= ~(1UL << 7);
                        InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook] |= 1UL << 5;
                        break;
                    case SpecialMove.QueenSideCastleMove:
                        InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook] &= ~1UL;
                        InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook] |= 1UL << 3;
                        break;
                }
            }
            else
            {
                switch (move.specialMove)
                {
                    case SpecialMove.EnPassant:
                        InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn] &= ~(toSquare << 8);
                        break;
                    case SpecialMove.KingSideCastleMove:
                        InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook] &= ~(1UL << 63);
                        InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook] |= 1UL << 61;
                        break;
                    case SpecialMove.QueenSideCastleMove:
                        InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook] &= ~(1UL << 56);
                        InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook] |= 1UL << 59;
                        break;
                }
            }
        }

        //private static void HandlePawnPromotionInternal(ulong toSquare)
        //{
        //    if (IsPawnPromotion(toSquare))
        //    {
        //        if (BoardManager.CurrentTurn == BoardManager.ComputerSide)
        //        {
        //            UpdatePromotedPawnEngine(toSquare);
        //        }
        //        else
        //        {
        //            UIController.Instance.ShowPromotionDropdown(toSquare);
        //        }
        //    }
        //    else
        //    {
        //        return;
        //    }
        //}

        //public static void UpdateBitboards(int originalXPosition, int originalYPosition, int newXPosition, int newYPosition)
        //{
        //    int fromSquareIndex = originalYPosition * 8 + originalXPosition;
        //    int toSquareIndex = newYPosition * 8 + newXPosition;

        //    ulong fromSquare = 1UL << fromSquareIndex;
        //    ulong toSquare = 1UL << toSquareIndex;

        //    Move move = legalMoves.Single(move => move.fromSquare == fromSquare && move.toSquare == toSquare);

        //    //UpdateCastlingRights(move);

        //    HandleSpecialMoveInternal(move, toSquare, fromSquare);

        //    // for a captured piece, AND the InternalBoard bitboards with the NOT of the isolated captured piece bitboard (endsquare)

        //    // remove any captured piece from bitboards
        //    ulong isolatedCapturedPieceBitmask = ~toSquare;
        //    if (whiteToMove)
        //    {

        //        var (pieceType, pieceWasCaptured) = CheckIfPieceWasCaptured(ChessBoard.Black, toSquare);
        //        if (pieceWasCaptured)
        //        {
        //            InternalBoard.Pieces[ChessBoard.Black, pieceType] &= isolatedCapturedPieceBitmask;
        //            ZobristHashKey ^= pieceAtEachSquareArray[ChessBoard.Black, pieceType, toSquareIndex];
        //        }

        //        // remove appropriate white piece from old square and move it to new square, update bitboards correspondingly 
        //        RemoveAndAddPieceBitboards(move.movedPiece, ChessBoard.White, fromSquare, toSquare, fromSquareIndex, toSquareIndex);

        //        halfMoveAccumulator++;

        //        // if the move was a capture, reset half-move accumulator
        //        UpdateHalfMoveAcc(pieceWasCaptured, move.specialMove, move.movedPiece);

        //        // update composite bitboards
        //        InternalBoard.UpdateCompositeBitboards();
        //    }
        //    else
        //    {
        //        var (pieceType, pieceWasCaptured) = CheckIfPieceWasCaptured(ChessBoard.White, toSquare);
        //        if (pieceWasCaptured)
        //        {
        //            InternalBoard.Pieces[ChessBoard.White, pieceType] &= isolatedCapturedPieceBitmask;
        //            ZobristHashKey ^= pieceAtEachSquareArray[ChessBoard.White, pieceType, toSquareIndex];
        //        }

        //        RemoveAndAddPieceBitboards(move.movedPiece, ChessBoard.Black, fromSquare, toSquare, fromSquareIndex, toSquareIndex);

        //        halfMoveAccumulator++;
        //        // a "move" consists of a player completing a turn followed by the opponent completing a turn, hence why this variable only gets accumulated on black's turn
        //        fullMoveAccumulator++;

        //        UpdateHalfMoveAcc(pieceWasCaptured, move.specialMove, move.movedPiece);

        //        // update composite bitboards
        //        InternalBoard.UpdateCompositeBitboards();
        //    }

        //    if (move.movedPiece == ChessBoard.Pawn)
        //    {
        //        // for extra special pawn moves
        //        // double square move keeps track of a double pawn move for potential en passant captures
        //        UpdateLastPawnDoubleSquareMove((int)Math.Log(fromSquare, 2), (int)Math.Log(toSquare, 2));
        //        // if the pawn makes it to the opposing sides last rank, initiate promotion
        //        HandlePawnPromotionInternal(toSquare);
        //    }
        //    else
        //    {
        //        // if not pawn move, remove last pawn double move for en passant
        //        lastPawnDoubleMoveBitboard = 0;
        //        if (enPassantFilePreviouslySet)
        //        {
        //            ZobristHashKey ^= enPassantFile[EnPassantFile];
        //        }
        //    }
        //    ZobristHashKey ^= sideToMove;
        //}


        //private static void UpdatePromotionPiece(int selectedPromotionPiece, ulong toSquare)
        //{
        //    if (whiteToMove)
        //    {
        //        InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn] &= ~toSquare;
        //        InternalBoard.Pieces[ChessBoard.White, selectedPromotionPiece] |= toSquare;

        //        ZobristHashKey ^= pieceAtEachSquareArray[ChessBoard.White, ChessBoard.Pawn, (int)Math.Log(toSquare, 2)];
        //        ZobristHashKey ^= pieceAtEachSquareArray[ChessBoard.White, selectedPromotionPiece, (int)Math.Log(toSquare, 2)];
        //    }
        //    else
        //    {
        //        InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn] &= ~toSquare;
        //        InternalBoard.Pieces[ChessBoard.Black, selectedPromotionPiece] |= toSquare;

        //        ZobristHashKey ^= pieceAtEachSquareArray[ChessBoard.Black, ChessBoard.Pawn, (int)Math.Log(toSquare, 2)];
        //        ZobristHashKey ^= pieceAtEachSquareArray[ChessBoard.Black, selectedPromotionPiece, (int)Math.Log(toSquare, 2)];
        //    }

        //    InternalBoard.UpdateCompositeBitboards();
        //}
        public static PromotionFlags UpdatePromotedPawnEngine()
        {
            UIController.Instance.UpdateMoveStatusUIInformation();
            return Engine.EvaluateBestPromotionPiece();
        }

        //public static void UpdatePromotedPawn(ulong toSquare)
        //{

        //    //UpdatePromotionPiece(UIController.Instance.promotionSelection, toSquare);

        //    // update the front end sprite so the new correct piece is visible
        //    PieceMovementManager.UpdateFrontEndPromotion(UIController.Instance.promotionSelection, (int)Math.Log(toSquare, 2) % 8, (int)Math.Log(toSquare, 2) / 8);

        //    HandleGameStateAfterMove();
        //}

        private static bool IsPawnPromotion(ulong toSquare)
        {
            if (toSquare >> 8 == 0 || toSquare << 8 == 0)
            {
                return true;
            }

            return false;
        }

        private static Move AddLegalMove(ulong startSquare, ulong endSquare, int movedPiece, SpecialMove specialMove, bool isPawnPromotion, PromotionFlags promotionFlag = PromotionFlags.None)
        {
            return
                new Move
                {
                    fromSquare = startSquare,
                    toSquare = endSquare,
                    movedPiece = movedPiece,
                    specialMove = specialMove,
                    IsPawnPromotion = isPawnPromotion,
                    promotionFlag = promotionFlag,
                };
        }

        //public static void HandleGameStateAfterMove()
        //{

        //    if (currentState == GameState.Normal)
        //    {
        //        // update who's move it is
        //        whiteToMove = !whiteToMove;

        //        MoveHistory.Push(ZobristHashKey);
        //        if (PositionHashes.ContainsKey(ZobristHashKey))
        //        {
        //            PositionHashes[ZobristHashKey] += 1;
        //        }
        //        else
        //        {
        //            PositionHashes[ZobristHashKey] = 1;
        //        }

        //        legalMoves = GenerateAllLegalMoves();

        //        //CheckForGameOverRules();
        //        //SwapTurn();

        //        if (currentState == GameState.Ended)
        //        {
        //            ClearListMoves();
        //        }

        //        // TODO Reference the UI instance in the top of this file
        //        UIController.Instance.UpdateMoveStatusUIInformation();
        //    }
        //}

        private static (int pieceType, bool pieceWasCaptured) CheckIfPieceWasCaptured(int opponentColor, ulong toSquare)
        {
            // Check if a piece is captured
            for (int pieceType = ChessBoard.Pawn; pieceType <= ChessBoard.King; pieceType++)
            {
                if ((InternalBoard.Pieces[opponentColor, pieceType] & toSquare) != 0)
                {
                    return (pieceType, true);
                }
            }
            return (-1, false);
        }

        private static void UpdateHalfMoveAcc(bool capturedPiece, SpecialMove move, int movedPiece)
        {
            // if pawn moved or a piece was captured, reset the accumulator
            // en passant is counted as a capture
            if (capturedPiece || move == SpecialMove.EnPassant || movedPiece == ChessBoard.Pawn)
            {
                // this move captured a piece, reset the fifty move rule
                halfMoveAccumulator = 0;
            }
        }

        private static int CountBits(ulong bits)
        {
            int count = 0;
            while (bits != 0)
            {
                count += 1;
                bits &= bits - 1; // Remove the lowest set bit
            }
            return count;
        }

        private static bool CheckForInsufficientMaterial()
        {
            int totalPieces = CountBits(InternalBoard.AllPieces);
            int totalBlackPieces = CountBits(InternalBoard.AllBlackPieces);
            int totalWhitePieces = CountBits(InternalBoard.AllWhitePieces);

            // Check for King vs. King
            if (totalPieces == 2)
            {
                return true;
            }

            // Check for King and Bishop/Knight vs. King, or King and Bishop vs. King and Bishop
            if (totalPieces == 3 || totalPieces == 4)
            {
                // Ensure each side has either 1 or 2 pieces
                if ((totalBlackPieces == 1 || totalBlackPieces == 2) && (totalWhitePieces == 1 || totalWhitePieces == 2))
                {

                    int blackBishopCount = CountBits(InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Bishop]);
                    int whiteBishopCount = CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Bishop]);
                    // Check if the pieces are bishops/knights
                    bool blackHasOnlyBishopOrKnight = (blackBishopCount + CountBits(InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Knight]) == totalBlackPieces - 1);
                    bool whiteHasOnlyBishopOrKnight = (whiteBishopCount + CountBits(InternalBoard.Pieces[ChessBoard.White, ChessBoard.Knight]) == totalWhitePieces - 1);

                    // If there's exactly one bishop per side, check their square colors
                    if (blackBishopCount == 1 && whiteBishopCount == 1)
                    {
                        ulong blackBishopSquare = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Bishop];
                        ulong whiteBishopSquare = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Bishop];

                        // Check if bishops are on the same color squares by checking their intersection with light/dark squares bitboards
                        bool blackBishopOnLightSquare = (blackBishopSquare & MoveTables.lightSquares) != 0;
                        bool whiteBishopOnLightSquare = (whiteBishopSquare & MoveTables.lightSquares) != 0;

                        // If both bishops are on light squares or both are on dark squares, it's a draw due to insufficient material
                        return blackBishopOnLightSquare == whiteBishopOnLightSquare;
                    }

                    return blackHasOnlyBishopOrKnight && whiteHasOnlyBishopOrKnight;
                }
            }

            return false;
        }

        //private static void CheckForGameOverRules()
        //{
        //    if (legalMoves.Count == 0 && kingInCheck)
        //    {
        //        UnityEngine.Debug.Log("CheckMate!");
        //        currentState = GameState.Ended;
        //    }

        //    if (legalMoves.Count == 0 && !kingInCheck)
        //    {
        //        UnityEngine.Debug.Log("Stalemate!");
        //        currentState = GameState.Ended;
        //    }

        //    // a "move" consists of a player completing a turn followed by the opponent completing a turn, hence
        //    if (halfMoveAccumulator == 100)
        //    {
        //        UnityEngine.Debug.Log("Draw by 50 move rule!");
        //        currentState = GameState.Ended;
        //    }

        //    // threefold repetition rule (position repeats three times is a draw)
        //    if (PositionHashes[ZobristHashKey] >= 3)
        //    {
        //        UnityEngine.Debug.Log("Draw by Threefold Repetition!");
        //        currentState = GameState.Ended;
        //    }

        //    // draw by insufficient material rule, for example: knight and king cannot deliver checkmate

        //    if (CheckForInsufficientMaterial())
        //    {
        //        UnityEngine.Debug.Log("Draw by Insufficient Material!");
        //        currentState = GameState.Ended;
        //    }
        //}

        private static void SwapTurn()
        {
            BoardManager.CurrentTurn = BoardManager.CurrentTurn == BoardManager.Sides.White
                                    ? BoardManager.Sides.Black
                                    : BoardManager.Sides.White;
        }

        /* these functions came from "https://www.chessprogramming.org/Pawn_Pushes_(Bitboards)"
        * the pawn is able to push if no impeding piece (friendly or foe) is blocking the path, these functions traverse
        * the intersection of pawns with the shifted down empty squares in the opposite direction
        */
        static ulong WhitePawnsAbleToPushOneSquare(ulong wpawns, ulong emptySquares)
        {
            ulong result = InternalBoard.SouthOne(emptySquares) & wpawns;
            return result;
        }

        static ulong WhitePawnsAbleToPushTwoSquares(ulong wpawns, ulong emptySquares)
        {
            ulong rank4 = MoveTables.RankMasks[3];
            ulong emptyRank3 = InternalBoard.SouthOne(emptySquares & rank4) & emptySquares;
            return WhitePawnsAbleToPushOneSquare(wpawns, emptyRank3);
        }

        static ulong BlackPawnsAbleToPushOneSquare(ulong wpawns, ulong emptySquares)
        {
            ulong result = InternalBoard.NorthOne(emptySquares) & wpawns;
            return result;
        }

        static ulong BlackPawnsAbleToPushTwoSquares(ulong wpawns, ulong emptySquares)
        {
            ulong rank5 = MoveTables.RankMasks[4];
            ulong emptyRank6 = InternalBoard.NorthOne(emptySquares & rank5) & emptySquares;
            return BlackPawnsAbleToPushOneSquare(wpawns, emptyRank6);
        }

        //

        public static void GenerateRookMoves(ref Span<Move> moves, ref ulong rookBitboard, ref ulong friendlyPieces)
        {

            ulong rooks = rookBitboard;

            while (rooks != 0)
            {
                // and with twos complement to isolate each rook
                ulong isolatedRooklsb = rooks & (~rooks + 1);

                ulong validRookMoves = GetRookAttacks(InternalBoard.AllPieces, GetLSB(ref isolatedRooklsb));

                // remove friendly piece blockers from potential captures 
                validRookMoves &= ~friendlyPieces;

                while (validRookMoves != 0)
                {
                    ulong movelsb = validRookMoves & (~validRookMoves + 1);

                    validRookMoves &= validRookMoves - 1;
                    moves[currentMoveIndex] = AddLegalMove(isolatedRooklsb, movelsb, ChessBoard.Rook, SpecialMove.None, false);
                    currentMoveIndex++;
                }
                rooks &= rooks - 1;
            }
        }

        public static void GenerateBishopMoves(ref Span<Move> moves, ref ulong bishopBitboard, ref ulong friendlyPieces)
        {
            ulong bishops = bishopBitboard;

            while (bishops != 0)
            {
                // and with twos complement to isolate each bishop
                ulong isolatedBishoplsb = bishops & (~bishops + 1);

                ulong validBishopMoves = GetBishopAttacks(InternalBoard.AllPieces, GetLSB(ref isolatedBishoplsb));

                // remove friendly piece blockers from potential captures 
                validBishopMoves &= ~friendlyPieces;

                while (validBishopMoves != 0)
                {
                    ulong movelsb = validBishopMoves & (~validBishopMoves + 1);

                    validBishopMoves &= validBishopMoves - 1;
                    moves[currentMoveIndex] = AddLegalMove(isolatedBishoplsb, movelsb, ChessBoard.Bishop, SpecialMove.None, false);
                    currentMoveIndex++;
                }
                bishops &= bishops - 1;
            }
        }

        public static void GenerateQueenMoves(ref Span<Move> moves, ref ulong queenBitboard, ref ulong friendlyPieces)
        {
            ulong queens = queenBitboard;

            while (queens != 0)
            {
                // and with twos complement to isolate each queen
                ulong isolatedQueenlsb = queens & (~queens + 1);

                int currentQueenPos = GetLSB(ref isolatedQueenlsb);
                ulong validQueenMoves = GetBishopAttacks(InternalBoard.AllPieces, currentQueenPos);
                validQueenMoves |= GetRookAttacks(InternalBoard.AllPieces, currentQueenPos);

                // remove friendly piece blockers from potential captures 
                validQueenMoves &= ~friendlyPieces;

                while (validQueenMoves != 0)
                {
                    ulong movelsb = validQueenMoves & (~validQueenMoves + 1);

                    validQueenMoves &= validQueenMoves - 1;
                    moves[currentMoveIndex] = AddLegalMove(isolatedQueenlsb, movelsb, ChessBoard.Queen, SpecialMove.None, false);
                    currentMoveIndex++;
                }
                queens &= queens - 1;
            }
        }

        public static void GenerateWhitePawnMoves(ref Span<Move> moves, ref ulong whitePawnBitboard)
        {
            ulong whitePawns = whitePawnBitboard;

            while (whitePawns != 0)
            {
                // isolates each pawn one by one
                ulong isolatedPawnlsb = whitePawns & (~whitePawns + 1);
                // gets current pawn position to add to legal move list
                int currentPawnPos = GetLSB(ref isolatedPawnlsb);
                // valid pawn moves include pushes, captures, and en passant
                ulong validPawnMoves = MoveTables.PrecomputedWhitePawnPushes[currentPawnPos];

                if (WhitePawnsAbleToPushOneSquare(isolatedPawnlsb, ~InternalBoard.AllPieces) != isolatedPawnlsb)
                {
                    validPawnMoves &= ~(isolatedPawnlsb << 8);
                }

                if (WhitePawnsAbleToPushTwoSquares(isolatedPawnlsb, ~InternalBoard.AllPieces) != isolatedPawnlsb)
                {
                    validPawnMoves &= ~(isolatedPawnlsb << 16);
                }

                // if a pawn can capture any black piece it is a pseudo-legal capture
                ulong validPawnCaptures = MoveTables.PrecomputedWhitePawnCaptures[currentPawnPos] & InternalBoard.AllBlackPieces;
                validPawnMoves |= validPawnCaptures;

                //if (lastPawnDoubleMoveBitboard != 0)
                //{
                //    // northwest potential en passant capture square
                //    int pawnCaptureOffset = (int)Math.Log(lastPawnDoubleMoveBitboard, 2) - currentPawnPos;
                //    if (pawnCaptureOffset == 7 && ((isolatedPawnlsb >> 1) & InternalBoard.AllBlackPieces) != 0)
                //    {
                //        if ((lastPawnDoubleMoveBitboard & AFileMask) != 0)
                //        {
                //            whitePawnMoves.Add(AddLegalMove(isolatedPawnlsb, lastPawnDoubleMoveBitboard, ChessBoard.Pawn, SpecialMove.EnPassant, false));
                //        }
                //    }

                //    // northeast potential en passant capture square 
                //    if (pawnCaptureOffset == 9 && ((isolatedPawnlsb << 1) & InternalBoard.AllBlackPieces) != 0)
                //    {
                //        if ((lastPawnDoubleMoveBitboard & HFileMask) != 0)
                //        {
                //            whitePawnMoves.Add(AddLegalMove(isolatedPawnlsb, lastPawnDoubleMoveBitboard, ChessBoard.Pawn, SpecialMove.EnPassant, false));
                //        }
                //    }
                //}

                while (validPawnMoves != 0)
                {
                    ulong movelsb = validPawnMoves & (~validPawnMoves + 1);

                    validPawnMoves &= validPawnMoves - 1;


                    if ((movelsb << 8) == 0)
                    {
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, true, PromotionFlags.PromoteToQueenFlag);
                        currentMoveIndex++;
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, true, PromotionFlags.PromoteToRookFlag);
                        currentMoveIndex++;
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, true, PromotionFlags.PromoteToBishopFlag);
                        currentMoveIndex++;
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, true, PromotionFlags.PromoteToKnightFlag);
                        currentMoveIndex++;
                    }
                    else
                    {
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, false);
                        currentMoveIndex++;
                    }
                }
                // move to the next pawn
                whitePawns &= whitePawns - 1;
            }
        }

        public static void GenerateBlackPawnMoves(ref Span<Move> moves, ref ulong blackPawnBitboard)
        {

            ulong blackPawns = blackPawnBitboard;

            while (blackPawns != 0)
            {
                ulong isolatedPawnlsb = blackPawns & (~blackPawns + 1);

                int currentPawnPos = GetLSB(ref isolatedPawnlsb);
                // valid pawn moves include pushes, captures, and en passant
                ulong validPawnMoves = MoveTables.PrecomputedBlackPawnPushes[currentPawnPos];

                if (BlackPawnsAbleToPushOneSquare(isolatedPawnlsb, ~InternalBoard.AllPieces) != isolatedPawnlsb)
                {
                    validPawnMoves &= ~(isolatedPawnlsb >> 8);
                }

                if (BlackPawnsAbleToPushTwoSquares(isolatedPawnlsb, ~InternalBoard.AllPieces) != isolatedPawnlsb)
                {
                    validPawnMoves &= ~(isolatedPawnlsb >> 16);
                }

                // if a pawn can capture any black piece it is a pseudo-legal capture
                ulong validPawnCaptures = MoveTables.PrecomputedBlackPawnCaptures[currentPawnPos] & InternalBoard.AllWhitePieces;
                validPawnMoves |= validPawnCaptures;

                //if (lastPawnDoubleMoveBitboard != 0)
                //{

                //    // southwest potential en passant capture square
                //    int pawnCaptureOffset = currentPawnPos - (int)Math.Log(lastPawnDoubleMoveBitboard, 2);
                //    if (pawnCaptureOffset == 9 && ((isolatedPawnlsb >> 1) & InternalBoard.AllWhitePieces) != 0)
                //    {
                //        if ((lastPawnDoubleMoveBitboard & AFileMask) != 0)
                //        {
                //            blackPawnMoves.Add(AddLegalMove(isolatedPawnlsb, lastPawnDoubleMoveBitboard, ChessBoard.Pawn, SpecialMove.EnPassant, false));
                //        }
                //    }

                //    // southeast potential en passant capture square 
                //    if (pawnCaptureOffset == 7 && ((isolatedPawnlsb >> 1) & InternalBoard.AllWhitePieces) != 0)
                //    {
                //        if ((lastPawnDoubleMoveBitboard & HFileMask) != 0)
                //        {
                //            blackPawnMoves.Add(AddLegalMove(isolatedPawnlsb, lastPawnDoubleMoveBitboard, ChessBoard.Pawn, SpecialMove.EnPassant, false));
                //        }
                //    }
                //}

                while (validPawnMoves != 0)
                {
                    ulong movelsb = validPawnMoves & (~validPawnMoves + 1);

                    validPawnMoves &= validPawnMoves - 1;

                    if ((movelsb >> 8) == 0)
                    {
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, true, PromotionFlags.PromoteToQueenFlag);
                        currentMoveIndex++;
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, true, PromotionFlags.PromoteToRookFlag);
                        currentMoveIndex++;
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, true, PromotionFlags.PromoteToBishopFlag);
                        currentMoveIndex++;
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, true, PromotionFlags.PromoteToKnightFlag);
                        currentMoveIndex++;
                    }
                    else
                    {
                        moves[currentMoveIndex] = AddLegalMove(isolatedPawnlsb, movelsb, ChessBoard.Pawn, SpecialMove.None, false);
                        currentMoveIndex++;
                    }
                }

                blackPawns &= blackPawns - 1;
            }
        }

        public static void GenerateKnightMoves(ref Span<Move> moves, ref ulong knightBitboard, ref ulong friendlyPieces)
        {

            ulong knights = knightBitboard;
            while (knights != 0)
            {
                // isolate each knight
                ulong isolatedKnightlsb = knights & (~knights + 1);
                int currentKnightPos = GetLSB(ref isolatedKnightlsb);

                // valid knight moves only include either empty squares or squares the opponent pieces occupy
                ulong validKnightMoves = MoveTables.PrecomputedKnightMoves[currentKnightPos] & ~friendlyPieces;

                while (validKnightMoves != 0)
                {
                    ulong movelsb = validKnightMoves & (~validKnightMoves + 1);

                    validKnightMoves &= validKnightMoves - 1;
                    moves[currentMoveIndex] = AddLegalMove(isolatedKnightlsb, movelsb, ChessBoard.Knight, SpecialMove.None, false);
                    currentMoveIndex++;
                }

                // move to next knight
                knights &= knights - 1;
            }
        }

        /* 
         *  The following functions were implemented through the help of these resources. Using magic bitboards is a complex process.
         *  Functions below "InitBishopLookup" and "InitRookLookup" are executed once to fill the lookup tables for magic bitboards. I chose to use
         *  the 'Plain Magic Bitboards' found on the chess programming wikipedia as it was the simplest for me to wrap my mind around.
         *  
         *  The magic numbers themselves I did not generate, as it was much easier to find a suitable precomputed table online.
         *  The Movetables C# class contains the array of magic bitboards
         *  
         *  https://www.chessprogramming.org/Magic_Bitboards#Plain
         *  https://stackoverflow.com/questions/67513005/how-to-generate-this-preinitialized-array-for-magic-bitboards
         */
        private static ulong GetBishopAttacks(ulong occupied, int square)
        {
            ulong blockers = occupied & MoveTables.BishopRelevantOccupancy[square];
            ulong index = (blockers * MoveTables.BishopMagics[square]) >> MoveTables.PrecomputedBishopShifts[square];
            return MoveTables.BishopAttackTable[square, index];
        }

        private static ulong GetRookAttacks(ulong occupied, int square)
        {
            ulong blockers = occupied & MoveTables.RookRelevantOccupancy[square];
            ulong index = (blockers * MoveTables.RookMagics[square]) >> MoveTables.PrecomputedRookShifts[square];
            return MoveTables.RookAttackTable[square, index];
        }


        private static int GetFile(int square)
        {
            return square % 8;
        }

        private static int GetRank(int square)
        {
            return square / 8;
        }

        private static int GetSquare(int rank, int file)
        {
            return rank * 8 + file;
        }

        // Generates the key, similar to the Java code snippet
        private static int Transform(ulong blockers, ulong magic, int shift)
        {
            return (int)((blockers * magic) >> shift);
        }

        private static int TrailingZeroCount(ulong value)
        {
            if (value == 0) return 64;
            int count = 0;

            while ((value & 1) == 0)
            {
                count++;
                value >>= 1;
            }

            return count;
        }

        private static int PopCount(ulong x)
        {
            int count;
            for (count = 0; x != 0; count++)
            {
                x &= x - 1; // Clear the least significant bit set
            }
            return count;
        }

        public static void InitBishopLookup()
        {
            for (int square = 0; square < 64; square++)
            {
                ulong mask = MoveTables.BishopRelevantOccupancy[square];
                int permutationCount = 1 << PopCount(mask);

                for (int i = 0; i < permutationCount; i++)
                {
                    ulong blockers = BlockersPermutation(i, mask);
                    ulong attacks = 0UL;
                    int rank = GetRank(square), r;
                    int file = GetFile(square), f;

                    for (r = rank + 1, f = file + 1; r <= 7 && f <= 7; r++, f++)
                    {
                        attacks |= 1UL << GetSquare(r, f);
                        if ((blockers & (1UL << GetSquare(r, f))) != 0)
                        {
                            break;
                        }
                    }

                    for (r = rank - 1, f = file + 1; r >= 0 && f <= 7; r--, f++)
                    {
                        attacks |= 1UL << GetSquare(r, f);
                        if ((blockers & (1UL << GetSquare(r, f))) != 0)
                        {
                            break;
                        }
                    }

                    for (r = rank - 1, f = file - 1; r >= 0 && f >= 0; r--, f--)
                    {
                        attacks |= 1UL << GetSquare(r, f);
                        if ((blockers & (1UL << GetSquare(r, f))) != 0)
                        {
                            break;
                        }
                    }

                    for (r = rank + 1, f = file - 1; r <= 7 && f >= 0; r++, f--)
                    {
                        attacks |= 1UL << GetSquare(r, f);
                        if ((blockers & (1UL << GetSquare(r, f))) != 0)
                        {
                            break;
                        }
                    }

                    int key = Transform(blockers, MoveTables.BishopMagics[square], MoveTables.PrecomputedBishopShifts[square]);

                    MoveTables.BishopAttackTable[square, key] = attacks;
                }
            }
        }

        public static void InitRookLookup()
        {
            for (int square = 0; square < 64; square++)
            {
                ulong mask = MoveTables.RookRelevantOccupancy[square];
                int permutationCount = 1 << PopCount(mask);

                for (int i = 0; i < permutationCount; i++)
                {
                    ulong blockers = BlockersPermutation(i, mask);
                    ulong attacks = 0UL;
                    int rank = GetRank(square), r;
                    int file = GetFile(square), f;

                    // Horizontal attacks to the right
                    for (f = file + 1; f <= 7; f++)
                    {
                        attacks |= 1UL << GetSquare(rank, f);
                        if ((blockers & (1UL << GetSquare(rank, f))) != 0)
                        {
                            break;
                        }
                    }

                    // Horizontal attacks to the left
                    for (f = file - 1; f >= 0; f--)
                    {
                        attacks |= 1UL << GetSquare(rank, f);
                        if ((blockers & (1UL << GetSquare(rank, f))) != 0)
                        {
                            break;
                        }
                    }

                    // Vertical attacks upwards
                    for (r = rank + 1; r <= 7; r++)
                    {
                        attacks |= 1UL << GetSquare(r, file);
                        if ((blockers & (1UL << GetSquare(r, file))) != 0)
                        {
                            break;
                        }
                    }

                    // Vertical attacks downwards
                    for (r = rank - 1; r >= 0; r--)
                    {
                        attacks |= 1UL << GetSquare(r, file);
                        if ((blockers & (1UL << GetSquare(r, file))) != 0)
                        {
                            break;
                        }
                    }

                    int key = Transform(blockers, MoveTables.RookMagics[square], MoveTables.PrecomputedRookShifts[square]);

                    MoveTables.RookAttackTable[square, key] = attacks;
                }
            }
        }


        private static ulong BlockersPermutation(int iteration, ulong mask)
        {
            ulong blockers = 0;

            while (iteration != 0)
            {
                if ((iteration & 1) != 0)
                {
                    int shift = TrailingZeroCount(mask);
                    blockers |= 1UL << shift;
                }

                iteration >>= 1;
                mask &= mask - 1; // Kernighan's bit count algorithm step
            }

            return blockers;
        }

        /*
         * 
         * 
         */



        private static bool CanCastleKingsideWhite()
        {
            // check to make sure squares between king and kingside rook are empty
            if (((InternalBoard.AllPieces) & 0x60) != 0)
            {
                return false;
            }

            // checks to make sure neither the kingside rook or king has moved
            if ((CastlingRights & 0x1) == 0)
            {
                return false;
            }

            // check if squares between king and kingside rook are underattack
            // from e1 to g1
            for (int i = 4; i < 7; i++)
            {
                if (SquareAttackedBy(i)) return false;
            }

            return true;
        }

        private static bool CanCastleQueensideWhite()
        {

            // check to make sure squares between king and queenside rook are empty
            if (((InternalBoard.AllPieces) & 0xE) != 0)
            {
                return false;
            }

            // checks to make sure neither the queenside rook or king has moved
            if ((CastlingRights & 0x2) == 0)
            {
                return false;
            }

            // check if squares between king and kingside rook are underattack
            // from c1 to e1
            for (int i = 2; i < 5; i++)
            {
                if (SquareAttackedBy(i)) return false;
            }
            return true;
        }

        private static bool CanCastleKingsideBlack()
        {

            // check to make sure squares between king and kingside rook are empty
            if (((InternalBoard.AllPieces) & 0x6000000000000000) != 0)
            {
                return false;
            }

            if ((CastlingRights & 0x4) == 0)
            {
                return false;
            }

            // from e8 to g8
            for (int i = 60; i < 63; i++)
            {
                if (SquareAttackedBy(i)) return false;
            }

            return true;
        }

        private static bool CanCastleQueensideBlack()
        {
            // check to make sure squares between king and queenside rook are empty
            if (((InternalBoard.AllPieces) & 0xE00000000000000) != 0)
            {
                return false;
            }

            if ((CastlingRights & 0x8) == 0)
            {
                return false;
            }

            // from c8 to e8
            for (int i = 58; i < 61; i++)
            {
                if (SquareAttackedBy(i)) return false;
            }

            return true;
        }

        public static void GenerateKingMoves(ref Span<Move> moves, ref ulong king, ref ulong friendlyPieces)
        {

            // king index converts the king bitboard 
            int kingIndex = GetLSB(ref king);

            // grabs the corresponding bitboard representing all legal moves from the given king index on the board
            ulong validKingMoves = MoveTables.PrecomputedKingMoves[kingIndex];

            // valid king moves only include either empty squares or squares the opponent pieces occupy (for now, this will change when check is implemented)
            validKingMoves &= ~friendlyPieces;

            // castling
            if (whiteToMove)
            {
                if (CanCastleKingsideWhite())
                {
                    moves[currentMoveIndex] = AddLegalMove(king, 1UL << 6, ChessBoard.King, SpecialMove.KingSideCastleMove, false);
                    currentMoveIndex++;
                }

                if (CanCastleQueensideWhite())
                {
                    moves[currentMoveIndex] = AddLegalMove(king, 1UL << 2, ChessBoard.King, SpecialMove.QueenSideCastleMove, false);
                    currentMoveIndex++;
                }

            }
            else
            {
                if (CanCastleKingsideBlack())
                {
                    moves[currentMoveIndex] = AddLegalMove(king, 1UL << 62, ChessBoard.King, SpecialMove.KingSideCastleMove, false);
                    currentMoveIndex++;
                }

                if (CanCastleQueensideBlack())
                {
                    moves[currentMoveIndex] = AddLegalMove(king, 1UL << 58, ChessBoard.King, SpecialMove.QueenSideCastleMove, false);
                    currentMoveIndex++;
                }
            }

            while (validKingMoves != 0)
            {
                // gets the least significant bit while validmoves are being parsed in order to find new square position
                ulong movelsb = validKingMoves & (~validKingMoves + 1);

                validKingMoves &= validKingMoves - 1;

                moves[currentMoveIndex] = AddLegalMove(king, movelsb, ChessBoard.King, SpecialMove.None, false);
                currentMoveIndex++;
            }
        }

        public static void Initialize()
        {
            currentMoveIndex = 0;
        }


        public static Move[] GenerateMoves()
        {
            Span<Move> moves = stackalloc Move[256];
            Span<Move> opponentMoves = stackalloc Move[256]; 

            int validMoveCount = GenerateAllLegalMoves(ref moves, ref opponentMoves);

            Move[] movesArray = new Move[validMoveCount];
            for (int i = 0; i < validMoveCount; i++)
            {
                movesArray[i] = moves[i];
            }
            return movesArray;
        }

        public static int GenerateAllLegalMoves(ref Span<Move> pseudoLegalMoves, ref Span<Move> opponentMoves)
        {
            Initialize();

            GenerateLegalMovesBitboard(ref pseudoLegalMoves);

            int legalMoveCount = 0;

            for (int i = 0; i < currentMoveIndex; i ++)
            {
                int savedCurrentMoveIndex = currentMoveIndex;
                ExecuteMove(pseudoLegalMoves[i]);
                ulong currentKingSquare = whiteToMove ? InternalBoard.Pieces[ChessBoard.Black, ChessBoard.King] : InternalBoard.Pieces[ChessBoard.White, ChessBoard.King];

                currentMoveIndex = 0;
                GenerateLegalMovesBitboard(ref opponentMoves);

                bool isLegal = true;
                for (int j = 0; j < currentMoveIndex; j++)
                {
                    if (opponentMoves[j].toSquare == currentKingSquare)
                    {
                        isLegal = false;
                        break;
                    }
                }

                if (isLegal)
                {
                    pseudoLegalMoves[legalMoveCount++] = pseudoLegalMoves[i];
                }

                UndoMove(pseudoLegalMoves[i]);
                currentMoveIndex = savedCurrentMoveIndex;
            }
            return legalMoveCount; // Returning the count of legal moves
        }

        private static int GetPieceAtSquare(int pieceColorIndex, ulong square)
        {

            // Check if a piece is captured
            for (int pieceType = ChessBoard.Pawn; pieceType <= ChessBoard.King; pieceType++)
            {
                if ((InternalBoard.Pieces[pieceColorIndex, pieceType] & square) != 0)
                {
                    return pieceType;
                }
            }

            return -1;
        }

        public static void ExecuteMove(Move move, bool inSearch = false)
        {
            int movedPiece = move.movedPiece;
            ulong toSquare = move.toSquare;
            ulong fromSquare = move.fromSquare;

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
            if (capturedPieceType != -1)
            {
                ulong captureSquare = toSquare;

                if (isEnPassant)
                {
                    captureSquare = whiteToMove ? captureSquare >> 8 : captureSquare << 8;
                    //InternalBoard.Pieces[opponentPieceColor, capturedPieceType] &= whiteToMove ? ~(fromSquare >> 8) : ~(fromSquare << 8);
                }

                InternalBoard.Pieces[OpponentColorIndex, capturedPieceType] &= ~captureSquare;
                // zobrist

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

                    // newZobristKey ^= Zobrist.piecesArray[rookPiece, castlingRookFromIndex];
                    // newZobristKey ^= Zobrist.piecesArray[rookPiece, castlingRookToIndex];
                }

                if (move.specialMove == SpecialMove.QueenSideCastleMove)
                {
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] &= ~(toSquare >> 2);
                    InternalBoard.Pieces[friendlyPieceColor, ChessBoard.Rook] |= toSquare << 1;

                    // newZobristKey ^= Zobrist.piecesArray[rookPiece, castlingRookFromIndex];
                    // newZobristKey ^= Zobrist.piecesArray[rookPiece, castlingRookToIndex];
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
                // int file = BoardHelper.FileIndex(startSquare) + 1;
                // newEnPassantFile = file;
                // newZobristKey ^= Zobrist.enPassantFile[file];
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
            // newZobristKey ^= Zobrist.sideToMove;
            // newZobristKey ^= Zobrist.piecesArray[movedPiece, startSquare];
            // newZobristKey ^= Zobrist.piecesArray[Square[targetSquare], targetSquare];
            // newZobristKey ^= Zobrist.enPassantFile[prevEnPassantFile];

            if (newCastlingRights != prevCastleState)
            {
                // newZobristKey ^= Zobrist.castlingRights[prevCastleState]; // remove old castling rights state
                // newZobristKey ^= Zobrist.castlingRights[newCastlingRights]; // add new castling rights state
            }

            whiteToMove = !whiteToMove;

            halfMoveAccumulator++;

            int newFiftyMoveCounter = CurrentGameState.fiftyMoveCounter + 1;


            InternalBoard.UpdateCompositeBitboards();

            // Pawn moves and captures reset the fifty move counter and clear 3-fold repetition history
            if (movedPiece == ChessBoard.Pawn || capturedPieceType != -1)
            {
                if (!inSearch)
                {
                    PositionHashes.Clear();
                }
                newFiftyMoveCounter = 0;
            }

            GameState newState = new(capturedPieceType, newEnPassantFile, newCastlingRights, newFiftyMoveCounter, newZobristKey);

            GameStateHistory.Push(newState);
            CurrentGameState = newState;

            if (!inSearch)
            {
                //PositionHashes[newState.zobristHashKey] += 1;
                //AllGameMoves.Add(move);
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
            bool undoingCapture = CurrentGameState.capturedPieceType != -1;

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

            if (!inSearch)
            {
                //PositionHashes
            }

            // go back to previous game state
            GameStateHistory.Pop();
            CurrentGameState = GameStateHistory.Peek();
            halfMoveAccumulator--;
        }


        public static void GenerateLegalMovesBitboard(ref Span<Move> moves)
        {
            // create list to store legal moves
            //List<Move> moves = new();

            // TODO edit genbishop, queen, and rook movesets to pass in reference to friendly piece bitboard

            if (whiteToMove)
            {
                GenerateBishopMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.Bishop], ref InternalBoard.AllWhitePieces);
                GenerateRookMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook], ref InternalBoard.AllWhitePieces);
                GenerateQueenMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.Queen], ref InternalBoard.AllWhitePieces);
                GenerateWhitePawnMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn]);
                GenerateKnightMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.Knight], ref InternalBoard.AllWhitePieces);
                GenerateKingMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.White, ChessBoard.King], ref InternalBoard.AllWhitePieces);
            }
            else
            {
                GenerateBishopMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Bishop], ref InternalBoard.AllBlackPieces);
                GenerateRookMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook], ref InternalBoard.AllBlackPieces);
                GenerateQueenMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Queen], ref InternalBoard.AllBlackPieces);
                GenerateBlackPawnMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn]);
                GenerateKnightMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Knight], ref InternalBoard.AllBlackPieces);
                GenerateKingMoves(ref moves, ref InternalBoard.Pieces[ChessBoard.Black, ChessBoard.King], ref InternalBoard.AllBlackPieces);
            }
        }

        // returns true if a piece is attacking the square
        private static bool SquareAttackedBy(int square)
        {

            /* What is going on here is confusing at first glance. For example, with pawn captures, if we want to find if a square is under
             * attack by a white pawn, we have to index the potential black pawn captures movetable to essentially
             * look from the perspective of that square if it had a black pawn on it and see if its potential capture squares intersect with
             * the white pawn we were originally addressing. Pawns capture opposite ways on opposite sides but in the same manner (diagonally).
             * */


            if (whiteToMove)
            {
                if ((MoveTables.PrecomputedWhitePawnCaptures[square] & InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Pawn]) != 0) return true;

                if ((MoveTables.PrecomputedKnightMoves[square] & InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Knight]) != 0) return true;

                if ((MoveTables.PrecomputedKingMoves[square] & InternalBoard.Pieces[ChessBoard.Black, ChessBoard.King]) != 0) return true;

                ulong bishopsAndQueens = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Bishop];
                ulong rooksAndQueens = InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.Black, ChessBoard.Rook];

                if ((GetBishopAttacks(InternalBoard.AllPieces, square) & bishopsAndQueens) != 0) return true;

                if ((GetRookAttacks(InternalBoard.AllPieces, square) & rooksAndQueens) != 0) return true;

            }
            else
            {

                if ((MoveTables.PrecomputedBlackPawnCaptures[square] & InternalBoard.Pieces[ChessBoard.White, ChessBoard.Pawn]) != 0) return true;

                if ((MoveTables.PrecomputedKnightMoves[square] & InternalBoard.Pieces[ChessBoard.White, ChessBoard.Knight]) != 0) return true;

                if ((MoveTables.PrecomputedKingMoves[square] & InternalBoard.Pieces[ChessBoard.White, ChessBoard.King]) != 0) return true;

                ulong bishopsAndQueens = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.White, ChessBoard.Bishop];
                ulong rooksAndQueens = InternalBoard.Pieces[ChessBoard.White, ChessBoard.Queen] | InternalBoard.Pieces[ChessBoard.White, ChessBoard.Rook];

                if ((GetBishopAttacks(InternalBoard.AllPieces, square) & bishopsAndQueens) != 0) return true;

                if ((GetRookAttacks(InternalBoard.AllPieces, square) & rooksAndQueens) != 0) return true;
            }

            return false;
        }
    }
}
