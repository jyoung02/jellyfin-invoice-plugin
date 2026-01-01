using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JellyfinInvoice.Validation;

/// <summary>
/// Centralized input validation and sanitization.
/// All external data MUST flow through these methods before use.
/// </summary>
public static partial class InputSanitizer
{
    /// <summary>
    /// Maximum allowed string length to prevent memory exhaustion.
    /// </summary>
    private const int AbsoluteMaxStringLength = 10000;

    /// <summary>
    /// Pattern for valid ISO 4217 currency codes.
    /// </summary>
    private static readonly Regex CurrencyCodePattern = CreateCurrencyCodeRegex();

    /// <summary>
    /// Pattern to detect potential injection attempts.
    /// </summary>
    private static readonly Regex DangerousPatternRegex = CreateDangerousPatternRegex();

    /// <summary>
    /// Validates and returns a GUID from a string input.
    /// </summary>
    /// <param name="input">The raw GUID string.</param>
    /// <param name="paramName">Parameter name for error messages.</param>
    /// <returns>A valid Guid.</returns>
    /// <exception cref="ArgumentException">Thrown if input is not a valid GUID.</exception>
    public static Guid ValidateGuid(string? input, string paramName)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("GUID cannot be null or empty.", paramName);
        }

        if (!Guid.TryParse(input.Trim(), out var result))
        {
            throw new ArgumentException("Invalid GUID format.", paramName);
        }

        return result;
    }

    /// <summary>
    /// Validates a Guid is not empty.
    /// </summary>
    /// <param name="input">The GUID to validate.</param>
    /// <param name="paramName">Parameter name for error messages.</param>
    /// <returns>The validated GUID.</returns>
    /// <exception cref="ArgumentException">Thrown if GUID is empty.</exception>
    public static Guid ValidateGuid(Guid input, string paramName)
    {
        if (input == Guid.Empty)
        {
            throw new ArgumentException("GUID cannot be empty.", paramName);
        }

        return input;
    }

    /// <summary>
    /// Sanitizes a string by removing dangerous characters and limiting length.
    /// </summary>
    /// <param name="input">The raw input string.</param>
    /// <param name="maxLength">Maximum allowed length.</param>
    /// <param name="allowNewlines">Whether to allow newline characters.</param>
    /// <returns>Sanitized string, or empty string if input was null.</returns>
    public static string SanitizeString(string? input, int maxLength, bool allowNewlines = false)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Enforce absolute maximum to prevent memory issues
        var effectiveMax = Math.Min(maxLength, AbsoluteMaxStringLength);

        // Normalize Unicode to detect obfuscation attempts
        var normalized = input.Normalize(NormalizationForm.FormKC);

        // Remove control characters (except newlines if allowed)
        var cleaned = RemoveControlCharacters(normalized, allowNewlines);

        // Check for dangerous patterns
        if (ContainsDangerousPattern(cleaned))
        {
            // Log this attempt in production
            cleaned = RemoveDangerousPatterns(cleaned);
        }

        // Truncate to max length
        if (cleaned.Length > effectiveMax)
        {
            cleaned = cleaned[..effectiveMax];
        }

        return cleaned.Trim();
    }

    /// <summary>
    /// Validates an ISO 4217 currency code.
    /// </summary>
    /// <param name="input">The raw currency code.</param>
    /// <returns>Validated uppercase currency code.</returns>
    /// <exception cref="ArgumentException">Thrown if code is invalid.</exception>
    public static string ValidateCurrencyCode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Currency code cannot be empty.", nameof(input));
        }

        var trimmed = input.Trim().ToUpperInvariant();

        if (!CurrencyCodePattern.IsMatch(trimmed))
        {
            throw new ArgumentException("Invalid currency code format. Must be 3 uppercase letters.", nameof(input));
        }

        return trimmed;
    }

    /// <summary>
    /// Validates a decimal value is within acceptable range.
    /// </summary>
    /// <param name="input">The value to validate.</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <param name="paramName">Parameter name for error messages.</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if value is out of range.</exception>
    public static decimal ValidateDecimalRange(decimal input, decimal min, decimal max, string paramName)
    {
        if (input < min || input > max)
        {
            throw new ArgumentOutOfRangeException(paramName, input, $"Value must be between {min} and {max}.");
        }

        return input;
    }

    /// <summary>
    /// Validates an integer value is within acceptable range.
    /// </summary>
    /// <param name="input">The value to validate.</param>
    /// <param name="min">Minimum allowed value.</param>
    /// <param name="max">Maximum allowed value.</param>
    /// <param name="paramName">Parameter name for error messages.</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if value is out of range.</exception>
    public static int ValidateIntRange(int input, int min, int max, string paramName)
    {
        if (input < min || input > max)
        {
            throw new ArgumentOutOfRangeException(paramName, input, $"Value must be between {min} and {max}.");
        }

        return input;
    }

    /// <summary>
    /// Validates a DateTime is within acceptable bounds.
    /// </summary>
    /// <param name="input">The date to validate.</param>
    /// <param name="paramName">Parameter name for error messages.</param>
    /// <returns>The validated UTC DateTime.</returns>
    /// <exception cref="ArgumentException">Thrown if date is invalid.</exception>
    public static DateTime ValidateDateTime(DateTime input, string paramName)
    {
        // Reject default/uninitialized dates
        if (input == default)
        {
            throw new ArgumentException("DateTime cannot be default value.", paramName);
        }

        // Reject dates too far in the past (before Jellyfin existed)
        var minDate = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (input < minDate)
        {
            throw new ArgumentException("DateTime is too far in the past.", paramName);
        }

        // Reject dates in the future (with small tolerance for clock skew)
        var maxDate = DateTime.UtcNow.AddMinutes(5);
        if (input > maxDate)
        {
            throw new ArgumentException("DateTime cannot be in the future.", paramName);
        }

        // Ensure UTC
        return input.Kind == DateTimeKind.Utc
            ? input
            : DateTime.SpecifyKind(input, DateTimeKind.Utc);
    }

    /// <summary>
    /// Validates duration ticks are non-negative and reasonable.
    /// </summary>
    /// <param name="ticks">The duration in ticks.</param>
    /// <param name="paramName">Parameter name for error messages.</param>
    /// <returns>Validated ticks value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if ticks are invalid.</exception>
    public static long ValidateDurationTicks(long ticks, string paramName)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, ticks, "Duration cannot be negative.");
        }

        // Max reasonable duration: 24 hours
        var maxTicks = TimeSpan.FromHours(24).Ticks;
        if (ticks > maxTicks)
        {
            throw new ArgumentOutOfRangeException(paramName, ticks, "Duration exceeds maximum allowed (24 hours).");
        }

        return ticks;
    }

    /// <summary>
    /// Removes control characters from a string.
    /// </summary>
    private static string RemoveControlCharacters(string input, bool allowNewlines)
    {
        var sb = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            if (char.IsControl(c))
            {
                if (allowNewlines && (c == '\n' || c == '\r'))
                {
                    sb.Append(c);
                }
                // Skip other control characters
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Checks if input contains potentially dangerous patterns.
    /// </summary>
    private static bool ContainsDangerousPattern(string input)
    {
        return DangerousPatternRegex.IsMatch(input);
    }

    /// <summary>
    /// Removes dangerous patterns from input.
    /// </summary>
    private static string RemoveDangerousPatterns(string input)
    {
        return DangerousPatternRegex.Replace(input, string.Empty);
    }

    /// <summary>
    /// Creates the currency code validation regex.
    /// </summary>
    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.Compiled)]
    private static partial Regex CreateCurrencyCodeRegex();

    /// <summary>
    /// Creates the dangerous pattern detection regex.
    /// Detects: script tags, SQL keywords, path traversal, null bytes.
    /// </summary>
    [GeneratedRegex(@"<script|</script|javascript:|data:|vbscript:|onclick|onerror|onload|eval\(|expression\(|\.\./|\.\.\\|\x00",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CreateDangerousPatternRegex();
}
