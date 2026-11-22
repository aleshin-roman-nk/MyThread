namespace JChrono.Domain;

public static class ProjectCode
{
    public const int MaxLength = 64;

    public static string Normalize(string? code, int maxLength, string label)
    {
        var value = code?.Trim() ?? "";
        if (value.Length == 0 || value.Length > maxLength || value.Any(char.IsControl)
            || value.Contains('\\') || value.Contains('/'))
            throw new ArgumentException($"{label}: от 1 до {maxLength} символов, без слешей и управляющих символов.");
        return value;
    }
}
