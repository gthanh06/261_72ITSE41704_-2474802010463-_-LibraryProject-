using ELibrary.Business.Security;
using ELibrary.Domain.Exceptions;

namespace ELibrary.Tests.Security;

[TestFixture]
public class PasswordPolicyTests
{
    [TestCase("Admin@123")]
    [TestCase("Librarian@123")]
    [TestCase("Member@123")]
    public void Sample_data_passwords_are_accepted(string password)
    {
        Assert.That(PasswordPolicy.IsValid(password), Is.True);
    }

    [TestCase("abc12", "Use at least 8 characters.")]
    [TestCase("abcdefgh", "Include at least one number.")]
    [TestCase("12345678", "Include at least one letter.")]
    [TestCase("", "Enter a password.")]
    [TestCase(null, "Enter a password.")]
    public void Weak_passwords_are_rejected_with_a_friendly_reason(string? password, string reason)
    {
        Assert.That(PasswordPolicy.Problems(password), Does.Contain(reason));
    }

    [Test]
    public void Password_longer_than_the_maximum_is_rejected()
    {
        var password = new string('a', PasswordPolicy.MaxLength) + "1";

        Assert.That(PasswordPolicy.Problems(password), Does.Contain("Use at most 100 characters."));
    }

    [Test]
    public void Password_equal_to_the_username_is_rejected_ignoring_case()
    {
        Assert.That(PasswordPolicy.IsValid("Librarian1", "librarian1"), Is.False);
        Assert.That(PasswordPolicy.IsValid("Librarian1", "admin"), Is.True);
    }

    [Test]
    public void All_problems_are_listed_in_the_order_to_fix_them()
    {
        Assert.That(PasswordPolicy.Problems("abc"),
            Is.EqualTo(new[] { "Use at least 8 characters.", "Include at least one number." }));
    }

    [Test]
    public void Validate_throws_a_ValidationException_with_the_first_problem()
    {
        var ex = Assert.Throws<ValidationException>(() => PasswordPolicy.Validate("short", field: "New password"));

        Assert.That(ex!.Field, Is.EqualTo("New password"));
        Assert.That(ex.Message, Is.EqualTo("Use at least 8 characters."));
    }

    [Test]
    public void Validate_does_not_throw_for_a_valid_password()
    {
        Assert.DoesNotThrow(() => PasswordPolicy.Validate("Member@123", "M0001"));
    }

    [TestCase("abc", PasswordStrength.TooWeak)]
    [TestCase("library7", PasswordStrength.Fair)]
    [TestCase("Library7", PasswordStrength.Good)]
    [TestCase("Library@7", PasswordStrength.Strong)]
    [TestCase("my library card 2026", PasswordStrength.Strong)]
    public void Strength_grows_with_length_mixed_case_and_symbols(string password, PasswordStrength expected)
    {
        Assert.That(PasswordPolicy.Strength(password), Is.EqualTo(expected));
    }
}
