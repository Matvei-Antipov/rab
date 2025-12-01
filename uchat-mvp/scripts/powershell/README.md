# PowerShell Provisioning and Seeding Scripts

This directory contains PowerShell scripts for database provisioning and demo data seeding for the Uchat MVP application.

## Scripts

### Provision-Database.ps1
Provisions the Oracle, MongoDB, and Redis databases by executing schema creation scripts.

**Usage**:
```powershell
# Provision all databases
.\Provision-Database.ps1

# Provision specific databases
.\Provision-Database.ps1 -SkipOracle
.\Provision-Database.ps1 -SkipMongo
.\Provision-Database.ps1 -SkipRedis

# Verbose output
.\Provision-Database.ps1 -Verbose
```

**What it does**:
1. Loads configuration from `src\Uchat.Server\appsettings.json`
2. Executes Oracle SQL scripts in order (01-04)
3. Creates MongoDB collections with schema validation
4. Verifies Redis connection

### Seed-DemoData.ps1
Seeds demo data into the databases for testing and development.

**Usage**:
```powershell
# Seed with default counts (10 users, 5 chats, 50 messages)
.\Seed-DemoData.ps1

# Seed with custom counts
.\Seed-DemoData.ps1 -UserCount 20 -ChatCount 10 -MessageCount 100

# Skip specific databases
.\Seed-DemoData.ps1 -SkipOracle
.\Seed-DemoData.ps1 -SkipMongo

# Verbose output
.\Seed-DemoData.ps1 -Verbose
```

**What it does**:
1. Generates demo users with realistic names and emails
2. Creates demo chats (direct messages, groups, channels)
3. Generates demo messages for the chats
4. Creates user preferences in MongoDB
5. Inserts all data into the respective databases

**Demo Credentials**:
- All demo users have the password: `Password123!`
- Sample usernames: `john.smith1`, `jane.johnson2`, etc.

## Prerequisites

### Required Software

1. **PowerShell**
   - Windows: PowerShell 5.1+ (built-in) or PowerShell 7+
   - Linux/macOS: PowerShell 7+ ([Install guide](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell))

