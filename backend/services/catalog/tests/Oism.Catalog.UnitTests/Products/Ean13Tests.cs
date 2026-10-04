using Oism.Catalog.Domain;

namespace Oism.Catalog.UnitTests.Products;

public sealed class Ean13Tests
{
    // Mã thật trên bao bì, số kiểm tra tính theo chuẩn GS1.
    [Theory]
    [InlineData("4006381333931")]
    [InlineData("5901234123457")]
    [InlineData("8934567890120")]
    [InlineData("2000000000015")]
    [Trait("UseCase", "UC-PROD-03 AC-2")]
    public void IsValid_CorrectCheckDigit_True(string code) => Assert.True(Ean13.IsValid(code));

    [Theory]
    [InlineData("4006381333932")]
    [InlineData("400638133393")]
    [InlineData("40063813339311")]
    [InlineData("400638133393A")]
    [InlineData("")]
    [Trait("UseCase", "UC-PROD-03 AC-2")]
    public void IsValid_WrongCheckDigitLengthOrCharacters_False(string code) => Assert.False(Ean13.IsValid(code));

    // Điều kiện xong của W1-07: EAN-13 tự sinh đúng số kiểm tra.
    [Theory]
    [InlineData(1, "2000000000015")]
    [InlineData(2, "2000000000022")]
    [InlineData(123_456_789, "2001234567893")]
    [InlineData(Ean13.MaxSequence, "2009999999997")]
    [Trait("UseCase", "UC-PROD-03 AC-1")]
    public void Generate_Sequence_ThirteenDigitsWithInternalPrefixAndValidCheckDigit(long sequence, string expected)
    {
        var code = Ean13.Generate(sequence);

        Assert.Equal(expected, code);
        Assert.StartsWith(Ean13.InternalPrefix, code);
        Assert.True(Ean13.IsValid(code));
        Assert.Equal(sequence, Ean13.SequenceOf(code));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Ean13.MaxSequence + 1)]
    [Trait("UseCase", "UC-PROD-03 AC-1")]
    public void Generate_SequenceOutsideNineDigits_Throws(long sequence) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Ean13.Generate(sequence));
}
