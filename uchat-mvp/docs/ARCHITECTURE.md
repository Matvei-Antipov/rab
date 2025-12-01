# Uchat MVP - Architecture Overview

## System Architecture

Uchat is a real-time chat application built with a modern, distributed architecture using .NET 8.

### High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                          Client Layer                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│   ┌───────────────────────────────────────────────────────┐    │
│   │         WPF Desktop Client (Uchat.Client)             │    │
│   │  - MVVM Architecture (CommunityToolkit.Mvvm)          │    │
│   │  - WebSocket Client                                    │    │
│   │  - Real-time Message Handling                          │    │
│   └───────────────────────────────────────────────────────┘    │
│                              │                                    │
│                              │ WebSocket + HTTP/REST              │
│                              ▼                                    │
└─────────────────────────────────────────────────────────────────┘
                               │
┌──────────────────────────────┼────────────────────────────────────┐
│                         Server Layer                              │
├──────────────────────────────┼────────────────────────────────────┤
│                              ▼                                     │
│   ┌───────────────────────────────────────────────────────┐      │
│   │      ASP.NET Core Web API (Uchat.Server)              │      │
│   │  - RESTful API Controllers                             │      │
│   │  - WebSocket Connection Manager                        │      │
│   │  - JWT Authentication                                  │      │
│   │  - Real-time Message Broadcasting                      │      │
│   └───────────────────────────────────────────────────────┘      │
│                              │                                     │
│              ┌───────────────┼───────────────┐                   │
│              │               │               │                    │
│              ▼               ▼               ▼                    │
└──────────────────────────────────────────────────────────────────┘
               │               │               │
