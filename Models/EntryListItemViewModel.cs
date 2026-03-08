namespace Passwords.Models;

public class EntryListItemViewModel
{
    public Entry Entry { get; set; } = null!;
    public bool CanRead { get; set; }
}
