using MarelliIawEcuStudio.Core.Decoding;

namespace MarelliIawEcuStudio.Tests;

public class ExpressionTests
{
    [Theory]
    [InlineData("x", 7, 7)]
    [InlineData("x - 40", 128, 88)]
    [InlineData("(x - 128) * 8", 130, 16)]
    [InlineData("x * 0.0625", 224, 14)]
    [InlineData("-x + 1", 3, -2)]
    [InlineData("2 + 3 * 4", 0, 14)]
    [InlineData("(2 + 3) * 4", 0, 20)]
    [InlineData("10 / 4", 0, 2.5)]
    [InlineData("idiv(x * 100, 255)", 128, 50)]
    [InlineData("idiv(-7, 2)", 0, -3)]
    [InlineData("x < 171 ? 1 : 2", 170, 1)]
    [InlineData("x < 171 ? 1 : 2", 171, 2)]
    [InlineData("x <= 171 ? 1 : 2", 171, 1)]
    [InlineData("x == 0 || x == 65535 ? 0 : 15000000 / x", 0, 0)]
    [InlineData("x == 0 || x == 65535 ? 0 : 15000000 / x", 65535, 0)]
    [InlineData("x == 0 || x == 65535 ? 0 : 15000000 / x", 15000, 1000)]
    [InlineData("x > 1 && x < 3", 2, 1)]
    [InlineData("!(x != 5)", 5, 1)]
    [InlineData("min(x, 10) + max(x, 10) + abs(-1)", 4, 15)]
    [InlineData("1.5e+3", 0, 1500)]
    [InlineData("x ? 1 : x ? 2 : 3", 0, 3)]
    public void Evaluates(string text, double x, double expected) =>
        Assert.Equal(expected, Expression.Parse(text).Evaluate(x), 12);

    [Fact]
    public void MatchesCSharpDoubleArithmeticBitForBit()
    {
        // Same operator order as the IES2 source: Buffer[0x09] * 0.4234 - 2.9638
        var e = Expression.Parse("x * 0.4234 - 2.9638");
        for (var x = 0; x < 256; x++)
            Assert.Equal(x * 0.4234 - 2.9638, e.Evaluate(x));
    }

    [Theory]
    [InlineData("")]
    [InlineData("x +")]
    [InlineData("(x")]
    [InlineData("y")]
    [InlineData("x = 1")]
    [InlineData("idiv(x)")]
    [InlineData("x ? 1")]
    [InlineData("1..2")]
    [InlineData("x x")]
    public void RejectsInvalidSyntax(string text) =>
        Assert.Throws<FormatException>(() => Expression.Parse(text));
}
