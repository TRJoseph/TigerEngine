using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using static Chess.Board;
using static Chess.MoveGen;
using System.IO;

namespace Chess
{
    public static class Verification
    {

        public static void RunBenchmark(string[] tokens)
        {
            if (Arbiter.positionLoaded == true)
            {
    
                Console.WriteLine("Executing Benchmark...");
                if (tokens.Length > 1)
                {
                    RunPerformanceTests(int.Parse(tokens[1]));
                } else
                {
                    RunPerformanceTests(5);
                }
                
            } else
            {
                Console.WriteLine("No position has been loaded, please run the ucinewgame or position command to load a custom position");
            }

        }

        /* This function will run perft over and over again to a specified depth, clocking in time and node values along the way */
        public static void RunPerformanceTests(int depth)
        {

            for (int i = 1; i <= depth; i++)
            {
                Stopwatch timer = Stopwatch.StartNew();
                int numNodes = Perft(i);
                timer.Stop();
                TimeSpan timespan = timer.Elapsed;
                Console.WriteLine("Depth " + i + " ply: " + numNodes + " nodes found in " + String.Format("{0:00} minutes {1:00} seconds {2:00} milliseconds", timespan.Minutes, timespan.Seconds, timespan.Milliseconds));
            }

            DebugPerft(depth);
        }

        /* Perft (performance test, move path enumeration). This function serves a debugging function designed to walk the move generation
        tree of legal moves to count all leaf nodes to a certain depth. These values can be compared to predetermined values in order to
        isolate issues with movegen. */
        public static int Perft(int depth)
        {
            if (depth == 0) return 1;

            Move[] moves = GenerateMoves();
            int numPositions = 0;
            for (int i = 0; i < moves.Length; i ++)
            {
                ExecuteMove(ref moves[i]);
                numPositions += Perft(depth - 1);
                UndoMove(ref moves[i]);
            }

            return numPositions;
        }

        public static void DebugPerft(int depth)
        {
            Move[] moves = GenerateMoves();
            int total = 0;

            for (int i = 0; i < moves.Length; i++)
            {
                ExecuteMove(ref moves[i]);
                int nodes = Perft(depth - 1);
                total += nodes;
                Console.WriteLine($"{ToUCI(ref moves[i])}: {nodes}");
                UndoMove(ref moves[i]);
            }

            Console.WriteLine($"Total nodes: {total}");
        }

        public static string ToUCI(ref Move move)
        {
            int fromIndex = System.Numerics.BitOperations.TrailingZeroCount(move.fromSquare);
            int toIndex = System.Numerics.BitOperations.TrailingZeroCount(move.toSquare);
            char fromFile = (char)('a' + (fromIndex % 8));
            int fromRank = fromIndex / 8 + 1;
            char toFile = (char)('a' + (toIndex % 8));
            int toRank = toIndex / 8 + 1;

            string uciMove = $"{fromFile}{fromRank}{toFile}{toRank}";

            // Append promotion character if needed
            switch (move.promotionFlag)
            {
                case PromotionFlags.PromoteToQueenFlag:
                    uciMove += "q";
                    break;
                case PromotionFlags.PromoteToRookFlag:
                    uciMove += "r";
                    break;
                case PromotionFlags.PromoteToBishopFlag:
                    uciMove += "b";
                    break;
                case PromotionFlags.PromoteToKnightFlag:
                    uciMove += "n";
                    break;
            }

            return uciMove;
        }

    }
}