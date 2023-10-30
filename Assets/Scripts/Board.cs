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

        private static void checkWhitePawnCaptures(int startSquare, int xPos, int yPos)
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

        private static void checkBlackPawnCaptures(int startSquare, int xPos, int yPos)
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

        private static void calculatePawnMoves(int startSquare, int decodedColor, int decodedPieceStatus, int xPos, int yPos)
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

                    checkWhitePawnCaptures(startSquare, xPos, yPos);

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

                    checkWhitePawnCaptures(startSquare, xPos, yPos);
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

                    checkBlackPawnCaptures(startSquare, xPos, yPos);

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

                    checkBlackPawnCaptures(startSquare, xPos, yPos);
                }
            }

        }


        private static void calculateKnightMovesHelper(Tile currentTile, int[] xOffsets, int[] yOffsets, int knightOffsetIndex, int startSquare, int decodedColor, int xPos, int yPos)
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
        private static void calculateKnightMoves(Tile currentTile, int startSquare, int decodedColor, int xPos, int yPos)
        {

            // {(𝑥±1,𝑦±2}∪{𝑥±2,y±1} represents available knight moves

            int[] xOffsets = { 1, -1 };
            int[] yOffsets = { 2, -2 };

            calculateKnightMovesHelper(currentTile, xOffsets, yOffsets, 0, startSquare, decodedColor, xPos, yPos);
            calculateKnightMovesHelper(currentTile, yOffsets, xOffsets, 1, startSquare, decodedColor, xPos, yPos);


        }

        private static void calculateRookMoves(Tile currentTile, int startSquare, int decodedColor, int xPos, int yPos)
        {

            // calculate legal moves north
            for (int i = 1, northOffset = cardinalOffsets[1]; i <= currentTile.distances.DistanceNorth; i++, northOffset += cardinalOffsets[1])
            {
                //if a square is occupied by a piece of the same color, stop the loop
                //by a different color, add the move and stop the loop(capturing the piece)
                if (Squares[startSquare + northOffset] != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + northOffset] & 24))
                    {
                        break;
                    }
                    else
                    {
                        AddLegalMove(startSquare, startSquare + northOffset, GridManager.chessTiles[xPos, yPos + i]);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + northOffset, GridManager.chessTiles[xPos, yPos + i]);
            }

            for (int i = 1, southOffset = cardinalOffsets[3]; i <= currentTile.distances.DistanceSouth; i++, southOffset += cardinalOffsets[3])
            {
                if (Squares[startSquare + southOffset] != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + southOffset] & 24))
                    {
                        break;
                    }
                    else
                    {
                        AddLegalMove(startSquare, startSquare + southOffset, GridManager.chessTiles[xPos, yPos - i]);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + southOffset, GridManager.chessTiles[xPos, yPos - i]);
            }

            for (int i = 1, eastOffset = cardinalOffsets[2]; i <= currentTile.distances.DistanceEast; i++, eastOffset += cardinalOffsets[2])
            {
                if (Squares[startSquare + eastOffset] != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + eastOffset] & 24))
                    {
                        break;
                    }
                    else
                    {
                        AddLegalMove(startSquare, startSquare + eastOffset, GridManager.chessTiles[xPos + i, yPos]);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + eastOffset, GridManager.chessTiles[xPos + i, yPos]);
            }

            for (int i = 1, westOffset = cardinalOffsets[0]; i <= currentTile.distances.DistanceWest; i++, westOffset += cardinalOffsets[0])
            {
                if (Squares[startSquare + westOffset] != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + westOffset] & 24))
                    {
                        break;
                    }
                    else
                    {

                        AddLegalMove(startSquare, startSquare + westOffset, GridManager.chessTiles[xPos - i, yPos]);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + westOffset, GridManager.chessTiles[xPos - i, yPos]);
            }

        }

        private static void calculateBishopMoves(Tile currentTile, int startSquare, int decodedColor, int xPos, int yPos)
        {
            // calculate legal moves northwest
            for (int i = 1, northWestOffset = interCardinalOffsets[0]; i <= currentTile.distances.DistanceNorthWest; i++, northWestOffset += interCardinalOffsets[0])
            {
                if (Squares[startSquare + northWestOffset] != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + northWestOffset] & 24))
                    {
                        break;
                    }
                    else
                    {
                        AddLegalMove(startSquare, startSquare + northWestOffset, GridManager.chessTiles[xPos - i, yPos + i]);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + northWestOffset, GridManager.chessTiles[xPos - i, yPos + i]);
            }

            for (int i = 1, northEastOffset = interCardinalOffsets[1]; i <= currentTile.distances.DistanceNorthEast; i++, northEastOffset += interCardinalOffsets[1])
            {
                if (Squares[startSquare + northEastOffset] != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + northEastOffset] & 24))
                    {
                        break;
                    }
                    else
                    {
                        AddLegalMove(startSquare, startSquare + northEastOffset, GridManager.chessTiles[xPos + i, yPos + i]);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + northEastOffset, GridManager.chessTiles[xPos + i, yPos + i]);
            }

            for (int i = 1, southWestOffset = interCardinalOffsets[3]; i <= currentTile.distances.DistanceSouthWest; i++, southWestOffset += interCardinalOffsets[3])
            {
                if (Squares[startSquare + southWestOffset] != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + southWestOffset] & 24))
                    {
                        break;
                    }
                    else
                    {
                        AddLegalMove(startSquare, startSquare + southWestOffset, GridManager.chessTiles[xPos - i, yPos - i]);
                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + southWestOffset, GridManager.chessTiles[xPos - i, yPos - i]);
            }

            for (int i = 1, southEastOffset = interCardinalOffsets[2]; i <= currentTile.distances.DistanceSouthEast; i++, southEastOffset += interCardinalOffsets[2])
            {
                if (Squares[startSquare + southEastOffset] != Piece.Empty)
                {
                    if (decodedColor == (Squares[startSquare + southEastOffset] & 24))
                    {
                        break;
                    }
                    else
                    {
                        AddLegalMove(startSquare, startSquare + southEastOffset, GridManager.chessTiles[xPos + i, yPos - i]);

                        break;
                    }
                }
                AddLegalMove(startSquare, startSquare + southEastOffset, GridManager.chessTiles[xPos + i, yPos - i]);
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
                    calculatePawnMoves(startSquare, decodedColor, decodedPieceStatus, xTilePos, yTilePos);
                    break;
                case Piece.Knight:
                    calculateKnightMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                case Piece.Rook:
                    calculateRookMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                case Piece.Bishop:
                    calculateBishopMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                case Piece.Queen:
                    // queen contains both movesets of a bishop and a rook
                    calculateRookMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    calculateBishopMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                case Piece.King:
                    break;
                default:
                    return legalMoves;
            }

            return legalMoves;
        }
    }
}
