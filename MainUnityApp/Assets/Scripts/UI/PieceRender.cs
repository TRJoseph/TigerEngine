using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Chess
{
    public class PieceRender : MonoBehaviour
    {
        public bool isWhitePiece;
        public Tile occupiedTile;

        [Header("White Piece Sprites")]
        public Sprite whitePawn;
        public Sprite whiteKnight;
        public Sprite whiteBishop;
        public Sprite whiteRook;
        public Sprite whiteQueen;
        public Sprite whiteKing;

        [Header("Black Piece Sprites")]
        public Sprite blackPawn;
        public Sprite blackKnight;
        public Sprite blackBishop;
        public Sprite blackRook;
        public Sprite blackQueen;
        public Sprite blackKing;

    }
}

