using System;
using JellyfinInvoice.Validation;
using Xunit;

namespace JellyfinInvoice.Tests;

/// <summary>
/// Unit tests for InputSanitizer.
/// Tests all validation and sanitization functions.
/// </summary>
public class InputSanitizerTests
{
    #region ValidateGuid Tests

    [Fact]
    public void ValidateGuid_ValidString_ReturnsGuid()
    {
        var input = "12345678-1234-1234-1234-123456789012";
        var result = InputSanitizer.ValidateGuid(input, "test");
        Assert.Equal(Guid.Parse(input), result);
    }

    [Fact]
    public void ValidateGuid_NullString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateGuid((string?)null, "test"));
    }

    [Fact]
    public void ValidateGuid_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateGuid("", "test"));
    }

    [Fact]
    public void ValidateGuid_InvalidFormat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateGuid("not-a-guid", "test"));
    }

    [Fact]
    public void ValidateGuid_EmptyGuid_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateGuid(Guid.Empty, "test"));
    }

    [Fact]
    public void ValidateGuid_ValidGuid_ReturnsGuid()
    {
        var input = Guid.NewGuid();
        var result = InputSanitizer.ValidateGuid(input, "test");
        Assert.Equal(input, result);
    }

    #endregion

    #region SanitizeString Tests

    [Fact]
    public void SanitizeString_NullInput_ReturnsEmpty()
    {
        var result = InputSanitizer.SanitizeString(null, 100);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SanitizeString_EmptyInput_ReturnsEmpty()
    {
        var result = InputSanitizer.SanitizeString("", 100);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SanitizeString_NormalInput_ReturnsUnchanged()
    {
        var input = "Hello World";
        var result = InputSanitizer.SanitizeString(input, 100);
        Assert.Equal(input, result);
    }

    [Fact]
    public void SanitizeString_ExceedsMaxLength_Truncates()
    {
        var input = "ThisIsAVeryLongString";
        var result = InputSanitizer.SanitizeString(input, 10);
        Assert.Equal(10, result.Length);
        Assert.Equal("ThisIsAVer", result);
    }

    [Fact]
    public void SanitizeString_ContainsControlChars_RemovesThem()
    {
        var input = "Hello\x00World\x01Test";
        var result = InputSanitizer.SanitizeString(input, 100);
        Assert.Equal("HelloWorldTest", result);
    }

    [Fact]
    public void SanitizeString_ContainsNewlines_RemovesWhenNotAllowed()
    {
        var input = "Hello\nWorld";
        var result = InputSanitizer.SanitizeString(input, 100, allowNewlines: false);
        Assert.Equal("HelloWorld", result);
    }

    [Fact]
    public void SanitizeString_ContainsNewlines_PreservesWhenAllowed()
    {
        var input = "Hello\nWorld";
        var result = InputSanitizer.SanitizeString(input, 100, allowNewlines: true);
        Assert.Equal("Hello\nWorld", result);
    }

    [Fact]
    public void SanitizeString_ContainsScriptTag_RemovesIt()
    {
        var input = "Hello<script>alert('xss')</script>World";
        var result = InputSanitizer.SanitizeString(input, 100);
        Assert.DoesNotContain("<script", result.ToLowerInvariant());
    }

    [Fact]
    public void SanitizeString_ContainsPathTraversal_RemovesIt()
    {
        var input = "file/../../../etc/passwd";
        var result = InputSanitizer.SanitizeString(input, 100);
        Assert.DoesNotContain("..", result);
    }

    [Fact]
    public void SanitizeString_ContainsNullByte_RemovesIt()
    {
        var input = "file.txt\x00.exe";
        var result = InputSanitizer.SanitizeString(input, 100);
        Assert.Equal("file.txt.exe", result);
        Assert.False(result.Contains('\x00'));
    }

    #endregion

    #region ValidateCurrencyCode Tests

    [Fact]
    public void ValidateCurrencyCode_ValidCode_ReturnsUppercase()
    {
        var result = InputSanitizer.ValidateCurrencyCode("usd");
        Assert.Equal("USD", result);
    }

    [Fact]
    public void ValidateCurrencyCode_NullInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateCurrencyCode(null));
    }

    [Fact]
    public void ValidateCurrencyCode_TooShort_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateCurrencyCode("US"));
    }

    [Fact]
    public void ValidateCurrencyCode_TooLong_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateCurrencyCode("USDD"));
    }

    [Fact]
    public void ValidateCurrencyCode_ContainsNumbers_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateCurrencyCode("US1"));
    }

    #endregion

    #region ValidateDecimalRange Tests

    [Fact]
    public void ValidateDecimalRange_WithinRange_ReturnsValue()
    {
        var result = InputSanitizer.ValidateDecimalRange(5.5m, 0m, 10m, "test");
        Assert.Equal(5.5m, result);
    }

    [Fact]
    public void ValidateDecimalRange_BelowMin_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputSanitizer.ValidateDecimalRange(-1m, 0m, 10m, "test"));
    }

    [Fact]
    public void ValidateDecimalRange_AboveMax_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputSanitizer.ValidateDecimalRange(11m, 0m, 10m, "test"));
    }

    [Fact]
    public void ValidateDecimalRange_AtMin_ReturnsValue()
    {
        var result = InputSanitizer.ValidateDecimalRange(0m, 0m, 10m, "test");
        Assert.Equal(0m, result);
    }

    [Fact]
    public void ValidateDecimalRange_AtMax_ReturnsValue()
    {
        var result = InputSanitizer.ValidateDecimalRange(10m, 0m, 10m, "test");
        Assert.Equal(10m, result);
    }

    #endregion

    #region ValidateIntRange Tests

    [Fact]
    public void ValidateIntRange_WithinRange_ReturnsValue()
    {
        var result = InputSanitizer.ValidateIntRange(5, 0, 10, "test");
        Assert.Equal(5, result);
    }

    [Fact]
    public void ValidateIntRange_BelowMin_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputSanitizer.ValidateIntRange(-1, 0, 10, "test"));
    }

    [Fact]
    public void ValidateIntRange_AboveMax_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputSanitizer.ValidateIntRange(11, 0, 10, "test"));
    }

    #endregion

    #region ValidateDateTime Tests

    [Fact]
    public void ValidateDateTime_ValidUtcDate_ReturnsDate()
    {
        var input = DateTime.UtcNow.AddDays(-1);
        var result = InputSanitizer.ValidateDateTime(input, "test");
        Assert.Equal(input, result);
    }

    [Fact]
    public void ValidateDateTime_DefaultValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateDateTime(default, "test"));
    }

    [Fact]
    public void ValidateDateTime_TooOld_ThrowsArgumentException()
    {
        var oldDate = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateDateTime(oldDate, "test"));
    }

    [Fact]
    public void ValidateDateTime_FutureDate_ThrowsArgumentException()
    {
        var futureDate = DateTime.UtcNow.AddDays(1);
        Assert.Throws<ArgumentException>(() =>
            InputSanitizer.ValidateDateTime(futureDate, "test"));
    }

    #endregion

    #region ValidateDurationTicks Tests

    [Fact]
    public void ValidateDurationTicks_ValidTicks_ReturnsValue()
    {
        var ticks = TimeSpan.FromHours(1).Ticks;
        var result = InputSanitizer.ValidateDurationTicks(ticks, "test");
        Assert.Equal(ticks, result);
    }

    [Fact]
    public void ValidateDurationTicks_NegativeTicks_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputSanitizer.ValidateDurationTicks(-1, "test"));
    }

    [Fact]
    public void ValidateDurationTicks_ExceedsMax_ThrowsArgumentOutOfRange()
    {
        var ticks = TimeSpan.FromHours(25).Ticks;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InputSanitizer.ValidateDurationTicks(ticks, "test"));
    }

    [Fact]
    public void ValidateDurationTicks_Zero_ReturnsZero()
    {
        var result = InputSanitizer.ValidateDurationTicks(0, "test");
        Assert.Equal(0, result);
    }

    #endregion
}
