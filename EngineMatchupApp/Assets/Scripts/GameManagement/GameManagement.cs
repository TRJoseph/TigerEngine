using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using static Chess.Board;
using static Chess.MoveGen;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Chess
{
    public static class GameManagement
    {
        public static Action OnGameCompleted;

        public static void TriggerGameCompletion()
        {
            OnGameCompleted?.Invoke();
        }

        // this FENStringList list will hold all 1000 chess matches' fen strings. this will be used in the test suite
        static readonly List<string> FENStringList = new List<string>();

        public static void LoadFENStringList()
        {
            string path = @"../EngineMatchupApp/Assets/Scripts/GameManagement/lichessGameResults.txt";
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // gets FENString from log
                    if (line.Contains("FENString"))
                    {
                        // the FEN String text is separated from the actual FEN String by a colon
                        int positionOfSeparator = line.IndexOf(':');
                        FENStringList.Add(line.Substring(positionOfSeparator + 2));
                    }
                }
            }
        }

        public static void ComputerVsComputerMatches(int numOfMatches)
        {
            // first load in the test game list from the test result log
            LoadFENStringList();

            string logFilePath = Path.Combine(Application.dataPath, "../Logs/ChessMatchResults.txt");
            File.AppendAllText(logFilePath, $"Starting {numOfMatches} matches:\n");

            int matchCount = 0;
            int totalWhiteWins = 0;
            int totalBlackWins = 0;
            int totalDraws = 0;

            OnGameCompleted = () =>
            {
                RecordResult(matchCount, logFilePath, ref totalWhiteWins, ref totalBlackWins, ref totalDraws);
                if (matchCount < numOfMatches - 1)
                {
                    matchCount++;
                    Arbiter.StartGame(FENStringList[matchCount], true);
                }
                else
                {

                    string summary = "Matches completed.\n" +
                                           "Total Matches: " + matchCount + "\n" +
                                           "Total White Wins: " + totalWhiteWins + "\n" +
                                           "Total Black Wins: " + totalBlackWins + "\n" +
                                           "Total Draws: " + totalDraws + "\n";

                    File.AppendAllText(logFilePath, summary);
                }
            };

            Arbiter.StartGame(FENStringList[matchCount], true);
        }


        private static void RecordResult(int matchCount, string logFilePath, ref int totalWhiteWins, ref int totalBlackWins, ref int totalDraws)
        {
            string resultLine = $"Match {matchCount}: ";
            switch (PositionInformation.currentStatus)
            {
                case GameResult.CheckMate:
                    if (!PositionInformation.whiteToMove)
                    {
                        resultLine += "White Wins by Checkmate!\n";
                        totalWhiteWins++;
                    }
                    else
                    {
                        resultLine += "Black Wins by Checkmate!\n";
                        totalBlackWins++;
                    }
                    break;
                case GameResult.Stalemate:
                    resultLine += "Draw by Stalemate!\n";
                    totalDraws++;
                    break;
                case GameResult.ThreeFold:
                    resultLine += "Draw by Threefold Repetition!\n";
                    totalDraws++;
                    break;
                case GameResult.FiftyMoveRule:
                    resultLine += "Draw by Fifty Move Rule!\n";
                    totalDraws++;
                    break;
                case GameResult.InsufficientMaterial:
                    resultLine += "Draw by Insufficient Material!\n";
                    totalDraws++;
                    break;
                default:
                    resultLine += "Game still in progress or unknown result.\n";
                    break;
            }

            File.AppendAllText(logFilePath, resultLine);
        }
    }
}

//    public static void ComputerVsComputerMatches(int numOfMatches)
//    {
//        string logFilePath = Path.Combine(Application.dataPath, "../Logs/ChessMatchResults.txt");

//        int totalWhiteWins = 0;
//        int totalBlackWins = 0;
//        int totalDraws = 0;

//        using StreamWriter writer = new(logFilePath, true);
//        writer.WriteLine($"Starting {numOfMatches} matches:");

//        for (int match = 1; match <= numOfMatches; match++)
//        {
//            Arbiter.StartGame("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", true);

//            string resultLine = $"Match {match}: ";
//            switch (PositionInformation.currentStatus)
//            {
//                case GameResult.CheckMate:
//                    if (!PositionInformation.whiteToMove)
//                    {
//                        resultLine += "White Wins by Checkmate!";
//                        totalWhiteWins++;
//                    }
//                    else
//                    {
//                        resultLine += "Black Wins by Checkmate!";
//                        totalBlackWins++;
//                    }
//                    break;
//                case GameResult.Stalemate:
//                    resultLine += "Draw by Stalemate!";
//                    totalDraws++;
//                    break;
//                case GameResult.ThreeFold:
//                    resultLine += "Draw by Threefold Repetition!";
//                    totalDraws++;
//                    break;
//                case GameResult.FiftyMoveRule:
//                    resultLine += "Draw by Fifty Move Rule!";
//                    totalDraws++;
//                    break;
//                case GameResult.InsufficientMaterial:
//                    resultLine += "Draw by Insufficient Material!";
//                    totalDraws++;
//                    break;
//                default:
//                    resultLine += "Game still in progress or unknown result.";
//                    break;
//            }

//            writer.WriteLine(resultLine);
//            //UnityEngine.Debug.Log(resultLine); // Also log to Unity Console
//        }

//        writer.WriteLine("Matches completed.");
//        writer.WriteLine("Total White Wins: " + totalWhiteWins);
//        writer.WriteLine("Total Black Wins: " + totalBlackWins);
//        writer.WriteLine("Total Draws: " + totalDraws);
//        writer.WriteLine("");
//    }

//}
