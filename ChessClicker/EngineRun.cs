using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ChessClicker
{
    public class EngineRun
    {
        private const string EngineDownloadUrl = "https://github.com";
        private const string EngineFolderName = "StockfishEngine";

        public async Task<string> EnsureEngineInstalledAsync()
        {
            string targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EngineFolderName);

            if (Directory.Exists(targetFolder))
            {
                string existingExe = Directory.GetFiles(targetFolder, "stockfish*.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(existingExe)) return existingExe;
            }

            Directory.CreateDirectory(targetFolder);
            string zipFilePath = Path.Combine(targetFolder, "stockfish.zip");

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                byte[] fileBytes = await client.GetByteArrayAsync(EngineDownloadUrl);
                await File.WriteAllBytesAsync(zipFilePath, fileBytes);
            }

            ZipFile.ExtractToDirectory(zipFilePath, targetFolder);
            File.Delete(zipFilePath);

            string installedExe = Directory.GetFiles(targetFolder, "stockfish*.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrEmpty(installedExe))
                throw new FileNotFoundException("Stockfish binary was not found in the downloaded archive.");

            return installedExe;
        }

        public string GetBestMove(string stockfishPath, string fen, int moveTimeMs = 1000)
        {
            string bestMove = "None";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = stockfishPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (Process engineProcess = new Process { StartInfo = startInfo })
            {
                if (!engineProcess.Start()) return "Error starting engine";

                using (StreamWriter inputWriter = engineProcess.StandardInput)
                {
                    inputWriter.WriteLine("uci");
                    inputWriter.WriteLine("ucinewgame");
                    inputWriter.WriteLine($"position fen {fen}");
                    inputWriter.WriteLine($"go movetime {moveTimeMs}");
                    inputWriter.Flush();

                    string line;
                    while ((line = engineProcess.StandardOutput.ReadLine()) != null)
                    {
                        if (line.StartsWith("bestmove"))
                        {
                            string[] tokens = line.Split(' ');
                            if (tokens.Length > 1) bestMove = tokens[1];
                            break;
                        }
                    }
                }
                if (!engineProcess.HasExited) engineProcess.Kill();
            }
            return bestMove;
        }
    }
}
