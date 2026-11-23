using System.Security.Cryptography;
using System.Text;
using ELibrary.Business.Abstractions;

namespace ELibrary.Tests.Support;

/// <summary>A fast, deterministic stand-in for PBKDF2 (the real hasher has its own tests).</summary>
internal sealed class FastHasher : IPasswordHasher
{
    public string Hash(string password) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    public bool Verify(string password, string storedHash) => Hash(password) == storedHash;
}

/// <summary>Records backup and restore calls instead of talking to SQL Server.</summary>
internal sealed class RecordingMaintenance : IDatabaseMaintenance
{
    public List<string> Calls { get; } = new();

    public void Backup(string backupFilePath) => Calls.Add("backup " + backupFilePath);

    public void Restore(string backupFilePath) => Calls.Add("restore " + backupFilePath);
}
