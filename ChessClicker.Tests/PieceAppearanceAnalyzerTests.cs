using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class PieceAppearanceAnalyzerTests
{
    private static readonly char[] PieceLabels = ['K', 'Q', 'R', 'B', 'N', 'P', 'k', 'q', 'r', 'b', 'n', 'p'];

    [Fact]
    public void PassesWhenAllTwelvePieceSignaturesArePresentAndDistinct()
    {
        PieceAppearanceSample[] samples = PieceLabels
            .Select((piece, index) => new PieceAppearanceSample(piece, CreateSignature(index)))
            .ToArray();

        PieceAppearanceReport report = PieceAppearanceAnalyzer.Analyze(samples, minimumSeparationPercent: 5);

        Assert.True(report.IsSeparable);
        Assert.Empty(report.MissingPieces);
        Assert.Equal(12, report.SampleCounts.Count);
        Assert.True(report.MinimumSeparationPercent >= 5);
    }

    [Fact]
    public void FailsWhenTwoPieceClassesHaveTheSameAppearanceSignature()
    {
        double[] sharedSignature = CreateSignature(0);
        PieceAppearanceSample[] samples = PieceLabels
            .Select((piece, index) => new PieceAppearanceSample(
                piece,
                index == 0 || index == 1 ? sharedSignature : CreateSignature(index + 2)))
            .ToArray();

        PieceAppearanceReport report = PieceAppearanceAnalyzer.Analyze(samples, minimumSeparationPercent: 5);

        Assert.False(report.IsSeparable);
        Assert.Equal(0, report.MinimumSeparationPercent);
        Assert.Contains(report.FirstClosestPiece, new[] { 'K', 'Q' });
        Assert.Contains(report.SecondClosestPiece, new[] { 'K', 'Q' });
    }

    [Fact]
    public void ReportsPieceClassesMissingFromCalibrationPosition()
    {
        PieceAppearanceReport report = PieceAppearanceAnalyzer.Analyze(
            [new PieceAppearanceSample('K', CreateSignature(0))],
            minimumSeparationPercent: 5);

        Assert.False(report.IsSeparable);
        Assert.Equal(11, report.MissingPieces.Count);
    }

    [Fact]
    public void RejectsUnnormalizedSignatureValues()
    {
        double[] invalidSignature = new double[64];
        invalidSignature[0] = 1.1;

        Assert.Throws<ArgumentException>(() => PieceAppearanceAnalyzer.Analyze(
            [new PieceAppearanceSample('K', invalidSignature)],
            minimumSeparationPercent: 5));
    }

    private static double[] CreateSignature(int pattern)
    {
        double[] signature = new double[64];
        for (int index = 0; index < signature.Length; index++)
            signature[index] = ((index + pattern * 7) % 12) < 6 ? -0.8 : 0.8;
        return signature;
    }
}
