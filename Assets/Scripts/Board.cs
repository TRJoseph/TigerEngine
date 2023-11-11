using JetBrains.Annotations;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


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


        private const int BoardSize = 64;

        public static InternalSquare[] Squares = new InternalSquare[BoardSize];


        // this structure will hold a move that can be executed
        public struct LegalMove
        {
            // 'startSquare' and 'endSquare' holds the internal board start square and end square 
            public int startSquare;
            public int endSquare;


            // special move flags
            public bool? castling;
            public bool? enPassant;
        }

        public static List<LegalMove> legalMoves = new List<LegalMove>();

        private enum Direction { North, South, East, West, NorthWest, NorthEast, SouthWest, SouthEast };

        // west, north, east, south
        static readonly int[] cardinalOffsets = { -1, 8, 1, -8 };

        // northwest, northeast, southeast, southwest
        static readonly int[] interCardinalOffsets = { 7, 9, -7, -9 };

        static readonly int[] pawnOffsets = { 7, 8, 9 };

        static readonly int[,] knightOffsets = { { 17, -15, 15, -17 }, { 10, -6, 6, -10 } };


        public static void UpdateInternalState(float originalXPosition, float originalYPosition, float newXPosition, float newYPosition)
        {
            int newPieceMove = (int)newYPosition * 8 + (int)newXPosition;
            // grab current piece and store it
            int currentPiece = Squares[(int)originalYPosition * 8 + (int)originalXPosition].encodedPiece;

            // when the piece has moved, set the 6th bit to 1
            currentPiece = currentPiece | 32;

            // removing the piece from its old position
            Squares[(int)originalYPosition * 8 + (int)originalXPosition].encodedPiece = Piece.Empty;

            // placing the piece in its new position
            Squares[newPieceMove].encodedPiece = currentPiece;

            // check for special move flags

            // TODO: fix for queenside castling (may need to have two special move flags, one for kingside and one for queenside) and add en passant check
            if (legalMoves.Any(move => move.endSquare == newPieceMove && move.castling == true))
            {

                // grab rook in the corner on kingside
                int cornerRook = Squares[newPieceMove + 1].encodedPiece;

                Squares[newPieceMove + 1].encodedPiece = Piece.Empty;

                // update move and piece move status
                Squares[newPieceMove - 1].encodedPiece = cornerRook | 32;

                // TODO, add extra parameters to the UpdateFrontEndSpecialMove to handle other special moves
                // updates front end board representation
                PieceMovementManager.UpdateFrontEndSpecialMove((int)newXPosition + 1, (int)newYPosition);

            }

        }

        private static void AddLegalMove(int startSquare, int endSquare, bool? castling, bool? enPassant)
        {
            legalMoves.Add(new LegalMove
            {
                startSquare = startSquare,
                endSquare = endSquare,
                castling = castling,
                enPassant = enPassant

            });
        }

        private static void CheckWhitePawnCaptures(int startSquare)
        {
            // square one square northWest, checking if an enemy piece is there available for capture
            if (Squares[startSquare + pawnOffsets[0]].encodedPiece != Piece.Empty && (Squares[startSquare + pawnOffsets[0]].encodedPiece & 24) == Piece.Black)
            {
                AddLegalMove(startSquare, startSquare + pawnOffsets[0], false, false);
            }
            // square one square northEast, checking if an enemy piece is there available for capture
            if (Squares[startSquare + pawnOffsets[2]].encodedPiece != Piece.Empty && (Squares[startSquare + pawnOffsets[2]].encodedPiece & 24) == Piece.Black)
            {
                AddLegalMove(startSquare, startSquare + pawnOffsets[2], false, false);
            }
        }

        private static void CheckBlackPawnCaptures(int startSquare)
        {
            // square one square southEast, checking if an enemy piece is there available for capture
            if (Squares[startSquare - pawnOffsets[0]].encodedPiece != Piece.Empty && (Squares[startSquare - pawnOffsets[0]].encodedPiece & 24) == Piece.White)
            {
                AddLegalMove(startSquare, startSquare - pawnOffsets[0], false, false);
            }

            // square one square southWest, checking if an enemy piece is there available for capture
            if (Squares[startSquare - pawnOffsets[2]].encodedPiece != Piece.Empty && (Squares[startSquare - pawnOffsets[2]].encodedPiece & 24) == Piece.White)
            {
                AddLegalMove(startSquare, startSquare - pawnOffsets[2], false, false);
            }
        }

        private static void CalculatePawnMoves(int startSquare, int decodedColor, int decodedPieceStatus)
        {
            // if white pawn
            if (decodedColor == 8)
            {
                if (decodedPieceStatus == 32)
                {
                    // if pawn has moved, legal moves is only a one square advance

                    // checks if the square in front of the pawn is empty
                    if (Squares[startSquare + pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare + pawnOffsets[1], false, false);
                    }

                    CheckWhitePawnCaptures(startSquare);

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance


                    if (Squares[startSquare + pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare + pawnOffsets[1], false, false);

                        if (Squares[startSquare + (2 * pawnOffsets[1])].encodedPiece == Piece.Empty)
                        {
                            AddLegalMove(startSquare, startSquare + (2 * pawnOffsets[1]), false, false);
                        }
                    }

                    CheckWhitePawnCaptures(startSquare);
                }


            }
            else
            {
                // if black pawn
                if (decodedPieceStatus == 32)
                {
                    // if pawn has moved, legal moves is only a one square advance

                    if (Squares[startSquare - pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare - pawnOffsets[1], false, false);
                    }

                    CheckBlackPawnCaptures(startSquare);

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance
                    if (Squares[startSquare - pawnOffsets[1]].encodedPiece == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare - pawnOffsets[1], false, false);

                        if (Squares[startSquare - (2 * pawnOffsets[1])].encodedPiece == Piece.Empty)
                        {
                            AddLegalMove(startSquare, startSquare - (2 * pawnOffsets[1]), false, false);
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
                                if (decodedColor == (Squares[startSquare + knightOffsets[knightOffsetIndex, offsetIndex]].encodedPiece & 24))
                                {
                                    // same color piece
                                    continue;
                                }
                            }

                            AddLegalMove(startSquare, startSquare + knightOffsets[knightOffsetIndex, offsetIndex], false, false);
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
                    if (decodedColor == (Squares[startSquare + offset].encodedPiece & 24))
                    {
                        break;
                    }
                    else
                    {
                        AddLegalMove(startSquare, startSquare + offset, false, false);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + offset, false, false);
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
            if ((Squares[startSquare].encodedPiece & 32) == 32 || ((Squares[startSquare + 3].encodedPiece & 32) == 32))
            {
                return;
            }

            // check if squares king is leaving, crossing-over, or finishing on are not under attack

            // TODO: refine the 'legalMoves' filtering logic to exclude moves that jeopardize the king's safety when castling
            // currently, 'legalMoves' includes friendly moves that incorrectly indicate the king's path as safe for traversal during castling
            // need to ensure that moves do not put the king into or through check
            for (int i = startSquare; i < (startSquare + 3); i++)
            {
                // if (legalMoves.Any(move => move.endSquare == i))
                // {
                //     return;
                // }
            }

            // check for empty squares between rook and king
            for (int i = startSquare + 1; i < (startSquare + 3); i++)
            {
                if (Squares[i].encodedPiece != Piece.Empty)
                {
                    return;
                }

            }


            /* TODO, this legal move needs to have some extra functionality built in some how to move the rook as well,
            maybe a flag in the legal move marking a special move */
            AddLegalMove(startSquare, startSquare + 2, true, false);


        }

        private static void CheckQueenSideCastle(int startSquare)
        {
            // decodes piece move status; if king or rook on queenside has moved, castling not allowed
            if ((Squares[startSquare].encodedPiece & 32) == 32 || ((Squares[startSquare - 4].encodedPiece & 32) == 32))
            {
                return;
            }

        }

        public static void ClearListMoves()
        {
            legalMoves.Clear();
        }

        public static void CalculateAllLegalMoves(bool whiteToMove)
        {

            // this calculates 
            for (int startSquare = 0; startSquare < BoardSize; startSquare++)
            {
                if (whiteToMove)
                {
                    if ((Squares[startSquare].encodedPiece & 24) == 8)
                    {
                        CalculateLegalMoves(startSquare, Squares[startSquare].encodedPiece);
                    }

                }
                else
                {
                    if ((Squares[startSquare].encodedPiece & 24) == 16)
                    {
                        CalculateLegalMoves(startSquare, Squares[startSquare].encodedPiece);
                    }

                }
            }
            return;
        }


        public static List<LegalMove> CalculateLegalMoves(int startSquare, int internalGamePiece)
        {


            int decodedPiece = internalGamePiece & 7;
            int decodedColor = internalGamePiece & 24;

            // if = 32, piece has moved, if 0, piece has not moved
            int decodedPieceStatus = internalGamePiece & 32;

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
                    return legalMoves;
            }

            return legalMoves;
        }
    }
}
