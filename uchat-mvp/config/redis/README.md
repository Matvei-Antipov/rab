# Redis Setup and Configuration

This document outlines the Redis key structures, TTL policies, and configuration for the Uchat application.

## Overview

Redis is used for:
- Session management and JWT token storage
- Real-time presence tracking (online/offline status)
- Message queue for WebSocket connections
- Rate limiting and throttling
- Caching frequently accessed data

## Key Naming Conventions

All keys follow a consistent naming pattern: `uchat:{category}:{identifier}[:{subcategory}]`

### Key Structure Examples

- Sessions: `uchat:session:{sessionId}`
- User presence: `uchat:presence:{userId}`
- WebSocket connections: `uchat:ws:{userId}:{connectionId}`
- Rate limiting: `uchat:ratelimit:{userId}:{endpoint}`
- Cached user data: `uchat:cache:user:{userId}`
- Cached chat data: `uchat:cache:chat:{chatId}`
- Message queue: `uchat:queue:messages:{chatId}`

## Key Types and Structures

### 1. Session Storage

**Key Pattern**: `uchat:session:{sessionId}`  
**Type**: Hash  
**TTL**: 24 hours (86400 seconds)

**Fields**:
```
userId          - User ID associated with this session
username        - Username for quick access
createdAt       - ISO 8601 timestamp when session was created
lastActivity    - ISO 8601 timestamp of last activity
ipAddress       - IP address of the session
userAgent       - User agent string
```

**Example**:
```redis
HSET uchat:session:sess_abc123 userId "user_123" username "john_doe" createdAt "2024-01-01T12:00:00Z" lastActivity "2024-01-01T12:30:00Z"
EXPIRE uchat:session:sess_abc123 86400
```

### 2. JWT Token Blacklist

**Key Pattern**: `uchat:token:blacklist:{jti}`  
**Type**: String  
**TTL**: Token expiration time (typically 1 hour = 3600 seconds)

**Value**: Reason for blacklisting (e.g., "user_logout", "token_revoked")

**Example**:
```redis
SET uchat:token:blacklist:jti_xyz789 "user_logout" EX 3600
```

### 3. User Presence

**Key Pattern**: `uchat:presence:{userId}`  
**Type**: Hash  
**TTL**: 5 minutes (300 seconds) - refreshed on activity

**Fields**:
```
status          - online | away | offline | dnd
lastSeen        - ISO 8601 timestamp
connectionCount - Number of active WebSocket connections
```

**Example**:
```redis
HSET uchat:presence:user_123 status "online" lastSeen "2024-01-01T12:00:00Z" connectionCount "2"
EXPIRE uchat:presence:user_123 300
```

### 4. WebSocket Connections

**Key Pattern**: `uchat:ws:{userId}:{connectionId}`  
**Type**: Hash  
**TTL**: 1 hour (3600 seconds) - refreshed with heartbeat

**Fields**:
```
connectedAt     - ISO 8601 timestamp
lastHeartbeat   - ISO 8601 timestamp
chatIds         - Comma-separated list of subscribed chat IDs
```

**Example**:
```redis
HSET uchat:ws:user_123:conn_abc status "active" connectedAt "2024-01-01T12:00:00Z"
EXPIRE uchat:ws:user_123:conn_abc 3600
```

### 5. Rate Limiting

**Key Pattern**: `uchat:ratelimit:{userId}:{endpoint}`  
**Type**: String (counter)  
**TTL**: 1 minute (60 seconds) - sliding window

**Value**: Number of requests in the current window

**Example**:
```redis
INCR uchat:ratelimit:user_123:api_messages
EXPIRE uchat:ratelimit:user_123:api_messages 60
```

**Rate Limits**:
- Message send: 30 requests/minute
- API calls: 100 requests/minute
- Login attempts: 5 requests/5 minutes

### 6. Cached User Data

**Key Pattern**: `uchat:cache:user:{userId}`  
**Type**: Hash  
**TTL**: 15 minutes (900 seconds)

