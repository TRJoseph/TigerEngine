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
            public int startSquare;
            public int endSquare;
        }

        public static List<LegalMove> legalMoves;

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

        private static void calculateRookMoves(Tile.Distances currentTileDistances)
        {
            

        }

        private static void calculateBishopMoves()
        {

        }


        public static void CalculateLegalMoves(Transform pieceObject)
        {
            int internalGamePiece = Squares[(int)pieceObject.position.y * 8 + (int)pieceObject.position.x];

            // this holds the distance to the edge of the board for each tile so the algorithm knows when the edge of the board has been reached
            Tile.Distances currentTileDistances = GridManager.chessTiles[(int)pieceObject.position.y, (int)pieceObject.position.x].distances;

            int decodedPiece = internalGamePiece & 7;

            switch (decodedPiece)
            {
                case Piece.Rook:
                    calculateRookMoves(currentTileDistances);
                    break;
                case Piece.Bishop:
                    calculateBishopMoves();
                    break;
                case Piece.Queen:
                    // queen contains both movesets of a bishop and a rook
                    calculateRookMoves(currentTileDistances);
                    calculateBishopMoves();
                    break;
                default:
                    return;
            }


        }
    }
}
