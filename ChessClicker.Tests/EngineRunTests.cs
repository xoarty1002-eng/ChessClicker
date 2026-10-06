using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class EngineRunTests
{
    [Fact]
    public async Task UciWrapperWaitsForHandshakeAndUsesSupportedOptions()
    {
        if (OperatingSystem.IsWindows())
            return;

        string directory = Path.Combine(Path.GetTempPath(), $"chessclicker-engine-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string executablePath = Path.Combine(directory, "fake-engine");
        string transcriptPath = Path.Combine(directory, "commands.txt");
        File.WriteAllText(executablePath, CreateFakeEngineScript(transcriptPath, hasSkillOption: true));
        File.SetUnixFileMode(executablePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            var engine = new EngineRun();
            string configuredPath = await engine.EnsureEngineInstalledAsync(executablePath);
            string move = await engine.GetBestMoveAsync(
                configuredPath, ChessBoard.StandardStartingFen, moveTimeMs: 100, skillLevel: 7);
            string transcript = File.ReadAllText(transcriptPath);

            Assert.Equal("e2e4", move);
            Assert.Contains("uci", transcript);
            Assert.Contains("setoption name Skill Level value 7", transcript);
            Assert.Contains("ucinewgame", transcript);
            Assert.Contains("isready", transcript);
            Assert.Contains($"position fen {ChessBoard.StandardStartingFen}", transcript);
            Assert.Contains("go movetime 100", transcript);
            Assert.Contains("quit", transcript);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UciWrapperSupportsEnginesWithoutStockfishSkillOption()
    {
        if (OperatingSystem.IsWindows())
            return;

        string directory = Path.Combine(Path.GetTempPath(), $"chessclicker-engine-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string executablePath = Path.Combine(directory, "fake-engine");
        string transcriptPath = Path.Combine(directory, "commands.txt");
        File.WriteAllText(executablePath, CreateFakeEngineScript(transcriptPath, hasSkillOption: false));
        File.SetUnixFileMode(executablePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            string move = await new EngineRun().GetBestMoveAsync(
                executablePath, ChessBoard.StandardStartingFen, moveTimeMs: 100, skillLevel: 7);
            string transcript = File.ReadAllText(transcriptPath);

            Assert.Equal("e2e4", move);
            Assert.DoesNotContain("setoption name Skill Level", transcript);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConfiguredEnginePathMustExist()
    {
        string nonexistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            new EngineRun().EnsureEngineInstalledAsync(nonexistentPath));
    }

    [Fact]
    public async Task OptInInstalledEngineReturnsALegalStartingPositionMove()
    {
        string? enginePath = Environment.GetEnvironmentVariable("CHESSCLICKER_TEST_UCI_ENGINE");
        if (string.IsNullOrWhiteSpace(enginePath))
            return;

        string move = await new EngineRun().GetBestMoveAsync(
            enginePath, ChessBoard.StandardStartingFen, moveTimeMs: 100, skillLevel: 10);
        move = ChessMoveNotation.Normalize(move);
        var board = new ChessBoard(ChessBoard.StandardStartingFen);

        Assert.True(board.ValidateMove(
            8 - (move[1] - '0'), move[0] - 'a',
            8 - (move[3] - '0'), move[2] - 'a'),
            $"Installed engine returned an illegal move: {move}");
    }

    private static string CreateFakeEngineScript(string transcriptPath, bool hasSkillOption)
    {
        string quotedPath = "'" + transcriptPath.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
        string skillOption = hasSkillOption
            ? "    echo 'option name Skill Level type spin default 20 min 0 max 20'\n"
            : string.Empty;
        return "#!/bin/sh\n" +
               "while IFS= read -r line; do\n" +
               $"  printf '%s\\n' \"$line\" >> {quotedPath}\n" +
               "  case \"$line\" in\n" +
               "    uci)\n" +
               "      echo 'id name Test UCI Engine'\n" +
               skillOption +
               "      echo uciok\n" +
               "      ;;\n" +
               "    isready) echo readyok ;;\n" +
               "    'go '*) echo 'bestmove e2e4' ;;\n" +
               "    quit) exit 0 ;;\n" +
               "  esac\n" +
               "done\n";
    }
}
