using System;

namespace ChessClicker
{
    public sealed class BoardStabilityTracker
    {
        private readonly int _requiredStableFrames;
        private readonly int _brightnessTolerance;
        private readonly TimeSpan _requiredStableDuration;
        private int[,]? _referenceFrame;
        private int _stableFrameCount;
        private DateTimeOffset? _stableSince;

        public BoardStabilityTracker(
            int requiredStableFrames = 3,
            int brightnessTolerance = 4,
            TimeSpan? requiredStableDuration = null)
        {
            if (requiredStableFrames < 2)
                throw new ArgumentOutOfRangeException(nameof(requiredStableFrames));
            if (brightnessTolerance < 0)
                throw new ArgumentOutOfRangeException(nameof(brightnessTolerance));
            if (requiredStableDuration < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(requiredStableDuration));

            _requiredStableFrames = requiredStableFrames;
            _brightnessTolerance = brightnessTolerance;
            _requiredStableDuration = requiredStableDuration ?? TimeSpan.Zero;
        }

        public bool AddFrame(int[,] frame) => AddFrame(frame, DateTimeOffset.UtcNow);

        public bool AddFrame(int[,] frame, DateTimeOffset timestamp)
        {
            ArgumentNullException.ThrowIfNull(frame);
            if (frame.GetLength(0) != 8 || frame.GetLength(1) != 8)
                throw new ArgumentException("A board frame must contain exactly 8-by-8 brightness values.", nameof(frame));

            if (_referenceFrame == null || !FramesAreClose(frame, _referenceFrame))
            {
                _referenceFrame = CopyFrame(frame);
                _stableFrameCount = 1;
                _stableSince = timestamp;
                return false;
            }

            _stableFrameCount++;
            return _stableFrameCount >= _requiredStableFrames &&
                   timestamp - _stableSince >= _requiredStableDuration;
        }

        public void Reset()
        {
            _referenceFrame = null;
            _stableFrameCount = 0;
            _stableSince = null;
        }

        private bool FramesAreClose(int[,] first, int[,] second)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    if (Math.Abs(first[rank, file] - second[rank, file]) > _brightnessTolerance)
                        return false;
                }
            }

            return true;
        }

        private static int[,] CopyFrame(int[,] frame)
        {
            var copy = new int[8, 8];
            Array.Copy(frame, copy, frame.Length);
            return copy;
        }
    }
}
