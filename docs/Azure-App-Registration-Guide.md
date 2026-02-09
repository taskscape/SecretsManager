# Azure AD Application Registration and Authentication Configuration Guide

## Quick Start - Without Azure Registration (for local testing only)

**WARNING**: This option will likely NOT work correctly.  
Microsoft requires a registered application in Azure Portal. Proceed directly to the full instructions below.

---

## RECOMMENDED PATH: Azure Registration (30 minutes)

Proceed directly to **Step 1** below and register your application in Azure Portal.  
This ensures everything works correctly from the start!

**Quick reference**: See also `docs/Local-Setup-Guide.md` for developer setup steps.

---

## Deployment Checklist (full version with Azure)

- [ ] **Step 1**: Register application in Azure Portal
- [ ] **Step 2**: Configure Redirect URIs
- [ ] **Step 3**: Create Client Secret
- [ ] **Step 4**: Retrieve configuration data
- [ ] **Step 5**: Configure application (appsettings.json)
- [ ] **Step 6**: Local verification
- [ ] **Step 7**: Deploy to T01 server
- [ ] **Step 8**: Production server configuration
- [ ] **Step 9**: Final testing

---

## Step 1: Register Application in Azure Portal

### 1.1 Login to Azure Portal

1. Navigate to: https://portal.azure.com
2. Sign in with administrator account
3. In the search bar, type **"Azure Active Directory"** or **"Microsoft Entra ID"**
4. Click on **"Azure Active Directory"**

### 1.2 Create New Application Registration

1. In the left menu, select **"App registrations"**
2. Click **"+ New registration"** button
3. Fill in the form:
   - **Name**: `SecretsManager` (or any name you prefer)
   - **Supported account types**: Choose:
     - **"Accounts in this organizational directory only"** - for your organization only
     - **"Accounts in any organizational directory"** - for multiple organizations
     - **"Accounts in any organizational directory and personal Microsoft accounts"** - for personal and organizational accounts
   - **Redirect URI**: 
     - Platform: **Web**
     - URI: `https://localhost:5001/signin-oidc` (for local testing)
4. Click **"Register"**

---

## Step 2: Configure Redirect URIs

### 2.1 Add Additional URIs

1. After creating the application, go to **"Authentication"**
2. In **"Platform configurations"** -> **"Web"**, add the following URIs:
   - `https://localhost:5001/signin-oidc` (for local testing)
   - `https://localhost:7001/signin-oidc` (if using a different port)
   - `https://your-domain.com/signin-oidc` (production T01 server address)
3. Click **"Save"**

### 2.2 Configure Logout URL (optional)

1. In the same section, add **Front-channel logout URL**:
   - `https://localhost:5001/signout-callback-microsoft`
   - `https://your-domain.com/signout-callback-microsoft`

### 2.3 Configure ID Tokens

1. In the **"Implicit grant and hybrid flows"** section, check:
   - **ID tokens** (used for implicit and hybrid flows)
2. Click **"Save"**

---

## Step 3: Create Client Secret

### 3.1 Generate Secret

1. In the left menu, select **"Certificates & secrets"**
2. Click the **"Client secrets"** tab
3. Click **"+ New client secret"**
4. Enter:
   - **Description**: `SecretsManager Production Secret`
   - **Expires**: Select appropriate period (recommended: 12 or 24 months)
5. Click **"Add"**

### 3.2 Save Client Secret

**IMPORTANT**: The secret will be visible only once! Copy it immediately and save it in a secure location.

```
Client Secret Value: [copy this value]
```

---

## Step 4: Retrieve Configuration Data

### 4.1 Application (client) ID

1. Go to **"Overview"**
2. Copy the **"Application (client) ID"** value
   ```
   Application (client) ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
   ```

### 4.2 Directory (tenant) ID

1. In the same location, copy **"Directory (tenant) ID"**
   ```
   Directory (tenant) ID: yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
   ```

### 4.3 Summary of Required Values

After completing this step, you should have:
- **Tenant ID** (Directory ID)
- **Client ID** (Application ID)
- **Client Secret** (value generated in step 3)

---

## Step 5: Application Configuration (appsettings.json)

### 5.1 Configuration Structure

Create or update the `appsettings.json` file:

