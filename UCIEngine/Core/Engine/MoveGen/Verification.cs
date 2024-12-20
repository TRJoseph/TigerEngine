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
        }

        /* Perft (performance test, move path enumeration). This function serves a debugging function designed to walk the move generation
        tree of legal moves to count all leaf nodes to a certain depth. These values can be compared to predetermined values in order to
        isolate issues with movegen. */
        public static int Perft(int depth)
        {
            if (depth == 0) return 1;

            Span<Move> moves = GenerateMoves();
            int numPositions = 0;

            foreach (Move move in moves)
            {
                ExecuteMove(move);
                numPositions += Perft(depth - 1);
                UndoMove(move);
            }

            return numPositions;
        }
    }
}