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

        public Dictionary<ulong, TranspositionEntry> TranspositionEntries;

        public TranspositionTable()
        {
            TranspositionEntries = new Dictionary<ulong, TranspositionEntry>();
        }
    }

}