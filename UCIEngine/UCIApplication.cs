using System.IO.Pipes;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Threading;
using static Chess.Arbiter;
using static Chess.MoveGen;

namespace Chess
{

    public class UCIEngine
    {

        // These facilitate interaction with the Engine Matchup Application when debugging/testing engine strength
        private static TcpClient client;
        private static StreamReader reader;
        private static StreamWriter writer;
        private static string serverIp = "127.0.0.1";
        private static int serverPort = 49152;
        //

        public static void Main(string[] args)
        {

            Console.WriteLine("TigerEngine Running...");
            Console.WriteLine("Enter A Command To Continue.");
            SendUCIResponse();


            string command;
            while ((command = Console.ReadLine()) != null)
            {
                if (command == "quit")
                {
                    break;  // Exit the loop and terminate the engine on "quit"
                }
                ProcessCommand(command);
            }

        }


        public static void ProcessCommand(string command)
        {
            string[] tokens = command.Split(' ');

            switch (tokens[0])
            {
                case "uci":
                    SendUCIResponse();
                    break;
                case "isready":
                    Console.WriteLine("readyok");
                    break;
                case "setoption":
                    SetOptions(tokens);
                    break;
                case "ucinewgame":
                    Arbiter.InitializeGame(Arbiter.StartFEN);
                    Console.WriteLine("readyok");
                    break;
                case "position":
                    // position [fen <fenstring> | startpos ]  moves <move1> .... <movei>
                    SetPosition(tokens);
                    break;
                case "go":
                    StartEngine();
                    break;
                case "stop":
                    ComputerPlayer1.Engine.searchCancelled = true;
                    ComputerPlayer1.Engine.searchedOneDepth = true;
                    break;
                case "quit":
                    Console.WriteLine("TigerEngine shutting down...");
                    Environment.Exit(0);
                    break;
                // extra commands for working with the matchmaking manager
                case "connect":
                    ConnectToServer();
                    break;
                case "disconnect":
                    Disconnect();
                    break;
                default:
                    Console.WriteLine("Unknown command");
                    break;
            }
        }

        public static void ConnectToServer()
        {

            client = new TcpClient(serverIp, serverPort);
            Console.WriteLine("Connected to server.");

            var networkStream = client.GetStream();
            reader = new StreamReader(networkStream);
            writer = new StreamWriter(networkStream) { AutoFlush = true };

            // Start listening to the server messages asynchronously
            Task.Run(() => ListenToServerAsync());
        }

