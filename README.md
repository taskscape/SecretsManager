# SecretsManager

Password and sensitive information management application with Microsoft Account authentication.

## Features

- Secure authentication via Microsoft Account (Azure AD)
- Access control for authorized users only
- Entry management: add, edit, and view passwords
- Audit logging for access tracking
- JSON file storage (no database required)

## Quick Start

### Requirements

- .NET 8 SDK
- Microsoft Account (Outlook, Hotmail, or Azure AD)
- Azure Account (for application registration)

### Local Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/taskscape/SecretsManager.git
   cd SecretsManager
   ```

2. **Restore packages:**
   ```bash
   dotnet restore
   ```

3. **Configure the application:**
   
   See detailed instructions: **[docs/Local-Setup-Guide.md](docs/Local-Setup-Guide.md)**

   Quick version:
   ```bash
   # Initialize User Secrets
   dotnet user-secrets init
   
   # Copy user template
   cp App_Data/users.json.example App_Data/users.json
   
   # Edit users.json and add your email
   ```

4. **Register application in Azure:**

   See: **[docs/Azure-App-Registration-Guide.md](docs/Azure-App-Registration-Guide.md)**

5. **Set authentication credentials:**
   ```bash
   dotnet user-secrets set "Authentication:Microsoft:ClientId" "YOUR-CLIENT-ID"
   dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "YOUR-SECRET"
   ```

6. **Run the application:**
   ```bash
   dotnet run
   ```

7. **Open your browser:**
   ```
   https://localhost:5001
   ```

## Documentation

| Document | Description |
|----------|-------------|
| **[docs/README.md](docs/README.md)** | Project and deployment summary |
| **[docs/Local-Setup-Guide.md](docs/Local-Setup-Guide.md)** | Local configuration for developers |
| **[docs/Azure-App-Registration-Guide.md](docs/Azure-App-Registration-Guide.md)** | Azure Portal application registration |
| **[docs/Deployment-Guide.md](docs/Deployment-Guide.md)** | Production server deployment (T01) |

## Architecture

- **Framework**: ASP.NET Core 8.0 (MVC)
- **Authentication**: Microsoft Identity Platform (OpenID Connect)
- **Storage**: JSON files (App_Data)
- **Frontend**: Razor Views + CSS

### Project Structure

```
SecretsManager/
??? Controllers/           # MVC Controllers
?   ??? AccountController.cs
?   ??? EntriesController.cs
??? Models/                # Data models
?   ??? Entry.cs
?   ??? User.cs
?   ??? ...
??? Services/              # Business logic
?   ??? JsonDataStore.cs
??? Views/                 # Razor views
?   ??? Account/
?   ??? Entries/
?   ??? Shared/
??? App_Data/              # JSON data (not in repo!)
?   ??? users.json
?   ??? entries.json
?   ??? access.json
??? docs/                  # Documentation
    ??? README.md
    ??? Local-Setup-Guide.md
    ??? Azure-App-Registration-Guide.md
    ??? Deployment-Guide.md
```

## Security

### Security Features:

- Microsoft authentication (OAuth 2.0 / OpenID Connect)
- Controller-level authorization (`[Authorize]`)
- Allowed users list (`users.json`)
- HTTPS required
- Anti-forgery tokens (CSRF protection)
- Audit log for entry access

### Files Not Committed to Git:

The following files are in `.gitignore` and **should NOT** be in the repository:

```
appsettings.json
appsettings.Production.json
appsettings.*.json
App_Data/users.json
App_Data/entries.json
App_Data/access.json
```

### Secrets Storage:

- **Local**: Use User Secrets (`dotnet user-secrets`)
- **Production**: Environment variables or `appsettings.Production.json` (not in repo)

## User Management

Add users by editing `App_Data/users.json`:

```json
[
  {
    "username": "john.doe@company.com"
  },
  {
    "username": "jane.smith@outlook.com"
  }
]
```

**Note**: Use the exact email address associated with the Microsoft Account.

Changes to this file are loaded dynamically - no application restart required.

## Production Deployment

To deploy the application to a production server (T01), follow the instructions in:

**[docs/Deployment-Guide.md](docs/Deployment-Guide.md)**

### Quick Steps:

1. Update Redirect URI in Azure Portal
2. Publish application: `dotnet publish -c Release -o ./publish`
3. Transfer files to T01 server
4. Configure IIS or Nginx
5. Create `appsettings.Production.json` on server
6. Test authentication

## Configuration

### appsettings.json

```json
{
  "Authentication": {
    "Microsoft": {
      "ClientId": "your-client-id-from-azure",
      "ClientSecret": "your-client-secret-from-azure",
      "CallbackPath": "/signin-oidc"
    }
  }
}
```

### Environment Variables (alternative)

```bash
Authentication__Microsoft__ClientId=your-client-id
Authentication__Microsoft__ClientSecret=your-secret
```

## Audit Log

The application automatically logs:
- User logins
- Entry access (Details view)
- New entry creation
- Entry modifications

Logs are stored in `App_Data/access.json`.

## Development

### Development Requirements:

- Visual Studio 2022 / Visual Studio Code / Rider
- .NET 8 SDK
- Git

### Useful Commands:

```bash
# Build
dotnet build

# Run
dotnet run

# Test
dotnet test

# Publish
dotnet publish -c Release

# User Secrets
dotnet user-secrets list
dotnet user-secrets set "Key" "Value"
dotnet user-secrets clear
```

## Troubleshooting

### Local Issues:
See: **[docs/Local-Setup-Guide.md](docs/Local-Setup-Guide.md)** - "Troubleshooting" section

### Production Issues:
See: **[docs/Deployment-Guide.md](docs/Deployment-Guide.md)** - "Troubleshooting" section

### Common Problems:

| Problem | Solution |
|---------|----------|
| "Microsoft sign-in failed" | Check ClientId/ClientSecret in configuration |
| "User is not allowed" | Add user email to `App_Data/users.json` |
| "AADSTS50011" | Check Redirect URI in Azure Portal |
| 502/503 Error | Check if application is running (IIS/systemd) |

## License

This project is private. No public license.

## Contact

**Repository**: https://github.com/taskscape/SecretsManager  
**Branch**: `microsoft-account-authentication`

## Project Status

- Code: **Ready**
- Documentation: **Complete**
- Azure Registration: **To be done by developer**
- T01 Deployment: **To be done**

---

**Version**: 1.0  
**Last Updated**: 2024  
**Status**: Ready for deployment
