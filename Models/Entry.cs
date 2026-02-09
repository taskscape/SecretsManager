namespace Passwords.Models;

public class Entry
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Details { get; set; } = "";
    public string? Users { get; set; }
}
