# Oracle Database Scripts

This directory contains Oracle SQL scripts for creating and managing the Uchat database schema.

## Execution Order

The scripts must be executed in the following order:

1. **01_create_users_table.sql** - Creates the `users` table with constraints and indexes

## Prerequisites

- Oracle Database 12c or later
- User with CREATE TABLE, CREATE INDEX, and CREATE TRIGGER privileges
- Connection credentials configured via environment variables (see `scripts/powershell/.env.template`)

## Manual Execution

To manually execute these scripts using SQL*Plus:

```bash
sqlplus username/password@connection_string @01_create_users_table.sql
```

## Automated Execution

Use the PowerShell provisioning script for automated execution:

```powershell
cd scripts/powershell
.\Provision-Database.ps1
```

See `scripts/powershell/README.md` for more details.

## Schema Overview

### users
Stores user account information including authentication credentials, profile data, and status.

**Key Features:**
- Unique constraints on username and email
- Status field (deprecated - now stored in Redis instead of Oracle)
- Automatic timestamp tracking via triggers
- Indexes on frequently queried columns

**Note:** The `status` field in the `users` table is deprecated. User online/offline status is now stored in Redis for better performance and real-time updates. The field remains in the schema for backward compatibility but is not read or written by the application.

### chats
Stores chat conversation metadata (direct messages, groups, channels).

**Key Features:**
- Support for three chat types (DirectMessage, Group, Channel)
- Foreign key to chat creator
- Automatic last_message_at updates via trigger
- Many-to-many relationship with users via `chat_participants`

### chat_participants
Junction table for chat membership (many-to-many relationship).

### messages
Stores individual chat messages with support for replies and soft deletes.

**Key Features:**
- Foreign keys to chat and sender
- Support for message replies (self-referencing FK)
- Soft delete via is_deleted flag
- Message status tracking (Sent, Delivered, Read, Failed)
- Automatic audit logging via triggers

### message_edit_history
Audit log for all message edits, automatically populated by triggers.

### message_delete_log
Audit log for all message deletions, automatically populated by triggers.

## Security Considerations

- **No Hardcoded Credentials**: All connection strings must be provided via environment variables
- **Password Hashing**: User passwords must be hashed using BCrypt before storage
- **Audit Trails**: Edit and delete operations are automatically logged for compliance
- **Foreign Key Constraints**: Ensure referential integrity across tables
- **Cascading Deletes**: Configured on appropriate relationships to maintain data consistency

## Data Types

- **VARCHAR2**: Used for all string fields with appropriate sizes
- **NUMBER(1)**: Used for enum types and boolean flags
- **TIMESTAMP**: Used for all date/time fields with automatic timezone support

## Indexes

Indexes are created on:
- Primary keys (automatic)
- Foreign keys (for join performance)
- Frequently queried columns (username, email, status, created_at)
- Composite indexes for common query patterns (chat_id + created_at)

## Triggers

All triggers are created with FOR EACH ROW to ensure row-level execution:
- **trg_users_updated_at**: Auto-updates updated_at on user modifications
- **trg_chats_updated_at**: Auto-updates updated_at on chat modifications
- **trg_chats_last_message**: Updates last_message_at when messages are inserted
- **trg_messages_edit_history**: Logs all content changes to audit table
- **trg_messages_delete_log**: Logs all soft deletes with content backup

## Maintenance

### Cleanup Old Audit Logs

Consider implementing a periodic cleanup job for audit tables:

```sql
-- Delete edit history older than 90 days
DELETE FROM message_edit_history WHERE edited_at < SYSDATE - 90;

-- Delete delete logs older than 90 days
DELETE FROM message_delete_log WHERE deleted_at < SYSDATE - 90;
```

### Performance Monitoring

Monitor index usage and table statistics:

```sql
-- Check index usage
SELECT index_name, table_name, last_used 
FROM user_indexes 
WHERE table_name IN ('USERS', 'CHATS', 'MESSAGES');

-- Gather statistics
EXEC DBMS_STATS.GATHER_TABLE_STATS(USER, 'USERS');
EXEC DBMS_STATS.GATHER_TABLE_STATS(USER, 'CHATS');
EXEC DBMS_STATS.GATHER_TABLE_STATS(USER, 'MESSAGES');
```
