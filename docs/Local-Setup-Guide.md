# Local Development Setup Guide

## Quick Setup for Developers

This guide will help you set up and run the SecretsManager application on your local development machine.

**Estimated time**: 15-20 minutes

---

## Prerequisites

- .NET 8 SDK installed
- Visual Studio 2022, Visual Studio Code, or Rider
- Git
- Microsoft Account (Outlook, Hotmail, or Azure AD)
- Azure Account (for application registration)

---

## Step 1: Clone and Restore Packages

### 1.1 Clone Repository

```bash
git clone https://github.com/taskscape/SecretsManager.git
cd SecretsManager
```

### 1.2 Restore NuGet Packages

```bash
dotnet restore
```

### 1.3 Verify Build

```bash
dotnet build
```

---

## Step 2: Configure Application Files

### 2.1 Copy Example Configuration Files

**Option A: Using Command Line (Windows)**
```bash
copy appsettings.json.example appsettings.json
copy App_Data\users.json.example App_Data\users.json
```

**Option B: Using Command Line (Linux/Mac)**
```bash
cp appsettings.json.example appsettings.json
cp App_Data/users.json.example App_Data/users.json
```

**Option C: Manually**
1. Open `appsettings.json.example`
2. Save as `appsettings.json` (remove `.example`)
3. Open `App_Data/users.json.example`
4. Save as `App_Data/users.json` (remove `.example`)

### 2.2 Configure Allowed Users

Edit `App_Data/users.json` and add your Microsoft Account email:

```json
[
  {
    "username": "your-email@outlook.com"
  }
]
```

**IMPORTANT**: Use the exact email address associated with your Microsoft Account.

---

## Step 3: Register Application in Azure Portal

You **must** register the application in Azure Portal for authentication to work.

**Detailed instructions**: See `docs/Azure-App-Registration-Guide.md`

### Quick Registration Steps:

1. Go to: https://portal.azure.com
2. Navigate to: Azure Active Directory ? App registrations ? New registration
3. Fill in:
   - **Name**: `SecretsManager-Dev`
   - **Supported account types**: Personal Microsoft accounts
   - **Redirect URI**: `https://localhost:5001/signin-oidc` (Web platform)
4. Click **Register**
5. Copy the **Application (client) ID**
6. Go to: Certificates & secrets ? New client secret
7. Copy the **Client secret value** (visible only once!)

---

## Step 4: Configure Authentication

### Option A: User Secrets (RECOMMENDED)

User Secrets store sensitive data outside your project directory and are never committed to Git.

```bash
# Initialize User Secrets
dotnet user-secrets init

# Set your values from Azure Portal
dotnet user-secrets set "Authentication:Microsoft:ClientId" "YOUR-APPLICATION-CLIENT-ID"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "YOUR-CLIENT-SECRET-VALUE"
```

**Verify secrets are set:**
```bash
dotnet user-secrets list
```

### Option B: appsettings.json (Quick Testing Only)

Edit `appsettings.json`:

```json
{
  "Authentication": {
    "Microsoft": {
      "ClientId": "YOUR-APPLICATION-CLIENT-ID",
      "ClientSecret": "YOUR-CLIENT-SECRET-VALUE",
      "CallbackPath": "/signin-oidc"
    }
  }
}
```

