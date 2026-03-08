using System.Text.Json;
using Passwords.Models;

namespace Passwords.Services;

public class JsonDataStore
{
    private readonly object _lock = new();
    private readonly string _usersPath;
    private readonly string _entriesPath;
    private readonly string _accessPath;

    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonDataStore(IHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);

        _usersPath = Path.Combine(dataDir, "users.json");
        _entriesPath = Path.Combine(dataDir, "entries.json");
        _accessPath = Path.Combine(dataDir, "access.json");

        EnsureSeedData();
        MigrateEntries();
    }

    public bool ValidateUser(string username, string password)
    {
        lock (_lock)
        {
            var users = Load<List<User>>(_usersPath) ?? new List<User>();
            return users.Any(u => u.Username == username && u.Password == password);
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
            var now = DateTime.UtcNow;
            var entry = new Entry
            {
                Id = nextId,
                Title = title,
                Details = details,
                Users = users,
                History = new List<EntryHistoryRecord>
                {
                    new EntryHistoryRecord
                    {
                        ChangedAtUtc = now,
                        ChangedBy = user,
                        Title = title,
                        Details = details,
                        Users = users
                    }
                }
            };

            entries.Add(entry);
            Save(_entriesPath, entries);
            LogAccess(new AccessLogEntry
            {
                TimestampUtc = now,
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

            var now = DateTime.UtcNow;
            entry.History ??= new List<EntryHistoryRecord>();
            entry.History.Add(new EntryHistoryRecord
            {
                ChangedAtUtc = now,
                ChangedBy = user,
                Title = title,
                Details = details,
                Users = users
            });

            entry.Title = title;
            entry.Details = details;
            entry.Users = users;
            Save(_entriesPath, entries);
            LogAccess(new AccessLogEntry
            {
                TimestampUtc = now,
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

    /// <summary>
    /// Backwards-compatible migration: entries that pre-date history tracking get a single
    /// synthetic history record seeded from their current field values.
    /// </summary>
    private void MigrateEntries()
    {
        lock (_lock)
        {
            if (!File.Exists(_entriesPath)) return;

            var entries = Load<List<Entry>>(_entriesPath) ?? new List<Entry>();
            var migrated = false;

            foreach (var entry in entries)
            {
                if (entry.History == null || entry.History.Count == 0)
                {
                    entry.History = new List<EntryHistoryRecord>
                    {
                        new EntryHistoryRecord
                        {
                            // DateTime.MinValue signals this record predates history tracking
                            ChangedAtUtc = DateTime.MinValue,
                            ChangedBy = "(pre-history)",
                            Title = entry.Title,
                            Details = entry.Details,
                            Users = entry.Users
                        }
                    };
                    migrated = true;
                }
            }

            if (migrated)
            {
                Save(_entriesPath, entries);
            }
        }
    }

    private void EnsureSeedData()
    {
        var users = Load<List<User>>(_usersPath) ?? new List<User>();
        var usersUpdated = false;

        void EnsureUser(string username, string password)
        {
            if (!users.Any(u => u.Username == username))
            {
                users.Add(new User { Username = username, Password = password });
                usersUpdated = true;
            }
        }

        EnsureUser("admin", "admin123");
        EnsureUser("test", "test");

        if (!File.Exists(_usersPath) || usersUpdated)
        {
            Save(_usersPath, users);
        }

        if (!File.Exists(_entriesPath))
        {
            var now = DateTime.UtcNow;
            var entries = new List<Entry>
            {
                new Entry
                {
                    Id = 1,
                    Title = "First Entry",
                    Details = "Replace this with real data.",
                    History = new List<EntryHistoryRecord>
                    {
                        new EntryHistoryRecord
                        {
                            ChangedAtUtc = now,
                            ChangedBy = "system",
                            Title = "First Entry",
                            Details = "Replace this with real data."
                        }
                    }
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

        return JsonSerializer.Deserialize<T>(json, _readOptions);
    }

    private static void Save<T>(string path, T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }
}