**Fields**: Serialized user profile data (JSON or individual fields)

**Example**:
```redis
HSET uchat:cache:user:user_123 data "{\"id\":\"user_123\",\"username\":\"john_doe\",\"displayName\":\"John Doe\"}"
EXPIRE uchat:cache:user:user_123 900
```

### 7. Cached Chat Data

**Key Pattern**: `uchat:cache:chat:{chatId}`  
**Type**: Hash  
**TTL**: 10 minutes (600 seconds)

**Fields**: Serialized chat metadata (JSON or individual fields)

**Example**:
```redis
HSET uchat:cache:chat:chat_456 data "{\"id\":\"chat_456\",\"type\":1,\"name\":\"Team Chat\"}"
EXPIRE uchat:cache:chat:chat_456 600
```

### 8. Message Delivery Queue

**Key Pattern**: `uchat:queue:messages:{chatId}`  
**Type**: List  
**TTL**: 1 hour (3600 seconds) - for offline message buffering

**Value**: Serialized message JSON objects

**Example**:
```redis
LPUSH uchat:queue:messages:chat_456 "{\"id\":\"msg_789\",\"content\":\"Hello\"}"
EXPIRE uchat:queue:messages:chat_456 3600
```

### 9. Typing Indicators

**Key Pattern**: `uchat:typing:{chatId}:{userId}`  
**Type**: String  
**TTL**: 5 seconds

**Value**: "typing"

**Example**:
```redis
SET uchat:typing:chat_456:user_123 "typing" EX 5
```

### 10. Unread Message Counters

**Key Pattern**: `uchat:unread:{userId}:{chatId}`  
**Type**: String (counter)  
**TTL**: 7 days (604800 seconds)

**Value**: Number of unread messages

**Example**:
```redis
INCR uchat:unread:user_123:chat_456
EXPIRE uchat:unread:user_123:chat_456 604800
```

## TTL Policies Summary

| Key Category | TTL | Reason |
|-------------|-----|--------|
| Sessions | 24 hours | Balance security and user experience |
| JWT Blacklist | Token expiry time | Match token lifetime |
| User Presence | 5 minutes | Quick expiry with active refresh |
| WebSocket Connections | 1 hour | Cleanup stale connections |
| Rate Limiting | 1-5 minutes | Sliding window for API throttling |
| Cached User Data | 15 minutes | Balance freshness and performance |
| Cached Chat Data | 10 minutes | Frequently changing data |
| Message Queue | 1 hour | Buffer for offline users |
| Typing Indicators | 5 seconds | Real-time ephemeral data |
| Unread Counters | 7 days | Persist across sessions |

## Redis Configuration

### Recommended redis.conf Settings

```conf
# Memory Management
maxmemory 2gb
maxmemory-policy allkeys-lru

# Persistence (for production)
save 900 1
save 300 10
save 60 10000
appendonly yes
appendfsync everysec

# Performance
tcp-keepalive 60
timeout 300

# Security
requirepass ${REDIS_PASSWORD}
bind 127.0.0.1
protected-mode yes
```

### Connection String Format

```
redis://localhost:6379
redis://:password@localhost:6379
redis://localhost:6379/0  # Database 0
```

## Common Redis Operations

### Session Management

```redis
# Create session
HSET uchat:session:sess_abc123 userId "user_123" username "john" createdAt "2024-01-01T12:00:00Z"
EXPIRE uchat:session:sess_abc123 86400

# Get session
HGETALL uchat:session:sess_abc123

# Update last activity
HSET uchat:session:sess_abc123 lastActivity "2024-01-01T13:00:00Z"
EXPIRE uchat:session:sess_abc123 86400

# Delete session (logout)
DEL uchat:session:sess_abc123
```

### Presence Tracking

