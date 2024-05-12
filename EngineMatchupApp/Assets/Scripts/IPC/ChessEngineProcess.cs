using System;
using System.Diagnostics;
using System.IO;


namespace Chess
{
    public class ChessEngineProcess
    {
        private Process engineProcess;

        public delegate void MoveReadyHandler(string move);
        public event MoveReadyHandler OnMoveReady;

        private void HandleEngineOutput(string data)
        {
            UnityEngine.Debug.Log("Engine output: " + data);
            if (data.StartsWith("bestmove"))
            {
                var move = data.Split(' ')[1];
                OnMoveReady?.Invoke(move);
            }
        }

        public void StartEngine(string enginePath)
        {
            if(IsRunning)
            {
                StopEngine();
            }

            engineProcess = new Process();
            engineProcess.StartInfo.FileName = enginePath;
            engineProcess.StartInfo.UseShellExecute = false;
            engineProcess.StartInfo.RedirectStandardInput = true;
            engineProcess.StartInfo.RedirectStandardOutput = true;
            engineProcess.StartInfo.CreateNoWindow = true;
            engineProcess.Start();


            engineProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    HandleEngineOutput(args.Data);
                }
            };
            engineProcess.BeginOutputReadLine(); // Begin asynchronous read operation
        }

        public bool IsRunning
        {
            get { return engineProcess != null && !engineProcess.HasExited; }
        }

        public void SendCommand(string command)
        {
            if (engineProcess != null && !engineProcess.HasExited)
                engineProcess.StandardInput.WriteLine(command);
        }

        public void StopEngine()
        {
            SendCommand("quit"); // Assuming the engine accepts 'quit' command to exit
            engineProcess?.Close();
        }
    }

}