┌──────────────┼───────────────┼───────────────┼──────────────────┐
│         Data Layer           │               │                   │
├──────────────┼───────────────┼───────────────┼──────────────────┤
│              ▼               ▼               ▼                   │
│  ┌────────────────┐  ┌──────────────┐  ┌──────────────┐        │
│  │  Oracle DB     │  │  MongoDB     │  │    Redis     │        │
│  │                │  │              │  │              │        │
│  │ - Users        │  │ - User       │  │ - Sessions   │        │
│  │ - Chats        │  │   Preferences│  │ - Presence   │        │
│  │ - Messages     │  │              │  │ - Cache      │        │
│  │ - Audit Logs   │  │              │  │ - Queues     │        │
│  └────────────────┘  └──────────────┘  └──────────────┘        │
└──────────────────────────────────────────────────────────────────┘
```

## Component Architecture

### 1. Uchat.Shared (Shared Library)

**Purpose**: Common code, models, and utilities shared between client and server.

**Key Components**:
- **Domain Models**: Core entities (User, Chat, Message)
- **DTOs**: Data transfer objects for API communication
- **Contracts**: WebSocket message contracts (typed messages)
- **Enums**: Status enumerations (UserStatus, ChatType, MessageStatus)
- **Abstractions**: Interfaces for dependency injection
- **Helpers**: Utility classes (password hashing, Redis keys, pagination)
- **Configuration**: Settings classes (JwtSettings, PasswordHashingOptions)
- **Serialization**: Source-generated JSON serializer context
- **Exceptions**: Custom exception types

**Design Patterns**:
- Repository pattern interfaces
- Dependency injection abstractions
- Value objects for configuration

### 2. Uchat.Server (ASP.NET Core Web API)

**Purpose**: Backend server providing REST API and WebSocket support.

**Key Components**:

#### API Controllers
- **AuthController**: User registration, login, logout
- **UsersController**: User profile management
- **ChatsController**: Chat creation and management
- **MessagesController**: Message operations (send, edit, delete)

#### Services
- **AuthenticationService**: JWT token generation and validation
- **UserService**: User CRUD operations
- **ChatService**: Chat management and participants
- **MessageService**: Message handling and history
- **WebSocketService**: Real-time connection management

#### Repositories
- **OracleUserRepository**: User data persistence
- **OracleChatRepository**: Chat data persistence
- **OracleMessageRepository**: Message data persistence
- **MongoUserPreferencesRepository**: User preferences in MongoDB
- **RedisCacheRepository**: Caching and session management

#### Middleware
- **JwtAuthenticationMiddleware**: Token validation
- **WebSocketMiddleware**: WebSocket connection handling
- **ExceptionHandlingMiddleware**: Global error handling
- **RateLimitingMiddleware**: API throttling

**Design Patterns**:
- MVC/API pattern
- Repository pattern
- Dependency injection
- Middleware pipeline
- Observer pattern (WebSocket broadcasting)

### 3. Uchat.Client (WPF Desktop Application)

**Purpose**: Desktop client application for end users.

**Architecture**: MVVM (Model-View-ViewModel)

**Key Components**:

#### Views
- **LoginWindow**: User authentication
- **MainWindow**: Main chat interface
- **ChatListView**: List of user's chats
- **MessageListView**: Message history
- **UserProfileView**: User profile settings

#### ViewModels
- **LoginViewModel**: Login/registration logic
- **MainViewModel**: Application state and navigation
- **ChatListViewModel**: Chat list management
- **MessageListViewModel**: Message display and sending
- **UserProfileViewModel**: Profile editing

#### Services
- **ApiService**: HTTP client for REST API calls
- **WebSocketService**: WebSocket client for real-time updates
- **AuthenticationService**: Token storage and management
- **NotificationService**: Desktop notifications
- **LoggingService**: Application logging

**Design Patterns**:
- MVVM pattern
- Command pattern (RelayCommand)
- Observable collections
- Dependency injection
- Service locator

### 4. Uchat.Tests (Test Project)

**Purpose**: Unit and integration tests.

**Test Categories**:
- Unit tests for services
- Unit tests for repositories
- Integration tests for API endpoints
- WebSocket integration tests
- End-to-end tests

**Testing Stack**:
- xUnit for test framework
- FluentAssertions for readable assertions
- NSubstitute for mocking

## Data Architecture

### Database Schema

#### Oracle Database (Primary Data Store)

**Tables**:

1. **users**
   - Primary key: `id` (VARCHAR2)
   - Unique constraints: `username`, `email`
   - Indexes: username, email, status, created_at
   - Audit: created_at, updated_at, last_seen_at

2. **chats**
   - Primary key: `id` (VARCHAR2)
   - Foreign key: `created_by` → users(id)
   - Indexes: chat_type, created_by, last_message_at
   - Audit: created_at, updated_at

3. **chat_participants** (junction table)
   - Composite primary key: (chat_id, user_id)
   - Foreign keys: chat_id → chats(id), user_id → users(id)
   - Cascade delete for referential integrity

4. **messages**
   - Primary key: `id` (VARCHAR2)
   - Foreign keys: chat_id → chats(id), sender_id → users(id)
   - Self-referencing FK: reply_to_id → messages(id)
   - Indexes: chat_id, sender_id, created_at, composite (chat_id, created_at)
   - Soft delete: is_deleted flag

5. **message_edit_history** (audit table)
   - Tracks all message content changes
   - Automatically populated by trigger

6. **message_delete_log** (audit table)
   - Tracks all message deletions
   - Stores content backup

**Triggers**:
- Auto-update timestamps (updated_at)
- Auto-populate audit tables
- Update last_message_at on chats

#### MongoDB (Document Store)

**Collections**:

1. **user_preferences**
   - Document per user (userId as unique key)
   - Schema validation enabled
   - Flexible nested structure for preferences
   - TTL index: auto-delete after 180 days of inactivity
   - Fields: theme, language, notification settings, privacy settings, chat settings

**Advantages**:
- Schema flexibility for evolving preferences
- Fast read/write for user-specific settings
- JSON-like structure matches client models
- TTL for automatic cleanup

#### Redis (In-Memory Data Store)

**Key Structures**:

1. **Sessions**: Hash (userId, username, timestamps)
   - TTL: 24 hours
   - Pattern: `uchat:session:{sessionId}`

2. **User Presence**: Hash (status, lastSeen, connectionCount)
   - TTL: 5 minutes (refreshed on activity)
   - Pattern: `uchat:presence:{userId}`

3. **WebSocket Connections**: Hash (connection metadata)
   - TTL: 1 hour (refreshed with heartbeat)
   - Pattern: `uchat:ws:{userId}:{connectionId}`

4. **Rate Limiting**: Counter
   - TTL: 1-5 minutes (sliding window)
   - Pattern: `uchat:ratelimit:{userId}:{endpoint}`

5. **Cache**: Hash (serialized data)
   - TTL: 10-15 minutes
   - Pattern: `uchat:cache:{type}:{id}`

6. **Message Queue**: List (offline messages)
   - TTL: 1 hour
   - Pattern: `uchat:queue:messages:{chatId}`

**Advantages**:
- Sub-millisecond latency
- Automatic TTL expiration
- Atomic operations for counters
- Pub/Sub for real-time features

## Communication Protocols

### REST API (HTTP)

**Base URL**: `http://localhost:5000/api`

