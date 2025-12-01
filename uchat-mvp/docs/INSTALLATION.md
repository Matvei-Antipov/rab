# Uchat MVP Installation - Complete Guide

## Table of Contents
1. [System Requirements](#system-requirements)
2. [Installing Required Components](#installing-required-components)
3. [Database Setup](#database-setup)
4. [Application Configuration](#application-configuration)
5. [Database Deployment](#database-deployment)
6. [Running the Application](#running-the-application)
7. [Troubleshooting](#troubleshooting)

---

## System Requirements

### Required Components
- **Windows 10/11** (64-bit)
- **.NET 8.0 SDK** or higher
- **Oracle Database XE 21c** or higher
- **MongoDB Community Edition 7.0** or higher
- **Redis 7.0** or higher

### Optional Tools
- **SQL*Plus** (for Oracle management)
- **MongoDB Shell (mongosh)** (for MongoDB management)
- **redis-cli** (for Redis management)

---

## Installing Required Components

### 1. .NET 8.0 SDK

```powershell
# Download and install from the official website:
# https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --version
```

### 2. Oracle Database XE

```powershell
# Download Oracle Database XE 21c from the official website:
# https://www.oracle.com/database/technologies/xe-downloads.html

# After installation, run:
sqlplus / as sysdba
```

### 3. MongoDB Community Edition

```powershell
# Download MongoDB from the official website:
# https://www.mongodb.com/try/download/community

# Start MongoDB as a service:
net start MongoDB

# Verify connection:
mongosh
```

### 4. Redis

```powershell
# Download Redis for Windows:
# https://github.com/microsoftarchive/redis/releases

# Start Redis server:
redis-server

# In another terminal, verify:
redis-cli ping
# Expected response: PONG
```

---

## Database Setup

### Oracle Database - User Creation

> [!IMPORTANT]
> The `uchat_admin` user must be created before running the provisioning script.

1. **Connect to Oracle as SYSDBA:**

```sql
sqlplus / as sysdba
```

2. **Create the user:**

```sql
-- Connect to PDB (if using multi-tenant architecture)
ALTER SESSION SET CONTAINER = XEPDB1;

-- Create uchat_admin user
CREATE USER uchat_admin IDENTIFIED BY securepass111!
DEFAULT TABLESPACE USERS
TEMPORARY TABLESPACE TEMP
QUOTA UNLIMITED ON USERS;

-- Grant necessary privileges
GRANT CONNECT, RESOURCE TO uchat_admin;
GRANT CREATE SESSION TO uchat_admin;
GRANT CREATE TABLE TO uchat_admin;
GRANT CREATE VIEW TO uchat_admin;
GRANT CREATE SEQUENCE TO uchat_admin;
GRANT CREATE PROCEDURE TO uchat_admin;

-- Verify user creation
SELECT username FROM all_users WHERE username = 'UCHAT_ADMIN';
```

3. **Verify connection:**

```sql
-- Exit SYSDBA
exit

-- Connect as the new user
sqlplus uchat_admin/securepass111!@localhost:1521/XEPDB1
```

### MongoDB - User Creation

> [!NOTE]
> For local development, MongoDB can work without authentication. If you want to enable authentication, follow these steps.

**Option 1: Without authentication (for local development)**

```javascript
// Just ensure MongoDB is running
// In appsettings.json use:
// "ConnectionString": "mongodb://localhost:27017"
```

**Option 2: With authentication**

1. **Connect to MongoDB:**

```bash
mongosh
```

2. **Create an administrative user:**

```javascript
use admin

db.createUser({
  user: "uchat_admin",
  pwd: "your_secure_password",
  roles: [
    { role: "readWrite", db: "uchat" },
    { role: "dbAdmin", db: "uchat" }
  ]
})
```

3. **Update the connection string in `appsettings.json`:**

```json
"MongoDB": {
  "ConnectionString": "mongodb://uchat_admin:your_secure_password@localhost:27017",
  "DatabaseName": "uchat"
}
```

### Redis - Configuration

> [!NOTE]
> Redis does not require authentication by default for local use.

**For local development:**
- Redis will be available on `localhost:6379`
- Authentication is not required
- No additional configuration needed

**If authentication is needed:**

1. Edit `redis.conf`:

```conf
requirepass your_redis_password
```

2. Restart Redis

3. Update `appsettings.json`:

```json
"Redis": {
  "ConnectionString": "localhost:6379,password=your_redis_password"
}
```

---

## Application Configuration

### 1. Clone the Repository

```powershell
git clone <repository-url>
cd Uchat-Develop\uchat-mvp
```

### 2. Configure appsettings.json

Edit the configuration file:

**Path:** `src\Uchat.Server\appsettings.json`

```json
{
  "Oracle": {
    "ConnectionString": "User Id=uchat_admin;Password=securepass111!;Data Source=localhost:1521/XEPDB1",
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
  },
  "Jwt": {
    "SecretKey": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6",
    "Issuer": "UchatServer",
    "Audience": "UchatClient",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

> [!WARNING]
> **Important configuration notes:**
> - Ensure passwords match the created database users
> - For production, change `Jwt.SecretKey` to a unique secure key
> - Verify the correctness of ports and host names

---

## Database Deployment

### Running the Provisioning Script

> [!IMPORTANT]
> Ensure all three databases (Oracle, MongoDB, Redis) are running before executing the script.

```powershell
# Navigate to the scripts directory
cd scripts\powershell

# Run the provisioning script
.\Provision-Database.ps1
```

**Optional flags:**

```powershell
# Skip Oracle
.\Provision-Database.ps1 -SkipOracle

# Skip MongoDB
.\Provision-Database.ps1 -SkipMongo

# Skip Redis
.\Provision-Database.ps1 -SkipRedis

# Combine flags
.\Provision-Database.ps1 -SkipOracle -SkipRedis
```

### What the script does:

1. **Oracle:**
   - Creates tables from files in `config\oracle\*.sql`
   - Applies database schema

2. **MongoDB:**
   - Creates collections from files in `config\mongodb\*_config.json`
   - Configures validators and indexes

3. **Redis:**
   - Verifies Redis connection
   - Displays server information

---

## Running the Application

### 1. Build the Solution

```powershell
# From the project root directory
dotnet build uchat-mvp.sln
```

### 2. Run the Server

```powershell
# Navigate to the server directory
cd src\Uchat.Server

# Run the server
dotnet run
```

The server will start on the port specified in command-line arguments (default 5000).

### 3. Run the Client

```powershell
# In a new terminal, navigate to the client directory
cd src\Uchat.Client

# Run the client
dotnet run
```

---

## Troubleshooting

### Oracle

**Issue:** `ORA-01017: invalid username/password`

```powershell
# Solution: Verify password and ensure user is created
sqlplus uchat_admin/Blend100!@localhost:1521/XEPDB1
```

**Issue:** `ORA-12541: TNS:no listener`

```powershell
# Solution: Start Oracle listener
lsnrctl start
```

**Issue:** SQL*Plus not found

```powershell
# Solution: Install Oracle Instant Client and add to PATH
# https://www.oracle.com/database/technologies/instant-client/downloads.html
```

### MongoDB

**Issue:** `MongoServerSelectionTimeoutError`

```powershell
# Solution: Ensure MongoDB is running
net start MongoDB

# Or run manually:
mongod --dbpath C:\data\db
```

**Issue:** mongosh not found

```powershell
# Solution: Install MongoDB Shell
# https://www.mongodb.com/try/download/shell

# Or use the legacy mongo shell (if installed)
```

### Redis

**Issue:** `Connection refused` to Redis

```powershell
# Solution: Start Redis server
redis-server

# Verify connection:
redis-cli ping
```

**Issue:** redis-cli not found

```powershell
# Solution: Ensure Redis is installed and added to PATH
# Or specify the full path to redis-cli
```

### Provisioning Script

**Issue:** `Configuration file not found`

```powershell
# Solution: Ensure appsettings.json exists
Test-Path e:\uchat\Uchat-Develop\uchat-mvp\src\Uchat.Server\appsettings.json
```

**Issue:** `Failed to parse configuration file`

```powershell
# Solution: Check JSON for syntax errors
# Use a JSON validator or open the file in VS Code
```

### Common Issues

**Issue:** Ports in use

```powershell
# Check which ports are being used:
netstat -ano | findstr :5000
netstat -ano | findstr :1521
netstat -ano | findstr :27017
netstat -ano | findstr :6379

# Stop processes or change ports in configuration
```

---

## Additional Information

### Project Structure

```
uchat-mvp/
├── src/
│   ├── Uchat.Server/        # Backend application
│   │   └── appsettings.json # Main configuration file
│   └── Uchat.Client/        # WPF client
├── config/
│   ├── oracle/              # SQL scripts for Oracle
│   ├── mongodb/             # JSON configs for MongoDB
│   └── redis/               # Redis configuration
└── scripts/
    └── powershell/
        └── Provision-Database.ps1  # Provisioning script
```

### Useful Commands

```powershell
# Check versions of installed components
dotnet --version
sqlplus -version
mongosh --version
redis-server --version

# Clean solution
dotnet clean

# Restore dependencies
dotnet restore

# View server logs
cd src\Uchat.Server
dotnet run --verbosity detailed
```

### Next Steps

After successful installation, you can:
1. Create a user account
2. Start creating chats
3. Send messages
4. Attach files

---

## Support

If you encounter issues not covered in this guide:
1. Check application logs
2. Ensure all database services are running
3. Verify configuration correctness in `appsettings.json`
