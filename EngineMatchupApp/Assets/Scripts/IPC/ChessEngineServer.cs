using System.IO.Pipes;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Chess
{
    public class ChessEngineServer
    {
        private static TcpListener listener;
        private static int port = 49152;
        public static Dictionary<string, TcpClient> clients = new Dictionary<string, TcpClient>();

        public static void StartServer()
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            UnityEngine.Debug.Log("Server started. Listening for connections...");

            AcceptClientsAsync();
        }

        private static async void AcceptClientsAsync()
        {
            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                string clientKey = AssignClientKey(client);
                clients[clientKey] = client;

                UnityEngine.Debug.Log($"Accepted new client: {clientKey}");

                MainThreadDispatcher.Enqueue(() =>
                {
                    UIController.Instance.UpdateEngineStatus(true, clientKey);
                });
                Task.Run(() => HandleClient(client, clientKey));
            }
        }

        private static string AssignClientKey(TcpClient client)
        {
            if (clients.Count == 0 || !clients.ContainsKey("Engine1"))
            {
                return "Engine1";
            } else
            {
                return "Engine2";
            }
        }

        private static void HandleClient(TcpClient client, string clientKey)
        {
            using (client)
            {
                using (var networkStream = client.GetStream())
                using (var reader = new StreamReader(networkStream))
                using (var writer = new StreamWriter(networkStream))
                {
                    while (true)
                    {
                        string command = reader.ReadLine();
                        if (string.IsNullOrEmpty(command))
                            break;

                        UnityEngine.Debug.Log($"{clientKey} sent: {command}");
                        string response = ProcessCommand(command, clientKey);
                        writer.WriteLine(response);
                        writer.Flush();
                    }
                }
            }

            MainThreadDispatcher.Enqueue(() =>
            {
                UIController.Instance.UpdateEngineStatus(false, clientKey);
            });
            UnityEngine.Debug.Log($"{clientKey} disconnected.");
            clients.Remove(clientKey);
        }

        public static void SendCommandToClient(string clientKey, string command)
        {
            if (clients.TryGetValue(clientKey, out TcpClient client))
            {
                if (client.Connected)
                {
                    NetworkStream networkStream = client.GetStream();
                    using (var writer = new StreamWriter(networkStream, Encoding.UTF8, 1024, leaveOpen: true))
                    {
                        writer.WriteLine(command);
                        writer.Flush();
                    }
                    UnityEngine.Debug.Log($"Sent command to {clientKey}: {command}");
                }
                else
                {
                    UnityEngine.Debug.Log($"Client {clientKey} is not connected.");
                }
            }
            else
            {
                UnityEngine.Debug.Log($"No client found with key {clientKey}.");
            }
        }


        private static string ProcessCommand(string command, string clientKey)
        {
            if (command.StartsWith("bestmove"))
            {
                var move = command.Split(' ')[1];
                Arbiter.HandleMoveReadyWithUI(move);
            }

            return $"From {clientKey}: Processed {command}";
        }
    }
}