**Endpoints**:

```
POST   /auth/register          - Register new user
POST   /auth/login             - Login and get JWT token
POST   /auth/logout            - Logout and invalidate token
GET    /users/{id}             - Get user by ID
PUT    /users/{id}             - Update user profile
GET    /chats                  - Get user's chats
POST   /chats                  - Create new chat
GET    /chats/{id}             - Get chat details
POST   /chats/{id}/messages    - Send message
GET    /chats/{id}/messages    - Get message history
PUT    /messages/{id}          - Edit message
DELETE /messages/{id}          - Delete message
```

**Authentication**: Bearer JWT token in Authorization header

**Request/Response Format**: JSON

### WebSocket (Real-Time)

**Endpoint**: `ws://localhost:5000/ws`

**Authentication**: JWT token passed as query parameter or in first message

**Message Format**: JSON with type discriminator

**Contract Types** (see MESSAGE_CONTRACTS.md):
- UserJoinedContract
- UserLeftContract
- UserStatusChangedContract
- MessageSentContract
- MessageEditedContract
- MessageDeletedContract
- TypingIndicatorContract
- MessageDeliveredContract
- MessageReadContract
- ErrorContract

**Flow**:
1. Client establishes WebSocket connection
2. Client sends authentication message
3. Server validates token and associates connection with user
4. Server subscribes client to relevant chat channels
5. Bidirectional message exchange
6. Heartbeat to keep connection alive

## Security Architecture

### Authentication

**Method**: JWT (JSON Web Tokens)

**Token Structure**:
```json
{
  "sub": "user_id",
  "username": "john.doe",
  "email": "john@example.com",
  "jti": "token_unique_id",
  "iat": 1234567890,
  "exp": 1234571490
}
```

**Token Lifetime**: 60 minutes (configurable)

**Refresh Strategy**: 
- Tokens are not automatically refreshed
- Client must re-authenticate on expiration
- Consider implementing refresh tokens for production

**Token Storage**:
- Client: Secure storage (encrypted)
- Server: Blacklist in Redis for revoked tokens

### Password Security

**Hashing**: BCrypt with work factor 12

**Validation Rules**:
- Minimum 8 characters
- Must contain uppercase, lowercase, digit, special character
- Not in common password list

**Storage**: Only hashed password stored in database (password_hash column)

### Authorization

**Method**: Role-based access control (future enhancement)

**Current Implementation**: 
- User can only access their own data
- User must be chat participant to access chat/messages
- Message author or chat owner can delete messages

### API Security

**Rate Limiting**:
- General API: 100 requests/minute per user
- Message sending: 30 messages/minute per user
- Login attempts: 5 attempts/5 minutes per IP

**CORS**: Configurable allowed origins

**SSL/TLS**: Required in production (disabled in dev)

**Input Validation**:
- Model validation with data annotations
- XSS protection via encoding
- SQL injection protection via parameterized queries

### Data Security

**Encryption**:
- At rest: Database encryption (Oracle TDE, MongoDB encryption)
- In transit: TLS/SSL for all connections
- Sensitive fields: Additional encryption for passwords (BCrypt)

**Audit Logging**:
- Message edits tracked in message_edit_history
- Message deletes tracked in message_delete_log
- User actions logged via Serilog

**Privacy**:
- User preferences control visibility settings
- Soft delete for messages (content preserved in audit log)
- GDPR compliance considerations (right to be forgotten)

## Scalability Considerations

### Horizontal Scaling

**Stateless API Servers**:
- Multiple server instances behind load balancer
- JWT tokens allow any server to validate requests
- No server-side session state

