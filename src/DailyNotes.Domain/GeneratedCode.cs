using System.Globalization;

namespace DailyNotes.Domain;

public static class GeneratedCode
{
    public const long MaxNumber = 99_999_999;

    public static string Format(long number)
    {
        if (number is < 1 or > MaxNumber) throw new ArgumentOutOfRangeException(nameof(number));
        return number.ToString("D4", CultureInfo.InvariantCulture);
    }
}
