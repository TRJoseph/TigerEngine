using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


namespace Chess
{
    public static class Board
    {
        public static int[] Squares = new int[64];


        // this structure will hold a move that can be executed
        public struct LegalMove
        {
            // 'startSquare' and 'endSquare' holds the internal board start square and end square 
            public int startSquare;
            public int endSquare;

            public Tile endTile;
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
            // grab current piece and store it
            int currentPiece = Squares[(int)originalYPosition * 8 + (int)originalXPosition];

            // when the piece has moved, set the 6th bit to 1
            currentPiece = currentPiece | 32;

            // removing the piece from its old position
            Squares[(int)originalYPosition * 8 + (int)originalXPosition] = Piece.Empty;

            // placing the piece in its new position
            Squares[(int)newYPosition * 8 + (int)newXPosition] = currentPiece;

        }

        private static void AddLegalMove(int startSquare, int endSquare, Tile endTile)
        {
            legalMoves.Add(new LegalMove
            {
                startSquare = startSquare,
                endSquare = endSquare,
                endTile = endTile
            });
        }

        private static void CheckWhitePawnCaptures(int startSquare, int xPos, int yPos)
        {
            // square one square northWest, checking if an enemy piece is there available for capture
            if (Squares[startSquare + pawnOffsets[0]] != Piece.Empty && (Squares[startSquare + pawnOffsets[0]] & 24) == Piece.Black)
            {
                AddLegalMove(startSquare, startSquare + pawnOffsets[0], GridManager.chessTiles[xPos - 1, yPos + 1]);
            }
            // square one square northEast, checking if an enemy piece is there available for capture
            if (Squares[startSquare + pawnOffsets[2]] != Piece.Empty && (Squares[startSquare + pawnOffsets[2]] & 24) == Piece.Black)
            {
                AddLegalMove(startSquare, startSquare + pawnOffsets[2], GridManager.chessTiles[xPos + 1, yPos + 1]);
            }
        }

        private static void CheckBlackPawnCaptures(int startSquare, int xPos, int yPos)
        {
            // square one square southEast, checking if an enemy piece is there available for capture
            if (Squares[startSquare - pawnOffsets[0]] != Piece.Empty && (Squares[startSquare - pawnOffsets[0]] & 24) == Piece.White)
            {
                AddLegalMove(startSquare, startSquare - pawnOffsets[0], GridManager.chessTiles[xPos + 1, yPos - 1]);
            }

            // square one square southWest, checking if an enemy piece is there available for capture
            if (Squares[startSquare - pawnOffsets[2]] != Piece.Empty && (Squares[startSquare - pawnOffsets[2]] & 24) == Piece.White)
            {
                AddLegalMove(startSquare, startSquare - pawnOffsets[2], GridManager.chessTiles[xPos - 1, yPos - 1]);
            }
        }

