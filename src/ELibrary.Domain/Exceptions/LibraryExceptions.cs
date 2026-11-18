namespace ELibrary.Domain.Exceptions;

/// <summary>
/// Base class of every expected error. The presentation layer shows the message of a
/// <see cref="LibraryException"/> to the user as it is; any other exception is treated as a defect.
/// </summary>
public abstract class LibraryException : Exception
{
    protected LibraryException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Invalid user input (missing field, wrong format, value out of range).</summary>
public sealed class ValidationException : LibraryException
{
    public ValidationException(string field, string message) : base(message)
    {
        Field = field;
    }

    public string Field { get; }
}

/// <summary>A business rule (BR1–BR7) or a state rule of an entity was violated.</summary>
public sealed class BusinessRuleException : LibraryException
{
    /// <summary>Rule identifier used for state transitions that are not one of BR1–BR7.</summary>
    public const string StateRule = "STATE";

    public BusinessRuleException(string rule, string message) : base(message)
    {
        Rule = rule;
    }

    /// <summary>"BR1" … "BR7", or <see cref="StateRule"/>.</summary>
    public string Rule { get; }
}

public sealed class NotFoundException : LibraryException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.")
    {
        EntityName = entityName;
    }

    public string EntityName { get; }
}

/// <summary>The current user's role does not allow the operation (NFR-04).</summary>
public sealed class AccessDeniedException : LibraryException
{
    public AccessDeniedException(string message) : base(message)
    {
    }
}

public sealed class AuthenticationFailedException : LibraryException
{
    public AuthenticationFailedException(string message) : base(message)
    {
    }
}

/// <summary>Wraps a database failure so that the upper layers do not depend on EF Core or SQL Server types.</summary>
public sealed class DataAccessException : LibraryException
{
    public DataAccessException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
