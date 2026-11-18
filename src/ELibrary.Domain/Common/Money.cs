using System.Globalization;

namespace ELibrary.Domain.Common;

/// <summary>Formatting of amounts in Vietnamese dong (VND has no fractional part).</summary>
public static class Money
{
    public static string Format(decimal amount) =>
        amount.ToString("#,0", CultureInfo.InvariantCulture) + " VND";
}
