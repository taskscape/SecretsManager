namespace Passwords.Models;

public class EntryDetailsViewModel
{
    public Entry Entry { get; set; } = null!;
    public bool CanRead { get; set; }
    public bool HasPendingRequest { get; set; }
    public string OwnerUsername { get; set; } = "";
}
