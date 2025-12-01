# Application Configuration Templates

This directory contains template configuration files for the Uchat Server application. These templates demonstrate the required configuration structure and support environment variable overrides.

## Configuration Files

- `appsettings.Development.template.json` - Development environment configuration template
- `appsettings.Test.template.json` - Test environment configuration template

## Usage

### 1. Copy Templates to Server Project

Copy the appropriate template to your Uchat.Server project directory:

```bash
# For development
cp config/appsettings.Development.template.json src/Uchat.Server/appsettings.Development.json

# For testing
cp config/appsettings.Test.template.json src/Uchat.Server/appsettings.Test.json
```

### 2. Configure Database Connections

#### Using Environment Variables (Recommended for Production)

Set environment variables to override default values:

```bash
# Oracle
export ORACLE_CONNECTION_STRING="User Id=myuser;Password=mypassword;Data Source=myhost:1521/XEPDB1"

# MongoDB
export MONGODB_CONNECTION_STRING="mongodb://username:password@localhost:27017"
export MONGODB_DATABASE="uchat_production"

# Redis
export REDIS_CONNECTION_STRING="myhost:6379,password=mypassword,ssl=true"

# JWT
export JWT_SECRET_KEY="YourVeryLongAndSecureSecretKeyHere123456789!"
export JWT_ISSUER="uchat-server-production"
export JWT_AUDIENCE="uchat-client-production"
```

#### Direct Configuration (Development Only)

Edit the configuration file directly and replace placeholder values:

```json
{
  "Oracle": {
    "ConnectionString": "User Id=uchat_dev;Password=dev_password;Data Source=localhost:1521/XEPDB1"
  }
}
```

**⚠️ Warning**: Never commit real credentials to version control!

## Configuration Sections

### Serilog

Configures structured logging:

- **MinimumLevel**: Controls log verbosity
- **WriteTo**: Defines log outputs (Console, File, etc.)
- **Enrich**: Adds contextual information to logs

### Oracle

Oracle database connection settings:

- **ConnectionString**: Oracle connection string (supports TNS names or EZ Connect)
- **CommandTimeout**: SQL command timeout in seconds (1-300)
- **MaxRetryAttempts**: Number of retry attempts for transient failures (0-5)

### MongoDB

MongoDB connection settings:

- **ConnectionString**: MongoDB connection string (supports authentication, replica sets, SSL)
- **DatabaseName**: Name of the MongoDB database
- **ConnectionTimeout**: Connection timeout in seconds (1-60)
- **ServerSelectionTimeout**: Server selection timeout in seconds (1-60)

### Redis

Redis cache connection settings:

- **ConnectionString**: Redis connection string (supports password, SSL)
- **DefaultDatabase**: Default Redis database index (-1 to 15, -1 = use default)
- **ConnectTimeout**: Connection timeout in milliseconds (1000-30000)
- **SyncTimeout**: Synchronous operation timeout in milliseconds (1000-30000)
- **AbortOnConnectFail**: Whether to abort if initial connection fails

### JWT

JWT token configuration:

- **SecretKey**: Secret key for signing tokens (minimum 32 characters)
- **Issuer**: Token issuer identifier
- **Audience**: Token audience identifier
- **ExpirationMinutes**: Access token expiration time (1-1440 minutes)
- **RefreshTokenExpirationDays**: Refresh token expiration time (1-90 days)

## Environment Variable Substitution

The application uses the format `${VARIABLE_NAME:default_value}` to support environment variable overrides:

- If the environment variable exists, its value is used
- If not, the default value after the colon is used

Example:
```json
"ConnectionString": "${ORACLE_CONNECTION_STRING:User Id=default;Password=default;Data Source=localhost:1521/XEPDB1}"
```

## Security Best Practices

1. **Never commit credentials** to version control
2. **Use environment variables** for sensitive configuration in production
3. **Rotate credentials regularly**
4. **Use strong passwords** (minimum 16 characters)
5. **Enable SSL/TLS** for all database connections in production
6. **Use secret management services** (Azure Key Vault, AWS Secrets Manager, etc.) in production
7. **Keep JWT secret keys secure** and at least 32 characters long

## Validation

All configuration options are validated at application startup using `IValidateOptions`:

- Missing required values will cause the application to fail at startup
- Invalid ranges will be rejected with descriptive error messages
- Startup validation ensures configuration errors are caught early

## Connection String Formats

### Oracle

```
User Id=username;Password=password;Data Source=host:port/service_name
```

Or with TNS names:
```
User Id=username;Password=password;Data Source=tns_name
```

### MongoDB

```
mongodb://username:password@host:port/database?authSource=admin
```

With replica set:
```
mongodb://user:pass@host1:27017,host2:27017,host3:27017/database?replicaSet=rs0
```

### Redis

Simple:
```
localhost:6379
```

With password:
```
host:6379,password=mypassword
```

With SSL:
```
host:6379,password=mypassword,ssl=true,abortConnect=false
```

## Troubleshooting

### Configuration Not Loading

Ensure the configuration file matches the environment:
- Development: `appsettings.Development.json`
- Production: `appsettings.Production.json`
- Test: `appsettings.Test.json`

### Environment Variables Not Working

Check that environment variables are set in the correct scope:
```bash
# Check if variable is set
echo $ORACLE_CONNECTION_STRING

# Set permanently (add to ~/.bashrc or ~/.profile)
export ORACLE_CONNECTION_STRING="..."
```

### Validation Errors

Read the error message carefully - it will indicate which configuration value is invalid and why. Common issues:
- Connection string is empty or null
- Numeric values out of allowed range
- JWT secret key too short (< 32 characters)

## Additional Resources

- [ASP.NET Core Configuration](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Serilog Configuration](https://github.com/serilog/serilog-settings-configuration)
- [Oracle Connection Strings](https://www.connectionstrings.com/oracle/)
- [MongoDB Connection Strings](https://docs.mongodb.com/manual/reference/connection-string/)
- [Redis Configuration](https://stackexchange.github.io/StackExchange.Redis/Configuration)
