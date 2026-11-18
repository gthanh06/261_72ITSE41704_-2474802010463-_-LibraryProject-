using System.Text.RegularExpressions;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Domain.Common;

/// <summary>
/// Input checks shared by entities and services. Every method throws a
/// <see cref="ValidationException"/> with a message that can be shown to the user.
/// </summary>
public static partial class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ValidationException(field, $"{field} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new ValidationException(field, $"{field} must not be longer than {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new ValidationException(field, $"{field} must not be longer than {maxLength} characters.");
        }

        return trimmed;
    }

    public static int InRange(int value, int min, int max, string field)
    {
        if (value < min || value > max)
        {
            throw new ValidationException(field, $"{field} must be between {min} and {max}.");
        }

        return value;
    }

    public static decimal Positive(decimal value, string field)
    {
        if (value <= 0)
        {
            throw new ValidationException(field, $"{field} must be greater than 0.");
        }

        return value;
    }

    public static decimal NotNegative(decimal value, string field)
    {
        if (value < 0)
        {
            throw new ValidationException(field, $"{field} must not be negative.");
        }

        return value;
    }

    public static string? Email(string? value, string field = "Email")
    {
        var email = Optional(value, field, 100);
        if (email != null && !EmailPattern().IsMatch(email))
        {
            throw new ValidationException(field, $"{field} '{email}' is not a valid e-mail address.");
        }

        return email;
    }

    public static string? Phone(string? value, string field = "Phone")
    {
        var phone = Optional(value, field, 20);
        if (phone != null && !PhonePattern().IsMatch(phone))
        {
            throw new ValidationException(field, $"{field} must contain 9 to 20 digits or spaces, optionally starting with +.");
        }

        return phone;
    }

    public static string Url(string? value, string field = "URL")
    {
        var url = Required(value, field, 500);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException(field, $"{field} must be an absolute http:// or https:// address.");
        }

        return url;
    }

    public static string Code(string? value, string field, int maxLength)
    {
        var code = Required(value, field, maxLength);
        if (!CodePattern().IsMatch(code))
        {
            throw new ValidationException(field, $"{field} may only contain letters, digits, '.', '-' and '_'.");
        }

        return code;
    }

    /// <summary>Accepts ISBN-10 or ISBN-13 with or without hyphens; returns the digits only.</summary>
    public static string? Isbn(string? value, string field = "ISBN")
    {
        var raw = Optional(value, field, 20);
        if (raw == null)
        {
            return null;
        }

        var isbn = raw.Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        var valid = isbn.Length switch
        {
            10 => IsValidIsbn10(isbn),
            13 => IsValidIsbn13(isbn),
            _ => false
        };

        if (!valid)
        {
            throw new ValidationException(field, $"{field} '{raw}' is not a valid ISBN-10 or ISBN-13.");
        }

        return isbn;
    }

    private static bool IsValidIsbn10(string isbn)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            int digit;
            if (i == 9 && isbn[i] == 'X')
            {
                digit = 10;
            }
            else if (char.IsDigit(isbn[i]))
            {
                digit = isbn[i] - '0';
            }
            else
            {
                return false;
            }

            sum += digit * (10 - i);
        }

        return sum % 11 == 0;
    }

    private static bool IsValidIsbn13(string isbn)
    {
        if (!isbn.All(char.IsDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            sum += (isbn[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9 ]{9,20}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex CodePattern();
}
