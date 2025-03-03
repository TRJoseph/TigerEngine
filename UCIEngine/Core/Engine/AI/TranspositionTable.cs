using System;
using System.Collections.Generic;
using static Chess.Board;
using static Chess.MoveGen;

namespace Chess
{
    public class TranspositionTable
    {
        public enum NodeType
        {
            PVNode = 0, // Score is Exact
            AllNode = 1, // Score is Upper Bound
            CutNode = 2, // Score is Lower Bound
        }

        private const int TableSize = 1 << 20; // 2^20 = 1,048,576 entries
        public TranspositionEntry[] TTable = new TranspositionEntry[TableSize];

        public struct TranspositionEntry
        {
            // https://www.chessprogramming.org/Transposition_Table#What_Information_is_Stored
            public readonly ulong ZobristHash;
            public readonly Move BestMove;
            public readonly int DepthOfSearch;
            public readonly int EvaluationScore;
            public readonly NodeType TypeOfNode;

            public TranspositionEntry(ulong zobristHash, Move bestMove, int depthOfSearch, int evaluationScore, NodeType typeOfNode)
            {
                ZobristHash = zobristHash;
                BestMove = bestMove;
                DepthOfSearch = depthOfSearch;
                EvaluationScore = evaluationScore;
                TypeOfNode = typeOfNode;
            }
        }

        public void Store(ulong zobristHash, Move bestMove, int depthOfSearch, int evaluationScore, NodeType typeOfNode)
        {
            TTable[(int)(zobristHash % TableSize)] = new TranspositionEntry(zobristHash, bestMove, depthOfSearch, evaluationScore, typeOfNode);
        }

        public bool TryGetValue(ulong zobristKey, out TranspositionEntry entry)
        {
            int index = (int)(zobristKey % TableSize);
            entry = TTable[index];
            return entry.ZobristHash == zobristKey;
        }

        public void Reset()
        {
            Array.Clear(TTable, 0, TTable.Length);
        }
    }
}