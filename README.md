# Vault — Secrets Manager

A lightweight ASP.NET Core MVC web application for storing and sharing secrets (passwords, tokens, API keys, etc.) within a small team. All data is persisted in plain JSON files — no external database required.

---

## Technology Stack

| Component | Details |
|-----------|---------|
| Framework | ASP.NET Core MVC (.NET 10) |
| Language | C# |
| Storage | JSON files in `App_Data/` |
| Authentication | Server-side sessions |
| Serialization | `System.Text.Json` |

---

## Running the Application

```bash
dotnet run
```

The app starts on the configured port (see `Properties/launchSettings.json`). On first run it automatically creates seed data (see [Seed Data](#seed-data) below).

---

## Data Storage

All state lives in four JSON files inside the `App_Data/` directory. The directory is created automatically if it does not exist. Files are written with indented JSON for readability. All reads are case-insensitive so hand-edited files are tolerated.

### `users.json`

Stores user accounts.

```json
[
  {
    "Username": "admin",
    "Password": "admin123",
    "IsAdmin": true
  },
  {
    "Username": "alice",
    "Password": "secret",
    "IsAdmin": false
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `Username` | string | Login name. Case-sensitive for login, case-insensitive for access checks. |
| `Password` | string | Stored in **plain text**. Change passwords before any production use. |
| `IsAdmin` | bool | When `true` the user can read all entries regardless of per-entry restrictions. |

> **Security note:** There is no user registration UI. Add users by editing `users.json` directly while the app is stopped, or by modifying the seed logic in `JsonDataStore.EnsureSeedData`.

---

### `entries.json`

Stores the secrets (called *entries*).

```json
[
  {
    "Id": 1,
    "Title": "Production DB",
    "Details": "host=db.example.com\nuser=app\npassword=hunter2",
    "Users": "alice, bob",
    "CreatedBy": "admin",
    "History": [
      {
        "ChangedAtUtc": "2026-03-08T10:00:00Z",
        "ChangedBy": "admin",
        "Title": "Production DB",
        "Details": "host=db.example.com\nuser=app\npassword=hunter2",
        "Users": "alice, bob"
      }
    ]
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `Id` | int | Auto-incrementing identifier. |
| `Title` | string | Human-readable name shown in the entry list. |
| `Details` | string | The secret content — free-form text (passwords, keys, notes, etc.). |
| `Users` | string? | Access control list — see [Per-Entry Access Control](#per-entry-access-control). `null`/empty means private; `"*"` means everyone. |
| `CreatedBy` | string | Username of the entry owner. Set automatically at creation time. |
| `History` | array | Full audit trail of every version — see [Change History](#change-history). |

---

### `access.json`

An append-only audit log. Every significant event is recorded with a UTC timestamp.

```json
[
  {
    "TimestampUtc": "2026-03-08T09:55:00Z",
    "User": "admin",
    "Action": "LoginSuccess",
    "EntryId": null,
    "EntryTitle": null,
    "Notes": null
  },
  {
    "TimestampUtc": "2026-03-08T10:01:00Z",
    "User": "alice",
    "Action": "OpenEntry",
    "EntryId": 1,
    "EntryTitle": "Production DB",
    "Notes": null
  }
]
```

Recorded actions:

| Action | Trigger |
|--------|---------|
| `LoginSuccess` | Successful login |
| `CreateEntry` | New entry saved |
| `UpdateEntry` | Existing entry edited and saved |
| `OpenEntry` | Entry details page viewed by a user who has read access |

`OpenEntry` is only logged when the viewing user actually has access; restricted views do not produce a log entry.

---

### `requests.json`

Pending access requests from users who do not have permission to read an entry.

```json
[
  {
    "Id": 1,
    "RequestedBy": "charlie",
    "EntryId": 1,
    "RequestedAtUtc": "2026-03-08T11:30:00Z"
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `Id` | int | Auto-incrementing identifier. |
| `RequestedBy` | string | Username of the user requesting access. |
| `EntryId` | int | The entry they want to access. |
| `RequestedAtUtc` | DateTime | When the request was submitted (UTC). |

Requests are removed when approved or declined. There can be at most one pending request per user per entry.

---

## Authentication

Authentication uses ASP.NET Core's server-side **session middleware**.

- Sessions expire after **30 minutes of inactivity** (configurable in `Program.cs`).
- The session cookie is `HttpOnly` and marked as essential.
- On login, the username is stored in the session under the key `"username"`.
- On logout, the entire session is cleared.
- Every controller action checks for a valid session before serving content; unauthenticated requests are redirected to the login page.
- `.json` files under `App_Data/` are blocked at the middleware level — direct URL access to any `.json` file returns `404 Not Found`.

---

## User Roles

### Regular user
- Can view all entry **titles** in the list.
- Can read entry **contents** only for entries they own, entries where the `Users` field includes them, or entries open to everyone.
- Can create new entries (they become the owner).
- Can edit entries they are allowed to read.
- Can request access to entries they cannot read.
- Sees pending access requests for entries they own.

### Administrator (`IsAdmin: true`)
- All regular-user capabilities, plus:
- Can read **all** entry contents regardless of the `Users` field.
- Can edit all entries.
- Receives no special UI beyond this — there is no separate admin panel.

---

## Per-Entry Access Control

The `Users` field on each entry controls who can read its contents. The field is evaluated as follows (first matching rule wins):

| `Users` value | Who can read the contents |
|---------------|--------------------------|
| `null` or empty | **Private** — owner and admins only (default) |
| `"*"` | Everyone |
| `"alice, bob"` | `alice` and `bob` only (plus owner and admins) |

**Regardless of the `Users` field**, the following always have read access:
- The entry **owner** (`CreatedBy` field).
- Any user with `IsAdmin: true`.

All users can always see the entry **title** in the listing — only the contents are restricted.

### Editing

Only users who can **read** an entry may edit it. This prevents direct URL navigation to `/Entries/Edit/{id}` from bypassing access control.

---

## Change History

Every create or update operation appends a snapshot to the entry's `History` array. Each snapshot captures the complete state at that moment:

```json
{
  "ChangedAtUtc": "2026-03-08T12:00:00Z",
  "ChangedBy": "alice",
  "Title": "Production DB",
  "Details": "host=db.example.com\npassword=newpassword",
  "Users": "alice, bob"
}
```

The **Details page** shows the full change history in a collapsible section, newest version first. If the title changed between versions, the old title is highlighted.

The current entry always reflects the latest version. History is read-only — individual history entries cannot be deleted or edited through the UI.

---

## Access Request Workflow

When a user tries to open an entry they cannot read, they see an **Access restricted** panel instead of the contents. The panel shows who owns the entry and offers a **Request access** button.

```
┌─────────────────────────────────────────────────┐
│ 🔒  Access restricted                           │
│                                                 │
│  This entry is owned by alice.                  │
│  You can request access and the owner will      │
│  be notified.                                   │
│                                                 │
│  [ Request access ]                             │
└─────────────────────────────────────────────────┘
```

### Submitting a request

Clicking **Request access** creates a record in `requests.json`. If a request is already pending, the button is replaced with a notice:

```
⏳  Request sent — waiting for alice to approve.
```

A user can have at most one pending request per entry. Submitting again (e.g. by reloading) is silently ignored.

### Owner notification

The next time the **entry owner** loads any page, a **red notification badge** appears on their avatar in the sidebar footer, showing the count of pending requests. A highlighted link appears below the avatar reading *"N pending requests"*.

Clicking the link opens `/Requests`, which lists every pending request for entries the current user owns:

```
┌──────────────────────────────────────────────────────────────┐
│  C  charlie                                                  │
│     requested access to  Production DB                       │
│     2026-03-08 11:30 UTC                                     │
│                                    [ ✓ Approve ] [ ✗ Decline]│
└──────────────────────────────────────────────────────────────┘
```

### Approving a request

When the owner clicks **Approve**:
1. The requesting user's username is appended to the entry's `Users` field.
2. The request record is removed from `requests.json`.
3. The user can now read the entry on their next visit.

If the entry's `Users` field is already `null` or `"*"` (everyone has access), the request is simply removed — no change to the entry is needed.

### Declining a request

When the owner clicks **Decline**:
1. The request record is removed from `requests.json`.
2. The entry's `Users` field is **not changed** — the user still cannot read the entry.
3. The user may submit a new request in the future (there is no permanent block).

---

## Seed Data

On first run (or whenever a data file is missing), the application creates default data:

| File | Default content |
|------|----------------|
| `users.json` | `admin` / `admin123` (IsAdmin: true), `test` / `test` |
| `entries.json` | One sample entry titled "First Entry" owned by `admin` |
| `access.json` | Empty array |
| `requests.json` | Empty array |

> **Change the default passwords before exposing the application to any network.**

---

## Automatic Data Migrations

When the application starts it runs three migration passes in order, each of which is a no-op if the data is already up to date:

| Migration | What it does |
|-----------|-------------|
| `MigrateEntries` | Adds a synthetic `(pre-history)` history record (with `ChangedAtUtc = DateTime.MinValue`) to any entry that has no history, preserving the entry's current field values. |
| `MigrateUsers` | Sets `IsAdmin = true` on any user whose username is `"admin"` if the flag is missing or false. |
| `MigrateEntryOwners` | Assigns `CreatedBy` to entries where the field is empty. The default owner is the first admin user found in `users.json`; if no admin exists, the string `"admin"` is used as a fallback. |

These migrations allow the application to be upgraded in place without manually editing the JSON files.

---

## Project Structure

```
SecretsManager2/
├── App_Data/               JSON data files (auto-created at runtime)
│   ├── users.json
│   ├── entries.json
│   ├── access.json
│   └── requests.json
├── Controllers/
│   ├── AccountController.cs    Login / logout
│   ├── EntriesController.cs    Entry CRUD + access request submission
│   └── RequestsController.cs   Approve / decline access requests
├── Models/
│   ├── User.cs
│   ├── Entry.cs
│   ├── EntryHistoryRecord.cs
│   ├── AccessRequest.cs
│   ├── AccessLogEntry.cs
│   ├── EntryListItemViewModel.cs
│   ├── EntryDetailsViewModel.cs
│   ├── AccessRequestViewModel.cs
│   ├── EntryCreateViewModel.cs
│   └── EntryEditViewModel.cs
├── Services/
│   └── JsonDataStore.cs        All data access, access control logic, migrations
├── Views/
│   ├── Account/Login.cshtml
│   ├── Entries/
│   │   ├── Index.cshtml        Entry list (all entries, locked ones visually distinguished)
│   │   ├── Details.cshtml      Entry detail, access-denied panel, change history
│   │   ├── Create.cshtml
│   │   └── Edit.cshtml
│   ├── Requests/
│   │   └── Index.cshtml        Pending requests inbox for entry owners
│   └── Shared/_Layout.cshtml   Sidebar, notification badge
└── wwwroot/css/site.css        Dark theme, all component styles
```