2. **Oracle Client** (for Oracle provisioning)
   - SQL*Plus must be in your PATH
   - [Download Oracle Instant Client](https://www.oracle.com/database/technologies/instant-client.html)

3. **MongoDB Shell** (for MongoDB provisioning)
   - `mongosh` (recommended) or legacy `mongo` shell
   - [Download MongoDB Shell](https://www.mongodb.com/try/download/shell)

4. **Redis CLI** (optional, for Redis verification)
   - `redis-cli` should be in your PATH
   - [Download Redis](https://redis.io/download)

### Configuration

> **Note:** The `.env` file is **no longer required** for local deployment. Configuration is now read directly from `appsettings.json`.

Configure your database connections in `src\Uchat.Server\appsettings.json`:

```json
{
  "Oracle": {
    "ConnectionString": "User Id=uchat_admin;Password=Blend100!;Data Source=localhost:1521/XEPDB1",
    "CommandTimeout": 30,
    "MaxRetryAttempts": 3
  },
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "uchat",
    "ConnectionTimeout": 10,
    "ServerSelectionTimeout": 5
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "DefaultDatabase": -1,
    "ConnectTimeout": 5000,
    "SyncTimeout": 5000,
    "AbortOnConnectFail": false
  }
}
```

**Security**: Never commit `appsettings.json` with production credentials! Use `appsettings.Production.json` or environment-specific configuration files for production environments.

For complete installation instructions, see [INSTALLATION.md](../../docs/INSTALLATION.md).

## Running on Linux/macOS

### Option 1: PowerShell Core

Install PowerShell 7+ and run the scripts directly:

```bash
# Install PowerShell (Ubuntu/Debian)
sudo apt-get update
sudo apt-get install -y wget apt-transport-https software-properties-common
wget -q "https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb"
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y powershell

# Run scripts
pwsh ./Provision-Database.ps1
pwsh ./Seed-DemoData.ps1
```

### Option 2: Bash Wrappers

Use the provided bash wrapper scripts:

```bash
chmod +x provision-database.sh seed-demo-data.sh
./provision-database.sh
./seed-demo-data.sh
```

## Common Issues and Solutions

### SQL*Plus Not Found

**Error**: `sqlplus : The term 'sqlplus' is not recognized`

**Solution**:
1. Install Oracle Instant Client
2. Add to PATH:
   ```powershell
   # Windows
   $env:PATH += ";C:\oracle\instantclient_19_14"
   
   # Linux/macOS
   export PATH=$PATH:/opt/oracle/instantclient_19_14
   ```

### MongoDB Shell Not Found

**Error**: `mongosh : The term 'mongosh' is not recognized`

**Solution**:
1. Install MongoDB Shell
2. Add to PATH or use full path to the executable

### Connection Failures

**Error**: Database connection refused or authentication failed

**Solution**:
1. Verify database services are running:
   ```bash
   # Check Oracle
   lsnrctl status
   
   # Check MongoDB
   sudo systemctl status mongod
   
   # Check Redis
   redis-cli ping
   ```

2. Verify credentials in `appsettings.json`
3. Check firewall settings

### Permission Denied

**Error**: `Execution of scripts is disabled on this system`

**Solution** (Windows):
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## Script Execution Flow

### Provisioning Flow

```
Load appsettings.json
    ↓
Parse JSON configuration
    ↓
Execute Oracle scripts (01-04) in order
    ↓
Execute MongoDB collection creation
    ↓
Verify Redis connection
    ↓
Report results
```

### Seeding Flow

```
Load .env file
    ↓
Generate demo data
    ↓
Create SQL INSERT statements for Oracle
    ↓
Create JavaScript INSERT statements for MongoDB
    ↓
Execute against databases
    ↓
Report results and demo credentials
```

## Advanced Usage

### Custom Configuration

To use different configuration for different environments, create environment-specific appsettings files:

- `appsettings.Development.json`
- `appsettings.Production.json`
- `appsettings.Test.json`

The application will automatically load the appropriate file based on the `ASPNETCORE_ENVIRONMENT` variable.

### Custom Seed Data

To customize the seeding data, edit the generation functions in `Seed-DemoData.ps1`:

- `Generate-DemoUsers`: Modify user names, emails, etc.
- `Generate-DemoChats`: Change chat names and types
- `Generate-DemoMessages`: Update message templates

### CI/CD Integration

For automated deployments:

```yaml
# GitHub Actions example
- name: Provision Database
  run: pwsh ./scripts/powershell/Provision-Database.ps1
  env:
    ORACLE_PASSWORD: ${{ secrets.ORACLE_PASSWORD }}
    MONGO_PASSWORD: ${{ secrets.MONGO_PASSWORD }}
    REDIS_PASSWORD: ${{ secrets.REDIS_PASSWORD }}
```

## Security Best Practices

1. **Never commit `appsettings.json` files** with production credentials to version control
2. **Use environment-specific configuration files** for different environments
3. **Use strong passwords** for all database accounts
4. **Rotate secrets regularly** in production environments
5. **Use secret management tools** (Azure Key Vault, AWS Secrets Manager, etc.)
6. **Limit database user permissions** to minimum required
7. **Enable SSL/TLS** for database connections in production
8. **Use read-only credentials** for CI/CD pipelines when possible

## Troubleshooting

### Enable Verbose Output

```powershell
.\Provision-Database.ps1 -Verbose
.\Seed-DemoData.ps1 -Verbose
```

### Check Generated Scripts

Temporary SQL and JavaScript files are saved in `$env:TEMP` (Windows) or `/tmp` (Linux/macOS). Check these files if seeding fails.

### Validate Configuration

```powershell
# Read and display current configuration
$config = Get-Content "..\..\src\Uchat.Server\appsettings.json" | ConvertFrom-Json
$config.Oracle
$config.MongoDB
$config.Redis
```

## Support

For issues or questions:
1. Check the main documentation in `docs/`
2. Review database-specific READMEs in `config/oracle/`, `config/mongodb/`, `config/redis/`
3. Verify prerequisites are installed
4. Check database service status
5. Review error messages with `-Verbose` flag