**WARNING**: If you use this option, DO NOT commit `appsettings.json` to Git! (it's already in `.gitignore`)

---

## Step 5: Run the Application

### 5.1 Start the Application

```bash
dotnet run
```

You should see output similar to:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### 5.2 Open in Browser

Navigate to: **https://localhost:5001**

---

## Step 6: Test Authentication

### 6.1 Sign In

1. You should see the login page
2. Click **"Sign in with Microsoft"**
3. You'll be redirected to Microsoft's login page
4. Sign in with your Microsoft Account
5. After successful authentication, you'll be redirected back to the application

### 6.2 Verify Access

- Your email should be displayed in the header
- You should be able to access the Entries page
- Try adding a new entry to test functionality

### 6.3 Test Access Control

Try signing in with a different Microsoft Account (one that's NOT in `users.json`):
- You should see an error: "Your Microsoft account is not authorized for this app"
- This confirms access control is working correctly

---

## Troubleshooting

### Problem: "Microsoft sign-in failed"

**Possible Causes:**
- ClientId or ClientSecret is incorrect or not set
- Redirect URI in Azure doesn't match application URL

**Solutions:**
1. Verify User Secrets are set correctly:
   ```bash
   dotnet user-secrets list
   ```
2. Check Azure Portal ? App registrations ? Your app ? Authentication
3. Ensure Redirect URI is: `https://localhost:5001/signin-oidc`

---

### Problem: "Your Microsoft account is not authorized for this app"

**Cause:** Your email is not in the allowed users list.

**Solution:**
1. Open `App_Data/users.json`
2. Add your exact Microsoft Account email
3. Use lowercase letters
4. **No application restart needed** - changes are loaded dynamically

---

### Problem: "AADSTS50011: The reply URL specified in the request does not match"

**Cause:** Redirect URI in Azure doesn't match the local URL.

**Solution:**
1. Go to: Azure Portal ? App registrations ? Your app
2. Click: Authentication ? Add a platform ? Web
3. Add: `https://localhost:5001/signin-oidc`
4. If using a different port, also add: `https://localhost:7001/signin-oidc`

---

### Problem: SSL Certificate Not Trusted

**Solution:**
```bash
dotnet dev-certs https --trust
```

If this doesn't work, try:
```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

---

### Problem: Port 5001 Already in Use

**Solution A: Use a different port**

Run with a specific port:
```bash
dotnet run --urls "https://localhost:7001;http://localhost:7000"
```

**Remember to update Redirect URI in Azure Portal!**

**Solution B: Find and stop the conflicting process**

Windows:
```powershell
netstat -ano | findstr :5001
taskkill /PID <process-id> /F
```

Linux/Mac:
```bash
lsof -i :5001
kill -9 <process-id>
```

---

## File Structure Overview

```
SecretsManager/
??? appsettings.json              (DO NOT commit - in .gitignore)
??? appsettings.json.example      (Template - commit this)
??? appsettings.Production.json   (DO NOT commit - in .gitignore)
?
??? App_Data/
?   ??? users.json                (DO NOT commit - in .gitignore)
?   ??? users.json.example        (Template - commit this)
?   ??? entries.json              (Generated at runtime)
?   ??? access.json               (Generated at runtime)
?
??? Controllers/
??? Models/
??? Services/
??? Views/
?
??? docs/
    ??? README.md
    ??? Local-Setup-Guide.md      (This file)
    ??? Azure-App-Registration-Guide.md
    ??? Deployment-Guide.md
```

---

## Pre-Commit Checklist

Before committing your code, verify:

- [ ] `appsettings.json` is NOT staged for commit
- [ ] `App_Data/users.json` is NOT staged for commit
- [ ] Only `.example` files are committed
- [ ] No Client Secret in any committed file
- [ ] `.gitignore` is up to date

**Check what will be committed:**
```bash
git status
git diff --cached
```

---

## Useful Commands

### NuGet Packages
```bash
# Restore packages
dotnet restore

# Add a package
dotnet add package PackageName

# List packages
dotnet list package
```

### User Secrets
```bash
# List all secrets
dotnet user-secrets list

# Set a secret
dotnet user-secrets set "Key" "Value"

# Remove a secret
dotnet user-secrets remove "Key"

# Clear all secrets
dotnet user-secrets clear
```

### Build and Run
```bash
# Build
dotnet build

# Run
dotnet run

# Run with specific environment
dotnet run --environment Development

# Publish
dotnet publish -c Release
```

---

## Next Steps

After successfully running locally:

1. **Test all features** - Ensure everything works as expected
2. **Review code** - Understand how authentication is implemented
3. **Read deployment guide** - `docs/Deployment-Guide.md` for production deployment
4. **Commit your changes** - Follow the pre-commit checklist above

---

## Getting Help

If you encounter issues:

1. Check this guide's Troubleshooting section
2. Review `docs/Azure-App-Registration-Guide.md`
3. Check application logs
4. Verify Azure Portal configuration
5. Ensure all required packages are installed

---

## Additional Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Microsoft Identity Platform](https://docs.microsoft.com/en-us/azure/active-directory/develop/)
- [.NET CLI Reference](https://docs.microsoft.com/en-us/dotnet/core/tools/)

---

**Version**: 1.0  
**Last Updated**: 2024  
**For**: SecretsManager - Local Development Setup
