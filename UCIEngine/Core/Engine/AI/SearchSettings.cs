﻿using Chess;
using System;

namespace Chess
{
    public enum SearchType
    {
        FixedDepth = 0,
        IterativeDeepening = 1
    }

    public class SearchSettings
    {
        // depth is for fixed depth search only
        public int Depth;

        public TimeSpan SearchTime;

        public SearchType SearchType;

        public SearchSettings(int depth, TimeSpan searchTime, SearchType searchType)
        {
            Depth = depth;
            SearchTime = searchTime;
            SearchType = searchType;
        }
    }
}
