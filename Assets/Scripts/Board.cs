using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.Timers;
using UnityEditor.Hardware;
using Unity.VisualScripting;
using UnityEngine.UIElements;
namespace Chess
{
    public static class Board
    {
        public struct InternalSquare
        {
            // this encoded value contains information about the piece itself (type, color, if it has moved)
            public int encodedPiece;

            // distances to edge of board from whichever tile the piece resides on
            public int DistanceNorth;
            public int DistanceSouth;
            public int DistanceEast;
            public int DistanceWest;

            public int DistanceNorthWest;
            public int DistanceNorthEast;
            public int DistanceSouthWest;
            public int DistanceSouthEast;
        }

        // values for decoding the encoded piece from binary (MCCTTT) M = Move Status flag bit, CC = Color bits, TTT = Piece Type bits
        public const int PieceTypeMask = 7;
        public const int PieceColorMask = 24;
        public const int PieceMoveStatusFlag = 32;
        //

        public struct ChessBoard
        {
            public ulong WhitePawns;
            public ulong WhiteRooks;
            public ulong WhiteKnights;
            public ulong WhiteBishops;
            public ulong WhiteQueens;
            public ulong WhiteKing;

            public ulong BlackPawns;
            public ulong BlackRooks;
            public ulong BlackKnights;
            public ulong BlackBishops;
            public ulong BlackQueens;
            public ulong BlackKing;

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

            public readonly ulong RankAttacks(int square)
            {
                return MoveTables.EastRayAttacks[square] | MoveTables.WestRayAttacks[square];
            }
            public readonly ulong FileAttacks(int square)
            {
                return MoveTables.NorthRayAttacks[square] | MoveTables.SouthRayAttacks[square];
            }
            public readonly ulong DiagonalAttacks(int square)
            {
                return MoveTables.NorthEastRayAttacks[square] | MoveTables.SouthWestRayAttacks[square];
            }
            public readonly ulong AntiDiagonalAttacks(int square)
            {
                return MoveTables.NorthWestRayAttacks[square] | MoveTables.SouthEastRayAttacks[square];
            }
            public readonly ulong RookAttacks(int square)
            {
                return RankAttacks(square) | FileAttacks(square);
            }
            public readonly ulong BishopAttacks(int square)
            {
                return DiagonalAttacks(square) | AntiDiagonalAttacks(square);
            }
            public readonly ulong QueenAttacks(int square)
            {
                return BishopAttacks(square) | RookAttacks(square);
            }

        }

        public static ChessBoard InternalBoard = new();

        // masks to prevent A file and H file wrapping for legal move calculations
        public const ulong AFileMask = 0x7f7f7f7f7f7f7f7f;
        public const ulong HFileMask = 0xfefefefefefefefe;

        // masks to prevent knight jumps from wrapping 
        public const ulong ABFileMask = 0x3F3F3F3F3F3F3F3F;
        public const ulong GHFileMask = 0xFCFCFCFCFCFCFCFC;

        public const int BoardSize = 64;
        public static InternalSquare[] Squares = new InternalSquare[BoardSize];

        // this structure will hold a move that can be executed
        public struct LegalMove
        {
            // 'startSquare' and 'endSquare' holds the internal board start square and end square 
            public int startSquare;
            public int endSquare;

            public int movedPiece;

            // special move flags
            public bool? kingSideCastling;

            public bool? queenSideCastling;

            public bool? enPassant;
        }

        public static List<LegalMove> legalMoves = new List<LegalMove>();

        private enum Direction { North, South, East, West, NorthWest, NorthEast, SouthWest, SouthEast };

        // west, north, east, south
        private static readonly int[] cardinalOffsets = { -1, 8, 1, -8 };

        // northwest, northeast, southeast, southwest
        private static readonly int[] interCardinalOffsets = { 7, 9, -7, -9 };

        private static readonly int[] pawnOffsets = { 7, 8, 9 };

        private static int lastPawnDoubleMoveSquare = -1;

        private static readonly int[,] knightOffsets = { { 17, -15, 15, -17 }, { 10, -6, 6, -10 } };

        // These flags are for game-ending conditions
        private static bool kingInCheck;

        private static int fiftyMoveAccumulator;


        public enum GameState
        {
            Normal,
            AwaitingPromotion,
            Ended
        }

        public static GameState currentState = GameState.Normal;

        private static void RemoveAndAddPieceBitboards(LegalMove move, ulong fromSquareMask, ulong toSquareMask, ref ulong friendlyPieces, ref ulong king, ref ulong knights, ref ulong bishops, ref ulong rooks, ref ulong queens, ref ulong pawns)
        {
            switch (move.movedPiece)
            {
                case Piece.King:
                    king &= ~fromSquareMask;
                    king |= toSquareMask;
                    break;
                case Piece.Queen:
                    queens &= ~fromSquareMask;
                    queens |= toSquareMask;
                    break;
                case Piece.Knight:
                    knights &= ~fromSquareMask;
                    knights |= toSquareMask;
                    break;
                case Piece.Bishop:
                    bishops &= ~fromSquareMask;
                    bishops |= toSquareMask;
                    break;
                case Piece.Rook:
                    rooks &= ~fromSquareMask;
                    rooks |= toSquareMask;
                    break;
                case Piece.Pawn:
                    pawns &= ~fromSquareMask;
                    pawns |= toSquareMask;
                    break;
            }

            // special moves here


        }

        public static void UpdateBitboards(int originalXPosition, int originalYPosition, int newXPosition, int newYPosition)
        {
            int oldPiecePosition = originalYPosition * 8 + originalXPosition;
            int newPiecePosition = newYPosition * 8 + newXPosition;

            LegalMove move = legalMoves.Single(move => move.startSquare == oldPiecePosition && move.endSquare == newPiecePosition);

            ulong fromSquare = 1UL << move.startSquare;
            ulong toSquare = 1UL << move.endSquare;

            // for a captured piece, AND the InternalBoard bitboards with the NOT of the isolated captured piece bitboard (endsquare)

            // remove any captured piece from bitboards
            ulong isolatedCapturedPieceBitmask = ~toSquare;
            if (BoardManager.whiteToMove)
            {
                // update potential captured piece 
                InternalBoard.BlackBishops &= isolatedCapturedPieceBitmask;
                InternalBoard.BlackKnights &= isolatedCapturedPieceBitmask;
                InternalBoard.BlackRooks &= isolatedCapturedPieceBitmask;
                InternalBoard.BlackPawns &= isolatedCapturedPieceBitmask;
                InternalBoard.BlackQueens &= isolatedCapturedPieceBitmask;
                InternalBoard.AllBlackPieces &= isolatedCapturedPieceBitmask;

                // remove appropriate white piece from old square and move it to new square, update bitboards correspondingly 
                RemoveAndAddPieceBitboards(move, fromSquare, toSquare, ref InternalBoard.AllWhitePieces, ref InternalBoard.WhiteKing, ref InternalBoard.WhiteKnights,
                    ref InternalBoard.WhiteBishops, ref InternalBoard.WhiteRooks, ref InternalBoard.WhiteQueens, ref InternalBoard.WhitePawns);
            }
            else
            {
                InternalBoard.WhiteBishops &= isolatedCapturedPieceBitmask;
                InternalBoard.WhiteKnights &= isolatedCapturedPieceBitmask;
                InternalBoard.WhiteRooks &= isolatedCapturedPieceBitmask;
                InternalBoard.WhitePawns &= isolatedCapturedPieceBitmask;
                InternalBoard.WhiteQueens &= isolatedCapturedPieceBitmask;
                InternalBoard.AllWhitePieces &= isolatedCapturedPieceBitmask;

                RemoveAndAddPieceBitboards(move, fromSquare, toSquare, ref InternalBoard.AllBlackPieces, ref InternalBoard.BlackKing, ref InternalBoard.BlackKnights,
                    ref InternalBoard.BlackBishops, ref InternalBoard.BlackRooks, ref InternalBoard.BlackQueens, ref InternalBoard.BlackPawns);
            }
            InternalBoard.AllPieces &= isolatedCapturedPieceBitmask;

            // update composite bitboards
            InternalBoard.AllWhitePieces = InternalBoard.WhitePawns | InternalBoard.WhiteKnights | InternalBoard.WhiteBishops | InternalBoard.WhiteRooks | InternalBoard.WhiteQueens | InternalBoard.WhiteKing;
            InternalBoard.AllBlackPieces = InternalBoard.BlackPawns | InternalBoard.BlackKnights | InternalBoard.BlackBishops | InternalBoard.BlackRooks | InternalBoard.BlackQueens | InternalBoard.BlackKing;
            InternalBoard.AllPieces = InternalBoard.AllBlackPieces | InternalBoard.AllWhitePieces;
        }