```json
{
  "Authentication": {
    "Microsoft": {
      "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "ClientSecret": "your-client-secret-from-step-3",
      "CallbackPath": "/signin-oidc"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 5.2 Production Environment Configuration

Create `appsettings.Production.json` file:

```json
{
  "Authentication": {
    "Microsoft": {
      "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "ClientSecret": "production-client-secret",
      "CallbackPath": "/signin-oidc"
    }
  }
}
```

### 5.3 Secure Secret Storage (User Secrets - for local development)

Instead of storing secrets in `appsettings.json`, use **User Secrets**:

```bash
dotnet user-secrets init
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "your-client-secret"
dotnet user-secrets set "Authentication:Microsoft:ClientId" "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
```

---

## Step 6: Local Verification

### 6.1 Install Required NuGet Packages

Ensure the project contains the following packages:

```bash
dotnet add package Microsoft.AspNetCore.Authentication.OpenIdConnect
dotnet add package Microsoft.Identity.Web
```

### 6.2 Program.cs Configuration

Verify that `Program.cs` contains the appropriate configuration:

```csharp
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Add Microsoft Identity authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("Authentication:Microsoft"));

builder.Services.AddControllersWithViews();
builder.Services.AddAuthorization();

// ... rest of configuration
```

### 6.3 Run Locally

```bash
dotnet run
```

Open your browser and navigate to `https://localhost:5001`

### 6.4 Test Authentication

1. Try signing in with a Microsoft account
2. You should be redirected to Microsoft sign-in page
3. After signing in, you will be redirected back to the application

---

## Step 7: Deploy to T01 Server

### 7.1 Publish Application

```bash
dotnet publish -c Release -o ./publish
```

### 7.2 Transfer Files to Server

1. Copy the contents of `./publish` folder to the server (via FTP, SCP, or CI/CD)
2. Place files in the appropriate directory (e.g., `/var/www/secretsmanager` or `C:\inetpub\secretsmanager`)

### 7.3 Configure Environment Variables on Server

**For Windows IIS:**

1. Open IIS Manager
2. Select your application
3. Open **Configuration Editor**
4. Navigate to `system.webServer/aspNetCore` section
5. In `environmentVariables` add:
   ```xml
   <environmentVariable name="Authentication__Microsoft__ClientSecret" value="production-client-secret" />
   <environmentVariable name="Authentication__Microsoft__ClientId" value="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" />
   ```

**For Linux/systemd:**

In service file (e.g., `/etc/systemd/system/secretsmanager.service`):

```ini
[Service]
Environment="Authentication__Microsoft__ClientSecret=production-client-secret"
Environment="Authentication__Microsoft__ClientId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
```

Alternatively, use `appsettings.Production.json` file on the server.

---

## Step 8: Production Server Configuration

### 8.1 Verify Redirect URI in Azure

1. Return to Azure Portal -> App registrations -> Your application
2. Go to **Authentication**
3. Ensure Redirect URI contains the production address:
   ```
   https://your-domain.com/signin-oidc
   ```

### 8.2 Configure HTTPS

**Microsoft authentication requires HTTPS!**

1. Ensure SSL certificate is properly installed
2. Verify the application works over HTTPS

### 8.3 Restart Application

```bash
# For systemd (Linux)
sudo systemctl restart secretsmanager

# For IIS (Windows)
iisreset
```

---

## Step 9: Final Testing

### 9.1 Testing Checklist

- [ ] Open browser and navigate to production address
- [ ] Click sign-in button
- [ ] Sign in with Microsoft account
- [ ] Verify redirection works correctly
- [ ] Verify user is signed in (username is displayed)
- [ ] Try signing out
- [ ] Test signing in again
- [ ] Check application logs for errors

### 9.2 Common Problems and Solutions

**Problem**: `AADSTS50011: The reply URL specified in the request does not match`
- **Solution**: Verify that Redirect URI in Azure exactly matches the application address

**Problem**: `AADSTS7000215: Invalid client secret is provided`
- **Solution**: Verify that Client Secret is correct and has not expired

**Problem**: Application cannot connect to Azure AD
- **Solution**: Check server internet connection and firewall settings

---

## Additional Notes

### Useful Links

- [Azure Portal](https://portal.azure.com)
- [Microsoft Identity Platform Docs](https://docs.microsoft.com/en-us/azure/active-directory/develop/)
- [Microsoft.Identity.Web Documentation](https://docs.microsoft.com/en-us/azure/active-directory/develop/microsoft-identity-web)

### Important Security Notes

1. **DO NOT** commit `appsettings.Production.json` with secrets to Git repository
2. Add to `.gitignore`:
   ```
   appsettings.Production.json
   appsettings.*.json
   ```
3. Use Azure Key Vault to store secrets in production (advanced)
4. Regularly rotate Client Secrets (every 6-12 months)

### Monitoring and Logs

After deployment, monitor:
- Application logs
- Azure AD Sign-in logs (Azure Portal -> Azure AD -> Sign-in logs)
- Authentication errors

---

## Completion

After completing all steps, the application should be fully configured and working with Microsoft Account authentication via Azure.

**Last Updated**: 2024  
**Version**: 1.0  
**For**: SecretsManager - Deployment Documentation
