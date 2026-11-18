using ELibrary.Domain.Exceptions;

namespace ELibrary.Business.Security;

/// <summary>How strong a password looks; used by the UI for the strength meter only.</summary>
public enum PasswordStrength
{
    TooWeak,
    Fair,
    Good,
    Strong
}

/// <summary>
/// Rules for new passwords (change own password, create account, reset password): 8–100 characters,
/// at least one letter and one digit, and not the same as the username. The rules are checked again in
/// the services, so the UI hints are only a convenience.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 100;
    public const string Field = "Password";

    /// <summary>A one-line description of the rules, for help text under password fields.</summary>
    public const string Description = "At least 8 characters, with letters and numbers.";

    /// <summary>The problems with <paramref name="password"/>, in the order the user should fix them; empty when it is acceptable.</summary>
    public static IReadOnlyList<string> Problems(string? password, string? username = null)
    {
        var problems = new List<string>();
        if (string.IsNullOrEmpty(password))
        {
            problems.Add("Enter a password.");
            return problems;
        }

        if (password.Length < MinLength)
        {
            problems.Add($"Use at least {MinLength} characters.");
        }

        if (password.Length > MaxLength)
        {
            problems.Add($"Use at most {MaxLength} characters.");
        }

        if (!password.Any(char.IsLetter))
        {
            problems.Add("Include at least one letter.");
        }

        if (!password.Any(char.IsDigit))
        {
            problems.Add("Include at least one number.");
        }

        if (!string.IsNullOrWhiteSpace(username) && string.Equals(password.Trim(), username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("The password must not be the same as the username.");
        }

        return problems;
    }

    public static bool IsValid(string? password, string? username = null) => Problems(password, username).Count == 0;

    /// <summary>Throws a <see cref="ValidationException"/> with the first problem, if any.</summary>
    public static void Validate(string? password, string? username = null, string field = Field)
    {
        var problems = Problems(password, username);
        if (problems.Count > 0)
        {
            throw new ValidationException(field, problems[0]);
        }
    }

    /// <summary>A rough strength estimate: passwords that break the rules are always <see cref="PasswordStrength.TooWeak"/>.</summary>
    public static PasswordStrength Strength(string? password, string? username = null)
    {
        if (!IsValid(password, username))
        {
            return PasswordStrength.TooWeak;
        }

        var score = 0;
        if (password!.Length >= 12)
        {
            score++;
        }

        if (password.Any(char.IsUpper) && password.Any(char.IsLower))
        {
            score++;
        }

        if (password.Any(c => !char.IsLetterOrDigit(c)))
        {
            score++;
        }

        return score switch
        {
            0 => PasswordStrength.Fair,
            1 => PasswordStrength.Good,
            _ => PasswordStrength.Strong
        };
    }
}
