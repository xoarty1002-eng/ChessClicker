using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChessClicker
{
    public class EngineRun
    {
        private const string EngineReleaseUrl = "https://api.github.com/repos/official-stockfish/Stockfish/releases/latest";
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
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ChessClicker/1.0");
                using JsonDocument release = await JsonDocument.ParseAsync(await client.GetStreamAsync(EngineReleaseUrl));
                JsonElement assets = release.RootElement.GetProperty("assets");
                JsonElement? asset = assets.EnumerateArray()
                    .Where(item => item.GetProperty("name").GetString() is string name &&
                                   name.Contains("windows", StringComparison.OrdinalIgnoreCase) &&
                                   (name.Contains("x86-64", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains("x64", StringComparison.OrdinalIgnoreCase)) &&
                                   name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => item.GetProperty("name").GetString()!.Contains("avx2", StringComparison.OrdinalIgnoreCase))
                    .Cast<JsonElement?>()
                    .FirstOrDefault();

                if (asset == null)
                    throw new FileNotFoundException("No Windows x64 Stockfish archive was found in the latest release.");

                string downloadUrl = asset.Value.GetProperty("browser_download_url").GetString()
                    ?? throw new InvalidDataException("Stockfish release archive URL is missing.");
                byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(zipFilePath, fileBytes);
            }

            ZipFile.ExtractToDirectory(zipFilePath, targetFolder);
            File.Delete(zipFilePath);

            string installedExe = Directory.GetFiles(targetFolder, "stockfish*.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrEmpty(installedExe))
                throw new FileNotFoundException("Stockfish binary was not found in the downloaded archive.");

            return installedExe;
        }

        public string GetBestMove(string stockfishPath, string fen, int moveTimeMs = 1000, int skillLevel = 20)
        {
            if (moveTimeMs < 100 || moveTimeMs > 10000)
                throw new ArgumentOutOfRangeException(nameof(moveTimeMs), "Move time must be between 100 and 10000 milliseconds.");
            if (skillLevel < 0 || skillLevel > 20)
                throw new ArgumentOutOfRangeException(nameof(skillLevel), "Stockfish skill level must be between 0 and 20.");

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
                    inputWriter.WriteLine($"setoption name Skill Level value {skillLevel}");
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