        private static async Task ListenToServerAsync()
        {
            try
            {
                string serverMessage;
                while ((serverMessage = await reader.ReadLineAsync()) != null)
                {
                    Console.WriteLine($"Received from server: {serverMessage}");

                    if(serverMessage.StartsWith("position")) {
                        SetPosition(serverMessage.Split(" "));
                    }

                    if(serverMessage.StartsWith("go"))
                    {
                        StartEngine();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation was canceled gracefully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }
        public static void Disconnect()
        {
            reader?.Dispose();
            writer?.Dispose();
            client?.Close();
            Console.WriteLine("Disconnected from server.");
        }

        public static async Task SendCommandToServerAsync(string command)
        {
            if (client != null && client.Connected)
            {
                await writer.WriteLineAsync(command);
                Console.WriteLine($"Sent to server: {command}");
            }
            else
            {
                Console.WriteLine("Client is not connected.");
            }
        }


        private static void CommunicateWithServer()
        {
            using (client)
            using (var networkStream = client.GetStream())
            using (var reader = new StreamReader(networkStream))
            using (var writer = new StreamWriter(networkStream))
            {
                // Initial communication or keep-alive message
                SendCommand("uci", writer, reader);
            }
        }

        private static void SendCommand(string command, StreamWriter writer, StreamReader reader)
        {
            writer.WriteLine(command);
            writer.Flush();

            // Optionally wait for a response immediately after sending
            string response = reader.ReadLine();
            Console.WriteLine($"Received from server: {response}");
        }


        public static void StartEngine()
        {
            if(Arbiter.positionLoaded)
            {
                // starts thread to keep UI responsive
                new Thread(() =>
                {
                    Arbiter.ComputerPlayer1.Engine.StartSearch();

                string bestMove;

                if (Arbiter.ComputerPlayer1.Engine.bestMove.promotionFlag != PromotionFlags.None)
                {
                    bestMove = BoardHelper.GetStringFromSquareBitboard(Arbiter.ComputerPlayer1.Engine.bestMove.fromSquare) + BoardHelper.GetStringFromSquareBitboard(Arbiter.ComputerPlayer1.Engine.bestMove.toSquare) + ConvertPromotionFlagToChar(Arbiter.ComputerPlayer1.Engine.bestMove.promotionFlag);
                }
                else
                {
                    bestMove = BoardHelper.GetStringFromSquareBitboard(Arbiter.ComputerPlayer1.Engine.bestMove.fromSquare) + BoardHelper.GetStringFromSquareBitboard(Arbiter.ComputerPlayer1.Engine.bestMove.toSquare);
                }

                Console.WriteLine("bestmove " + bestMove);
                SendCommandToServerAsync("bestmove " + bestMove);
                })
                { IsBackground = true }.Start();
            }
            else
            {
                Console.WriteLine("Please set a position first by executing the commands: ucinewgame or position");
            }
        }

        public static void SetPosition(string[] tokens)
        {

            try
            {
                if (tokens[1] == "startpos")
                {
                    Arbiter.InitializeGame(Arbiter.StartFEN);

                    if (tokens.Length > 2 && tokens[2] == "moves")
                    {
                        // apply moves
                        int movesIndex = Array.IndexOf(tokens, "moves");
                        if (movesIndex != -1 && movesIndex + 1 < tokens.Length)
                        {
                            ApplyMoves(tokens.Skip(movesIndex + 1).ToArray());
                        }
                    }
                    Console.WriteLine("readyok");

                }
                else if (tokens[1] == "fen")
                {
                    string fen = string.Join(" ", tokens.Skip(2).TakeWhile(token => token != "moves"));
                    Arbiter.InitializeGame(fen);
                    int movesIndex = Array.IndexOf(tokens, "moves");
                    if (movesIndex != -1 && movesIndex + 1 < tokens.Length)
                    {
                        ApplyMoves(tokens.Skip(movesIndex + 1).ToArray());
                    }
                    Console.WriteLine("readyok");
                }
                else
                {
                    Console.WriteLine("Please follow this format for inputting a custom position: position [fen <fenstring> | startpos ]  moves <move1> .... <movei>");
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Please follow this format for inputting a custom position: position [fen <fenstring> | startpos ]  moves <move1> .... <movei>");
            }

        }

        public static void ApplyMoves(string[] moves)
        {
            foreach (string move in moves)
            {
                // Validate the move format: e.g., "e2e4" or "e7e8q" for promotion
                if (move.Length < 4)
                {
                    Console.WriteLine("Invalid move format, moves were not applied: " + move);
                    continue; // Skip this iteration
                }

                string fromSquare = move[..2];
                string toSquare = move[2..4]; // Use range expression for clarity
                char? promotionChar = move.Length > 4 ? move[4] : null; // Promotion character if present

                // Retrieve the bitboard positions from square names
                ulong fromBitboard = BoardHelper.GetSquareBitboard(fromSquare);
                ulong toBitboard = BoardHelper.GetSquareBitboard(toSquare);

                // Find a matching move, considering promotion if applicable
                Move selectedMove = FindMatchingMove(fromBitboard, toBitboard, promotionChar);

 
                // Execute the move
                Arbiter.DoTurn(selectedMove);
            }
        }

        private static Move FindMatchingMove(ulong fromBitboard, ulong toBitboard, char? promotionChar)
        {
            IEnumerable<Move> candidateMoves = legalMoves.Where(x =>
                x.fromSquare == fromBitboard && x.toSquare == toBitboard);

            if (promotionChar.HasValue)
            {
                // Convert the promotion character to a promotion flag
                PromotionFlags? flag = ConvertCharToPromotionFlag(promotionChar.Value);
                if (flag.HasValue)
                {
                    candidateMoves = candidateMoves.Where(x => x.promotionFlag == flag.Value);
                }
                else
                {
                    Console.WriteLine("Invalid promotion character: " + promotionChar.Value);
                }
            }

            return candidateMoves.SingleOrDefault(); // There should be exactly one or none
        }

        private static PromotionFlags? ConvertCharToPromotionFlag(char promotionChar)
        {
            return promotionChar switch
            {
                'q' => PromotionFlags.PromoteToQueenFlag,
                'r' => PromotionFlags.PromoteToRookFlag,
                'k' => PromotionFlags.PromoteToKnightFlag,
                'b' => PromotionFlags.PromoteToBishopFlag,
                _ => null // Return null if invalid promotion character
            };
        }

        private static char? ConvertPromotionFlagToChar(PromotionFlags? promotionFlag)
        {
            return promotionFlag switch
            {
                PromotionFlags.PromoteToQueenFlag => 'q',
                PromotionFlags.PromoteToRookFlag => 'r',
                PromotionFlags.PromoteToKnightFlag => 'k',
                PromotionFlags.PromoteToBishopFlag => 'b',
                _ => null // Return null if invalid promotion character
            };
        }


        public static void SendUCIResponse()
        {
            if(Arbiter.SearchSettings.SearchType == SearchType.IterativeDeepening)
            {
                Console.WriteLine("id name TigerEngine - Version 5 " + Arbiter.SearchSettings.SearchTime.TotalMilliseconds.ToString() + " ms Think Time");

            } else
            {
                Console.WriteLine("id name TigerEngine - Version 5 Fixed Depth Search");
            }

            Console.WriteLine("id author Thomas R. Joseph");
            Console.WriteLine("option name Depth: " + Arbiter.SearchSettings.Depth);
            Console.WriteLine("option name SearchType type: " + Arbiter.SearchSettings.SearchType);
            Console.WriteLine("option name SearchTime time: " + Arbiter.SearchSettings.SearchTime.TotalMilliseconds.ToString() + " ms");


            Console.WriteLine("uciok");
        }

        public static void SetOptions(string[] tokens)
        {
            // here we will need to set a variety of options
            // hash - represents the maximum allowable size of the transposition table for this version of the engine
            // TODO: implement the transposition table
            try
            {
                switch(tokens[1].ToLower())
                {
                    case "searchtype":
                        if (tokens[2].Contains("iterative"))
                        {
                            Arbiter.SearchSettings.SearchType = SearchType.IterativeDeepening;
                        }
                        else if (tokens[2].Contains("fixed"))
                        {
                            Arbiter.SearchSettings.SearchType = SearchType.FixedDepth;
                        } else
                        {
                            Console.WriteLine("Please input a search type: iterative or fixed depth");
                        }
                        break;
                    case "searchtime":
                        try
                        {
                            Arbiter.SearchSettings.SearchTime = TimeSpan.FromMilliseconds(double.Parse(tokens[2]));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Please input the search time in milliseconds!");
                        }
                        break;

                    default:
                        Console.WriteLine("Please follow this format for setting custom options: setoption name <id> [value <x>]");
                        break;
                }
            } catch
            {
                Console.WriteLine("Please follow this format for setting custom options: setoption name <id> [value <x>]");
            }

        }

    }

}