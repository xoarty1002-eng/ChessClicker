using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChessClicker
{
    public sealed record PieceBoardScanResult(
        string Fen,
        IReadOnlyList<string> UncertainSquares,
        bool UsesDefaultMetadata);

    public sealed class PieceAppearanceRecognizer
    {
        private const double MaximumSignatureDistance = 0.55;
        private const double MinimumClassificationGap = 0.025;
        private readonly IReadOnlyDictionary<char, double[][]> _templates;

        private PieceAppearanceRecognizer(IReadOnlyDictionary<char, double[][]> templates)
        {
            _templates = templates;
        }

        public static PieceAppearanceRecognizer TrainFromStandardPosition(
            Bitmap boardImage,
            bool isWhiteView,
            double minimumSignatureSeparationPercent)
        {
            ArgumentNullException.ThrowIfNull(boardImage);
            if (minimumSignatureSeparationPercent is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(minimumSignatureSeparationPercent));
            ChessBoard standardPosition = new(ChessBoard.StandardStartingFen);
            if (!PieceAppearanceImageSampler.TryExtractOccupiedSquares(
                    boardImage, standardPosition, isWhiteView, out IReadOnlySet<string> occupiedSquares))
                throw new InvalidOperationException(
                    "Could not separate pieces from empty squares. Adjust the crop or board theme and try again.");

            HashSet<string> expectedSquares = new(StringComparer.Ordinal);
            for (char file = 'a'; file <= 'h'; file++)
                for (char rank = '1'; rank <= '8'; rank++)
                {
                    string square = $"{file}{rank}";
                    if (standardPosition.GetPieceAt(square) != ' ')
                        expectedSquares.Add(square);
                }

            if (!expectedSquares.SetEquals(occupiedSquares))
                throw new InvalidOperationException(
                    "Piece training requires the standard starting position to be visible during calibration.");

            PieceAppearanceReport appearanceReport = PieceAppearanceAnalyzer.Analyze(
                PieceAppearanceImageSampler.ExtractSamples(
                    boardImage, ChessBoard.StandardStartingFen, isWhiteView),
                minimumSignatureSeparationPercent);
            if (!appearanceReport.IsSeparable)
                throw new InvalidOperationException(
                    $"The board image does not distinguish all piece types clearly enough. " +
                    $"Closest signatures: {appearanceReport.FirstClosestPiece} and " +
                    $"{appearanceReport.SecondClosestPiece} ({appearanceReport.MinimumSeparationPercent:0.0}%; " +
                    $"minimum {minimumSignatureSeparationPercent:0.0}%).");

            IReadOnlyDictionary<string, double[]> squareSignatures =
                PieceAppearanceImageSampler.ExtractSquareSignatures(boardImage, isWhiteView);
            Dictionary<char, List<double[]>> samples = new();
            foreach ((string square, double[] signature) in squareSignatures)
            {
                char piece = standardPosition.GetPieceAt(square);
                if (!samples.TryGetValue(piece, out List<double[]>? signatures))
                {
                    signatures = new List<double[]>();
                    samples.Add(piece, signatures);
                }
                signatures.Add(signature);
            }

            return new PieceAppearanceRecognizer(
                samples.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        }

        public PieceBoardScanResult Scan(Bitmap boardImage, bool isWhiteView)
        {
            ArgumentNullException.ThrowIfNull(boardImage);
            IReadOnlyDictionary<string, double[]> squareSignatures =
                PieceAppearanceImageSampler.ExtractSquareSignatures(boardImage, isWhiteView);
            char[,] pieces = new char[8, 8];
            List<string> uncertainSquares = new();

            foreach ((string square, double[] signature) in squareSignatures)
            {
                PieceSignatureClassification classification =
                    PieceSignatureClassifier.Classify(_templates, signature);
                int rank = 8 - (square[1] - '0');
                int file = square[0] - 'a';
                pieces[rank, file] = classification.Piece;

                if (classification.BestDistance > MaximumSignatureDistance ||
                    classification.SecondDistance - classification.BestDistance < MinimumClassificationGap)
                    uncertainSquares.Add(square);
            }

            string placement = PieceSignatureClassifier.CreatePiecePlacement(pieces);
            bool isStandardStartingPosition =
                placement == ChessBoard.StandardStartingFen.Split(' ')[0];
            string metadata = isStandardStartingPosition
                ? "w KQkq - 0 1"
                : "w - - 0 1";
            return new PieceBoardScanResult(
                $"{placement} {metadata}", uncertainSquares, !isStandardStartingPosition);
        }

        public void SaveToFile(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            PieceAppearanceModelFile model = new(
                1,
                _templates.Select(pair =>
                    new PieceAppearanceTemplateFile(pair.Key, pair.Value)).ToList());
            using FileStream stream = File.Create(fullPath);
            JsonSerializer.Serialize(stream, model, new JsonSerializerOptions { WriteIndented = true });
        }

        public static PieceAppearanceRecognizer LoadFromFile(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using FileStream stream = File.OpenRead(path);
            PieceAppearanceModelFile model =
                JsonSerializer.Deserialize<PieceAppearanceModelFile>(stream)
                ?? throw new InvalidDataException("The piece appearance file does not contain a model.");
            if (model.Version != 1 || model.Templates == null)
                throw new InvalidDataException("The piece appearance file has an unsupported format.");

            Dictionary<char, double[][]> templates = new();
            foreach (PieceAppearanceTemplateFile template in model.Templates)
            {
                if (" KQRBNPkqrbnp".IndexOf(template.Piece, StringComparison.Ordinal) < 0 ||
                    template.Signatures == null ||
                    template.Signatures.Length == 0 ||
                    template.Signatures.Any(signature =>
                        signature == null ||
                        signature.Length != 64 ||
                        signature.Any(value => !double.IsFinite(value) || value is < -1 or > 1)) ||
                    !templates.TryAdd(template.Piece, template.Signatures))
                    throw new InvalidDataException("The piece appearance file contains invalid templates.");
            }

            if (templates.Count != 13)
                throw new InvalidDataException("The piece appearance file is missing one or more piece classes.");

            return new PieceAppearanceRecognizer(templates);
        }

        private sealed record PieceAppearanceModelFile(
            int Version,
            List<PieceAppearanceTemplateFile>? Templates);

        private sealed record PieceAppearanceTemplateFile(char Piece, double[][]? Signatures);
    }
}
