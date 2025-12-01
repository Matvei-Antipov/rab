# MongoDB Scripts

This directory contains MongoDB scripts for creating collections, indexes, and schema validation for the Uchat application.

## Collections

### user_preferences
Stores user-specific preferences and settings in a flexible JSON document structure.

**Schema Fields:**
- `userId` (string, required, unique) - Reference to user ID from Oracle database
- `theme` (string) - UI theme: 'light', 'dark', or 'auto'
- `language` (string) - Language preference in ISO 639-1 format (e.g., 'en', 'en-US')
- `notificationSettings` (object) - Notification preferences
  - `enableSound` (bool) - Enable notification sounds
  - `enableDesktop` (bool) - Enable desktop notifications
  - `enableEmail` (bool) - Enable email notifications
  - `muteUntil` (date) - Mute notifications until this timestamp
- `privacySettings` (object) - Privacy preferences
  - `showOnlineStatus` (bool) - Show online status to others
  - `showLastSeen` (bool) - Show last seen timestamp
  - `allowDirectMessages` (bool) - Allow DMs from non-contacts
  - `readReceipts` (bool) - Send read receipts
- `chatSettings` (object) - Chat interface preferences
  - `fontSize` (int, 10-24) - Font size for messages
  - `enterToSend` (bool) - Press Enter to send
  - `showTimestamps` (bool) - Show message timestamps
  - `compactMode` (bool) - Use compact display
- `customSettings` (object) - Additional custom key-value pairs
- `createdAt` (date, required) - Creation timestamp
- `updatedAt` (date, required) - Last update timestamp

## Prerequisites

- MongoDB 4.0 or later (for JSON Schema validation support)
- User with createCollection and createIndex privileges
- Connection credentials configured via environment variables

## Execution

### Using MongoDB Shell (mongosh)

```bash
mongosh "mongodb://localhost:27017/uchat" --file create_user_preferences_collection.js
```

### Using Legacy Mongo Shell

```bash
mongo uchat < create_user_preferences_collection.js
```

### Using PowerShell Provisioning Script

```powershell
cd scripts/powershell
.\Provision-Database.ps1
```

## Indexes

The following indexes are created on the `user_preferences` collection:

1. **idx_user_preferences_user_id** (unique)
   - Field: `userId`
   - Purpose: Ensure one preferences document per user, fast lookups by user ID

2. **idx_user_preferences_updated_at**
   - Field: `updatedAt`
   - Purpose: Efficient sorting and filtering by update time

3. **idx_user_preferences_user_updated**
   - Fields: `userId`, `updatedAt` (descending)
   - Purpose: Compound index for common query patterns

4. **idx_user_preferences_ttl** (TTL Index)
   - Field: `updatedAt`
   - Expiration: 180 days (15,552,000 seconds)
   - Purpose: Auto-delete orphaned preferences for deleted users

## Schema Validation

The collection uses MongoDB's JSON Schema validation with the following configuration:
- **validationLevel**: `moderate` - Applies to inserts and updates of valid documents
- **validationAction**: `error` - Rejects documents that don't match the schema

### Validation Rules

- `userId` is required and must be a string
- `theme` must be one of: 'light', 'dark', 'auto'
- `language` must match ISO 639-1 format (e.g., 'en', 'en-US')
- `fontSize` must be between 10 and 24
- All boolean fields must be actual booleans
- `createdAt` and `updatedAt` are required date fields

## Default Values

When creating a new user preferences document, use these defaults:

```javascript
{
    userId: "user_id_here",
    theme: "auto",
    language: "en",
    notificationSettings: {
        enableSound: true,
        enableDesktop: true,
        enableEmail: true,
        muteUntil: null
    },
    privacySettings: {
        showOnlineStatus: true,
        showLastSeen: true,
        allowDirectMessages: false,
        readReceipts: true
    },
    chatSettings: {
        fontSize: 14,
        enterToSend: true,
        showTimestamps: true,
        compactMode: false
    },
    customSettings: {},
    createdAt: new Date(),
    updatedAt: new Date()
}
```

## Common Operations

### Insert Default Preferences

```javascript
db.user_preferences.insertOne({
    userId: "user_123",
    theme: "auto",
    language: "en",
    notificationSettings: {
        enableSound: true,
        enableDesktop: true,
        enableEmail: true,
        muteUntil: null
    },
    privacySettings: {
        showOnlineStatus: true,
        showLastSeen: true,
        allowDirectMessages: false,
        readReceipts: true
    },
    chatSettings: {
        fontSize: 14,
        enterToSend: true,
        showTimestamps: true,
        compactMode: false
    },
    customSettings: {},
    createdAt: new Date(),
    updatedAt: new Date()
});
```

### Update Preferences

```javascript
db.user_preferences.updateOne(
    { userId: "user_123" },
    {
        $set: {
            theme: "dark",
            "notificationSettings.enableSound": false,
            updatedAt: new Date()
        }
    }
);
```

### Query Preferences

```javascript
// Get preferences for a user
db.user_preferences.findOne({ userId: "user_123" });

// Get all users with dark theme
db.user_preferences.find({ theme: "dark" });

// Get users who disabled notifications
db.user_preferences.find({
    "notificationSettings.enableDesktop": false
});
```

### Delete Preferences

```javascript
// Delete preferences for a specific user
db.user_preferences.deleteOne({ userId: "user_123" });
```

## TTL Index Behavior

The TTL (Time To Live) index automatically deletes documents where `updatedAt` is older than 180 days. This helps:
- Clean up preferences for deleted users
- Maintain database size
- Remove stale data

**Note**: The TTL monitor runs approximately once per minute, so deletions are not immediate.

To disable automatic deletion, drop the TTL index:

```javascript
db.user_preferences.dropIndex("idx_user_preferences_ttl");
```

## Security Considerations

- **No Hardcoded Credentials**: Connection strings must use environment variables
- **Field Validation**: Schema validation prevents invalid data
- **Unique userId**: Prevents duplicate preferences for the same user
- **Data Retention**: TTL index ensures compliance with data retention policies

## Monitoring

### Check Collection Stats

```javascript
db.user_preferences.stats();
```

### Check Index Usage

```javascript
db.user_preferences.aggregate([
    { $indexStats: {} }
]);
```

### View Schema Validation Rules

```javascript
db.getCollectionInfos({ name: "user_preferences" });
```

## Backup and Restore

### Export Collection

```bash
mongoexport --db=uchat --collection=user_preferences --out=user_preferences_backup.json
```

### Import Collection

```bash
mongoimport --db=uchat --collection=user_preferences --file=user_preferences_backup.json
```