        private static void CalculatePawnMoves(int startSquare, int decodedColor, int decodedPieceStatus, int xPos, int yPos)
        {
            // if white pawn
            if (decodedColor == 8)
            {
                if (decodedPieceStatus == 32)
                {
                    // if pawn has moved, legal moves is only a one square advance

                    // checks if the square in front of the pawn is empty
                    if (Squares[startSquare + pawnOffsets[1]] == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare + pawnOffsets[1], GridManager.chessTiles[xPos, yPos + 1]);
                    }

                    CheckWhitePawnCaptures(startSquare, xPos, yPos);

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance


                    if (Squares[startSquare + pawnOffsets[1]] == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare + pawnOffsets[1], GridManager.chessTiles[xPos, yPos + 1]);

                        if (Squares[startSquare + (2 * pawnOffsets[1])] == Piece.Empty)
                        {
                            AddLegalMove(startSquare, startSquare + (2 * pawnOffsets[1]), GridManager.chessTiles[xPos, yPos + 2]);
                        }
                    }

                    CheckWhitePawnCaptures(startSquare, xPos, yPos);
                }


            }
            else
            {
                // if black pawn
                if (decodedPieceStatus == 32)
                {
                    // if pawn has moved, legal moves is only a one square advance

                    if (Squares[startSquare - pawnOffsets[1]] == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare - pawnOffsets[1], GridManager.chessTiles[xPos, yPos - 1]);
                    }

                    CheckBlackPawnCaptures(startSquare, xPos, yPos);

                }
                else
                {
                    // if pawn has not moved, legal moves is a two square advance
                    if (Squares[startSquare - pawnOffsets[1]] == Piece.Empty)
                    {
                        AddLegalMove(startSquare, startSquare - pawnOffsets[1], GridManager.chessTiles[xPos, yPos - 1]);

                        if (Squares[startSquare - (2 * pawnOffsets[1])] == Piece.Empty)
                        {
                            AddLegalMove(startSquare, startSquare - (2 * pawnOffsets[1]), GridManager.chessTiles[xPos, yPos - 2]);
                        }
                    }

                    CheckBlackPawnCaptures(startSquare, xPos, yPos);
                }
            }

        }


        private static void CalculateKnightMovesHelper(Tile currentTile, int[] xOffsets, int[] yOffsets, int knightOffsetIndex, int startSquare, int decodedColor, int xPos, int yPos)
        {
            for (int i = 0; i < 2; i++) // Loop for x
            {
                int xOffset = xOffsets[i];
                if (xOffset <= currentTile.distances.DistanceEast && xOffset >= -currentTile.distances.DistanceWest)
                {
                    for (int j = 0; j < 2; j++) // Loop for y
                    {
                        int yOffset = yOffsets[j];
                        if (yOffset <= currentTile.distances.DistanceNorth && yOffset >= -currentTile.distances.DistanceSouth)
                        {

                            int offsetIndex = (i * 2 + j); // Calculate the offset index based on i and j

                            if (Squares[startSquare + knightOffsets[knightOffsetIndex, offsetIndex]] != Piece.Empty)
                            {
                                if (decodedColor == (Squares[startSquare + knightOffsets[knightOffsetIndex, offsetIndex]] & 24))
                                {
                                    // same color piece
                                    continue;
                                }
                            }

                            AddLegalMove(startSquare, startSquare + knightOffsets[knightOffsetIndex, offsetIndex], GridManager.chessTiles[xPos + xOffset, yPos + yOffset]);
                        }
                    }
                }
            }
        }
        private static void CalculateKnightMoves(Tile currentTile, int startSquare, int decodedColor, int xPos, int yPos)
        {

            // {(𝑥±1,𝑦±2}∪{𝑥±2,y±1} represents available knight moves

            int[] xOffsets = { 1, -1 };
            int[] yOffsets = { 2, -2 };

            CalculateKnightMovesHelper(currentTile, xOffsets, yOffsets, 0, startSquare, decodedColor, xPos, yPos);
            CalculateKnightMovesHelper(currentTile, yOffsets, xOffsets, 1, startSquare, decodedColor, xPos, yPos);


        }
        
        private static void CalculateSlidingPiecesMoves(int piece, Direction direction, Tile currentTile, int startSquare, int decodedColor, int xPos, int yPos)
        {
            // direction offset
            int dOffset = 0, distance = 0;
            int yHighLightOffset = 0, xHighLightOffset = 0;

            // limits search algorithm to one square if the sliding piece is the king
            int kingLimits = (piece == Piece.King ? 1 : int.MaxValue);

            switch(direction)
            {
                case Direction.North:
                    dOffset = cardinalOffsets[1];
                    distance = currentTile.distances.DistanceNorth;
                    yHighLightOffset = 1;
                    break;
                case Direction.South:
                    dOffset = cardinalOffsets[3];
                    distance = currentTile.distances.DistanceSouth;
                    yHighLightOffset = -1;
                    break;
                case Direction.East:
                    dOffset = cardinalOffsets[2];
                    distance = currentTile.distances.DistanceEast;
                    xHighLightOffset = 1;
                    break;
                case Direction.West:
                    dOffset = cardinalOffsets[0];
                    distance = currentTile.distances.DistanceWest;
                    xHighLightOffset = -1;
                    break;
                case Direction.NorthWest:
                    dOffset = interCardinalOffsets[0];
                    distance = currentTile.distances.DistanceNorthWest;
                    xHighLightOffset = -1;
                    yHighLightOffset = 1;
                    break;
                case Direction.NorthEast:
                    dOffset = interCardinalOffsets[1];
                    distance = currentTile.distances.DistanceNorthEast;
                    xHighLightOffset = 1;
                    yHighLightOffset = 1;
                    break;
                case Direction.SouthWest:
                    dOffset = interCardinalOffsets[3];
                    distance = currentTile.distances.DistanceSouthWest;
                    xHighLightOffset = -1;
                    yHighLightOffset = -1;
                    break;
                case Direction.SouthEast:
                    dOffset = interCardinalOffsets[2];
                    distance = currentTile.distances.DistanceSouthEast;
                    xHighLightOffset = 1;
                    yHighLightOffset = -1;
                    break;
            }

            for (int i = 1, offset = dOffset; i <= distance && i <= kingLimits; i++, offset += dOffset)
            {
                //if a square is occupied by a piece of the same color, stop the loop
                //by a different color, add the move and stop the loop(capturing the piece)
                if (Squares[startSquare + offset] != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + offset] & 24))
                    {
                        break;
                    }
                    else
                    {
                        AddLegalMove(startSquare, startSquare + offset, GridManager.chessTiles[xPos + (i * xHighLightOffset), yPos + (i * yHighLightOffset)]);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + offset, GridManager.chessTiles[xPos + (i * xHighLightOffset), yPos + (i * yHighLightOffset)]);
            }

        }

        private static void CalculateRookMoves(Tile currentTile, int startSquare, int decodedColor, int xPos, int yPos)
        {
            CalculateSlidingPiecesMoves(Piece.Rook, Direction.North, currentTile, startSquare, decodedColor, xPos, yPos);
            CalculateSlidingPiecesMoves(Piece.Rook, Direction.South, currentTile, startSquare, decodedColor, xPos, yPos);
            CalculateSlidingPiecesMoves(Piece.Rook, Direction.East, currentTile, startSquare, decodedColor, xPos, yPos);
            CalculateSlidingPiecesMoves(Piece.Rook, Direction.West, currentTile, startSquare, decodedColor, xPos, yPos);

        }

        private static void CalculateBishopMoves(Tile currentTile, int startSquare, int decodedColor, int xPos, int yPos)
        {
            CalculateSlidingPiecesMoves(Piece.Bishop, Direction.NorthWest, currentTile, startSquare, decodedColor, xPos, yPos);
            CalculateSlidingPiecesMoves(Piece.Bishop, Direction.NorthEast, currentTile, startSquare, decodedColor, xPos, yPos);
            CalculateSlidingPiecesMoves(Piece.Bishop, Direction.SouthWest, currentTile, startSquare, decodedColor, xPos, yPos);
            CalculateSlidingPiecesMoves(Piece.Bishop, Direction.SouthEast, currentTile, startSquare, decodedColor, xPos, yPos);
        }

        private static void CalculateKingMoves(Tile currentTile, int startSquare, int decodedColor, int xPos, int yPos)
        {
            foreach (Direction direction in Enum.GetValues(typeof(Direction))) {
                CalculateSlidingPiecesMoves(Piece.King, direction, currentTile, startSquare, decodedColor, xPos, yPos);
            }

        }

        public static void ClearListMoves()
        {
            legalMoves.Clear();
        }


        public static List<LegalMove> CalculateLegalMoves(Transform pieceObject)
        {
            int startSquare = (int)pieceObject.position.y * 8 + (int)pieceObject.position.x;
            int internalGamePiece = Squares[startSquare];

            int xTilePos = (int)pieceObject.position.x;
            int yTilePos = (int)pieceObject.position.y;


            // this holds the distance to the edge of the board for each tile so the algorithm knows when the edge of the board has been reached
            Tile currentTile = GridManager.chessTiles[xTilePos, yTilePos];


            int decodedPiece = internalGamePiece & 7;
            int decodedColor = internalGamePiece & 24;

            // if = 32, piece has moved, if 0, piece has not moved
            int decodedPieceStatus = internalGamePiece & 32;

            switch (decodedPiece)
            {
                case Piece.Pawn:
                    CalculatePawnMoves(startSquare, decodedColor, decodedPieceStatus, xTilePos, yTilePos);
                    break;
                case Piece.Knight:
                    CalculateKnightMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                case Piece.Rook:
                    CalculateRookMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                case Piece.Bishop:
                    CalculateBishopMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                case Piece.Queen:
                    // queen contains both movesets of a bishop and a rook
                    CalculateRookMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    CalculateBishopMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                case Piece.King:
                    CalculateKingMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                default:
                    return legalMoves;
            }

            return legalMoves;
        }
    }
}