        public static void UpdateInternalState(int originalXPosition, int originalYPosition, int newXPosition, int newYPosition)
        {
            int newPieceMove = newYPosition * 8 + newXPosition;
            // grab current piece and store it
            int currentPiece = Squares[originalYPosition * 8 + originalXPosition].encodedPiece;

            // before the piece is changed this rule needs to be verified (50 moves without a piece capture or pawn advance)
            Verify50MoveRule(currentPiece, newPieceMove);

            // when the piece has moved, set the 6th bit to 1
            currentPiece = currentPiece | PieceMoveStatusFlag;

            // removing the piece from its old position
            Squares[originalYPosition * 8 + originalXPosition].encodedPiece = Piece.Empty;

            // placing the piece in its new position
            Squares[newPieceMove].encodedPiece = currentPiece;

            // check for special move flags

            // checks for pawn promotions and handles them appropriately
            HandlePawnPromotionInternal(newPieceMove, currentPiece, newYPosition);

            // checks for a potential en passant capture
            HandleEnPassantInternal(currentPiece, originalXPosition, originalYPosition, newXPosition, newYPosition, newPieceMove);

            // checks for moves with the kingSideCastling flag 
            HandleKingSideCastleInternal(newPieceMove, newXPosition, newYPosition);

            // checks for moves with queenSideCastling flag
            HandleQueenSideCastleInternal(newPieceMove, newXPosition, newYPosition);
        }

        private static void HandlePawnPromotionInternal(int newPieceMove, int currentPiece, int newYPosition)
        {
            if (IsPawnPromotion(currentPiece, newYPosition))
            {
                if (BoardManager.CurrentTurn == BoardManager.ComputerSide)
                {
                    UpdatePromotedPawnEngine(newPieceMove);
                }
                else
                {
                    UIController.Instance.ShowPromotionDropdown(newPieceMove);
                }

            }
        }

        public static void UpdatePromotedPawnEngine(int newPieceMove)
        {
            int chosenPiece = Engine.EvaluateBestPromotionPiece();
            switch (chosenPiece)
            {
                case Piece.Queen:
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 5;
                    break;

                case Piece.Rook:
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 4;
                    break;

                case Piece.Bishop:
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 3;
                    break;

                case Piece.Knight:
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 2;
                    break;

                default:
                    // this should not happen
                    throw new Exception();
            }

            UIController.Instance.UpdateMoveStatusUIInformation();
        }

        public static void UpdatePromotedPawn(int newPieceMove)
        {
            // this line performs a logical and operation on the entire piece to remove the piece type from the three least-significant bits
            Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece & (PieceColorMask + PieceMoveStatusFlag);

            int newPieceXPos = newPieceMove % 8;
            int newPieceYPos = newPieceMove / 8;

            switch (UIController.Instance.promotionSelection)
            {
                case "Queen":
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 5;

                    break;

                case "Rook":
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 4;
                    break;

                case "Bishop":
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 3;
                    break;

                case "Knight":
                    Squares[newPieceMove].encodedPiece = Squares[newPieceMove].encodedPiece | 2;
                    break;

                default:

                    // this should not happen
                    throw new Exception();
            }

            // update the front end sprite so the new correct piece is visible
            PieceMovementManager.UpdateFrontEndPromotion(Squares[newPieceMove].encodedPiece, newPieceXPos, newPieceYPos);

            // once the pawn has been swapped internally
            ClearListMoves();

            legalMoves = AfterMove();

            UIController.Instance.UpdateMoveStatusUIInformation();

        }

        private static bool IsPawnPromotion(int currentPiece, int newYPosition)
        {
            if ((currentPiece & PieceTypeMask) == Piece.Pawn)
            {
                if ((currentPiece & PieceColorMask) == Piece.White && newYPosition == 7)
                {
                    return true;
                }

                if ((currentPiece & PieceColorMask) == Piece.Black && newYPosition == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static void HandleEnPassantInternal(int currentPiece, int originalXPosition, int originalYPosition, int newXPosition, int newYPosition, int newPieceMove)
        {
            if ((currentPiece & PieceTypeMask) == Piece.Pawn && Math.Abs(originalYPosition * 8 + originalXPosition - newPieceMove) == 16)
            {
                lastPawnDoubleMoveSquare = newPieceMove;
            }
            else
            {
                lastPawnDoubleMoveSquare = -1;
            }

            if (legalMoves.Any(move => move.endSquare == newPieceMove && move.enPassant == true))
            {
                if ((currentPiece & PieceColorMask) == Piece.White)
                {
                    Squares[newPieceMove - 8].encodedPiece = Piece.Empty;
                    //blackPieces.Remove(newPieceMove - 8);
                    //PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition, newYPosition - 1, false, false, true);
                }
                else if ((currentPiece & PieceColorMask) == Piece.Black)
                {
                    Squares[newPieceMove + 8].encodedPiece = Piece.Empty;
                    //whitePieces.Remove(newPieceMove + 8);
                    //PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition, newYPosition + 1, false, false, true);
                }

            }
        }

        private static void HandleKingSideCastleInternal(int newPieceMove, int newXPosition, int newYPosition)
        {
            if (legalMoves.Any(move => move.endSquare == newPieceMove && move.kingSideCastling == true))
            {

                // grab rook in the corner on kingside
                int cornerRook = Squares[newPieceMove + 1].encodedPiece;

                Squares[newPieceMove + 1].encodedPiece = Piece.Empty;

                // update move and piece move status
                Squares[newPieceMove - 1].encodedPiece = cornerRook | PieceMoveStatusFlag;

                // updates front end board representation, moves king to new position and moves kingside rook to the square left of new king position
                //PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition + 1, newYPosition, true, false, false);
            }
        }

        private static void HandleQueenSideCastleInternal(int newPieceMove, int newXPosition, int newYPosition)
        {
            if (legalMoves.Any(move => move.endSquare == newPieceMove && move.queenSideCastling == true))
            {
                // grab rook in the corner on queenside
                int cornerRook = Squares[newPieceMove - 2].encodedPiece;

                Squares[newPieceMove - 2].encodedPiece = Piece.Empty;

                // update move and piece move status
                Squares[newPieceMove + 1].encodedPiece = cornerRook | PieceMoveStatusFlag;

                // updates front end board representation, moves king to new position and moves queenside rook to the square right of new king position
                //PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition - 2, newYPosition, false, true, false);
            }

        }

        private static LegalMove AddLegalMove(int startSquare, int endSquare, bool? kingSideCastling, bool? queenSideCastling, bool? enPassant, int movedPiece)
        {
            return
                new LegalMove
                {
                    startSquare = startSquare,
                    endSquare = endSquare,
                    kingSideCastling = kingSideCastling,
                    queenSideCastling = queenSideCastling,
                    enPassant = enPassant,
                    movedPiece = movedPiece
                };
        }

        private static List<LegalMove> CheckWhitePawnCaptures(int startSquare)
        {
            List<LegalMove> pawnCaptureMoves = new List<LegalMove>();
            int northWestSquare = startSquare + pawnOffsets[0];
            int northEastSquare = startSquare + pawnOffsets[2];

            // square one square northWest, checking if an enemy piece is there available for capture
            if (Squares[startSquare].DistanceNorthWest >= 1)
            {
                if (IsOpponentPiece(northWestSquare, Piece.Black))
                {
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, northWestSquare, false, false, false, Piece.Pawn));
                }
            }

            // square one square northEast, checking if an enemy piece is there available for capture

            if (Squares[startSquare].DistanceNorthEast >= 1)
            {
                if (IsOpponentPiece(northEastSquare, Piece.Black))
                {
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, northEastSquare, false, false, false, Piece.Pawn));
                }
            }

