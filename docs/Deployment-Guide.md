# Production Deployment Guide - T01 Server

## Pre-Deployment Checklist

- [ ] Application runs successfully locally
- [ ] Application is registered in Azure Portal
- [ ] You have **Client ID**, **Client Secret**, **Tenant ID** from Azure
- [ ] You have access to T01 server (SSH/RDP/FTP)
- [ ] You know the production application URL (e.g., `https://secrets.company.com`)

---

## Part 1: Finalize Azure Portal Configuration

### Step 1.1: Add Production Redirect URI

1. Sign in to [Azure Portal](https://portal.azure.com)
2. Navigate to: **Azure Active Directory** ? **App registrations**
3. Find your application (e.g., `SecretsManager`)
4. Click **Authentication** in the left menu
5. In **Web** ? **Redirect URIs**, click **Add URI**
6. Add production URL:
   ```
   https://secrets.company.com/signin-oidc
   ```
   Replace `secrets.company.com` with your actual T01 server address

7. Click **Save**

### Step 1.2: Verify Configuration

Confirm you have:
- Redirect URI for localhost (development)
- Redirect URI for T01 server (production)
- ID tokens enabled (in "Implicit grant and hybrid flows" section)

---

## Part 2: Publish Application

### Step 2.1: Build Production Package

Open terminal in the project directory and execute:

```bash
dotnet publish -c Release -o ./publish
```

This creates a `./publish` folder with the deployable application.

### Step 2.2: Verify Package Contents

Check that `./publish` folder contains:
- `Passwords.dll`
- `appsettings.json`
- `web.config` (for IIS)
- `wwwroot` folder with CSS/JS
- **NO** `appsettings.Production.json` (will be added on server)

---

## Part 3: Transfer to T01 Server

### OPTION A: FTP/SFTP

1. Connect to T01 server via FTP (e.g., FileZilla, WinSCP)
2. Copy entire contents of `./publish` folder to server directory:
   - Windows IIS: `C:\inetpub\wwwroot\SecretsManager`
   - Linux: `/var/www/secretsmanager`

### OPTION B: Remote Desktop (RDP)

1. Connect to T01 server via RDP
2. Copy `./publish` folder to server (via shared folder or removable media)
3. Extract to target location

### OPTION C: SCP/SSH (Linux)

```bash
# Compress folder
tar -czf publish.tar.gz ./publish

# Transfer to server
scp publish.tar.gz user@server-t01:/tmp/

# Login and extract
ssh user@server-t01
cd /var/www/secretsmanager
tar -xzf /tmp/publish.tar.gz --strip-components=1
```

---

## Part 4: Configure on T01 Server

### Step 4.1: Create Production Configuration File

**On T01 server**, create `appsettings.Production.json` in application directory:

```json
{
  "Authentication": {
    "Microsoft": {
      "ClientId": "PASTE-YOUR-CLIENT-ID-FROM-AZURE",
      "ClientSecret": "PASTE-YOUR-CLIENT-SECRET-FROM-AZURE",
      "CallbackPath": "/signin-oidc"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**IMPORTANT**: This file must NOT be in Git repository!

### Step 4.2: Configure Allowed Users

On T01 server, edit `App_Data/users.json`:

```json
[
  {
    "username": "admin@company.com"
  },
  {
    "username": "john.smith@company.com"
  }
]
```

Add all users who should have access to the application.

---

## Part 5: Web Server Configuration

### OPTION A: Windows IIS

#### Step 5A.1: Install .NET 8 Hosting Bundle

1. Download [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Install on T01 server
3. Restart IIS:
   ```powershell
   iisreset
   ```

#### Step 5A.2: Create Application Pool

1. Open **IIS Manager**
2. Right-click **Application Pools** ? **Add Application Pool**
3. Settings:
   - **Name**: `SecretsManager`
   - **.NET CLR version**: `No Managed Code`
   - **Managed pipeline mode**: `Integrated`
4. Click **OK**
5. Right-click created pool ? **Advanced Settings**
6. Change **Start Mode** to `AlwaysRunning`
7. Change **Identity** to appropriate account (e.g., `ApplicationPoolIdentity`)

#### Step 5A.3: Create Website in IIS

1. In **IIS Manager**, right-click **Sites** ? **Add Website**
2. Settings:
   - **Site name**: `SecretsManager`
   - **Application pool**: `SecretsManager` (select from list)
   - **Physical path**: `C:\inetpub\wwwroot\SecretsManager`
   - **Binding**:
     - Type: `https`
     - Port: `443`
     - Host name: `secrets.company.com`
     - SSL certificate: Select your SSL certificate
3. Click **OK**

#### Step 5A.4: Configure Permissions

1. In **IIS Manager**, select your site
2. Right-click ? **Edit Permissions**
3. **Security** tab ? **Edit**
4. Add user **IIS AppPool\SecretsManager**
5. Permissions:
   - Read & execute
   - List folder contents
   - Read
   - Write (only for `App_Data` folder)

#### Step 5A.5: Configure Environment Variables (optional)

Alternative to `appsettings.Production.json`:

1. In **IIS Manager**, select your site
2. Open **Configuration Editor**
3. Select section: `system.webServer/aspNetCore`
4. Click `environmentVariables` ? `...`
5. Add:
   ```
   Name: ASPNETCORE_ENVIRONMENT
   Value: Production
   
   Name: Authentication__Microsoft__ClientId
   Value: [your-client-id]
   
   Name: Authentication__Microsoft__ClientSecret
   Value: [your-client-secret]
   ```
6. Click **OK** and **Apply**

#### Step 5A.6: Restart

```powershell
iisreset
```

---

### OPTION B: Linux + Nginx + Kestrel

#### Step 5B.1: Install .NET 8 Runtime

```bash
# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-8.0
```

#### Step 5B.2: Create systemd Service

Create file `/etc/systemd/system/secretsmanager.service`:

```ini
[Unit]
Description=SecretsManager ASP.NET Core App
After=network.target

[Service]
Type=notify
User=www-data
WorkingDirectory=/var/www/secretsmanager
ExecStart=/usr/bin/dotnet /var/www/secretsmanager/Passwords.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=secretsmanager
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://localhost:5000

# Authentication configuration
Environment="Authentication__Microsoft__ClientId=your-client-id"
Environment="Authentication__Microsoft__ClientSecret=your-client-secret"

[Install]
WantedBy=multi-user.target
```

#### Step 5B.3: Enable and Start Service

```bash
sudo systemctl daemon-reload
sudo systemctl enable secretsmanager
sudo systemctl start secretsmanager
sudo systemctl status secretsmanager
```

#### Step 5B.4: Configure Nginx as Reverse Proxy

Create file `/etc/nginx/sites-available/secretsmanager`:

```nginx
server {
    listen 80;
    server_name secrets.company.com;
    
    # Redirect HTTP -> HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name secrets.company.com;

    # SSL certificates
    ssl_certificate /etc/ssl/certs/your-certificate.crt;
    ssl_certificate_key /etc/ssl/private/your-key.key;

    # SSL parameters
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers on;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Logs
    access_log /var/log/nginx/secretsmanager_access.log;
    error_log /var/log/nginx/secretsmanager_error.log;
}
```

#### Step 5B.5: Enable Nginx Configuration

```bash
sudo ln -s /etc/nginx/sites-available/secretsmanager /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## Part 6: Verify Deployment

### Step 6.1: Local Test on Server

**For Windows:**
```powershell
Invoke-WebRequest -Uri https://localhost/Account/Login -UseBasicParsing
```

**For Linux:**
```bash
curl -k https://localhost:5000/Account/Login
```

Should return HTML of the login page.

### Step 6.2: Browser Test

1. Open browser
2. Navigate to: `https://secrets.company.com`
3. You should see the login page

### Step 6.3: Test Microsoft Authentication

1. Click **"Sign in with Microsoft"**
2. Sign in with Microsoft account (one that's in `users.json`)
3. Verify:
   - Redirection to Microsoft works
   - After signing in, you're redirected back to application
   - You're signed in (email displayed in header)
   - You can add/edit entries

### Step 6.4: Check Logs

**Windows (IIS):**
- Event Viewer ? Windows Logs ? Application
- Or: `C:\inetpub\logs\LogFiles\`

**Linux:**
```bash
sudo journalctl -u secretsmanager -f
```

---

## Part 7: Production Security

### Security Checklist

- [ ] HTTPS is enforced (no HTTP access)
- [ ] SSL certificate is valid
- [ ] `appsettings.Production.json` is NOT in Git repo
- [ ] `App_Data/users.json` is NOT in Git repo
- [ ] Client Secret is securely stored
- [ ] Server firewall allows only ports 80/443
- [ ] Application runs under dedicated user (not root/administrator)
- [ ] Logs are monitored
- [ ] Backup of `App_Data/*.json` files is configured

### Additional Security (optional)

#### A. Hide ASP.NET Version (IIS)

In `web.config` add:

```xml
<system.webServer>
  <security>
    <requestFiltering removeServerHeader="true" />
  </security>
  <httpProtocol>
    <customHeaders>
      <remove name="X-Powered-By" />
    </customHeaders>
  </httpProtocol>
</system.webServer>
```

#### B. Enforce HTTPS in Application

In `Program.cs` (should already be present):

```csharp
app.UseHttpsRedirection();
```

#### C. Rate Limiting for Login

Consider adding rate limiting for `/Account/MicrosoftLogin` endpoint.

---

## Part 8: Backup and Monitoring

### Automatic Data Backup

**Windows (Task Scheduler):**

Create PowerShell script `backup-secrets.ps1`:

```powershell
$source = "C:\inetpub\wwwroot\SecretsManager\App_Data"
$destination = "C:\Backups\SecretsManager\$(Get-Date -Format 'yyyy-MM-dd')"
Copy-Item -Path $source -Destination $destination -Recurse
```

**Linux (cron):**

```bash
# Add to crontab
0 2 * * * tar -czf /backups/secretsmanager-$(date +\%Y\%m\%d).tar.gz /var/www/secretsmanager/App_Data/
```

### Availability Monitoring

Use monitoring tools (e.g., UptimeRobot, Pingdom) to check:
- Page availability: `https://secrets.company.com/Account/Login`
- Expected status code: `200 OK`

---

## Production Troubleshooting

### Problem: "502 Bad Gateway" (Nginx)

**Cause**: .NET application is not running

**Solution:**
```bash
sudo systemctl status secretsmanager
sudo journalctl -u secretsmanager -n 50
```

### Problem: "503 Service Unavailable" (IIS)

**Cause**: Application Pool is stopped

**Solution:**
1. IIS Manager ? Application Pools
2. Find `SecretsManager` and click **Start**
3. Check Event Viewer for errors

### Problem: "AADSTS50011" Error After Deployment

**Cause**: Redirect URI in Azure doesn't match production URL

**Solution:**
1. Check exact production URL
2. In Azure Portal add: `https://your-server.com/signin-oidc`
3. Ensure protocol is `https://` not `http://`

### Problem: "User is not allowed"

**Cause**: User email is not in `App_Data/users.json`

**Solution:**
1. Check `App_Data/users.json` on server
2. Add user email (use exact same as in Microsoft Account)
3. No restart needed - file is read dynamically

---

## Post-Deployment Checklist

After deployment, verify:

- [ ] Application works over https://
- [ ] Microsoft sign-in works
- [ ] Users on list can sign in
- [ ] Users not on list are rejected
- [ ] Can add/edit entries
- [ ] Data is saved correctly
- [ ] Sign-out works
- [ ] Logs are being written
- [ ] Backup is configured
- [ ] Monitoring is active

---

## Support

If you encounter problems:

1. Check application logs
2. Check web server logs (IIS/Nginx)
3. Check Azure Portal ? App registrations ? Sign-in logs
4. Review documentation: `docs/Local-Setup-Guide.md`

---

**Last Updated**: 2024  
**Version**: 1.0  
**For**: SecretsManager - Production Deployment
