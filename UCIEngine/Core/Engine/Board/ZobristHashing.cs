using System;
using System.Collections;
using System.Collections.Generic;
using static Chess.Board;
using static Chess.PositionInformation;


namespace Chess
{
    public class ZobristHashing
    {
        public Board board;
        public ZobristHashing(Board board)
        {
            this.board = board;
        }

        // Zobrist hashing is a way to represent a chess position as a single number
        // https://www.chessprogramming.org/Zobrist_Hashing
        // one number for each color (2), for each piece (6), at each square (64)
        public readonly ulong[,,] pieceAtEachSquareArray = new ulong[2, 6, 64];

        public ulong sideToMove;

        // no castling, kingside, queenside, both
        // TODO: this may need to be changed to an array of 4 long
        public readonly ulong[] castlingRightsArray = new ulong[16];

        // 1 is a file, 8 is h file, 0 is none
        public readonly ulong[] enPassantFile = new ulong[9];


        public void GenerateZobristHashes()
        {
            Random rnd = new Random(15066146);
            byte[] randomBytes = new byte[8];
            ulong longRand;

            // for each color
            for (int i = 0; i < 2; i++)
            {
                // for each piece
                for (int j = 0; j < 6; j++)
                {

                    // for each square
                    for (int k = 0; k < 64; k++)
                    {
                        rnd.NextBytes(randomBytes);
                        longRand = BitConverter.ToUInt64(randomBytes, 0);
                        pieceAtEachSquareArray[i, j, k] = longRand;
                    }
                }
            }

            // for castling rights
            for (int i = 0; i < 16; i++)
            {
                rnd.NextBytes(randomBytes);
                longRand = BitConverter.ToUInt64(randomBytes, 0);
                castlingRightsArray[i] = longRand;
            }


            // for en passant square
            for (int i = 0; i < 9; i++)
            {
                // index 0 represents no possible en passant capture
                rnd.NextBytes(randomBytes);
                longRand = BitConverter.ToUInt64(randomBytes, 0);
                enPassantFile[i] = longRand;
            }

            rnd.Next();
            rnd.NextBytes(randomBytes);
            longRand = BitConverter.ToUInt64(randomBytes, 0);
            sideToMove = longRand;
        }

        public ulong InitializeHashKey()
        {
            // this will initialize the hash key for the initial position
            ulong ZobristHash = 0;

            for (int pieceColor = ChessBoard.White; pieceColor <= ChessBoard.Black; pieceColor++)
            {
                for (int pieceType = ChessBoard.Pawn; pieceType <= ChessBoard.King; pieceType++)
                {
                    for (int square = 0; square < 64; square++)
                    {
                        if ((InternalBoard.Pieces[pieceColor, pieceType] & (1UL << square)) != 0)
                        {
                            ZobristHash ^= pieceAtEachSquareArray[pieceColor, pieceType, square];
                        }
                    }
                }
            }

            if (board.posInfo.whiteToMove == false)
            {
                ZobristHash ^= sideToMove;
            }

            ZobristHash ^= castlingRightsArray[board.posInfo.CurrentGameState.castlingRights];

            ZobristHash ^= enPassantFile[board.posInfo.CurrentGameState.enPassantFile];

            return ZobristHash;

        }

    }
}