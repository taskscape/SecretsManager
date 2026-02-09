using System.Text.Json;
using Passwords.Models;

namespace Passwords.Services;

public class JsonDataStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _lock = new();
    private readonly string _usersPath;
    private readonly string _entriesPath;
    private readonly string _accessPath;

    public JsonDataStore(IHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);

        _usersPath = Path.Combine(dataDir, "users.json");
        _entriesPath = Path.Combine(dataDir, "entries.json");
        _accessPath = Path.Combine(dataDir, "access.json");

        EnsureSeedData();
    }

    public bool IsAllowedUser(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        var normalized = identifier.Trim();
        lock (_lock)
        {
            var users = Load<List<User>>(_usersPath) ?? new List<User>();
            return users.Any(u => string.Equals(u.Username, normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<Entry> GetEntries()
    {
        lock (_lock)
        {
            var entries = Load<List<Entry>>(_entriesPath) ?? new List<Entry>();
            return entries.ToList();
        }
    }

    public Entry? GetEntry(int id)
    {
        lock (_lock)
        {
            var entries = Load<List<Entry>>(_entriesPath) ?? new List<Entry>();
            return entries.FirstOrDefault(e => e.Id == id);
        }
    }

    public Entry AddEntry(string title, string details, string user)
    {
        return AddEntry(title, details, null, user);
    }

    public Entry AddEntry(string title, string details, string? users, string user)
    {
        lock (_lock)
        {
            var entries = Load<List<Entry>>(_entriesPath) ?? new List<Entry>();
            var nextId = entries.Count == 0 ? 1 : entries.Max(e => e.Id) + 1;
            var entry = new Entry
            {
                Id = nextId,
                Title = title,
                Details = details,
                Users = users
            };

            entries.Add(entry);
            Save(_entriesPath, entries);
            LogAccess(new AccessLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                User = user,
                Action = "CreateEntry",
                EntryId = entry.Id,
                EntryTitle = entry.Title
            });

            return entry;
        }
    }

    public bool UpdateEntry(int id, string title, string details, string user)
    {
        return UpdateEntry(id, title, details, null, user);
    }

    public bool UpdateEntry(int id, string title, string details, string? users, string user)
    {
        lock (_lock)
        {
            var entries = Load<List<Entry>>(_entriesPath) ?? new List<Entry>();
            var entry = entries.FirstOrDefault(e => e.Id == id);
            if (entry == null)
            {
                return false;
            }

            entry.Title = title;
            entry.Details = details;
            entry.Users = users;
            Save(_entriesPath, entries);
            LogAccess(new AccessLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                User = user,
                Action = "UpdateEntry",
                EntryId = entry.Id,
                EntryTitle = entry.Title
            });

            return true;
        }
    }

    public void LogLogin(string user)
    {
        lock (_lock)
        {
            LogAccess(new AccessLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                User = user,
                Action = "LoginSuccess"
            });
        }
    }

    public void LogEntryOpened(string user, Entry entry)
    {
        lock (_lock)
        {
            LogAccess(new AccessLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                User = user,
                Action = "OpenEntry",
                EntryId = entry.Id,
                EntryTitle = entry.Title
            });
        }
    }

    private void LogAccess(AccessLogEntry logEntry)
    {
        var logs = Load<List<AccessLogEntry>>(_accessPath) ?? new List<AccessLogEntry>();
        logs.Add(logEntry);
        Save(_accessPath, logs);
    }

    private void EnsureSeedData()
    {
        if (!File.Exists(_usersPath))
        {
            var users = new List<User>
            {
                new User { Username = "admin@contoso.com" }
            };
            Save(_usersPath, users);
        }

        if (!File.Exists(_entriesPath))
        {
            var entries = new List<Entry>
            {
                new Entry
                {
                    Id = 1,
                    Title = "First Entry",
                    Details = "Replace this with real data."
                }
            };
            Save(_entriesPath, entries);
        }

        if (!File.Exists(_accessPath))
        {
            Save(_accessPath, new List<AccessLogEntry>());
        }
    }

    private static T? Load<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }

    private static void Save<T>(string path, T data)
    {
        var json = JsonSerializer.Serialize(data, SerializerOptions);
        File.WriteAllText(path, json);
    }
}
