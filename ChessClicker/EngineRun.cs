using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChessClicker
{
    public class EngineRun
    {
        private const string EngineReleaseUrl = "https://api.github.com/repos/official-stockfish/Stockfish/releases/latest";
        private const string EngineFolderName = "StockfishEngine";

        public async Task<string> EnsureEngineInstalledAsync(string configuredPath = "")
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                string fullPath = Path.GetFullPath(configuredPath);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("The configured UCI engine executable does not exist.", fullPath);
                return fullPath;
            }

            string targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EngineFolderName);
            if (Directory.Exists(targetFolder))
            {
                string? existingExe = Directory.GetFiles(targetFolder, "stockfish*.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (existingExe != null)
                    return existingExe;
            }

            Directory.CreateDirectory(targetFolder);
            string zipFilePath = Path.Combine(targetFolder, "stockfish.zip");
            using (HttpClient client = new())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ChessClicker/1.0");
                using JsonDocument release = await JsonDocument.ParseAsync(
                    await client.GetStreamAsync(EngineReleaseUrl));
                JsonElement assets = release.RootElement.GetProperty("assets");
                JsonElement? asset = assets.EnumerateArray()
                    .Where(item => item.GetProperty("name").GetString() is string name &&
                                   name.Contains("windows", StringComparison.OrdinalIgnoreCase) &&
                                   (name.Contains("x86-64", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains("x64", StringComparison.OrdinalIgnoreCase)) &&
                                   name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => item.GetProperty("name").GetString()!
                        .Contains("avx2", StringComparison.OrdinalIgnoreCase))
                    .Cast<JsonElement?>()
                    .FirstOrDefault();

                if (asset == null)
                    throw new FileNotFoundException("No Windows x64 Stockfish archive was found in the latest release.");

                string downloadUrl = asset.Value.GetProperty("browser_download_url").GetString()
                    ?? throw new InvalidDataException("Stockfish release archive URL is missing.");
                byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(zipFilePath, fileBytes);
            }

            try
            {
                ZipFile.ExtractToDirectory(zipFilePath, targetFolder);
            }
            finally
            {
                if (File.Exists(zipFilePath))
                    File.Delete(zipFilePath);
            }

            string? installedExe = Directory.GetFiles(targetFolder, "stockfish*.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            return installedExe ?? throw new FileNotFoundException(
                "Stockfish binary was not found in the downloaded archive.");
        }

        public string GetBestMove(string enginePath, string fen, int moveTimeMs = 1000, int skillLevel = 20) =>
            GetBestMoveAsync(enginePath, fen, moveTimeMs, skillLevel).GetAwaiter().GetResult();

        public async Task<string> GetBestMoveAsync(
            string enginePath,
            string fen,
            int moveTimeMs = 1000,
            int skillLevel = 20,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(enginePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(fen);
            if (moveTimeMs < 100 || moveTimeMs > 10000)
                throw new ArgumentOutOfRangeException(nameof(moveTimeMs), "Move time must be between 100 and 10000 milliseconds.");
            if (skillLevel < 0 || skillLevel > 20)
                throw new ArgumentOutOfRangeException(nameof(skillLevel), "Skill level must be between 0 and 20.");
            if (!File.Exists(enginePath))
                throw new FileNotFoundException("The UCI engine executable does not exist.", enginePath);

            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = enginePath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            StringBuilder errorOutput = new();
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data != null)
                    lock (errorOutput)
                        errorOutput.AppendLine(args.Data);
            };
            if (!process.Start())
                throw new InvalidOperationException("The UCI engine process could not be started.");
            process.BeginErrorReadLine();

            TimeSpan timeoutDuration = TimeSpan.FromMilliseconds(Math.Max(15000, moveTimeMs + 10000));
            using CancellationTokenSource timeout = new(timeoutDuration);
            using CancellationTokenSource linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeout.Token);
            try
            {
                StreamWriter input = process.StandardInput;
                StreamReader output = process.StandardOutput;
                await input.WriteLineAsync("uci");
                await input.FlushAsync(linkedToken.Token);
                bool supportsSkillLevel = false;
                while (true)
                {
                    string line = await ReadLineOrFailAsync(output, linkedToken.Token, "uciok");
                    if (line.StartsWith("option name Skill Level ", StringComparison.OrdinalIgnoreCase))
                        supportsSkillLevel = true;
                    if (line.Equals("uciok", StringComparison.Ordinal))
                        break;
                }

                if (supportsSkillLevel)
                    await input.WriteLineAsync($"setoption name Skill Level value {skillLevel}");
                await input.WriteLineAsync("ucinewgame");
                await input.WriteLineAsync("isready");
                await input.FlushAsync(linkedToken.Token);
                await ReadUntilAsync(output, linkedToken.Token, "readyok");

                await input.WriteLineAsync($"position fen {fen}");
                await input.WriteLineAsync($"go movetime {moveTimeMs}");
                await input.FlushAsync(linkedToken.Token);

                while (true)
                {
                    string line = await ReadLineOrFailAsync(output, linkedToken.Token, "a bestmove response");
                    if (!line.StartsWith("bestmove ", StringComparison.Ordinal))
                        continue;
                    string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length < 2)
                        throw new InvalidDataException("The UCI engine returned an incomplete bestmove response.");
                    return tokens[1];
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The UCI engine did not respond within {timeoutDuration}.");
            }
            catch (InvalidDataException exception)
            {
                string diagnostic;
                lock (errorOutput)
                    diagnostic = errorOutput.ToString().Trim();
                throw new InvalidDataException(
                    diagnostic.Length == 0
                        ? exception.Message
                        : $"{exception.Message} Engine diagnostic: {diagnostic}",
                    exception);
            }
            finally
            {
                if (!process.HasExited)
                {
                    try
                    {
                        await process.StandardInput.WriteLineAsync("quit");
                        await process.StandardInput.FlushAsync();
                        using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await process.WaitForExitAsync(shutdownTimeout.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (IOException)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
            }
        }

        private static async Task<string> ReadLineOrFailAsync(
            StreamReader reader,
            CancellationToken cancellationToken,
            string expected)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                throw new InvalidDataException($"The UCI engine exited before responding with {expected}.");
            return line;
        }

        private static async Task ReadUntilAsync(
            StreamReader reader,
            CancellationToken cancellationToken,
            string expected)
        {
            while (true)
            {
                string line = await ReadLineOrFailAsync(reader, cancellationToken, expected);
                if (line.Equals(expected, StringComparison.Ordinal))
                    return;
            }
        }
    }
}
