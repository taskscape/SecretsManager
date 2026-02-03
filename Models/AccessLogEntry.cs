namespace Passwords.Models;

public class AccessLogEntry
{
    public DateTime TimestampUtc { get; set; }
    public string User { get; set; } = "";
    public string Action { get; set; } = "";
    public int? EntryId { get; set; }
    public string? EntryTitle { get; set; }
    public string? Notes { get; set; }
}