```redis
# Set user online
HSET uchat:presence:user_123 status "online" lastSeen "2024-01-01T12:00:00Z" connectionCount "1"
EXPIRE uchat:presence:user_123 300

# Increment connection count
HINCRBY uchat:presence:user_123 connectionCount 1
EXPIRE uchat:presence:user_123 300

# Check if user is online
EXISTS uchat:presence:user_123

# Get all online users (use SET for efficiency)
SADD uchat:online:users user_123
EXPIRE uchat:online:users 60
SMEMBERS uchat:online:users
```

### Rate Limiting

```redis
# Check and increment rate limit
SET counter [GET uchat:ratelimit:user_123:api_messages]
IF counter < 30 THEN
    INCR uchat:ratelimit:user_123:api_messages
    EXPIRE uchat:ratelimit:user_123:api_messages 60
    RETURN OK
ELSE
    RETURN RATE_LIMITED
END
```

### Message Queue

```redis
# Add message to queue
LPUSH uchat:queue:messages:chat_456 "{\"id\":\"msg_789\",\"content\":\"Hello\"}"
EXPIRE uchat:queue:messages:chat_456 3600

# Get pending messages
LRANGE uchat:queue:messages:chat_456 0 -1

# Pop message from queue
RPOP uchat:queue:messages:chat_456
```

### Cache Operations

```redis
# Cache user data
HSET uchat:cache:user:user_123 data "{\"id\":\"user_123\",\"username\":\"john\"}"
EXPIRE uchat:cache:user:user_123 900

# Get cached data
HGET uchat:cache:user:user_123 data

# Invalidate cache
DEL uchat:cache:user:user_123
```

## Monitoring and Maintenance

### Health Check

```redis
PING
INFO stats
INFO memory
```

### Key Statistics

```redis
# Count keys by pattern
SCAN 0 MATCH uchat:session:* COUNT 1000

# Get memory usage of a key
MEMORY USAGE uchat:session:sess_abc123

# Check TTL
TTL uchat:session:sess_abc123
```

### Cleanup Operations

```redis
# Delete all sessions (careful in production!)
EVAL "return redis.call('del', unpack(redis.call('keys', 'uchat:session:*')))" 0

# Find keys without TTL (memory leaks)
SCAN 0 MATCH uchat:* COUNT 1000
# Then check each key: TTL <key>
```

## Security Best Practices

1. **Use Strong Passwords**: Set `requirepass` in redis.conf
2. **Bind to Localhost**: Use `bind 127.0.0.1` unless Redis is on a separate server
3. **Enable Protected Mode**: Set `protected-mode yes`
4. **Use TLS**: For production, enable Redis TLS/SSL
5. **Rename Dangerous Commands**: Rename or disable commands like FLUSHALL, FLUSHDB, CONFIG
6. **Regular Backups**: Use RDB and AOF persistence
7. **Monitor Access**: Use Redis ACL (Redis 6+) to limit user permissions

## Environment Variables

See `scripts/powershell/.env.template` for required Redis configuration:

```env
REDIS_HOST=localhost
REDIS_PORT=6379
REDIS_PASSWORD=your_secure_password
REDIS_DB=0
REDIS_SSL=false
```

## Performance Tuning

### Connection Pooling

Use connection pooling with these recommended settings:
- Min pool size: 5
- Max pool size: 50
- Connection timeout: 5 seconds
- Idle timeout: 60 seconds

### Pipeline Operations

For bulk operations, use Redis pipelining:

```csharp
// Example in C# with StackExchange.Redis
var batch = redis.CreateBatch();
batch.HashSetAsync("uchat:cache:user:user_123", "field", "value");
batch.KeyExpireAsync("uchat:cache:user:user_123", TimeSpan.FromMinutes(15));
batch.Execute();
```

### Monitoring Slow Commands

```redis
CONFIG SET slowlog-log-slower-than 10000  # 10ms
SLOWLOG GET 10
```

## Scaling Considerations

For high-traffic scenarios, consider:
- **Redis Cluster**: Horizontal scaling with sharding
- **Redis Sentinel**: High availability with automatic failover
- **Read Replicas**: Separate read and write operations
- **Separate Redis Instances**: Use different instances for sessions, cache, and queues
