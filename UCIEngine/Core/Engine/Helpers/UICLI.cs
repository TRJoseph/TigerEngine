using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Chess.Board;

namespace Chess
{
    public static class UICLI
    {
        // Method to print the board
        public static void PrintBoard(ChessBoard board, bool whiteToMove)
        {
            string border = "  +---+---+---+---+---+---+---+---+";
            string[] files = { "a", "b", "c", "d", "e", "f", "g", "h" };

            Console.WriteLine("\n" + border);
            for (int rank = 7; rank >= 0; rank--)
            {
                Console.Write((rank + 1) + " |");
                for (int file = 0; file < 8; file++)
                {
                    int index = rank * 8 + file;
                    Console.Write(" " + GetPieceSymbol(board, index) + " |");
                }
                Console.WriteLine("\n" + border);
            }

            Console.WriteLine("    " + string.Join("   ", files));
            Console.WriteLine(whiteToMove ? "White to move" : "Black to move");
        }

        // Helper method to get the piece symbol at a specific position
        private static string GetPieceSymbol(ChessBoard board, int index)
        {
            ulong mask = 1UL << index;
            for (int color = 0; color <= 1; color++)
            {
                for (int pieceType = 0; pieceType <= 5; pieceType++)
                {
                    if ((board.Pieces[color, pieceType] & mask) != 0)
                    {
                        return GetPieceNotation(pieceType, color);
                    }
                }
            }
            return ".";
        }

        // Map piece type and color to a symbol
        private static string GetPieceNotation(int pieceType, int color)
        {
            string[] whiteSymbols = ["P", "B", "N", "R", "Q", "K"];
            string[] blackSymbols = ["p", "b", "n", "r", "q", "k"];
            return (color == ChessBoard.White) ? whiteSymbols[pieceType] : blackSymbols[pieceType];
        }
    }
}
