using Chess;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Threading;
using static Chess.Arbiter;
using static Chess.MoveGen;

namespace Chess
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Application app = new Application();
            app.Run(args);
        }
    }

    public class Application
    {
        public TigerEngine tigerEngine;
        public UCIProtocol uciProtocol;

        private void Initialize()
        {
            tigerEngine = new TigerEngine();
            uciProtocol = new UCIProtocol(tigerEngine);
        }

        public void Run(string[] args)
        {
            Initialize();

            Console.WriteLine("TigerEngine Running...");
            Console.WriteLine("Enter A Command To Continue.");
            uciProtocol.SendUCIResponse();


            string command;
            while ((command = Console.ReadLine()) != null)
            {
                uciProtocol.ProcessCommand(command);
            }

        }

    }

}