using System;
using System.Diagnostics;

namespace Chess
{
    public class SearchInformation
    {
        public int DepthSearched;

        public int PositionsEvaluated;

        public int NumOfCheckMates;

        public int MoveNumber;

        public Evaluation.MoveEvaluation MoveEvaluationInformation;

        public SearchDiagnostics searchDiagnostics = new();
    }

    public class SearchDiagnostics
    {
        public Stopwatch stopwatch;

        public TimeSpan timeSpan;

        public string formattedTime;

        public void FormatElapsedTime()
        {
            this.formattedTime = "Search Time: " + String.Format("{0:00} minutes {1:00} seconds {2:00} milliseconds", this.timeSpan.Minutes, this.timeSpan.Seconds, this.timeSpan.Milliseconds);
        }

        public void LogElapsedTime()
        {
            Console.WriteLine(this.formattedTime);
        }
    }
}