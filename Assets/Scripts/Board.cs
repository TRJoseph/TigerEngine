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


        public static void CalculateLegalMoves()
        {

        }
    }
}
