using System.Security.Cryptography;
using ELibrary.Business.Abstractions;

namespace ELibrary.Business.Security;

/// <summary>
/// Password hashing with PBKDF2-SHA256, 100,000 iterations and a 16-byte random salt (NFR-03).
/// The stored value is self-describing — <c>PBKDF2-SHA256$iterations$salt$hash</c> (Base64) — so the
/// iteration count can be raised later without breaking existing accounts.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const string Algorithm = "PBKDF2-SHA256";
    public const int DefaultIterations = 100_000;
    public const int SaltSize = 16;
    public const int HashSize = 32;

    // Guards Verify against stored values that would make it run for a very long time.
    private const int MaxIterations = 10_000_000;

    private readonly int _iterations;

    public Pbkdf2PasswordHasher() : this(DefaultIterations)
    {
    }

    /// <summary>Lets tests use fewer iterations; production code uses the default constructor.</summary>
    public Pbkdf2PasswordHasher(int iterations)
    {
        if (iterations < 1 || iterations > MaxIterations)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations));
        }

        _iterations = iterations;
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, _iterations);
        return string.Join('$', Algorithm, _iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>
    /// True when <paramref name="password"/> matches <paramref name="storedHash"/>. A missing or malformed
    /// stored value never matches (it does not throw), so a damaged record cannot be used to sign in.
    /// </summary>
    public bool Verify(string password, string storedHash)
    {
        if (password == null || !TryParse(storedHash, out var iterations, out var salt, out var expected))
        {
            return false;
        }

        var actual = Derive(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>True when the stored hash uses fewer iterations than this hasher (it should be re-hashed at the next sign-in).</summary>
    public bool NeedsRehash(string storedHash) =>
        !TryParse(storedHash, out var iterations, out _, out _) || iterations < _iterations;

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);

    private static bool TryParse(string? stored, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = hash = Array.Empty<byte>();
        var parts = stored?.Split('$');
        if (parts is not { Length: 4 } || parts[0] != Algorithm ||
            !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out iterations) ||
            iterations < 1 || iterations > MaxIterations)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length == SaltSize && hash.Length == HashSize;
    }
}