**WebSocket Scaling**:
- Sticky sessions for WebSocket connections
- Redis Pub/Sub for cross-server message broadcasting
- Connection count tracking in Redis

**Database Scaling**:
- Oracle: Read replicas for read-heavy operations
- MongoDB: Sharding for user preferences
- Redis: Redis Cluster for high availability

### Vertical Scaling

**Database Optimization**:
- Proper indexing on frequently queried columns
- Composite indexes for common query patterns
- Partitioning for large tables (messages)

**Caching Strategy**:
- Redis cache for frequently accessed data
- Cache-aside pattern with TTL
- Cache invalidation on updates

**Connection Pooling**:
- Database connection pools
- Redis connection multiplexing
- HTTP client connection reuse

### Performance Optimization

**Message Pagination**: 
- Cursor-based pagination for message history
- Limit: 50 messages per page

**Lazy Loading**:
- User profiles loaded on demand
- Chat details fetched only when opened

**Batch Operations**:
- Bulk message delivery via WebSocket
- Batch status updates (read receipts)

**Compression**:
- WebSocket message compression
- HTTP response compression (Gzip)

## Monitoring and Observability

### Logging

**Framework**: Serilog

**Log Levels**:
- Error: Exceptions and critical issues
- Warning: Validation failures, rate limiting
- Information: API requests, WebSocket events
- Debug: Detailed execution flow (dev only)

**Log Sinks**:
- Console (development)
- File (production)
- Future: Application Insights, ELK stack

### Metrics

**Application Metrics**:
- Request count and latency
- WebSocket connection count
- Message throughput
- Cache hit/miss ratio

**Database Metrics**:
- Query execution time
- Connection pool utilization
- Table sizes and growth

**System Metrics**:
- CPU and memory usage
- Network I/O
- Disk I/O

### Health Checks

**Endpoints**:
- `/health` - Overall health status
- `/health/ready` - Readiness probe
- `/health/live` - Liveness probe

**Checks**:
- Database connectivity (Oracle, MongoDB, Redis)
- Disk space availability
- Memory usage

## Deployment Architecture

### Development Environment

```
Developer Machine
├── Oracle Express Edition (local)
├── MongoDB Community (local)
├── Redis (local)
├── Uchat.Server (IIS Express / Kestrel)
└── Uchat.Client (Visual Studio / Rider)
```

### Production Environment (Future)

```
┌─────────────────────────────────────────────┐
│              Load Balancer                  │
├─────────────────────────────────────────────┤
│  ┌─────────┐  ┌─────────┐  ┌─────────┐    │
│  │ Server1 │  │ Server2 │  │ Server3 │    │
│  └─────────┘  └─────────┘  └─────────┘    │
└─────────────────────────────────────────────┘
              │         │         │
       ┌──────┘         │         └──────┐
       ▼                ▼                 ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│   Oracle    │  │  MongoDB    │  │   Redis     │
│   Cluster   │  │  Replica    │  │   Cluster   │
└─────────────┘  └─────────────┘  └─────────────┘
```

## Technology Stack Summary

| Layer | Technology | Purpose |
|-------|------------|---------|
| Client | WPF (.NET 8) | Desktop UI |
| Client Framework | CommunityToolkit.Mvvm | MVVM implementation |
| Server | ASP.NET Core 8 | Web API and WebSocket |
| Primary DB | Oracle 12c+ | Relational data storage |
| Document DB | MongoDB 4.0+ | User preferences |
| Cache | Redis 6.0+ | Session, cache, presence |
| Auth | JWT | Stateless authentication |
| Logging | Serilog | Structured logging |
| Testing | xUnit + FluentAssertions | Unit/integration tests |
| Serialization | System.Text.Json | High-performance JSON |
| Password | BCrypt | Secure password hashing |

## Future Enhancements

### Phase 2
- File and media sharing
- Voice and video calls
- Message reactions and emoji
- Message search functionality
- User blocking and reporting

### Phase 3
- Mobile clients (Xamarin/MAUI)
- Web client (Blazor)
- End-to-end encryption
- Compliance features (GDPR, HIPAA)

### Phase 4
- AI-powered features (chatbots, translation)
- Analytics and insights
- Custom integrations and webhooks
- Enterprise features (SSO, LDAP)
