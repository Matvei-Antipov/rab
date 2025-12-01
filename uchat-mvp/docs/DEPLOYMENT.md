# Deployment Guide

This guide covers deployment procedures for the Uchat MVP application in various environments.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Environment Setup](#environment-setup)
3. [Database Provisioning](#database-provisioning)
4. [Server Deployment](#server-deployment)
5. [Client Distribution](#client-distribution)
6. [Configuration Management](#configuration-management)
7. [Production Considerations](#production-considerations)
8. [Monitoring and Maintenance](#monitoring-and-maintenance)
9. [Troubleshooting](#troubleshooting)

## Prerequisites

### System Requirements

#### Development Environment
- .NET 8 SDK
- Visual Studio 2022 / JetBrains Rider / VS Code
- Windows OS (for WPF client development)
- 8GB RAM minimum, 16GB recommended
- 10GB free disk space

#### Server Environment
- .NET 8 Runtime (ASP.NET Core)
- Linux (Ubuntu 20.04+) or Windows Server 2019+
- 4GB RAM minimum, 8GB recommended
- 20GB free disk space

#### Database Servers
- Oracle Database 12c or later (Express Edition supported)
- MongoDB 4.0 or later
- Redis 6.0 or later

### Software Dependencies

#### Oracle Client Tools
```bash
# Download Oracle Instant Client from oracle.com
# For Linux (Ubuntu/Debian):
wget https://download.oracle.com/otn_software/linux/instantclient/instantclient-basic-linux.x64-21.8.0.0.0dbru.zip
unzip instantclient-basic-linux.x64-21.8.0.0.0dbru.zip -d /opt/oracle
echo "/opt/oracle/instantclient_21_8" > /etc/ld.so.conf.d/oracle-instantclient.conf
ldconfig
```

#### MongoDB Shell
```bash
# Ubuntu/Debian
wget -qO - https://www.mongodb.org/static/pgp/server-6.0.asc | sudo apt-key add -
echo "deb [ arch=amd64,arm64 ] https://repo.mongodb.org/apt/ubuntu focal/mongodb-org/6.0 multiverse" | sudo tee /etc/apt/sources.list.d/mongodb-org-6.0.list
sudo apt-get update
sudo apt-get install -y mongodb-mongosh
```

#### Redis
```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install -y redis-server
sudo systemctl enable redis-server
sudo systemctl start redis-server
```

## Environment Setup

### 1. Clone Repository

```bash
git clone https://github.com/your-org/uchat-develop.git
cd uchat-develop/uchat-mvp
```

### 2. Configure Environment Variables

Copy the template and configure for your environment:

```bash
cd scripts/powershell
cp .env.template .env
```

Edit `.env` with your actual configuration:

```env
# Oracle Configuration
ORACLE_HOST=your-oracle-host
ORACLE_PORT=1521
ORACLE_SERVICE_NAME=XEPDB1
ORACLE_USER=uchat_admin
ORACLE_PASSWORD=secure_password_here

# MongoDB Configuration
MONGO_HOST=your-mongo-host
MONGO_PORT=27017
MONGO_DATABASE=uchat
MONGO_USER=uchat_admin
MONGO_PASSWORD=secure_password_here

# Redis Configuration
REDIS_HOST=your-redis-host
REDIS_PORT=6379
REDIS_PASSWORD=secure_password_here

# JWT Configuration
JWT_SECRET=your_jwt_secret_minimum_32_characters_long
JWT_ISSUER=uchat-server
JWT_AUDIENCE=uchat-client
JWT_EXPIRATION_MINUTES=60

# Server Configuration
SERVER_PORT=5000
SERVER_HOST=0.0.0.0
ASPNETCORE_ENVIRONMENT=Production
```

### 3. Secure Secrets

#### Option A: Environment Variables (Linux)

Add to `/etc/environment` or user profile:

```bash
export ORACLE_PASSWORD="your_secure_password"
export MONGO_PASSWORD="your_secure_password"
export REDIS_PASSWORD="your_secure_password"
export JWT_SECRET="your_jwt_secret"
```

#### Option B: Docker Secrets (Docker Swarm)

```bash
echo "your_oracle_password" | docker secret create oracle_password -
echo "your_mongo_password" | docker secret create mongo_password -
echo "your_redis_password" | docker secret create redis_password -
echo "your_jwt_secret" | docker secret create jwt_secret -
```

#### Option C: Azure Key Vault (Azure)

```bash
az keyvault create --name uchat-keyvault --resource-group uchat-rg
az keyvault secret set --vault-name uchat-keyvault --name oracle-password --value "your_secure_password"
az keyvault secret set --vault-name uchat-keyvault --name mongo-password --value "your_secure_password"
az keyvault secret set --vault-name uchat-keyvault --name redis-password --value "your_secure_password"
az keyvault secret set --vault-name uchat-keyvault --name jwt-secret --value "your_jwt_secret"
```

## Database Provisioning

### Automated Provisioning

Using PowerShell (cross-platform):

```powershell
# Install PowerShell (if not already installed)
# Ubuntu/Debian:
sudo apt-get install -y powershell

# Run provisioning script
cd scripts/powershell
pwsh ./Provision-Database.ps1
```

Using bash wrapper:

```bash
cd scripts/powershell
chmod +x provision-database.sh
./provision-database.sh
```

### Manual Provisioning

#### Oracle

Execute scripts in order using SQL*Plus:

```bash
cd config/oracle
sqlplus uchat_admin/password@localhost:1521/XEPDB1 @01_create_users_table.sql
sqlplus uchat_admin/password@localhost:1521/XEPDB1 @02_create_chats_table.sql
sqlplus uchat_admin/password@localhost:1521/XEPDB1 @03_create_messages_table.sql
sqlplus uchat_admin/password@localhost:1521/XEPDB1 @04_create_triggers.sql
```

#### MongoDB

```bash
cd config/mongodb
mongosh "mongodb://uchat_admin:password@localhost:27017/uchat?authSource=admin" --file create_user_preferences_collection.js
```

#### Redis

Verify connection:

```bash
redis-cli -h localhost -p 6379 -a your_password PING
```

### Seed Demo Data (Development Only)

```powershell
cd scripts/powershell
pwsh ./Seed-DemoData.ps1 -UserCount 20 -ChatCount 10 -MessageCount 100
```

Or using bash:

```bash
./seed-demo-data.sh --user-count 20 --chat-count 10 --message-count 100
```

## Server Deployment

### Build the Server

```bash
cd src/Uchat.Server
dotnet publish -c Release -o ../../publish/server
```

### Deployment Options

#### Option 1: Kestrel (Self-Hosted)

Create systemd service file `/etc/systemd/system/uchat-server.service`:

```ini
[Unit]
Description=Uchat Server
After=network.target

[Service]
Type=notify
User=uchat
WorkingDirectory=/opt/uchat/server
ExecStart=/usr/bin/dotnet /opt/uchat/server/Uchat.Server.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=uchat-server
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ORACLE_PASSWORD=secret
Environment=MONGO_PASSWORD=secret
Environment=REDIS_PASSWORD=secret
Environment=JWT_SECRET=secret

[Install]
WantedBy=multi-user.target
```

Deploy and start:

```bash
# Copy published files
sudo mkdir -p /opt/uchat/server
sudo cp -r publish/server/* /opt/uchat/server/
sudo chown -R uchat:uchat /opt/uchat/server

# Enable and start service
sudo systemctl daemon-reload
sudo systemctl enable uchat-server
sudo systemctl start uchat-server

# Check status
sudo systemctl status uchat-server
```

#### Option 2: Docker

Create `Dockerfile` in `src/Uchat.Server/`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Uchat.Server/Uchat.Server.csproj", "Uchat.Server/"]
COPY ["src/Uchat.Shared/Uchat.Shared.csproj", "Uchat.Shared/"]
RUN dotnet restore "Uchat.Server/Uchat.Server.csproj"
COPY src/ .
WORKDIR "/src/Uchat.Server"
RUN dotnet build "Uchat.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Uchat.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Uchat.Server.dll"]
```

Build and run:

```bash
# Build image
docker build -t uchat-server:latest -f src/Uchat.Server/Dockerfile .

# Run container
docker run -d \
  --name uchat-server \
  -p 5000:5000 \
  -e ORACLE_PASSWORD=secret \
  -e MONGO_PASSWORD=secret \
  -e REDIS_PASSWORD=secret \
  -e JWT_SECRET=secret \
  uchat-server:latest
```

#### Option 3: IIS (Windows)

1. Install IIS and ASP.NET Core Hosting Bundle
2. Create IIS site pointing to publish folder
3. Configure application pool (.NET CLR Version: No Managed Code)
4. Set environment variables in application pool settings

#### Option 4: Nginx Reverse Proxy

Install Nginx:

```bash
sudo apt-get install -y nginx
```

Create Nginx configuration `/etc/nginx/sites-available/uchat`:

```nginx
upstream uchat_backend {
    server localhost:5000;
}

server {
    listen 80;
    server_name uchat.example.com;

    location / {
        proxy_pass http://uchat_backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # WebSocket specific configuration
    location /ws {
        proxy_pass http://uchat_backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_read_timeout 86400;
        proxy_send_timeout 86400;
    }
}
```

Enable site:

```bash
sudo ln -s /etc/nginx/sites-available/uchat /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

## Client Distribution

### Build the Client

```bash
cd src/Uchat.Client
dotnet publish -c Release -r win-x64 --self-contained -o ../../publish/client
```

### Distribution Options

#### Option 1: ZIP Archive

```bash
cd publish/client
zip -r uchat-client-win-x64.zip .
```

#### Option 2: Windows Installer (MSI)

Use WiX Toolset or Advanced Installer to create MSI package.

#### Option 3: ClickOnce Deployment

Configure in Visual Studio:
1. Right-click Uchat.Client project
2. Select Publish
3. Configure ClickOnce settings
4. Publish to file share or web server

#### Option 4: Microsoft Store

Package as MSIX and submit to Microsoft Store.

### Client Configuration

Create `appsettings.json` in client directory:

```json
{
  "ServerUrl": "https://uchat.example.com",
  "WebSocketUrl": "wss://uchat.example.com/ws",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Configuration Management

### Server Configuration (appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Oracle": "User Id=${ORACLE_USER};Password=${ORACLE_PASSWORD};Data Source=${ORACLE_HOST}:${ORACLE_PORT}/${ORACLE_SERVICE_NAME}",
    "MongoDB": "mongodb://${MONGO_USER}:${MONGO_PASSWORD}@${MONGO_HOST}:${MONGO_PORT}/${MONGO_DATABASE}?authSource=admin",
    "Redis": "${REDIS_HOST}:${REDIS_PORT},password=${REDIS_PASSWORD},ssl=true,abortConnect=False"
  },
  "JwtSettings": {
    "Secret": "${JWT_SECRET}",
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}",
    "ExpirationMinutes": 60
  },
  "RateLimiting": {
    "Enabled": true,
    "RequestsPerMinute": 100,
    "MessageRateLimitPerMinute": 30
  },
  "Cors": {
    "AllowedOrigins": ["https://uchat.example.com"]
  }
}
```

### Environment-Specific Configuration

Create environment-specific files:
- `appsettings.Development.json`
- `appsettings.Staging.json`
- `appsettings.Production.json`

## Production Considerations

### Security Hardening

1. **Enable HTTPS**
   - Obtain SSL/TLS certificate (Let's Encrypt recommended)
   - Configure Nginx/IIS with SSL
   - Enforce HTTPS redirection

2. **Firewall Configuration**
   ```bash
   # Allow only necessary ports
   sudo ufw allow 22/tcp    # SSH
   sudo ufw allow 80/tcp    # HTTP
   sudo ufw allow 443/tcp   # HTTPS
   sudo ufw enable
   ```

3. **Database Security**
   - Use strong passwords (min 16 characters)
   - Enable SSL/TLS for database connections
   - Restrict database access to application servers only
   - Regular security patches

4. **Application Security**
   - Remove development/debugging features
   - Enable request logging
   - Configure CORS restrictively
   - Implement rate limiting
   - Use security headers

### Performance Tuning

1. **Database Optimization**
   - Verify all indexes are created
   - Enable query caching
   - Monitor slow queries
   - Regular ANALYZE/VACUUM operations

2. **Redis Configuration**
   ```conf
   maxmemory 2gb
   maxmemory-policy allkeys-lru
   save 900 1
   save 300 10
   save 60 10000
   appendonly yes
   ```

3. **Server Configuration**
   - Increase Kestrel connection limits
   - Configure thread pool sizing
   - Enable response compression
   - Configure connection pooling

### High Availability

1. **Database Replication**
   - Oracle: Data Guard or Active Data Guard
   - MongoDB: Replica sets with 3+ nodes
   - Redis: Sentinel or Redis Cluster

2. **Load Balancing**
   - Deploy multiple server instances
   - Use Nginx/HAProxy as load balancer
   - Configure sticky sessions for WebSocket

3. **Backup Strategy**
   - Automated daily backups
   - Point-in-time recovery enabled
   - Test restore procedures regularly
   - Off-site backup storage

## Monitoring and Maintenance

### Health Checks

Configure health check endpoints:

```csharp
// In Program.cs
builder.Services.AddHealthChecks()
    .AddOracle(connectionString)
    .AddMongoDb(mongoConnectionString)
    .AddRedis(redisConnectionString);

app.MapHealthChecks("/health");
```

### Logging

Configure Serilog for production:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/uchat/server-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

### Monitoring Tools

Recommended tools:
- Application Performance: Application Insights, New Relic
- Infrastructure: Prometheus + Grafana
- Uptime: UptimeRobot, Pingdom
- Error Tracking: Sentry, Raygun

### Maintenance Tasks

Regular maintenance checklist:

**Daily**:
- Monitor error logs
- Check disk space
- Verify backups completed

**Weekly**:
- Review performance metrics
- Check security advisories
- Update dependencies (if needed)

**Monthly**:
- Database maintenance (statistics, cleanup)
- Review and archive old logs
- Security audit
- Capacity planning review

**Quarterly**:
- Penetration testing
- Disaster recovery drill
- Performance testing
- Dependency updates

## Troubleshooting

### Common Issues

#### Database Connection Failures

```bash
# Test Oracle connection
sqlplus uchat_admin/password@host:port/service

# Test MongoDB connection
mongosh "mongodb://user:pass@host:port/db"

# Test Redis connection
redis-cli -h host -p port -a password PING
```

#### Server Won't Start

Check logs:
```bash
sudo journalctl -u uchat-server -n 100 --no-pager
```

Common causes:
- Port already in use
- Missing environment variables
- Database connectivity issues
- Permission problems

#### WebSocket Connection Failures

1. Check Nginx/reverse proxy configuration
2. Verify firewall allows WebSocket upgrades
3. Check server logs for authentication errors
4. Test with WebSocket client tools (wscat)

#### Performance Issues

1. Check database query performance
2. Monitor Redis memory usage
3. Review connection pool settings
4. Check for memory leaks
5. Analyze application metrics

### Debug Mode

Enable debug logging temporarily:

```bash
export ASPNETCORE_ENVIRONMENT=Development
sudo systemctl restart uchat-server
```

### Support Resources

- Documentation: `/docs` directory
- GitHub Issues: Repository issue tracker
- Logs: `/var/log/uchat/`
- Health Check: `https://your-server/health`
