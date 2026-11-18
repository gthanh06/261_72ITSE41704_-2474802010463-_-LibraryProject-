using ELibrary.Business.Security;

namespace ELibrary.Tests.Security;

[TestFixture]
public class Pbkdf2PasswordHasherTests
{
    // Few iterations keep the tests fast; the format and checks are the same as in production.
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);

    [Test]
    public void Hash_then_Verify_with_the_same_password_succeeds()
    {
        var stored = _hasher.Hash("Librarian@123");

        Assert.That(_hasher.Verify("Librarian@123", stored), Is.True);
    }

    [Test]
    public void Verify_with_a_different_password_fails()
    {
        var stored = _hasher.Hash("Librarian@123");

        Assert.Multiple(() =>
        {
            Assert.That(_hasher.Verify("librarian@123", stored), Is.False, "case matters");
            Assert.That(_hasher.Verify("Librarian@124", stored), Is.False);
            Assert.That(_hasher.Verify(string.Empty, stored), Is.False);
        });
    }

    [Test]
    public void Hash_uses_a_new_random_salt_each_time()
    {
        var first = _hasher.Hash("Member@123");
        var second = _hasher.Hash("Member@123");

        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(_hasher.Verify("Member@123", first) && _hasher.Verify("Member@123", second), Is.True);
    }

    [Test]
    public void Stored_value_has_algorithm_iterations_salt_and_hash()
    {
        var parts = _hasher.Hash("Admin@123").Split('$');

        Assert.Multiple(() =>
        {
            Assert.That(parts, Has.Length.EqualTo(4));
            Assert.That(parts[0], Is.EqualTo("PBKDF2-SHA256"));
            Assert.That(parts[1], Is.EqualTo("1000"));
            Assert.That(Convert.FromBase64String(parts[2]), Has.Length.EqualTo(Pbkdf2PasswordHasher.SaltSize));
            Assert.That(Convert.FromBase64String(parts[3]), Has.Length.EqualTo(Pbkdf2PasswordHasher.HashSize));
        });
    }

    [Test]
    public void Default_hasher_uses_100000_iterations_NFR03()
    {
        var parts = new Pbkdf2PasswordHasher().Hash("Admin@123").Split('$');

        Assert.That(parts[1], Is.EqualTo("100000"));
    }

    [Test]
    public void Verify_reads_the_iteration_count_from_the_stored_value()
    {
        var stored = new Pbkdf2PasswordHasher(iterations: 500).Hash("Admin@123");

        Assert.That(_hasher.Verify("Admin@123", stored), Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("plain-text-password")]
    [TestCase("PBKDF2-SHA256$1000$abc")]
    [TestCase("MD5$1000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [TestCase("PBKDF2-SHA256$-5$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [TestCase("PBKDF2-SHA256$1000$not base64!$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [TestCase("PBKDF2-SHA256$1000$AAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Verify_returns_false_for_a_malformed_stored_value(string? stored)
    {
        Assert.That(_hasher.Verify("Admin@123", stored!), Is.False);
    }

    [Test]
    public void NeedsRehash_is_true_only_for_weaker_or_unreadable_hashes()
    {
        var weak = new Pbkdf2PasswordHasher(iterations: 500).Hash("Admin@123");
        var current = _hasher.Hash("Admin@123");

        Assert.Multiple(() =>
        {
            Assert.That(_hasher.NeedsRehash(weak), Is.True);
            Assert.That(_hasher.NeedsRehash(current), Is.False);
            Assert.That(_hasher.NeedsRehash("garbage"), Is.True);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_rejects_a_non_positive_iteration_count(int iterations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Pbkdf2PasswordHasher(iterations));
    }
}