            // for en passant
            if (lastPawnDoubleMoveSquare != -1)
            {
                if ((lastPawnDoubleMoveSquare + 8 - startSquare) == pawnOffsets[2] && Squares[startSquare].DistanceEast >= 1)
                {
                    // one square above the black pawn that just moved
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, lastPawnDoubleMoveSquare + 8, false, false, true, Piece.Pawn));
                }

                if ((lastPawnDoubleMoveSquare + 8 - startSquare) == pawnOffsets[0] && Squares[startSquare].DistanceWest >= 1)
                {
                    // one square above the black pawn that just moved
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, lastPawnDoubleMoveSquare + 8, false, false, true, Piece.Pawn));
                }
            }

            return pawnCaptureMoves;
        }

        private static List<LegalMove> CheckBlackPawnCaptures(int startSquare)
        {
            List<LegalMove> pawnCaptureMoves = new List<LegalMove>();
            int southEastSquare = startSquare - pawnOffsets[0];
            int southWestSquare = startSquare - pawnOffsets[2];

            // square one square southEast, checking if an enemy piece is there available for capture
            if (Squares[startSquare].DistanceSouthEast >= 1)
            {
                if (IsOpponentPiece(southEastSquare, Piece.White))
                {
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, southEastSquare, false, false, false, Piece.Pawn));
                }
            }
            // square one square southWest, checking if an enemy piece is there available for capture

            if (Squares[startSquare].DistanceSouthWest >= 1)
            {
                if (IsOpponentPiece(southWestSquare, Piece.White))
                {
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, southWestSquare, false, false, false, Piece.Pawn));
                }
            }

            // for en passant
            if (lastPawnDoubleMoveSquare != -1)
            {
                if (startSquare - (lastPawnDoubleMoveSquare - 8) == pawnOffsets[2] && Squares[startSquare].DistanceWest >= 1)
                {
                    // one square below the white pawn that just moved
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, lastPawnDoubleMoveSquare - 8, false, false, true, Piece.Pawn));
                }

                if (startSquare - (lastPawnDoubleMoveSquare - 8) == pawnOffsets[0] && Squares[startSquare].DistanceEast >= 1)
                {
                    // one square below the white pawn that just moved
                    pawnCaptureMoves.Add(AddLegalMove(startSquare, lastPawnDoubleMoveSquare - 8, false, false, true, Piece.Pawn));
                }
            }

            return pawnCaptureMoves;
        }

        private static bool IsOpponentPiece(int square, int opponentColor)
        {
            return Squares[square].encodedPiece != Piece.Empty && (Squares[square].encodedPiece & PieceColorMask) == opponentColor;
        }

        private static List<LegalMove> CalculatePawnMoves(int startSquare, int decodedColor, int decodedPieceStatus)
        {
            List<LegalMove> pawnMoves = new List<LegalMove>();

            // if white pawn
            if (decodedColor == Piece.White)
            {
                if (decodedPieceStatus == PieceMoveStatusFlag)
                {
                    // if pawn has moved, legal moves is only a one square advance
                    // checks if the square in front of the pawn is empty
                    if (Squares[startSquare + pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        pawnMoves.Add(AddLegalMove(startSquare, startSquare + pawnOffsets[1], false, false, false, Piece.Pawn));
                    }

                    pawnMoves.AddRange(CheckWhitePawnCaptures(startSquare));

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance
                    if (Squares[startSquare + pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        pawnMoves.Add(AddLegalMove(startSquare, startSquare + pawnOffsets[1], false, false, false, Piece.Pawn));

                        if (Squares[startSquare + (2 * pawnOffsets[1])].encodedPiece == Piece.Empty)
                        {
                            pawnMoves.Add(AddLegalMove(startSquare, startSquare + (2 * pawnOffsets[1]), false, false, false, Piece.Pawn));
                        }
                    }

                    pawnMoves.AddRange(CheckWhitePawnCaptures(startSquare));
                }
            }
            else
            {
                // if black pawn
                if (decodedPieceStatus == PieceMoveStatusFlag)
                {
                    // if pawn has moved, legal moves is only a one square advance
                    if (Squares[startSquare - pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        pawnMoves.Add(AddLegalMove(startSquare, startSquare - pawnOffsets[1], false, false, false, Piece.Pawn));
                    }

                    pawnMoves.AddRange(CheckBlackPawnCaptures(startSquare));

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance
                    if (Squares[startSquare - pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        pawnMoves.Add(AddLegalMove(startSquare, startSquare - pawnOffsets[1], false, false, false, Piece.Pawn));

                        if (Squares[startSquare - (2 * pawnOffsets[1])].encodedPiece == Piece.Empty)
                        {
                            pawnMoves.Add(AddLegalMove(startSquare, startSquare - (2 * pawnOffsets[1]), false, false, false, Piece.Pawn));
                        }
                    }

                    pawnMoves.AddRange(CheckBlackPawnCaptures(startSquare));
                }
            }

            return pawnMoves;
        }


        private static List<LegalMove> CalculateKnightMovesHelper(int[] xOffsets, int[] yOffsets, int knightOffsetIndex, int startSquare, int decodedColor)
        {
            List<LegalMove> knightJumps = new List<LegalMove>();
            for (int i = 0; i < 2; i++) // Loop for x
            {
                int xOffset = xOffsets[i];
                if (xOffset <= Squares[startSquare].DistanceEast && xOffset >= -Squares[startSquare].DistanceWest)
                {
                    for (int j = 0; j < 2; j++) // Loop for y
                    {
                        int yOffset = yOffsets[j];
                        if (yOffset <= Squares[startSquare].DistanceNorth && yOffset >= -Squares[startSquare].DistanceSouth)
                        {

                            int offsetIndex = (i * 2 + j); // Calculate the offset index based on i and j

                            if (Squares[startSquare + knightOffsets[knightOffsetIndex, offsetIndex]].encodedPiece != Piece.Empty)
                            {
                                if (decodedColor == (Squares[startSquare + knightOffsets[knightOffsetIndex, offsetIndex]].encodedPiece & PieceColorMask))
                                {
                                    // same color piece
                                    continue;
                                }
                            }

                            knightJumps.Add(AddLegalMove(startSquare, startSquare + knightOffsets[knightOffsetIndex, offsetIndex], false, false, false, Piece.Knight));
                        }
                    }
                }
            }
            return knightJumps;
        }
        private static List<LegalMove> CalculateKnightMoves(int startSquare, int decodedColor)
        {
            List<LegalMove> knightMoves = new List<LegalMove>();

            // {(𝑥±1,𝑦±2}∪{𝑥±2,y±1} represents available knight moves

            int[] xOffsets = { 1, -1 };
            int[] yOffsets = { 2, -2 };

            knightMoves.AddRange(CalculateKnightMovesHelper(xOffsets, yOffsets, 0, startSquare, decodedColor));
            knightMoves.AddRange(CalculateKnightMovesHelper(yOffsets, xOffsets, 1, startSquare, decodedColor));

            return knightMoves;
        }

        private static List<LegalMove> CalculateSlidingPiecesMoves(int piece, Direction direction, int startSquare, int decodedColor)
        {
            List<LegalMove> slidingPiecesMoves = new();

            // direction offset
            int dOffset = 0, distance = 0;

            // limits search algorithm to one square if the sliding piece is the king
            int kingLimits = piece == Piece.King ? 1 : int.MaxValue;

            switch (direction)
            {
                case Direction.North:
                    dOffset = cardinalOffsets[1];
                    distance = Squares[startSquare].DistanceNorth;
                    break;
                case Direction.South:
                    dOffset = cardinalOffsets[3];
                    distance = Squares[startSquare].DistanceSouth;
                    break;
                case Direction.East:
                    dOffset = cardinalOffsets[2];
                    distance = Squares[startSquare].DistanceEast;
                    break;
                case Direction.West:
                    dOffset = cardinalOffsets[0];
                    distance = Squares[startSquare].DistanceWest;
                    break;
                case Direction.NorthWest:
                    dOffset = interCardinalOffsets[0];
                    distance = Squares[startSquare].DistanceNorthWest;
                    break;
                case Direction.NorthEast:
                    dOffset = interCardinalOffsets[1];
                    distance = Squares[startSquare].DistanceNorthEast;
                    break;
                case Direction.SouthWest:
                    dOffset = interCardinalOffsets[3];
                    distance = Squares[startSquare].DistanceSouthWest;
                    break;
                case Direction.SouthEast:
                    dOffset = interCardinalOffsets[2];
                    distance = Squares[startSquare].DistanceSouthEast;
                    break;
            }

            for (int i = 1, offset = dOffset; i <= distance && i <= kingLimits; i++, offset += dOffset)
            {
                //if a square is occupied by a piece of the same color, stop the loop
                //by a different color, add the move and stop the loop(capturing the piece)
                if (Squares[startSquare + offset].encodedPiece != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + offset].encodedPiece & PieceColorMask))
                    {
                        break;
                    }
                    else
                    {
                        slidingPiecesMoves.Add(AddLegalMove(startSquare, startSquare + offset, false, false, false, Squares[startSquare].encodedPiece));
                        break;
                    }
                }
                slidingPiecesMoves.Add(AddLegalMove(startSquare, startSquare + offset, false, false, false, Squares[startSquare].encodedPiece));
            }

            return slidingPiecesMoves;
        }

        private static List<LegalMove> CalculateRookMoves(int startSquare, int decodedColor)
        {
            List<LegalMove> rookMoves = new List<LegalMove>();
            rookMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Rook, Direction.North, startSquare, decodedColor));
            rookMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Rook, Direction.South, startSquare, decodedColor));
            rookMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Rook, Direction.East, startSquare, decodedColor));
            rookMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Rook, Direction.West, startSquare, decodedColor));

            return rookMoves;

        }

        private static List<LegalMove> CalculateBishopMoves(int startSquare, int decodedColor)
        {
            List<LegalMove> bishopMoves = new List<LegalMove>();
            bishopMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Bishop, Direction.NorthWest, startSquare, decodedColor));
            bishopMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Bishop, Direction.NorthEast, startSquare, decodedColor));
            bishopMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Bishop, Direction.SouthWest, startSquare, decodedColor));
            bishopMoves.AddRange(CalculateSlidingPiecesMoves(Piece.Bishop, Direction.SouthEast, startSquare, decodedColor));

            return bishopMoves;
        }

        private static List<LegalMove> CalculateKingMoves(int startSquare, int decodedColor)
        {
            List<LegalMove> kingMoves = new();
            foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            {
                kingMoves.AddRange(CalculateSlidingPiecesMoves(Piece.King, direction, startSquare, decodedColor));
            }
            return kingMoves;
        }

        /// <summary>
        ///  This method checks if king-side castling is even possible in the current position.
        ///  Castling requires a rook and king that have both not moved to have no pieces in between them in order to castle
        ///  At this point the opponent responses are not necessarily available (for the final rule: king cannot leave a square, 
        ///  traverse across a square, or land on a square that is under attack) so this function essentially adds a potential castling move.
        ///  The move added here will always be pseudo-legal.
        /// </summary>
        /// <param name="startSquare"></param>
        /// <returns></returns>
        private static LegalMove? CheckKingSideCastle(int startSquare)
        {
            // first checks if a rook piece is present in the corner and a king is on the e file
            // (this can be useful in non-standard FEN string positions)
            if ((Squares[startSquare].encodedPiece & PieceTypeMask) != Piece.King || ((Squares[startSquare + 3].encodedPiece & PieceTypeMask) != Piece.Rook))
            {
                return null;
            }
            // decodes piece move status; if king or rook on kingside has moved, castling not allowed
            if ((Squares[startSquare].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag || ((Squares[startSquare + 3].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag))
            {
                return null;
            }

            // check for empty squares between rook and king
            for (int i = startSquare + 1; i < (startSquare + 3); i++)
            {
                if (Squares[i].encodedPiece != Piece.Empty)
                {
                    return null;
                }

            }

            /* final rule (king cannot leave a square, traverse across a square, or land on a square that is under attack) is verified
            in the GenerateLegalMoves method.
            */


            // this adds a legal move with the kingSideCastling flag set to true
            return AddLegalMove(startSquare, startSquare + 2, true, false, false, Piece.King);

        }

        /// <summary>
        ///  This method checks if queen-side castling is even possible in the current position.
        ///  Refer to 'CheckKingSideCastle' in order to read more about what these methods do.
        /// </summary>
        /// <param name="startSquare"></param>
        /// <returns></returns>
        private static LegalMove? CheckQueenSideCastle(int startSquare)
        {
            // first checks if a rook piece is present in the corner and a king is on the e file
            // (this can be useful in non-standard FEN string positions)
            if ((Squares[startSquare].encodedPiece & PieceTypeMask) != Piece.King || ((Squares[startSquare - 4].encodedPiece & PieceTypeMask) != Piece.Rook))
            {
                return null;
            }
            // decodes piece move status; if king or rook on queenside has moved, castling not allowed
            if ((Squares[startSquare].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag || ((Squares[startSquare - 4].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag))
            {
                return null;
            }

            // check for empty squares between rook and king
            for (int i = startSquare - 1; i > (startSquare - 4); i--)
            {
                if (Squares[i].encodedPiece != Piece.Empty)
                {
                    return null;
                }

            }

            // this adds a legal move with the queenSideCastling flag set to true
            return AddLegalMove(startSquare, startSquare - 2, false, true, false, Piece.King);

        }
        private static int FindKingPosition(bool whiteToMove)
        {
            int color;
            if (whiteToMove)
            {
                color = Piece.White;
            }
            else
            {
                color = Piece.Black;
            }

            for (int i = 0; i < BoardSize; i++)
            {
                if ((Squares[i].encodedPiece & PieceTypeMask) == Piece.King)
                {
                    if ((Squares[i].encodedPiece & PieceColorMask) == color)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        public static void ClearListMoves()
        {
            legalMoves.Clear();
        }

        private static List<int> FindAllPieceSquares(bool whiteToMove)
        {
            List<int> squareList = new();
            int color;
            if (whiteToMove)
            {
                color = Piece.White;
            }
            else
            {
                color = Piece.Black;
            }

            for (int i = 0; i < BoardSize; i++)
            {
                if ((Squares[i].encodedPiece & PieceColorMask) == color)
                {
                    squareList.Add(i);
                }
            }
            return squareList;

        }


        public static List<LegalMove> AfterMove()
        {
            //Stopwatch timer = Stopwatch.StartNew();

            // calculates all legal moves in a given position
            legalMoves = GenerateLegalMovesBitboard();


            // timer.Stop();
            // TimeSpan timespan = timer.Elapsed;
            // UnityEngine.Debug.Log(String.Format("{0:00}:{1:00}:{2:00}", timespan.Minutes, timespan.Seconds, timespan.Milliseconds));

            // TODO this might need to be done inside of GenerateLegalMoves();
            CheckForGameOverRules();

            //SwapTurn();

            return legalMoves;
        }

        private static void Verify50MoveRule(int currentPiece, int newPieceMove)
        {
            // if the current piece being moved is a pawn, reset the 50 move accumulator
            // if the newpiecemove was a capture, reset the 50 move accumulator
            if ((Squares[currentPiece].encodedPiece & PieceTypeMask) == Piece.Pawn || (Squares[newPieceMove].encodedPiece & PieceTypeMask) != Piece.Empty)
            {
                // this move captured a piece, reset the fifty move rule
                fiftyMoveAccumulator = 0;
            }
            else
            {
                fiftyMoveAccumulator++;
            }
        }

        private static void CheckForGameOverRules()
        {
            if (legalMoves.Count == 0 && kingInCheck)
            {
                UnityEngine.Debug.Log("CheckMate!");
                currentState = GameState.Ended;
            }

            if (legalMoves.Count == 0 && !kingInCheck)
            {
                UnityEngine.Debug.Log("Stalemate!");
                currentState = GameState.Ended;
            }

            // a "move" consists of a player completing a turn followed by the opponent completing a turn, hence
            // why this is checking for 100, not 50. 
            if (fiftyMoveAccumulator == 100)
            {
                UnityEngine.Debug.Log("Draw by 50 move rule!");
                currentState = GameState.Ended;
            }

            // threefold repetition rule (position repeats three times is a draw)

            // draw by insufficient material rule, for example: knight and king cannot deliver checkmate

        }

        private static void SwapTurn()
        {
            BoardManager.CurrentTurn = BoardManager.CurrentTurn == BoardManager.Sides.White
                                    ? BoardManager.Sides.Black
                                    : BoardManager.Sides.White;
        }

        private static bool IsKingSideCastleLegal(int startSquare, List<LegalMove> opponentMoves)
        {
            int kingFinalSquare = startSquare + 2; // For kingside castling, king ends two squares to the right
            int kingPathSquare = startSquare + 1; // The square king passes through

            return !opponentMoves.Any(move => move.endSquare == kingFinalSquare || move.endSquare == kingPathSquare || move.endSquare == startSquare);
        }

        private static bool IsQueenSideCastleLegal(int startSquare, List<LegalMove> opponentMoves)
        {
            int kingPathSquare = startSquare - 2;
            int kingPathSquare2 = startSquare - 1; // The square king passes through

            return !opponentMoves.Any(move => move.endSquare == kingPathSquare || move.endSquare == kingPathSquare2 || move.endSquare == startSquare);
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


        private static ulong GetPrecomputedMoves(string direction, int currentQueenPos)
        {
            switch (direction)
            {
                case "North":
                    return MoveTables.NorthRayAttacks[currentQueenPos];
                case "South":
                    return MoveTables.SouthRayAttacks[currentQueenPos];
                case "East":
                    return MoveTables.EastRayAttacks[currentQueenPos];
                case "West":
                    return MoveTables.WestRayAttacks[currentQueenPos];
                case "NorthWest":
                    return MoveTables.NorthWestRayAttacks[currentQueenPos];
                case "NorthEast":
                    return MoveTables.NorthEastRayAttacks[currentQueenPos];
                case "SouthWest":
                    return MoveTables.SouthWestRayAttacks[currentQueenPos];
                case "SouthEast":
                    return MoveTables.SouthEastRayAttacks[currentQueenPos];
                default:
                    return 0;
            }
        }

        public static ulong GetMostSignificantBit(ulong value)
        {
            if (value == 0) return 0;
            ulong msb = value;
            msb |= (msb >> 1);
            msb |= (msb >> 2);
            msb |= (msb >> 4);
            msb |= (msb >> 8);
            msb |= (msb >> 16);
            msb |= (msb >> 32);
            return msb - (msb >> 1);
        }


        /////////
        /// <summary>
        /// This Function's primary purpose is to drop bits representing moves that are 'past' the blocked piece in the position given any ray attack.
        /// For example, a ray attack north of a white queen can have a few different outcomes. First, if a friendly piece is blocking, all moves past the blocked piece
        /// need to be excluded (bitwise anded) out of the bitboard. Secondly, if the piece is an enemy piece, the piece 'blocking' needs to be included in the
        /// attack bitboard. Finally, if there are no blockers, the original ray attack bitboard needs to be preserved.
        /// </summary>
        /// <param name="blockers"></param>
        /// <param name="rayAttack"></param>
        /// <returns></returns>
        public static ulong TruncateRayAttack(ulong blockers, ulong rayAttack)
        {
            ulong isolatedclosestBlocker = blockers & (~blockers + 1);

            ulong attackedPiece = isolatedclosestBlocker & rayAttack & (BoardManager.whiteToMove ? InternalBoard.AllBlackPieces : InternalBoard.AllWhitePieces);

            ulong blockerBitMask = ~(~isolatedclosestBlocker + 1);

            ulong MovesNotBlocked = (rayAttack & blockerBitMask) | attackedPiece;

            return MovesNotBlocked;
        }

        public static ulong TruncateRayAttackReverse(ulong blockers, ulong rayAttack)
        {
            ulong mostSignificantBlocker = GetMostSignificantBit(blockers);

            // If mostSignificantBlocker is zero, it means there are no blockers, and the entire ray attack is valid.
            if (mostSignificantBlocker == 0) return rayAttack;

            ulong attackedPiece = mostSignificantBlocker & rayAttack & (BoardManager.whiteToMove ? InternalBoard.AllBlackPieces : InternalBoard.AllWhitePieces);

            ulong blockerBitMask = mostSignificantBlocker | (mostSignificantBlocker - 1);

            ulong MovesNotBlocked = rayAttack & ~blockerBitMask | attackedPiece;

            return MovesNotBlocked;
        }
        /////////

        public static ulong TruncateBlockedMoves(string direction, int queenPosition, ulong blockers)
        {

            switch (direction)
            {
                case "North":
                    return TruncateRayAttack(blockers, MoveTables.NorthRayAttacks[queenPosition]);
                case "South":
                    return TruncateRayAttackReverse(blockers, MoveTables.SouthRayAttacks[queenPosition]);
                case "East":
                    return TruncateRayAttack(blockers, MoveTables.EastRayAttacks[queenPosition]);
                case "West":
                    return TruncateRayAttackReverse(blockers, MoveTables.WestRayAttacks[queenPosition]);
                case "NorthWest":
                    return TruncateRayAttack(blockers, MoveTables.NorthWestRayAttacks[queenPosition]);
                case "NorthEast":
                    return TruncateRayAttack(blockers, MoveTables.NorthEastRayAttacks[queenPosition]);
                case "SouthWest":
                    return TruncateRayAttackReverse(blockers, MoveTables.SouthWestRayAttacks[queenPosition]);
                case "SouthEast":
                    return TruncateRayAttackReverse(blockers, MoveTables.SouthEastRayAttacks[queenPosition]);
                default:
                    return 0;
            }
        }

        public static List<LegalMove> GenerateRookMoves(ref ulong rookBitboard)
        {
            List<LegalMove> rookMoves = new();

            ulong rooks = rookBitboard;

            while (rooks != 0)
            {
                // and with twos complement to isolate each rook
                ulong isolatedRooklsb = rooks & (~rooks + 1);
                int currentRookPos = (int)Math.Log(isolatedRooklsb, 2);

                ulong validRookMoves = 0;

                ulong movesInDirection = GetPrecomputedMoves("North", currentRookPos);
                ulong blockers = movesInDirection & InternalBoard.AllPieces;
                validRookMoves |= TruncateBlockedMoves("North", currentRookPos, blockers);

                movesInDirection = GetPrecomputedMoves("South", currentRookPos);
                blockers = movesInDirection & InternalBoard.AllPieces;
                validRookMoves |= TruncateBlockedMoves("South", currentRookPos, blockers);

                movesInDirection = GetPrecomputedMoves("East", currentRookPos);
                blockers = movesInDirection & InternalBoard.AllPieces;
                validRookMoves |= TruncateBlockedMoves("East", currentRookPos, blockers);

                movesInDirection = GetPrecomputedMoves("West", currentRookPos);
                blockers = movesInDirection & InternalBoard.AllPieces;
                validRookMoves |= TruncateBlockedMoves("West", currentRookPos, blockers);

                while (validRookMoves != 0)
                {
                    ulong movelsb = validRookMoves & (~validRookMoves + 1);
                    int validRookMove = (int)Math.Log(movelsb, 2);

                    validRookMoves &= validRookMoves - 1;
                    rookMoves.Add(AddLegalMove(currentRookPos, validRookMove, false, false, false, Piece.Rook));
                }
                rooks &= rooks - 1;
            }

            return rookMoves;
        }

        public static List<LegalMove> GenerateBishopMoves(ref ulong bishopBitboard)
        {
            List<LegalMove> bishopMoves = new();

            ulong bishops = bishopBitboard;

            while (bishops != 0)
            {
                // and with twos complement to isolate each bishop
                ulong isolatedBishoplsb = bishops & (~bishops + 1);
                int currentBishopPos = (int)Math.Log(isolatedBishoplsb, 2);

                ulong validBishopMoves = 0;

                ulong movesInDirection = GetPrecomputedMoves("NorthWest", currentBishopPos);
                ulong blockers = movesInDirection & InternalBoard.AllPieces;
                validBishopMoves |= TruncateBlockedMoves("NorthWest", currentBishopPos, blockers);

                movesInDirection = GetPrecomputedMoves("SouthWest", currentBishopPos);
                blockers = movesInDirection & InternalBoard.AllPieces;
                validBishopMoves |= TruncateBlockedMoves("SouthWest", currentBishopPos, blockers);

                movesInDirection = GetPrecomputedMoves("NorthEast", currentBishopPos);
                blockers = movesInDirection & InternalBoard.AllPieces;
                validBishopMoves |= TruncateBlockedMoves("NorthEast", currentBishopPos, blockers);

                movesInDirection = GetPrecomputedMoves("SouthEast", currentBishopPos);
                blockers = movesInDirection & InternalBoard.AllPieces;
                validBishopMoves |= TruncateBlockedMoves("SouthEast", currentBishopPos, blockers);


                while (validBishopMoves != 0)
                {
                    ulong movelsb = validBishopMoves & (~validBishopMoves + 1);
                    int validBishopMove = (int)Math.Log(movelsb, 2);

                    validBishopMoves &= validBishopMoves - 1;
                    bishopMoves.Add(AddLegalMove(currentBishopPos, validBishopMove, false, false, false, Piece.Bishop));
                }
                bishops &= bishops - 1;
            }

            return bishopMoves;
        }

        public static List<LegalMove> GenerateQueenMoves(ref ulong queenBitboard)
        {
            List<LegalMove> queenMoves = new();

            ulong queens = queenBitboard;

            while (queens != 0)
            {
                // and with twos complement to isolate each queen
                ulong isolatedQueenlsb = queens & (~queens + 1);
                int currentQueenPos = (int)Math.Log(isolatedQueenlsb, 2);

                //ulong validQueenMoves = InternalBoard.QueenAttacks(currentQueenPos);

                ulong validQueenMoves = 0;

                // This is the classical method, I am opting to not implement magic bitboards as of now because I want to focus more on engine development.
                // This is not quite as efficient as magic bitboards as this introduces a level of complexity to the calculations
                foreach (var direction in Enum.GetNames(typeof(Direction)))
                {
                    ulong movesInDirection = GetPrecomputedMoves(direction, currentQueenPos);
                    ulong blockers = movesInDirection & InternalBoard.AllPieces;
                    validQueenMoves |= TruncateBlockedMoves(direction, currentQueenPos, blockers);
                }

                while (validQueenMoves != 0)
                {
                    ulong movelsb = validQueenMoves & (~validQueenMoves + 1);
                    int validQueenMove = (int)Math.Log(movelsb, 2);

                    validQueenMoves &= validQueenMoves - 1;
                    queenMoves.Add(AddLegalMove(currentQueenPos, validQueenMove, false, false, false, Piece.Queen));
                }
                queens &= queens - 1;
            }

            return queenMoves;
        }

        public static List<LegalMove> GenerateWhitePawnMoves(ref ulong whitePawnBitboard)
        {
            List<LegalMove> whitePawnMoves = new();

            ulong whitePawns = whitePawnBitboard;

            while (whitePawns != 0)
            {
                // isolates each pawn one by one
                ulong isolatedPawnlsb = whitePawns & (~whitePawns + 1);
                // gets current pawn position to add to legal move list
                int currentPawnPos = (int)Math.Log(isolatedPawnlsb, 2);
                // valid pawn moves include pushes, captures, and en passant
                ulong validPawnMoves = MoveTables.PrecomputedWhitePawnPushes[currentPawnPos];

                if (WhitePawnsAbleToPushOneSquare(isolatedPawnlsb, ~InternalBoard.AllPieces) != isolatedPawnlsb)
                {
                    validPawnMoves &= ~MoveTables.RankMasks[(currentPawnPos / 8) + 1];
                }

                if (WhitePawnsAbleToPushTwoSquares(isolatedPawnlsb, ~InternalBoard.AllPieces) != isolatedPawnlsb)
                {
                    // fix this later
                    if (currentPawnPos < 48)
                    {
                        validPawnMoves &= ~MoveTables.RankMasks[(currentPawnPos / 8) + 2];
                    }
                }

                // if a pawn can capture any black piece it is a pseudo-legal capture
                ulong validPawnCaptures = MoveTables.PrecomputedWhitePawnCaptures[currentPawnPos] & InternalBoard.AllBlackPieces;
                validPawnMoves |= validPawnCaptures;

                // TODO: add en passant here?

                while (validPawnMoves != 0)
                {
                    ulong movelsb = validPawnMoves & (~validPawnMoves + 1);
                    int validPawnMove = (int)Math.Log(movelsb, 2);

                    validPawnMoves &= validPawnMoves - 1;
                    whitePawnMoves.Add(AddLegalMove(currentPawnPos, validPawnMove, false, false, false, Piece.Pawn));
                }

                // move to the next pawn
                whitePawns &= whitePawns - 1;
            }

            return whitePawnMoves;
        }

        public static List<LegalMove> GenerateBlackPawnMoves(ref ulong blackPawnBitboard)
        {
            List<LegalMove> blackPawnMoves = new();

            ulong blackPawns = blackPawnBitboard;

            while (blackPawns != 0)
            {
                ulong isolatedPawnlsb = blackPawns & (~blackPawns + 1);

                int currentPawnPos = (int)Math.Log(isolatedPawnlsb, 2);
                // valid pawn moves include pushes, captures, and en passant
                ulong validPawnMoves = MoveTables.PrecomputedBlackPawnPushes[currentPawnPos];

                if (BlackPawnsAbleToPushOneSquare(isolatedPawnlsb, ~InternalBoard.AllPieces) != isolatedPawnlsb)
                {
                    validPawnMoves &= ~MoveTables.RankMasks[(currentPawnPos / 8) - 1];
                }

                if (BlackPawnsAbleToPushTwoSquares(isolatedPawnlsb, ~InternalBoard.AllPieces) != isolatedPawnlsb)
                {
                    // fix this later
                    if (currentPawnPos > 15)
                    {
                        validPawnMoves &= ~MoveTables.RankMasks[(currentPawnPos / 8) - 2];
                    }
                }

                // if a pawn can capture any black piece it is a pseudo-legal capture
                ulong validPawnCaptures = MoveTables.PrecomputedBlackPawnCaptures[currentPawnPos] & InternalBoard.AllWhitePieces;
                validPawnMoves |= validPawnCaptures;

                while (validPawnMoves != 0)
                {
                    ulong movelsb = validPawnMoves & (~validPawnMoves + 1);
                    int validPawnMove = (int)Math.Log(movelsb, 2);

                    validPawnMoves &= validPawnMoves - 1;
                    blackPawnMoves.Add(AddLegalMove(currentPawnPos, validPawnMove, false, false, false, Piece.Pawn));
                }

                blackPawns &= blackPawns - 1;
            }

            return blackPawnMoves;
        }

        public static List<LegalMove> GenerateKnightMoves(ref ulong knightBitboard, ref ulong friendlyPieces)
        {
            List<LegalMove> knightMoves = new();

            ulong knights = knightBitboard;
            while (knights != 0)
            {
                // isolate each knight
                ulong isolatedKnightlsb = knights & (~knights + 1);
                int currentKnightPos = (int)Math.Log(isolatedKnightlsb, 2);

                // valid knight moves only include either empty squares or squares the opponent pieces occupy
                ulong validKnightMoves = MoveTables.PrecomputedKnightMoves[currentKnightPos] & ~friendlyPieces;

                while (validKnightMoves != 0)
                {
                    ulong movelsb = validKnightMoves & (~validKnightMoves + 1);
                    int validKnightMove = (int)Math.Log(movelsb, 2);

                    validKnightMoves &= validKnightMoves - 1;
                    knightMoves.Add(AddLegalMove(currentKnightPos, validKnightMove, false, false, false, Piece.Knight));
                }

                // move to next knight
                knights &= knights - 1;
            }
            return knightMoves;
        }

        public static List<LegalMove> GenerateKingMoves(ref ulong king, ref ulong friendlyPieces)
        {
            List<LegalMove> kingMoves = new();

            // king index converts the king bitboard 
            int kingIndex = (int)Math.Log(king, 2);

            // grabs the corresponding bitboard representing all legal moves from the given king index on the board
            ulong validKingMoves = MoveTables.PrecomputedKingMoves[kingIndex];

            // valid king moves only include either empty squares or squares the opponent pieces occupy (for now, this will change when check is implemented)
            validKingMoves &= ~friendlyPieces;

            while (validKingMoves != 0)
            {
                // gets the least significant bit while validmoves are being parsed in order to find new square position
                ulong lsb = validKingMoves & (~validKingMoves + 1);

                int validKingMove = (int)Math.Log(lsb, 2);

                validKingMoves &= validKingMoves - 1;

                kingMoves.Add(AddLegalMove(kingIndex, validKingMove, false, false, false, Piece.King));
            }

            return kingMoves;
        }

        public static List<LegalMove> GenerateLegalMovesBitboard()
        {
            // create list to store legal moves
            List<LegalMove> moves = new();

            // TODO edit genbishop, queen, and rook movesets to pass in reference to friendly piece bitboard

            if (BoardManager.whiteToMove)
            {
                moves.AddRange(GenerateBishopMoves(ref InternalBoard.WhiteBishops));
                moves.AddRange(GenerateRookMoves(ref InternalBoard.WhiteRooks));
                moves.AddRange(GenerateQueenMoves(ref InternalBoard.WhiteQueens));
                moves.AddRange(GenerateWhitePawnMoves(ref InternalBoard.WhitePawns));
                moves.AddRange(GenerateKnightMoves(ref InternalBoard.WhiteKnights, ref InternalBoard.AllWhitePieces));
                moves.AddRange(GenerateKingMoves(ref InternalBoard.WhiteKing, ref InternalBoard.AllWhitePieces));
            }
            else
            {
                moves.AddRange(GenerateBishopMoves(ref InternalBoard.BlackBishops));
                moves.AddRange(GenerateRookMoves(ref InternalBoard.BlackRooks));
                moves.AddRange(GenerateQueenMoves(ref InternalBoard.BlackQueens));
                moves.AddRange(GenerateBlackPawnMoves(ref InternalBoard.BlackPawns));
                moves.AddRange(GenerateKnightMoves(ref InternalBoard.BlackKnights, ref InternalBoard.AllBlackPieces));
                moves.AddRange(GenerateKingMoves(ref InternalBoard.BlackKing, ref InternalBoard.AllBlackPieces));
            }

            return moves;

        }

        public static List<LegalMove> GenerateLegalMovesBlackPieces()
        {
            List<LegalMove> blackMoves = new();


            return blackMoves;
        }

        //public static List<LegalMove> GenerateLegalMovesBitBoard()
        //{
        //    List<LegalMove> pseudoLegalMoves = new List<LegalMove>();


        //    pseudoLegalMoves.AddRange(GenerateLegalMovesBitboards(BoardManager.whiteToMove));


        //    return pseudoLegalMoves;
        //}

        public static ulong ComputeKnightMoves(ulong knight_loc)
        {
            ulong square1 = InternalBoard.NorthNorthEast(knight_loc);
            ulong square2 = InternalBoard.NorthNorthWest(knight_loc);
            ulong square3 = InternalBoard.NorthWestWest(knight_loc);
            ulong square4 = InternalBoard.NorthEastEast(knight_loc);
            ulong square5 = InternalBoard.SouthSouthEast(knight_loc);
            ulong square6 = InternalBoard.SouthSouthWest(knight_loc);
            ulong square7 = InternalBoard.SouthWestWest(knight_loc);
            ulong square8 = InternalBoard.SouthEastEast(knight_loc);


            ulong knightMoves = square1 | square2 | square3 | square4 | square5 | square6 | square7 | square8;

            ulong knightValidMoves = BoardManager.whiteToMove ? knightMoves & ~InternalBoard.AllWhitePieces : knightMoves & ~InternalBoard.AllBlackPieces;

            return knightValidMoves;
        }

        public static ulong ComputeKingMoves(ulong king_loc)
        {
            ulong square1 = InternalBoard.EastOne(king_loc);
            ulong square2 = InternalBoard.NorthEastOne(king_loc);
            ulong square3 = InternalBoard.SouthEastOne(king_loc);
            ulong square4 = InternalBoard.NorthOne(king_loc);
            ulong square5 = InternalBoard.NorthWestOne(king_loc);
            ulong square6 = InternalBoard.WestOne(king_loc);
            ulong square7 = InternalBoard.SouthWestOne(king_loc);
            ulong square8 = InternalBoard.SouthOne(king_loc);

            ulong kingMoves = square1 | square2 | square3 | square4 | square5 | square6 | square7 | square8;

            ulong kingValidMoves = BoardManager.whiteToMove ? kingMoves & ~InternalBoard.AllWhitePieces : kingMoves & ~InternalBoard.AllBlackPieces;
            return kingValidMoves;
        }




        public static List<LegalMove> GenerateLegalMoves()
        {
            // calculate all pseudo legal moves for the friendly side (whoevers turn it is)
            List<LegalMove> pseudoLegalMoves = CalculateAllMoves(BoardManager.whiteToMove);

            List<LegalMove> legalMoves = new List<LegalMove>();

            int originalkingSquare = FindKingPosition(BoardManager.whiteToMove);

            kingInCheck = false;

            foreach (LegalMove move in pseudoLegalMoves)
            {
                int rememberedPiece = ExecuteMove(move);

                // replace this with current king square
                int currentKingSquare = FindKingPosition(BoardManager.whiteToMove);

                List<LegalMove> opponentResponses = CalculateAllMoves(!BoardManager.whiteToMove);

                // Special handling for castling moves
                if (move.kingSideCastling == true)
                {
                    if (IsKingSideCastleLegal(originalkingSquare, opponentResponses))
                    {
                        legalMoves.Add(move);
                    }
                }
                else if (move.queenSideCastling == true)
                {
                    if (IsQueenSideCastleLegal(originalkingSquare, opponentResponses))
                    {
                        legalMoves.Add(move);
                    }
                }
                // handle other moves
                else if (!opponentResponses.Any(response => response.endSquare == currentKingSquare))
                {
                    // if the king is not under attack after the move, add it to legal moves
                    legalMoves.Add(move);
                }
                else
                {
                    kingInCheck = true;
                }
                // Undo the move for the next iteration
                UndoMove(move, rememberedPiece);
            }
            return legalMoves;
        }

        private static int ExecuteMove(LegalMove move)
        {
            int startSquareIndex = move.startSquare;
            int endSquareIndex = move.endSquare;
            int movingPiece = Squares[startSquareIndex].encodedPiece;

            // remember potential captured piece (this is needed when the move is un-done and the position returns to its previous state)
            int rememberedPiece = Squares[endSquareIndex].encodedPiece;

            // Move the piece
            Squares[endSquareIndex].encodedPiece = movingPiece;
            Squares[startSquareIndex].encodedPiece = Piece.Empty;
            return rememberedPiece;
        }

        private static void UndoMove(LegalMove move, int rememberedPiece)
        {
            int startSquareIndex = move.startSquare;
            int endSquareIndex = move.endSquare;
            int movingPiece = Squares[endSquareIndex].encodedPiece;
            int capturedPiece = rememberedPiece;

            // Revert the move
            Squares[startSquareIndex].encodedPiece = movingPiece;
            Squares[endSquareIndex].encodedPiece = capturedPiece;
        }

        public static List<LegalMove> CalculateAllMoves(bool friendlyMove)
        {
            List<int> whichPieces = FindAllPieceSquares(friendlyMove);
            List<LegalMove> moveList = new List<LegalMove>();

            foreach (int square in whichPieces)
            {
                moveList.AddRange(CalculateLegalMoves(square, Squares[square].encodedPiece));
            }
            return moveList;
        }

        public static List<LegalMove> CalculateLegalMoves(int startSquare, int internalGamePiece)
        {
            int decodedPiece = internalGamePiece & PieceTypeMask;
            int decodedColor = internalGamePiece & PieceColorMask;

            // if = PieceMoveStatusFlag (32), piece has moved, if 0, piece has not moved
            int decodedPieceStatus = internalGamePiece & PieceMoveStatusFlag;
            switch (decodedPiece)
            {
                case Piece.Pawn:
                    // a pawn can never have more than 4 moves in any given position
                    List<LegalMove> pawnMoves = new List<LegalMove>(4);
                    pawnMoves.AddRange(CalculatePawnMoves(startSquare, decodedColor, decodedPieceStatus));
                    return pawnMoves;
                case Piece.Knight:
                    List<LegalMove> knightMoves = new List<LegalMove>(8);
                    knightMoves.AddRange(CalculateKnightMoves(startSquare, decodedColor));
                    return knightMoves;
                case Piece.Rook:
                    List<LegalMove> rookMoves = new List<LegalMove>(14);
                    rookMoves.AddRange(CalculateRookMoves(startSquare, decodedColor));
                    return rookMoves;
                case Piece.Bishop:
                    List<LegalMove> bishopMoves = new List<LegalMove>(13);
                    bishopMoves.AddRange(CalculateBishopMoves(startSquare, decodedColor));
                    return bishopMoves;
                case Piece.Queen:
                    // queen contains both movesets of a bishop and a rook
                    List<LegalMove> queenMoves = new List<LegalMove>(27);
                    queenMoves.AddRange(CalculateRookMoves(startSquare, decodedColor));
                    queenMoves.AddRange(CalculateBishopMoves(startSquare, decodedColor));
                    return queenMoves;
                case Piece.King:
                    List<LegalMove> kingMoves = new List<LegalMove>(8);
                    kingMoves.AddRange(CalculateKingMoves(startSquare, decodedColor));

                    // check for castling ability
                    // makes sure king is on e file
                    if (startSquare == 4 || startSquare == 60)
                    {
                        var kingSideCastleMove = CheckKingSideCastle(startSquare);

                        if (kingSideCastleMove != null)
                        {

                            kingMoves.Add(kingSideCastleMove.Value);
                        }

                        var queenSideCastleMove = CheckQueenSideCastle(startSquare);

                        if (queenSideCastleMove != null)
                        {
                            kingMoves.Add(queenSideCastleMove.Value);
                        }
                    }
                    return kingMoves;
                default:
                    return null;
            }
        }
    }
}
