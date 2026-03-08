namespace Passwords.Models;

public class AccessRequest
{
    public int Id { get; set; }
    public string RequestedBy { get; set; } = "";
    public int EntryId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
}
