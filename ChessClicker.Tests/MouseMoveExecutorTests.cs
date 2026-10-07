using System.Drawing;
using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class MouseMoveExecutorTests
{
    private static readonly Rectangle BoardBounds = new(100, 200, 800, 800);

    [Theory]
    [InlineData("e2", true, 550, 850)]
    [InlineData("e4", true, 550, 650)]
    [InlineData("e2", false, 450, 350)]
    [InlineData("e4", false, 450, 550)]
    public void SquareCenterMapsBothPerspectives(string square, bool whiteView, int x, int y)
    {
        Assert.Equal(new Point(x, y), BoardMouseCoordinates.SquareCenter(square, BoardBounds, whiteView));
    }

    [Theory]
    [InlineData(true, "a1", 100, 900)]
    [InlineData(true, "h8", 800, 200)]
    [InlineData(false, "a1", 800, 200)]
    [InlineData(false, "h8", 100, 900)]
    public void ScreenPointToSquareUsesBoardPerspective(bool whiteView, string square, int x, int y)
    {
        Assert.Equal(square, BoardMouseCoordinates.ScreenPointToSquare(new Point(x, y), BoardBounds, whiteView));
    }

    [Theory]
    [InlineData(true, 0, 0, "a8")]
    [InlineData(true, 7, 0, "h8")]
    [InlineData(true, 0, 7, "a1")]
    [InlineData(true, 7, 7, "h1")]
    [InlineData(false, 0, 0, "h1")]
    [InlineData(false, 7, 0, "a1")]
    [InlineData(false, 0, 7, "h8")]
    [InlineData(false, 7, 7, "a8")]
    public void ConvertsScreenCellsToCorrectFilesAndRanks(
        bool whiteView,
        int screenFile,
        int screenRank,
        string square)
    {
        Assert.Equal(square,
            BoardMouseCoordinates.ScreenCellToSquare(screenFile, screenRank, whiteView));
        Assert.Equal((screenFile, screenRank),
            BoardMouseCoordinates.SquareToScreenCell(square, whiteView));
    }

    [Fact]
    public void ExecuteMoveClicksStartAndDestinationThenRestoresCursor()
    {
        var mouse = new FakeMouseInput { Position = new Point(17, 29) };
        mouse.Events.Clear();
        var delays = new List<TimeSpan>();
        var executor = new MouseMoveExecutor(mouse, delays.Add);

        executor.ExecuteMove("e2e4", BoardBounds, isWhiteView: true);

        Assert.Equal(
            new[]
            {
                "position:550,850",
                "down",
                "up",
                "position:550,650",
                "down",
                "up",
                "position:17,29"
            },
            mouse.Events);
        Assert.Equal(
            new[]
            {
                TimeSpan.FromMilliseconds(80),
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(150),
                TimeSpan.FromMilliseconds(80),
                TimeSpan.FromMilliseconds(50)
            },
            delays);
    }

    [Fact]
    public void ExecuteTypedBlackPerspectiveMoveClicksCorrectSquaresFromHyphenatedInput()
    {
        var mouse = new FakeMouseInput { Position = new Point(17, 29) };
        mouse.Events.Clear();
        var executor = new MouseMoveExecutor(mouse, _ => { });

        executor.ExecuteMove("a8-a6", BoardBounds, isWhiteView: false);

        Assert.Equal(
            new[]
            {
                "position:850,950",
                "down",
                "up",
                "position:850,750",
                "down",
                "up",
                "position:17,29"
            },
            mouse.Events);
    }

    [Fact]
    public void ExecuteMoveRestoresCursorWhenInputFails()
    {
        var mouse = new FakeMouseInput
        {
            Position = new Point(17, 29),
            ThrowOnButtonDownNumber = 2
        };
        mouse.Events.Clear();
        var executor = new MouseMoveExecutor(mouse, _ => { });

        Assert.Throws<InvalidOperationException>(() => executor.ExecuteMove("e2e4", BoardBounds, true));

        Assert.Equal(new Point(17, 29), mouse.Position);
    }

    [Fact]
    public void ExecuteMoveReleasesButtonAndRestoresCursorWhenDelayFails()
    {
        var mouse = new FakeMouseInput { Position = new Point(17, 29) };
        mouse.Events.Clear();
        int delayCount = 0;
        var executor = new MouseMoveExecutor(mouse, _ =>
        {
            if (++delayCount == 2)
                throw new InvalidOperationException("Simulated delay failure.");
        });

        Assert.Throws<InvalidOperationException>(() => executor.ExecuteMove("e2e4", BoardBounds, true));

        Assert.Contains("down", mouse.Events);
        Assert.Contains("up", mouse.Events);
        Assert.Equal(new Point(17, 29), mouse.Position);
    }

    [Theory]
    [InlineData("e2i4")]
    [InlineData("e2e9")]
    [InlineData("e2e8x")]
    [InlineData("e2e4garbage")]
    public void ExecuteMoveRejectsInvalidNotationBeforeMoving(string move)
    {
        var mouse = new FakeMouseInput { Position = new Point(17, 29) };
        mouse.Events.Clear();
        var executor = new MouseMoveExecutor(mouse, _ => { });

        Assert.Throws<ArgumentException>(() => executor.ExecuteMove(move, BoardBounds, true));

        Assert.Empty(mouse.Events);
    }

    [Fact]
    public void ScreenPointToSquareRejectsPointsOutsideTheBoard()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BoardMouseCoordinates.ScreenPointToSquare(new Point(900, 300), BoardBounds, true));
    }

    [Fact]
    public void NormalizesCalibratedBoundsToCenteredSquare()
    {
        Rectangle square = BoardBoundsGeometry.ToSquare(new Rectangle(10, 20, 820, 800));

        Assert.Equal(new Rectangle(20, 20, 800, 800), square);
        Assert.True(BoardBoundsGeometry.IsSquare(square));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EverySquareCenterMapsBackToTheSameSquare(bool whiteView)
    {
        Rectangle unevenBoardBounds = new(17, 31, 803, 797);

        for (int rank = 1; rank <= 8; rank++)
        {
            for (char file = 'a'; file <= 'h'; file++)
            {
                string square = $"{file}{rank}";
                Point center = BoardMouseCoordinates.SquareCenter(
                    square, unevenBoardBounds, whiteView);

                Assert.Equal(square, BoardMouseCoordinates.ScreenPointToSquare(
                    center, unevenBoardBounds, whiteView));
            }
        }
    }

    private sealed class FakeMouseInput : IMouseInput
    {
        private int _buttonDownCount;
        private Point _position;

        public Point Position
        {
            get => _position;
            set
            {
                _position = value;
                Events.Add($"position:{value.X},{value.Y}");
            }
        }
        public List<string> Events { get; } = new();
        public int ThrowOnButtonDownNumber { get; init; }

        public void LeftButtonDown()
        {
            _buttonDownCount++;
            if (_buttonDownCount == ThrowOnButtonDownNumber)
                throw new InvalidOperationException("Simulated mouse driver failure.");
            Events.Add("down");
        }

        public void LeftButtonUp() => Events.Add("up");
    }
}
