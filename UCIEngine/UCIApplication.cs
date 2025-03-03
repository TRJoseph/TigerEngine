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
                    bool verbose = tokens.Length > 1 && (tokens[1].ToLower() == "verbose" || tokens[1].ToLower() == "-v");
                    StartEngine(verbose);
                    break;
                case "stop":
                    ComputerPlayer1.Engine.searchCancelled = true;
                    ComputerPlayer1.Engine.searchedOneDepth = true;
                    break;
                case "quit":
                    Console.WriteLine("TigerEngine shutting down...");
                    Environment.Exit(0);
                    break;
                // extra commands for working with the matchmaking manager, and other benchmarking
                case "connect":
                    ConnectToServer();
                    break;
                case "disconnect":
                    Disconnect();
                    break;
                case "runbenchmark":
                    Verification.RunBenchmark(tokens);
                    break;
                case "help":
                    PrintHelpSection();
                    break;
                default:
                    Console.WriteLine("Unrecognized Command, please refer to the help section below:");
                    PrintHelpSection();
                    break;
            }
        }

        public static void PrintHelpSection()
        {
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Command List:");
            Console.Write("\nuci - Prints author and current version information about the engine.\nUsage: uci\n\n" +
                "isready - Replies letting the user know if the engine is responsive.\nUsage: isready\n\n" +
                "setoption - User can tweak configuration parameters of the engine such as the search type, depth, etc\nUsage: setoption <option> <extra parameters>\n\n" +
                "ucinewgame - Initializes a fresh board state and begins a new game from the default chess starting position.\nUsage: ucinewgame\n\n" +
                "position - Lets the user initialize a custom position with a FEN string and can also optionally provide a move history from said position\nUsage: position [fen <fenstring> | startpos ]  moves <move1> .... <movei>\n\n" +
                "go - Starts engine analysis on the current position and replies with the best move\nUsage: go <-v | verbose>\n\n" +
                "runbenchmark - runs perft to a certain depth to determine engine performance\nUsage: runbenchmark <depth>\n\n" +
                "quit - Stops the TigerEngine executable\nUsage: quit\n\n" +
                "help - Displays this help section\nUsage: help\n\n"); 
            Console.WriteLine("--------------------------------");
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

                    if (serverMessage.StartsWith("position"))
                    {
                        SetPosition(serverMessage.Split(" "));
                    }

                    if (serverMessage.StartsWith("go"))
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


        public static void StartEngine(bool verbose = false)
        {
            if (Arbiter.positionLoaded)
            {
                // starts thread to keep UI responsive
                new Thread(() =>
                {
                    Arbiter.ComputerPlayer1.Engine.StartSearch();

                    Move bestMove = ComputerPlayer1.Engine.bestMove;

                    string bestMoveString = BoardHelper.GetStringFromSquareBitboard(Arbiter.ComputerPlayer1.Engine.bestMove.fromSquare) + BoardHelper.GetStringFromSquareBitboard(Arbiter.ComputerPlayer1.Engine.bestMove.toSquare);

                    if (Arbiter.ComputerPlayer1.Engine.bestMove.promotionFlag != PromotionFlags.None)
                    {
                        bestMoveString += ConvertPromotionFlagToChar(Arbiter.ComputerPlayer1.Engine.bestMove.promotionFlag);
                    }

                    // if the engine is operating in verbose mode, provide extra detail to the move
                    if (verbose)
                    {
                        string unalteredBestMoveString = bestMoveString;
                        bestMoveString += $"|movedPiece:{bestMove.movedPiece}";

                        if (bestMove.capturedPieceType != -1)
                        {
                            bestMoveString += $"|capturedPiece:{bestMove.capturedPieceType}";
                        }
                        
                        // append a special move flag if applicable
                        switch (bestMove.specialMove)
                        {
                            case SpecialMove.KingSideCastleMove:
                                bestMoveString += "|specialMove:KingSideCastle";
                                break;
                            case SpecialMove.QueenSideCastleMove:
                                bestMoveString += "|specialMove:QueenSideCastle";
                                break;
                            case SpecialMove.EnPassant:
                                bestMoveString += "|specialMove:EnPassant";
                                break;
                            case SpecialMove.None:
                            default:
                                // no special move; do nothing
                                break;
                        }

                        if(bestMove.IsPawnPromotion)
                        {
                            bestMoveString += "|promotion:";
                            switch (bestMove.promotionFlag)
                            {
                                case PromotionFlags.PromoteToQueenFlag:
                                    bestMoveString += "queen";
                                    break;
                                case PromotionFlags.PromoteToRookFlag:
                                    bestMoveString += "rook";
                                    break;
                                case PromotionFlags.PromoteToKnightFlag:
                                    bestMoveString += "knight";
                                    break;
                                case PromotionFlags.PromoteToBishopFlag:
                                    bestMoveString += "bishop";
                                    break;
                                case PromotionFlags.None:
                                default:
                                    break;
                            }
                        }
                        Console.WriteLine("bestmove " + bestMoveString);
                        SendCommandToServerAsync("bestmove " + unalteredBestMoveString);
                    } else
                    {
                        Console.WriteLine("bestmove " + bestMoveString);
                        SendCommandToServerAsync("bestmove " + bestMoveString);
                    }
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
                // validates the move format: e.g., "e2e4" or "e7e8q" for promotion
                if (move.Length < 4)
                {
                    Console.WriteLine("Invalid move format, moves were not applied: " + move);
                    continue; 
                }

                string fromSquare = move[..2];
                string toSquare = move[2..4]; // use range expression for clarity
                char? promotionChar = move.Length > 4 ? move[4] : null; // promotion character if present

                // retrieve the bitboard positions from square names
                ulong fromBitboard = BoardHelper.GetSquareBitboard(fromSquare);
                ulong toBitboard = BoardHelper.GetSquareBitboard(toSquare);

                // find a matching move, considering promotion if applicable
                Move selectedMove = FindMatchingMove(fromBitboard, toBitboard, promotionChar);

                // if the move is illegal, throw error
                if (selectedMove.IsDefault())
                {
                    Console.WriteLine("ERROR: AN INVALID OR ILLEGAL MOVE WAS PLAYED");
                    Console.WriteLine("Please try again.");
                    return;
                }

                // executes the move if a valid move was found
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
            Console.WriteLine("id name TigerEngine - Version 6 with second TT implementation");
            Console.WriteLine("id author Thomas R. Joseph");

            // TODO: Actually implement these options 
            // Console.WriteLine("option name Debug Log File type string default");
            // Console.WriteLine("option name Threads type spin default 1 min 3 max 1024");
            // Console.WriteLine("option name Clear Hash type button");
            // Console.WriteLine("option name Ponder type check default false");

            // These are 'go' options as stated by the UCI
            // Console.WriteLine("option name Depth " + Arbiter.SearchSettings.Depth);
            // Console.WriteLine("option name SearchType type " + Arbiter.SearchSettings.SearchType);
            // Console.WriteLine("option name SearchTime time " + Arbiter.SearchSettings.SearchTime.TotalMilliseconds.ToString() + " ms");


            Console.WriteLine("uciok");
        }

        public static void SetOptions(string[] tokens)
        {
            // here we will need to set a variety of options
            // hash - represents the maximum allowable size of the transposition table for this version of the engine
            // TODO: implement the transposition table
            try
            {
                switch (tokens[1].ToLower())
                {
                    case "searchtype":
                        if (tokens[2].Contains("iterative"))
                        {
                            Arbiter.SearchSettings.SearchType = SearchType.IterativeDeepening;
                        }
                        else if (tokens[2].Contains("fixed"))
                        {
                            Arbiter.SearchSettings.SearchType = SearchType.FixedDepth;
                        }
                        else
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
            }
            catch
            {
                Console.WriteLine("Please follow this format for setting custom options: setoption name <id> [value <x>]");
            }

        }

    }

}