namespace JChrono.Domain;

public sealed class Sticker
{
    private Sticker() { }

    public Sticker(string? text) => UpdateText(text);

    public void UpdateText(string? text)
    {
        if (TaskId.HasValue || NoteId.HasValue)
            throw new ArgumentException("Текст можно изменить только у текстового стикера.");
        var value = text?.Trim() ?? "";
        if (value.Length is 0 or > Note.MaxTextLength)
            throw new ArgumentException($"Текст стикера должен содержать от 1 до {Note.MaxTextLength} символов.");
        Text = value;
    }

    public int Id { get; private set; }
    public int DesktopId { get; private set; } = Desktop.DefaultId;
    public string? Text { get; private set; }
    public int? TaskId { get; private set; }
    public ProjectTask? Task { get; private set; }
    public int? NoteId { get; private set; }
    public Note? Note { get; private set; }
    public double? X { get; private set; }
    public double? Y { get; private set; }

    public void Move(double? x, double? y)
    {
        if (!x.HasValue || !y.HasValue || !double.IsFinite(x.Value) || !double.IsFinite(y.Value)
            || x is < 0 or > 1 || y is < 0 or > 1)
            throw new ArgumentException("Укажите положение стикера в пределах рабочего стола.");
        X = x;
        Y = y;
    }
}
