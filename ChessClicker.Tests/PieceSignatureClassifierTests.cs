using ChessClicker;
using Xunit;

namespace ChessClicker.Tests;

public sealed class PieceSignatureClassifierTests
{
    [Fact]
    public void ClassifiesAgainstLabeledPieceAppearanceTemplates()
    {
        double[] whitePawn = CreateSignature(0.5);
        double[] blackPawn = CreateSignature(-0.5);
        Dictionary<char, double[][]> templates = new()
        {
            ['P'] = [whitePawn],
            ['p'] = [blackPawn]
        };

        PieceSignatureClassification result =
            PieceSignatureClassifier.Classify(templates, whitePawn);

        Assert.Equal('P', result.Piece);
        Assert.Equal(0, result.BestDistance);
        Assert.True(result.SecondDistance > result.BestDistance);
    }

    [Fact]
    public void ClassifiesEmptySquareTemplates()
    {
        double[] emptySquare = CreateSignature(0);
        PieceSignatureClassification result = PieceSignatureClassifier.Classify(
            new Dictionary<char, double[][]> { [' '] = [emptySquare] },
            emptySquare);

        Assert.Equal(' ', result.Piece);
        Assert.Equal(0, result.BestDistance);
    }

    [Fact]
    public void EncodesDetectedPieceGridAsFenPlacement()
    {
        char[,] pieces = new char[8, 8];
        for (int rank = 0; rank < 8; rank++)
            for (int file = 0; file < 8; file++)
                pieces[rank, file] = ' ';
        pieces[0, 0] = 'r';
        pieces[0, 4] = 'k';
        pieces[1, 0] = 'p';
        pieces[6, 4] = 'P';
        pieces[7, 0] = 'R';
        pieces[7, 4] = 'K';

        Assert.Equal("r3k3/p7/8/8/8/8/4P3/R3K3",
            PieceSignatureClassifier.CreatePiecePlacement(pieces));
    }

    [Fact]
    public void RejectsMalformedPieceSignature()
    {
        Assert.Throws<ArgumentException>(() =>
            PieceSignatureClassifier.Classify(new Dictionary<char, double[][]>(), new double[63]));
    }

    private static double[] CreateSignature(double value)
    {
        double[] signature = new double[64];
        Array.Fill(signature, value);
        return signature;
    }
}
