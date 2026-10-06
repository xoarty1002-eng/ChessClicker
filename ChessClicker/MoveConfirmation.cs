using System;
using System.Linq;

namespace ChessClicker
{
    public sealed class MoveConfirmation
    {
        public string? PendingMove { get; private set; }

        public void Expect(string move)
        {
            if (move == null || (move.Length != 4 && move.Length != 5))
                throw new ArgumentException("Expected moves must use UCI notation.", nameof(move));

            PendingMove = move;
        }

        public bool TryConfirm(string? detectedMoveCandidates, out string? confirmedMove)
        {
            confirmedMove = null;
            if (PendingMove == null || string.IsNullOrWhiteSpace(detectedMoveCandidates))
                return false;

            string expectedMove = PendingMove[..4];
            bool matched = detectedMoveCandidates
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Any(candidate => string.Equals(candidate, expectedMove, StringComparison.OrdinalIgnoreCase));

            if (!matched)
                return false;

            confirmedMove = PendingMove;
            PendingMove = null;
            return true;
        }

        public void Cancel()
        {
            PendingMove = null;
        }
    }
}
