# Configuration and Database Scripts

This directory contains database scripts, configuration files, and documentation for the Uchat MVP application's data layer.

## Directory Structure

```
config/
├── oracle/                 # Oracle SQL scripts
│   ├── 01_create_users_table.sql
│   ├── 02_create_chats_table.sql
│   ├── 03_create_messages_table.sql
│   ├── 04_create_triggers.sql
│   └── README.md
├── mongodb/                # MongoDB scripts
│   ├── create_user_preferences_collection.js
│   └── README.md
├── redis/                  # Redis documentation
│   └── README.md
└── README.md              # This file
```

## Quick Start

### Automated Setup

The easiest way to provision all databases is using the PowerShell scripts:

```bash
cd ../scripts/powershell
cp .env.template .env
# Edit .env with your credentials
pwsh ./Provision-Database.ps1
```

See [scripts/powershell/README.md](../scripts/powershell/README.md) for details.

### Manual Setup

Execute scripts in order for each database.

## Database Overview

### Oracle Database (Primary Data Store)

**Purpose**: Stores core application data (users, chats, messages)

**Tables**:
- `users` - User accounts and authentication
- `chats` - Chat metadata
- `chat_participants` - Chat membership (junction table)
- `messages` - Chat messages
- `message_edit_history` - Audit log for message edits
- `message_delete_log` - Audit log for message deletions

**Key Features**:
- Foreign key constraints for referential integrity
- Indexes on frequently queried columns
- Triggers for automatic timestamp updates
- Triggers for audit logging
- Check constraints for data validation

**Execution Order**:
1. `01_create_users_table.sql` - Users table
2. `02_create_chats_table.sql` - Chats and participants
3. `03_create_messages_table.sql` - Messages and audit tables
4. `04_create_triggers.sql` - Triggers for automation

See [oracle/README.md](oracle/README.md) for detailed schema documentation.

### MongoDB (Document Store)

**Purpose**: Stores flexible user preferences and settings

**Collections**:
- `user_preferences` - User-specific settings with schema validation

**Key Features**:
- JSON Schema validation
- Flexible nested document structure
- TTL index for automatic cleanup (180 days)
- Unique index on userId
- Support for custom settings

**Benefits**:
- Schema flexibility for evolving requirements
- Fast document-based queries
- Native JSON support
- Horizontal scalability

See [mongodb/README.md](mongodb/README.md) for collection documentation.

### Redis (In-Memory Cache)

**Purpose**: Session management, caching, real-time presence, and message queues

**Key Structures**:
- Sessions - User session data (24h TTL)
- Presence - Online/offline status (5m TTL)
- WebSocket - Connection tracking (1h TTL)
- Rate Limiting - API throttling (1-5m TTL)
- Cache - Frequently accessed data (10-15m TTL)
- Queues - Message delivery buffers (1h TTL)

**Key Features**:
- Automatic expiration via TTL
- Sub-millisecond latency
- Atomic operations
- Pub/Sub for real-time events

See [redis/README.md](redis/README.md) for key patterns and TTL policies.

## Design Decisions

### Why Multiple Databases?

**Polyglot Persistence** - Each database is optimized for specific use cases:

1. **Oracle** - Relational data with ACID guarantees
   - Perfect for core entities (users, chats, messages)
   - Strong consistency and transactional integrity
   - Mature tooling and enterprise support
   - Complex queries and joins

2. **MongoDB** - Flexible document storage
   - Ideal for user preferences (evolving schema)
   - Fast document reads/writes
   - Native JSON support
   - Schema flexibility without migrations

3. **Redis** - High-performance caching and sessions
   - Sub-millisecond latency
   - Automatic expiration (TTL)
   - Perfect for ephemeral data
   - Pub/Sub for real-time features

### Data Distribution

| Data Type | Database | Reason |
|-----------|----------|--------|
| Users, Chats, Messages | Oracle | ACID, relationships, complex queries |
| User Preferences | MongoDB | Flexible schema, nested documents |
| Sessions, Cache | Redis | Speed, TTL, ephemeral nature |
| Presence Status | Redis | Real-time updates, short TTL |
| Rate Limiting | Redis | Atomic operations, performance |

## Execution Instructions

### Prerequisites

Before running scripts, ensure:
1. Databases are installed and running
2. User accounts have necessary privileges
3. Environment variables are configured (`.env` file)
4. Client tools are installed (sqlplus, mongosh, redis-cli)

### Oracle Scripts

```bash
cd oracle

# Execute each script in order
sqlplus username/password@connection @01_create_users_table.sql
sqlplus username/password@connection @02_create_chats_table.sql
sqlplus username/password@connection @03_create_messages_table.sql
sqlplus username/password@connection @04_create_triggers.sql

# Verify tables created
sqlplus username/password@connection
SQL> SELECT table_name FROM user_tables;
```

### MongoDB Scripts

