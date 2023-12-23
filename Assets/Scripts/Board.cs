using JetBrains.Annotations;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System.Security.Cryptography;


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
        private const int PieceTypeMask = 7;
        private const int PieceColorMask = 24;
        private const int PieceMoveStatusFlag = 32;
        //

        private const int BoardSize = 64;
        public static InternalSquare[] Squares = new InternalSquare[BoardSize];

        // this structure will hold a move that can be executed
        public struct LegalMove
        {
            // 'startSquare' and 'endSquare' holds the internal board start square and end square 
            public int startSquare;
            public int endSquare;

            // special move flags
            public bool? kingSideCastling;

            public bool? queenSideCastling;

            public bool? enPassant;
        }

        /* opting for a variable to control which list the algorithm places a move 
        into instead of passing the corresponding list as a parameter to every move 
        calculation function (opponent list or friendly list). */
        public static bool friendlyList = true;

        public static List<LegalMove> legalMoves = new List<LegalMove>();

        public static List<LegalMove> opponentMoves = new List<LegalMove>();


        private enum Direction { North, South, East, West, NorthWest, NorthEast, SouthWest, SouthEast };

        // west, north, east, south
        private static readonly int[] cardinalOffsets = { -1, 8, 1, -8 };

        // northwest, northeast, southeast, southwest
        private static readonly int[] interCardinalOffsets = { 7, 9, -7, -9 };

        private static readonly int[] pawnOffsets = { 7, 8, 9 };

        private static int lastPawnDoubleMoveSquare = -1;

        private static readonly int[,] knightOffsets = { { 17, -15, 15, -17 }, { 10, -6, 6, -10 } };


        public static void UpdateInternalState(int originalXPosition, int originalYPosition, int newXPosition, int newYPosition)
        {
            int newPieceMove = newYPosition * 8 + newXPosition;
            // grab current piece and store it
            int currentPiece = Squares[originalYPosition * 8 + originalXPosition].encodedPiece;

            // when the piece has moved, set the 6th bit to 1
            currentPiece = currentPiece | PieceMoveStatusFlag;

            // removing the piece from its old position
            Squares[originalYPosition * 8 + originalXPosition].encodedPiece = Piece.Empty;

            // placing the piece in its new position
            Squares[newPieceMove].encodedPiece = currentPiece;

            // check for special move flags
            /* TODO, THIS MAY CAUSE ISSUES WHEN CREATING THE CHESS ENGINE PART. I have not given this enough thought quite yet
               although my intitial hunch is that it should be okay as the engine will likely work entirely on the back end 
               and never have to update the internal state from the front end, but there may be a need to have new internal update functions for
               the engine to take advantage of. */

            HandlePawnPromotionInternal(currentPiece, newYPosition);

            // checks for a potential en passant capture
            HandleEnPassantInternal(currentPiece, originalXPosition, originalYPosition, newXPosition, newYPosition, newPieceMove);

            // checks for moves with the kingSideCastling flag 
            HandleKingSideCastleInternal(newPieceMove, newXPosition, newYPosition);

            // checks for moves with queenSideCastling flag
            HandleQueenSideCastleInternal(newPieceMove, newXPosition, newYPosition);

        }

        private static void HandlePawnPromotionInternal(int currentPiece, int newYPosition)
        {
            if (IsPawnPromotion(currentPiece, newYPosition))
            {
                // TODO will likely need to tweak this for the chess engine to automatically select a promotion choice based on the position

                PieceMovementManager.ShowPromotionDropdown();

            }
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
                    PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition, newYPosition - 1, false, false, true);
                }
                else if ((currentPiece & PieceColorMask) == Piece.Black)
                {
                    Squares[newPieceMove + 8].encodedPiece = Piece.Empty;
                    PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition, newYPosition + 1, false, false, true);
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
                PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition + 1, newYPosition, true, false, false);
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
                PieceMovementManager.UpdateFrontEndSpecialMove(newXPosition - 2, newYPosition, false, true, false);
            }
        }

        private static void AddLegalMove(int startSquare, int endSquare, bool? kingSideCastling, bool? queenSideCastling, bool? enPassant)
        {
            if (friendlyList)
            {
                legalMoves.Add(new LegalMove
                {
                    startSquare = startSquare,
                    endSquare = endSquare,
                    kingSideCastling = kingSideCastling,
                    queenSideCastling = queenSideCastling,
                    enPassant = enPassant

                });
            }
            else
            {
                opponentMoves.Add(new LegalMove
                {
                    startSquare = startSquare,
                    endSquare = endSquare,
                    kingSideCastling = kingSideCastling,
                    queenSideCastling = queenSideCastling,
                    enPassant = enPassant
                });
            }

        }

        private static void CheckWhitePawnCaptures(int startSquare)
        {
            int northWestSquare = startSquare + pawnOffsets[0];
            int northEastSquare = startSquare + pawnOffsets[2];

            // square one square northWest, checking if an enemy piece is there available for capture
            if (Squares[startSquare].DistanceNorthWest >= 1 && IsOpponentPiece(northWestSquare, Piece.Black))
            {
                AddLegalMove(startSquare, northWestSquare, false, false, false);
            }

            // square one square northEast, checking if an enemy piece is there available for capture
            if (Squares[startSquare].DistanceNorthEast >= 1 && IsOpponentPiece(northEastSquare, Piece.Black))
            {
                AddLegalMove(startSquare, northEastSquare, false, false, false);
            }

            // for en passant
            if (lastPawnDoubleMoveSquare != -1)
            {
                if ((lastPawnDoubleMoveSquare + 8 - startSquare) == pawnOffsets[2] && Squares[startSquare].DistanceEast >= 1)
                {
                    // one square above the black pawn that just moved
                    AddLegalMove(startSquare, lastPawnDoubleMoveSquare + 8, false, false, true);
                }

                if ((lastPawnDoubleMoveSquare + 8 - startSquare) == pawnOffsets[0] && Squares[startSquare].DistanceWest >= 1)
                {
                    // one square above the black pawn that just moved
                    AddLegalMove(startSquare, lastPawnDoubleMoveSquare + 8, false, false, true);
                }
            }
        }

        private static void CheckBlackPawnCaptures(int startSquare)
        {
            int southEastSquare = startSquare - pawnOffsets[0];
            int southWestSquare = startSquare - pawnOffsets[2];

            // square one square southEast, checking if an enemy piece is there available for capture
            if (Squares[startSquare].DistanceSouthEast >= 1 && IsOpponentPiece(southEastSquare, Piece.White))
            {
                AddLegalMove(startSquare, southEastSquare, false, false, false);
            }

            // square one square southWest, checking if an enemy piece is there available for capture
            if (Squares[startSquare].DistanceSouthWest >= 1 && IsOpponentPiece(southWestSquare, Piece.White))
            {
                AddLegalMove(startSquare, southWestSquare, false, false, false);
            }

            // for en passant
            if (lastPawnDoubleMoveSquare != -1)
            {
                if (startSquare - (lastPawnDoubleMoveSquare - 8) == pawnOffsets[2] && Squares[startSquare].DistanceWest >= 1)
                {
                    // one square below the white pawn that just moved
                    AddLegalMove(startSquare, lastPawnDoubleMoveSquare - 8, false, false, true);
                }

                if (startSquare - (lastPawnDoubleMoveSquare - 8) == pawnOffsets[0] && Squares[startSquare].DistanceEast >= 1)
                {
                    // one square below the white pawn that just moved
                    AddLegalMove(startSquare, lastPawnDoubleMoveSquare - 8, false, false, true);
                }
            }
        }

        private static bool IsOpponentPiece(int square, int opponentColor)
        {
            return Squares[square].encodedPiece != Piece.Empty && (Squares[square].encodedPiece & PieceColorMask) == opponentColor;
        }

        private static void CalculatePawnMoves(int startSquare, int decodedColor, int decodedPieceStatus)
        {
            // if white pawn
            if (decodedColor == Piece.White)
            {
                if (decodedPieceStatus == PieceMoveStatusFlag)
                {
                    // if pawn has moved, legal moves is only a one square advance
                    // checks if the square in front of the pawn is empty
                    if (Squares[startSquare + pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare + pawnOffsets[1], false, false, false);
                    }

                    CheckWhitePawnCaptures(startSquare);

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance
                    if (Squares[startSquare + pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare + pawnOffsets[1], false, false, false);

                        if (Squares[startSquare + (2 * pawnOffsets[1])].encodedPiece == Piece.Empty)
                        {
                            AddLegalMove(startSquare, startSquare + (2 * pawnOffsets[1]), false, false, false);
                        }
                    }

                    CheckWhitePawnCaptures(startSquare);
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
                        AddLegalMove(startSquare, startSquare - pawnOffsets[1], false, false, false);
                    }

                    CheckBlackPawnCaptures(startSquare);

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance
                    if (Squares[startSquare - pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare - pawnOffsets[1], false, false, false);

                        if (Squares[startSquare - (2 * pawnOffsets[1])].encodedPiece == Piece.Empty)
                        {
                            AddLegalMove(startSquare, startSquare - (2 * pawnOffsets[1]), false, false, false);
                        }
                    }

                    CheckBlackPawnCaptures(startSquare);
                }
            }

        }


        private static void CalculateKnightMovesHelper(int[] xOffsets, int[] yOffsets, int knightOffsetIndex, int startSquare, int decodedColor)
        {
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

                            AddLegalMove(startSquare, startSquare + knightOffsets[knightOffsetIndex, offsetIndex], false, false, false);
                        }
                    }
                }
            }
        }
        private static void CalculateKnightMoves(int startSquare, int decodedColor)
        {

            // {(𝑥±1,𝑦±2}∪{𝑥±2,y±1} represents available knight moves

            int[] xOffsets = { 1, -1 };
            int[] yOffsets = { 2, -2 };

            CalculateKnightMovesHelper(xOffsets, yOffsets, 0, startSquare, decodedColor);
            CalculateKnightMovesHelper(yOffsets, xOffsets, 1, startSquare, decodedColor);
        }

        private static void CalculateSlidingPiecesMoves(int piece, Direction direction, int startSquare, int decodedColor)
        {
            // direction offset
            int dOffset = 0, distance = 0;

            // limits search algorithm to one square if the sliding piece is the king
            int kingLimits = (piece == Piece.King ? 1 : int.MaxValue);

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
                        AddLegalMove(startSquare, startSquare + offset, false, false, false);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + offset, false, false, false);
            }

        }

        private static void CalculateRookMoves(int startSquare, int decodedColor)
        {
            CalculateSlidingPiecesMoves(Piece.Rook, Direction.North, startSquare, decodedColor);
            CalculateSlidingPiecesMoves(Piece.Rook, Direction.South, startSquare, decodedColor);
            CalculateSlidingPiecesMoves(Piece.Rook, Direction.East, startSquare, decodedColor);
            CalculateSlidingPiecesMoves(Piece.Rook, Direction.West, startSquare, decodedColor);

        }

        private static void CalculateBishopMoves(int startSquare, int decodedColor)
        {
            CalculateSlidingPiecesMoves(Piece.Bishop, Direction.NorthWest, startSquare, decodedColor);
            CalculateSlidingPiecesMoves(Piece.Bishop, Direction.NorthEast, startSquare, decodedColor);
            CalculateSlidingPiecesMoves(Piece.Bishop, Direction.SouthWest, startSquare, decodedColor);
            CalculateSlidingPiecesMoves(Piece.Bishop, Direction.SouthEast, startSquare, decodedColor);
        }

        private static void CalculateKingMoves(int startSquare, int decodedColor)
        {
            foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            {
                CalculateSlidingPiecesMoves(Piece.King, direction, startSquare, decodedColor);
            }

        }

        private static void CheckKingSideCastle(int startSquare)
        {
            // decodes piece move status; if king or rook on kingside has moved, castling not allowed
            if ((Squares[startSquare].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag || ((Squares[startSquare + 3].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag))
            {
                return;
            }

            // check for empty squares between rook and king
            for (int i = startSquare + 1; i < (startSquare + 3); i++)
            {
                if (Squares[i].encodedPiece != Piece.Empty)
                {
                    return;
                }

            }

            // check if squares king is leaving, crossing-over, or finishing on are not under attack
            for (int i = startSquare; i < (startSquare + 3); i++)
            {
                if (opponentMoves.Any(move => move.endSquare == i))
                {
                    return;
                }
            }

            // this adds a legal move with the kingSideCastling flag set to true
            AddLegalMove(startSquare, startSquare + 2, true, false, false);

        }

        private static void CheckQueenSideCastle(int startSquare)
        {
            // decodes piece move status; if king or rook on queenside has moved, castling not allowed
            if ((Squares[startSquare].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag || ((Squares[startSquare - 4].encodedPiece & PieceMoveStatusFlag) == PieceMoveStatusFlag))
            {
                return;
            }

            // check for empty squares between rook and king
            for (int i = startSquare - 1; i > (startSquare - 4); i--)
            {
                if (Squares[i].encodedPiece != Piece.Empty)
                {
                    return;
                }

            }

            // check if squares king is leaving, crossing-over, or finishing on are not under attack
            for (int i = startSquare; i > (startSquare - 3); i--)
            {
                if (opponentMoves.Any(move => move.endSquare == i))
                {
                    return;
                }
            }

            // this adds a legal move with the queenSideCastling flag set to true
            AddLegalMove(startSquare, startSquare - 2, false, true, false);

        }

        public static void ClearListMoves()
        {
            legalMoves.Clear();
            opponentMoves.Clear();
        }


        public static void AfterMove(bool whiteToMove)
        {
            // 'friendlyList' controls which list (opponent or friendly) the algorithm places a legal move into

            // for opponent side
            friendlyList = false;
            CalculateAllLegalMoves(!whiteToMove);

            // for friendly side
            friendlyList = true;
            CalculateAllLegalMoves(whiteToMove);
        }

        public static void CalculateAllLegalMoves(bool whiteToMove)
        {
            // this calculates all legal moves in a given position for either white or black
            for (int startSquare = 0; startSquare < BoardSize; startSquare++)
            {
                if (whiteToMove)
                {
                    if ((Squares[startSquare].encodedPiece & PieceColorMask) == Piece.White)
                    {
                        CalculateLegalMoves(startSquare, Squares[startSquare].encodedPiece);
                    }
                }
                else
                {
                    if ((Squares[startSquare].encodedPiece & PieceColorMask) == Piece.Black)
                    {
                        CalculateLegalMoves(startSquare, Squares[startSquare].encodedPiece);
                    }
                }
            }
            return;
        }


        public static void CalculateLegalMoves(int startSquare, int internalGamePiece)
        {

            int decodedPiece = internalGamePiece & PieceTypeMask;
            int decodedColor = internalGamePiece & PieceColorMask;

            // if = PieceMoveStatusFlag (32), piece has moved, if 0, piece has not moved
            int decodedPieceStatus = internalGamePiece & PieceMoveStatusFlag;

            switch (decodedPiece)
            {
                case Piece.Pawn:
                    CalculatePawnMoves(startSquare, decodedColor, decodedPieceStatus);
                    break;
                case Piece.Knight:
                    CalculateKnightMoves(startSquare, decodedColor);
                    break;
                case Piece.Rook:
                    CalculateRookMoves(startSquare, decodedColor);
                    break;
                case Piece.Bishop:
                    CalculateBishopMoves(startSquare, decodedColor);
                    break;
                case Piece.Queen:
                    // queen contains both movesets of a bishop and a rook
                    CalculateRookMoves(startSquare, decodedColor);
                    CalculateBishopMoves(startSquare, decodedColor);
                    break;
                case Piece.King:
                    CalculateKingMoves(startSquare, decodedColor);

                    // check for castling ability
                    CheckKingSideCastle(startSquare);
                    CheckQueenSideCastle(startSquare);
                    break;
                default:
                    return;
            }
            return;
        }
    }
}
