namespace Passwords.Models;

public class EntryHistoryRecord
{
    public DateTime ChangedAtUtc { get; set; }
    public string ChangedBy { get; set; } = "";
    public string Title { get; set; } = "";
    public string Details { get; set; } = "";
    public string? Users { get; set; }
}
