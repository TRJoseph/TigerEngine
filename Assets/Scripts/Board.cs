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
        static readonly int[] interCardinalDirections = { 7, 9, -7, -9 };


        public static void UpdateInternalState(float originalXPosition, float originalYPosition, float newXPosition, float newYPosition)
        {

            // Debug.Log("originalXPosition: " + originalXPosition + " originalYPosition: " + originalYPosition);

            // Debug.Log("xValue: " + newXPosition + " yValue: " + newYPosition);

            // grab current piece and store it
            int currentPiece = Squares[(int)originalYPosition * 8 + (int)originalXPosition];

            // removing the piece from its old position
            Squares[(int)originalYPosition * 8 + (int)originalXPosition] = Piece.Empty;

            // placing the piece in its new position
            Squares[(int)newYPosition * 8 + (int)newXPosition] = currentPiece;

        }

        private static void calculateRookMoves(Tile currentTile, int startSquare, int decodedColor, int xPos, int yPos)
        {

            // calculate legal moves north (TODO still need to stop loop when enemy piece or friendly piece is seen)
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
                        legalMoves.Add(new LegalMove { startSquare = startSquare, endSquare = startSquare + northOffset, 
                            endTile = GridManager.chessTiles[xPos, yPos + i]
                    });
                        break;
                    }
                }
                legalMoves.Add(new LegalMove { startSquare = startSquare, endSquare = startSquare + northOffset,
                    endTile = GridManager.chessTiles[xPos, yPos + i]
                });
            }
            

        }

        private static void calculateBishopMoves()
        {

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

            switch (decodedPiece)
            {
                case Piece.Rook:
                    calculateRookMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    break;
                case Piece.Bishop:
                    calculateBishopMoves();
                    break;
                case Piece.Queen:
                    // queen contains both movesets of a bishop and a rook
                    calculateRookMoves(currentTile, startSquare, decodedColor, xTilePos, yTilePos);
                    calculateBishopMoves();
                    break;
                default:
                    return legalMoves;
            }

            return legalMoves;
        }
    }
}
