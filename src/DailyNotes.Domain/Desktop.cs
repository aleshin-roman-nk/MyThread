namespace DailyNotes.Domain;

public sealed class Desktop
{
    public const int DefaultId = 1;
    public int Id { get; private set; }
    public IReadOnlyCollection<Sticker> Stickers { get; private set; } = new List<Sticker>();
}