```bash
cd mongodb

# Execute collection creation
mongosh "connection_string" --file create_user_preferences_collection.js

# Verify collection created
mongosh "connection_string"
> use uchat
> db.getCollectionNames()
> db.user_preferences.getIndexes()
```

### Redis Setup

Redis doesn't require schema creation, but you should verify the connection:

```bash
redis-cli -h host -p port -a password PING
# Should return: PONG

# Check Redis info
redis-cli -h host -p port -a password INFO server
```

## Security Considerations

### Credentials Management

**Never hardcode credentials!**

1. Use environment variables (`.env` file)
2. Use secret management services in production:
   - Azure Key Vault
   - AWS Secrets Manager
   - HashiCorp Vault

2. Rotate credentials regularly

3. Use least privilege principles:
   - Grant only necessary permissions
   - Use separate users for different environments
   - Restrict network access

### Connection Security

1. **Enable SSL/TLS** for all database connections:
   ```
   Oracle: Use TCPS protocol
   MongoDB: Add ?ssl=true to connection string
   Redis: Use SSL=true in connection string
   ```

2. **Firewall Rules**:
   - Restrict database access to application servers only
   - Use VPCs/private networks in cloud environments

3. **Password Strength**:
   - Minimum 16 characters
   - Mix of uppercase, lowercase, numbers, symbols
   - Avoid common passwords

### Data Protection

1. **Encryption at Rest**:
   - Oracle: Enable Transparent Data Encryption (TDE)
   - MongoDB: Enable encryption at rest
   - Redis: Use disk encryption at OS level

2. **Encryption in Transit**:
   - Always use SSL/TLS connections
   - Verify certificates

3. **Backups**:
   - Regular automated backups
   - Encrypted backup storage
   - Test restore procedures

## Maintenance

### Regular Tasks

**Daily**:
- Monitor disk space
- Check error logs
- Verify backups completed

**Weekly**:
- Review slow queries
- Check index usage
- Monitor connection counts

**Monthly**:
- Cleanup old audit logs (90+ days)
- Update database statistics (ANALYZE)
- Review security patches

**Quarterly**:
- Full database maintenance
- Capacity planning
- Performance tuning

### Cleanup Scripts

#### Oracle - Remove Old Audit Logs

```sql
-- Delete edit history older than 90 days
DELETE FROM message_edit_history 
WHERE edited_at < SYSDATE - 90;

-- Delete delete logs older than 90 days
DELETE FROM message_delete_log 
WHERE deleted_at < SYSDATE - 90;

COMMIT;
```

#### MongoDB - Manual Cleanup

The TTL index handles automatic cleanup, but you can manually delete if needed:

```javascript
// Delete preferences for inactive users (90+ days)
db.user_preferences.deleteMany({
    updatedAt: { $lt: new Date(Date.now() - 90 * 24 * 60 * 60 * 1000) }
});
```

#### Redis - Cleanup Operations

```bash
# Find keys without TTL (potential memory leaks)
redis-cli --scan --pattern "uchat:*" | while read key; do
    ttl=$(redis-cli TTL "$key")
    if [ "$ttl" = "-1" ]; then
        echo "Key without TTL: $key"
    fi
done

# Optional: Set TTL for keys without expiration
redis-cli EXPIRE "key_name" 86400
```

## Troubleshooting

### Oracle Connection Issues

```bash
# Test connection
sqlplus username/password@host:port/service

# Check listener
lsnrctl status

# Check tnsnames.ora configuration
cat $ORACLE_HOME/network/admin/tnsnames.ora
```

### MongoDB Connection Issues

```bash
# Test connection
mongosh "mongodb://user:pass@host:port/db?authSource=admin"

# Check MongoDB service
sudo systemctl status mongod

# Check logs
sudo tail -f /var/log/mongodb/mongod.log
```

### Redis Connection Issues

```bash
# Test connection
redis-cli -h host -p port -a password PING

# Check Redis service
sudo systemctl status redis-server

# Check logs
sudo tail -f /var/log/redis/redis-server.log

# Check memory usage
redis-cli INFO memory
```

### Common Errors

1. **"ORA-12154: TNS:could not resolve the connect identifier"**
   - Check tnsnames.ora configuration
   - Verify ORACLE_HOME environment variable

2. **"MongoServerError: Authentication failed"**
   - Verify username/password
   - Check authSource parameter
   - Ensure user has proper roles

3. **"NOAUTH Authentication required"**
   - Redis requires password
   - Use `-a password` flag or AUTH command

4. **"Table already exists"**
   - Scripts are not idempotent by default
   - Drop tables before re-running (use with caution!)

## Additional Resources

- **Oracle Documentation**: https://docs.oracle.com/en/database/
- **MongoDB Documentation**: https://docs.mongodb.com/
- **Redis Documentation**: https://redis.io/documentation
- **PowerShell Scripts**: [../scripts/powershell/README.md](../scripts/powershell/README.md)
- **Deployment Guide**: [../docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md)
- **Architecture Overview**: [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)
- **Security Guide**: [../docs/SECURITY.md](../docs/SECURITY.md)
