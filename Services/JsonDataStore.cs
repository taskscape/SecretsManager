using System.Text.Json;
using Passwords.Models;

namespace Passwords.Services;

public class JsonDataStore
{
    private readonly object _lock = new();
    private readonly string _usersPath;
    private readonly string _entriesPath;
    private readonly string _accessPath;
    private readonly string _requestsPath;

    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonDataStore(IHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);

        _usersPath    = Path.Combine(dataDir, "users.json");
        _entriesPath  = Path.Combine(dataDir, "entries.json");
        _accessPath   = Path.Combine(dataDir, "access.json");
        _requestsPath = Path.Combine(dataDir, "requests.json");

        EnsureSeedData();
        MigrateEntries();
        MigrateUsers();
        MigrateEntryOwners();
    }

    // ── User helpers ──────────────────────────────────────────────────────────

    public bool ValidateUser(string username, string password)
    {
        lock (_lock)
        {
            var users = Load<List<User>>(_usersPath) ?? new List<User>();
            return users.Any(u => u.Username == username && u.Password == password);
        }
    }

    public User? GetUser(string username)
    {
        lock (_lock)
        {
            var users = Load<List<User>>(_usersPath) ?? new List<User>();
            return users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ── Access control ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="currentUser"/> can read the entry's content.
    /// Admins and the entry owner always have access.
    /// Only "*" in the Users field grants access to everyone.
    /// Null/empty Users means private — owner and admins only.
    /// </summary>
    public bool CanUserReadEntry(Entry entry, User? currentUser)
    {
        if (currentUser == null) return false;

        if (string.Equals(entry.CreatedBy, currentUser.Username, StringComparison.OrdinalIgnoreCase))
            return true;

        if (currentUser.IsAdmin) return true;

        // null/empty Users = private (owner and admins only, already checked above)
        if (string.IsNullOrWhiteSpace(entry.Users)) return false;

        if (entry.Users.Trim() == "*") return true;

        var allowed = entry.Users
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim());
        return allowed.Any(u => string.Equals(u, currentUser.Username, StringComparison.OrdinalIgnoreCase));
    }

    // ── Entry CRUD ────────────────────────────────────────────────────────────

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
            var nextId  = entries.Count == 0 ? 1 : entries.Max(e => e.Id) + 1;
            var now     = DateTime.UtcNow;
            var entry = new Entry
            {
                Id        = nextId,
                Title     = title,
                Details   = details,
                Users     = users,
                CreatedBy = user,
                History = new List<EntryHistoryRecord>
                {
                    new EntryHistoryRecord
                    {
                        ChangedAtUtc = now,
                        ChangedBy    = user,
                        Title        = title,
                        Details      = details,
                        Users        = users
                    }
                }
            };

            entries.Add(entry);
            Save(_entriesPath, entries);
            LogAccess(new AccessLogEntry
            {
                TimestampUtc = now,
                User         = user,
                Action       = "CreateEntry",
                EntryId      = entry.Id,
                EntryTitle   = entry.Title
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
            var entry   = entries.FirstOrDefault(e => e.Id == id);
            if (entry == null) return false;

            var now = DateTime.UtcNow;
            entry.History ??= new List<EntryHistoryRecord>();
            entry.History.Add(new EntryHistoryRecord
            {
                ChangedAtUtc = now,
                ChangedBy    = user,
                Title        = title,
                Details      = details,
                Users        = users
            });

            entry.Title   = title;
            entry.Details = details;
            entry.Users   = users;
            Save(_entriesPath, entries);
            LogAccess(new AccessLogEntry
            {
                TimestampUtc = now,
                User         = user,
                Action       = "UpdateEntry",
                EntryId      = entry.Id,
                EntryTitle   = entry.Title
            });

            return true;
        }
    }

    // ── Access requests ───────────────────────────────────────────────────────

    public bool HasPendingRequest(string requesterUsername, int entryId)
    {
        lock (_lock)
        {
            var requests = Load<List<AccessRequest>>(_requestsPath) ?? new List<AccessRequest>();
            return requests.Any(r =>
                string.Equals(r.RequestedBy, requesterUsername, StringComparison.OrdinalIgnoreCase) &&
                r.EntryId == entryId);
        }
    }

    /// <summary>Creates an access request. Returns false if one already exists.</summary>
    public bool CreateAccessRequest(string requesterUsername, int entryId)
    {
        lock (_lock)
        {
            var requests = Load<List<AccessRequest>>(_requestsPath) ?? new List<AccessRequest>();
            if (requests.Any(r =>
                string.Equals(r.RequestedBy, requesterUsername, StringComparison.OrdinalIgnoreCase) &&
                r.EntryId == entryId))
            {
                return false;
            }

            var nextId = requests.Count == 0 ? 1 : requests.Max(r => r.Id) + 1;
            requests.Add(new AccessRequest
            {
                Id              = nextId,
                RequestedBy     = requesterUsername,
                EntryId         = entryId,
                RequestedAtUtc  = DateTime.UtcNow
            });
            Save(_requestsPath, requests);
            return true;
        }
    }

    /// <summary>Returns all pending requests for entries owned by the given user.</summary>
    public IReadOnlyList<AccessRequest> GetPendingRequestsForOwner(string ownerUsername)
    {
        lock (_lock)
        {
            var requests = Load<List<AccessRequest>>(_requestsPath) ?? new List<AccessRequest>();
            var entries  = Load<List<Entry>>(_entriesPath)          ?? new List<Entry>();

            var ownedIds = entries
                .Where(e => string.Equals(e.CreatedBy, ownerUsername, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Id)
                .ToHashSet();

            return requests
                .Where(r => ownedIds.Contains(r.EntryId))
                .OrderByDescending(r => r.RequestedAtUtc)
                .ToList();
        }
    }

    public int GetPendingRequestCountForOwner(string ownerUsername)
    {
        return GetPendingRequestsForOwner(ownerUsername).Count;
    }

    /// <summary>
    /// Approves the request: adds the requester to the entry's Users list (if the list is
    /// restricted) and removes the request. Only the entry owner may approve.
    /// </summary>
    public bool ApproveRequest(int requestId, string currentUser)
    {
        lock (_lock)
        {
            var requests = Load<List<AccessRequest>>(_requestsPath) ?? new List<AccessRequest>();
            var request  = requests.FirstOrDefault(r => r.Id == requestId);
            if (request == null) return false;

            var entries = Load<List<Entry>>(_entriesPath) ?? new List<Entry>();
            var entry   = entries.FirstOrDefault(e => e.Id == request.EntryId);
            if (entry == null) return false;
            if (!string.Equals(entry.CreatedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                return false;

            // If the entry is open to everyone ("*"), the requester already has access —
            // just remove the request. Otherwise (null/empty = private, or an explicit list),
            // add the requester to the Users field.
            if (entry.Users?.Trim() != "*")
            {
                var allowed = string.IsNullOrWhiteSpace(entry.Users)
                    ? new List<string>()
                    : entry.Users.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(u => u.Trim())
                                 .ToList();
                if (!allowed.Any(u => string.Equals(u, request.RequestedBy, StringComparison.OrdinalIgnoreCase)))
                    allowed.Add(request.RequestedBy);
                entry.Users = string.Join(", ", allowed);
                Save(_entriesPath, entries);
            }

            requests.Remove(request);
            Save(_requestsPath, requests);
            return true;
        }
    }

    /// <summary>Declines the request (removes it without granting access). Owner only.</summary>
    public bool DeclineRequest(int requestId, string currentUser)
    {
        lock (_lock)
        {
            var requests = Load<List<AccessRequest>>(_requestsPath) ?? new List<AccessRequest>();
            var request  = requests.FirstOrDefault(r => r.Id == requestId);
            if (request == null) return false;

            var entries = Load<List<Entry>>(_entriesPath) ?? new List<Entry>();
            var entry   = entries.FirstOrDefault(e => e.Id == request.EntryId);
            if (entry == null) return false;
            if (!string.Equals(entry.CreatedBy, currentUser, StringComparison.OrdinalIgnoreCase))
                return false;

            requests.Remove(request);
            Save(_requestsPath, requests);
            return true;
        }
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    public void LogLogin(string user)
    {
        lock (_lock)
        {
            LogAccess(new AccessLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                User         = user,
                Action       = "LoginSuccess"
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
                User         = user,
                Action       = "OpenEntry",
                EntryId      = entry.Id,
                EntryTitle   = entry.Title
            });
        }
    }

    private void LogAccess(AccessLogEntry logEntry)
    {
        var logs = Load<List<AccessLogEntry>>(_accessPath) ?? new List<AccessLogEntry>();
        logs.Add(logEntry);
        Save(_accessPath, logs);
    }

    // ── Migrations ────────────────────────────────────────────────────────────

    private void MigrateEntries()
    {
        lock (_lock)
        {
            if (!File.Exists(_entriesPath)) return;

            var entries  = Load<List<Entry>>(_entriesPath) ?? new List<Entry>();
            var migrated = false;

            foreach (var entry in entries)
            {
                if (entry.History == null || entry.History.Count == 0)
                {
                    entry.History = new List<EntryHistoryRecord>
                    {
                        new EntryHistoryRecord
                        {
                            ChangedAtUtc = DateTime.MinValue,
                            ChangedBy    = "(pre-history)",
                            Title        = entry.Title,
                            Details      = entry.Details,
                            Users        = entry.Users
                        }
                    };
                    migrated = true;
                }
            }

            if (migrated) Save(_entriesPath, entries);
        }
    }

    private void MigrateUsers()
    {
        lock (_lock)
        {
            if (!File.Exists(_usersPath)) return;

            var users    = Load<List<User>>(_usersPath) ?? new List<User>();
            var migrated = false;

            var adminUser = users.FirstOrDefault(u =>
                string.Equals(u.Username, "admin", StringComparison.OrdinalIgnoreCase));
            if (adminUser != null && !adminUser.IsAdmin)
            {
                adminUser.IsAdmin = true;
                migrated = true;
            }

            if (migrated) Save(_usersPath, users);
        }
    }

    private void MigrateEntryOwners()
    {
        lock (_lock)
        {
            if (!File.Exists(_entriesPath)) return;

            var entries = Load<List<Entry>>(_entriesPath) ?? new List<Entry>();
            if (!entries.Any(e => string.IsNullOrEmpty(e.CreatedBy))) return;

            var users        = Load<List<User>>(_usersPath) ?? new List<User>();
            var defaultOwner = users.FirstOrDefault(u => u.IsAdmin)?.Username ?? "admin";

            foreach (var entry in entries.Where(e => string.IsNullOrEmpty(e.CreatedBy)))
                entry.CreatedBy = defaultOwner;

            Save(_entriesPath, entries);
        }
    }

    // ── Seed data ─────────────────────────────────────────────────────────────

    private void EnsureSeedData()
    {
        // Users are managed exclusively via users.json — no automatic creation here.

        if (!File.Exists(_entriesPath))
        {
            var now = DateTime.UtcNow;
            Save(_entriesPath, new List<Entry>
            {
                new Entry
                {
                    Id        = 1,
                    Title     = "First Entry",
                    Details   = "Replace this with real data.",
                    CreatedBy = "admin",
                    History   = new List<EntryHistoryRecord>
                    {
                        new EntryHistoryRecord
                        {
                            ChangedAtUtc = now,
                            ChangedBy    = "system",
                            Title        = "First Entry",
                            Details      = "Replace this with real data."
                        }
                    }
                }
            });
        }

        if (!File.Exists(_accessPath))
            Save(_accessPath, new List<AccessLogEntry>());

        if (!File.Exists(_requestsPath))
            Save(_requestsPath, new List<AccessRequest>());
    }

    // ── Storage helpers ───────────────────────────────────────────────────────

    private static T? Load<T>(string path)
    {
        if (!File.Exists(path)) return default;
        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, _readOptions);
    }

    private static void Save<T>(string path, T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
