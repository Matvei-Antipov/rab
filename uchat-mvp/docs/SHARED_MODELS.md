# Shared Models Documentation

This document provides an overview of the shared models, DTOs, and utilities in the `Uchat.Shared` project.

## Architecture

The `Uchat.Shared` project follows Domain-Driven Design (DDD) principles and contains:

1. **Domain Entities** - Core business objects
2. **DTOs** - Data transfer objects for API communication
3. **Value Objects** - Immutable objects representing domain concepts
4. **Contracts** - WebSocket message definitions
5. **Abstractions** - Interfaces for dependency injection
6. **Helpers** - Utility classes for common operations
7. **Configuration** - POCO classes for settings

All code follows StyleCop conventions and uses dependency injection patterns.

## Domain Entities

Located in `Models/` directory.

### User

Represents a user account in the system.

**Properties:**
- `Id` (string) - Unique identifier
- `Username` (string) - Login username
- `Email` (string) - Email address
- `PasswordHash` (string) - BCrypt-hashed password
- `DisplayName` (string) - Display name shown to other users
- `AvatarUrl` (string?) - Optional avatar image URL
- `Status` (UserStatus) - Current online status
- `CreatedAt` (DateTime) - Account creation timestamp
- `UpdatedAt` (DateTime) - Last update timestamp
- `LastSeenAt` (DateTime?) - Last seen online timestamp

### Chat

Represents a chat conversation (direct message, group, or channel).

**Properties:**
- `Id` (string) - Unique identifier
- `Type` (ChatType) - Type of chat (DirectMessage, Group, Channel)
- `Name` (string?) - Chat name (for groups and channels)
- `Description` (string?) - Chat description
- `AvatarUrl` (string?) - Chat avatar/icon URL
- `ParticipantIds` (List\<string\>) - User IDs of participants
- `CreatedBy` (string) - User ID of creator
- `CreatedAt` (DateTime) - Creation timestamp
- `UpdatedAt` (DateTime) - Last update timestamp
- `LastMessageAt` (DateTime?) - Last message timestamp

### Message

Represents a message in a chat.

**Properties:**
- `Id` (string) - Unique identifier
- `ChatId` (string) - Chat this message belongs to
- `SenderId` (string) - Sender user ID
- `Content` (string) - Message text content
- `Status` (MessageStatus) - Delivery/read status
- `CreatedAt` (DateTime) - Message creation timestamp
- `EditedAt` (DateTime?) - Last edit timestamp
- `ReplyToId` (string?) - ID of message being replied to
- `IsDeleted` (bool) - Soft delete flag

## Enumerations

Located in `Enums/` directory.

### MessageStatus

```csharp
public enum MessageStatus
{
    Sent = 0,        // Message sent but not delivered
    Delivered = 1,   // Message delivered to recipient(s)
    Read = 2,        // Message read by recipient(s)
    Failed = 3       // Message failed to send
}
```

### UserStatus

```csharp
public enum UserStatus
{
    Offline = 0,       // User is offline
    Online = 1,        // User is online and available
    Away = 2,          // User is away from keyboard
    DoNotDisturb = 3   // User is in do not disturb mode
}
```

### ChatType

```csharp
public enum ChatType
{
    DirectMessage = 0,  // 1-on-1 conversation
    Group = 1,          // Group chat with multiple participants
    Channel = 2         // Public channel that users can join
}
```

## Data Transfer Objects (DTOs)

Located in `Dtos/` directory. DTOs are used for API requests and responses.

### UserDto

User information for API responses. Excludes sensitive fields like `PasswordHash`.

### ChatDto

Chat information for API responses.

### MessageDto

Message information for API responses.

### Request/Response DTOs

- **LoginRequest** - Authentication credentials
- **LoginResponse** - JWT token and user info
- **RegisterRequest** - New account information
- **RegisterResponse** - Registration result
- **SendMessageRequest** - New message data
- **PaginatedResponse\<T\>** - Generic paginated result wrapper

## Abstractions (Interfaces)

Located in `Abstractions/` directory.

### IClock

Provides system time abstraction for testability.

```csharp
public interface IClock
{
    DateTime UtcNow { get; }
}
```

**Implementation:** `SystemClock` in `Helpers/`

### IIdGenerator

Generates unique identifiers.

```csharp
public interface IIdGenerator
{
    string GenerateId();
}
```

**Implementation:** `GuidIdGenerator` in `Helpers/`

### IPasswordHasher

Handles password hashing and verification.

```csharp
public interface IPasswordHasher
{
    Task<string> HashPasswordAsync(string password);
    Task<bool> VerifyPasswordAsync(string password, string hash);
}
```

**Implementation:** `PasswordHasher` in `Helpers/` using BCrypt

## Helpers

Located in `Helpers/` directory.

### PasswordHasher

BCrypt-based password hashing with configurable work factor.

**Configuration:** Uses `PasswordHashingOptions` (default: 12 rounds, minimum: 10)

**Usage:**
```csharp
services.Configure<PasswordHashingOptions>(options => 
{
    options.WorkFactor = 12;
});
services.AddSingleton<IPasswordHasher, PasswordHasher>();

// In your code
var hash = await passwordHasher.HashPasswordAsync("mypassword");
var isValid = await passwordHasher.VerifyPasswordAsync("mypassword", hash);
```

### RedisKeys

Static helper for generating consistent Redis cache keys.

**Methods:**
- `UserSession(userId)` - User session data
- `UserStatus(userId)` - User online status
- `ChatMessages(chatId)` - Chat messages cache
- `UserChats(userId)` - User's active chats
- `UnreadCount(userId, chatId)` - Unread message count
- `TypingIndicator(chatId)` - Typing indicator state
- `RefreshToken(userId, tokenId)` - Refresh token storage

**Example:**
```csharp
var key = RedisKeys.UserSession("user-123");
// Returns: "uchat:user:session:user-123"
```

### PaginationHelper

Creates paginated responses.

**Usage:**
```csharp
var pageRequest = new PageRequest(page: 1, pageSize: 20);
var response = PaginationHelper.CreateResponse(items, totalCount, pageRequest);
```

## Configuration POCOs

Located in `Configuration/` directory.

### JwtSettings

JWT token configuration.

**Properties:**
- `SecretKey` (string) - Signing key (min 256 bits)
- `Issuer` (string) - Token issuer
- `Audience` (string) - Token audience
- `AccessTokenExpirationMinutes` (int) - Access token lifetime (default: 60)
- `RefreshTokenExpirationDays` (int) - Refresh token lifetime (default: 7)

### PasswordHashingOptions

Password hashing configuration.

**Properties:**
- `WorkFactor` (int) - BCrypt rounds (default: 12, minimum: 10)

## Value Objects

Located in `ValueObjects/` directory.

### PageRequest

Represents a pagination request with validation.

**Properties:**
- `Page` (int) - Page number (1-based, minimum: 1)
- `PageSize` (int) - Items per page (1-100, default: 20)
- `Skip` (int) - Items to skip for queries
- `Take` (int) - Items to take for queries

## Exceptions

Located in `Exceptions/` directory.

### ValidationException

Thrown when input validation fails. Contains a dictionary of field-level errors.

**Usage:**
```csharp
var errors = new Dictionary<string, string[]>
{
    ["Email"] = new[] { "Email is required", "Email format is invalid" },
    ["Password"] = new[] { "Password must be at least 8 characters" }
};
throw new ValidationException(errors);
```

### DomainException

Base exception for domain logic errors and business rule violations.

## Serialization

Located in `Serialization/` directory.

### UchatJsonSerializerContext

Source-generated JSON serializer context for System.Text.Json. Provides optimal performance for all shared DTOs and contracts.

### JsonSerializerOptionsFactory

Factory for pre-configured `JsonSerializerOptions`.

**Properties:**
- `Default` - General purpose options with camelCase naming
- `WebSocket` - Optimized for WebSocket messages (compact formatting)

**Usage:**
```csharp
using Uchat.Shared.Serialization;

var options = JsonSerializerOptionsFactory.Default;
var json = JsonSerializer.Serialize(dto, options);
var obj = JsonSerializer.Deserialize<UserDto>(json, options);
```

**Features:**
- camelCase property naming
- Null values omitted from output
- Case-insensitive deserialization
- Enum serialization as camelCase strings
- Source-generated for AOT compatibility

## WebSocket Contracts

Located in `Contracts/` directory.

All WebSocket messages implement `IWebSocketMessage` interface and have a `Type` property for discrimination.

See [MESSAGE_CONTRACTS.md](MESSAGE_CONTRACTS.md) for detailed documentation of all WebSocket message types.

## Dependency Injection Setup

Recommended service registration in your `Program.cs` or `Startup.cs`:

```csharp
using Uchat.Shared.Abstractions;
using Uchat.Shared.Configuration;
using Uchat.Shared.Helpers;

// Configuration
services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
services.Configure<PasswordHashingOptions>(configuration.GetSection("PasswordHashing"));

// Abstractions
services.AddSingleton<IClock, SystemClock>();
services.AddSingleton<IIdGenerator, GuidIdGenerator>();
services.AddSingleton<IPasswordHasher, PasswordHasher>();

// JSON serialization
services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.TypeInfoResolver = UchatJsonSerializerContext.Default;
    });
```

## Testing

All shared components are designed to be unit-testable:

- Use `IClock` instead of `DateTime.UtcNow` for time-dependent logic
- Use `IIdGenerator` instead of `Guid.NewGuid()` for ID generation
- Use `IPasswordHasher` for password operations

Example test setup:

```csharp
using NSubstitute;

var mockClock = Substitute.For<IClock>();
mockClock.UtcNow.Returns(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));

var mockIdGenerator = Substitute.For<IIdGenerator>();
mockIdGenerator.GenerateId().Returns("test-id-123");
```

## Best Practices

1. **Always use abstractions** - Don't use `DateTime.UtcNow` or `Guid.NewGuid()` directly
2. **Validate input** - Use `ValidationException` for invalid user input
3. **Use DTOs for serialization** - Never serialize domain entities directly
4. **Follow StyleCop rules** - Using directives inside namespaces, no underscore prefixes
5. **Document with XML comments** - All public types should have XML documentation
6. **Use source-generated JSON** - Use `UchatJsonSerializerContext` for performance
7. **Hash passwords properly** - Always use `IPasswordHasher`, never store plain text
8. **Paginate large results** - Use `PageRequest` and `PaginatedResponse<T>`
9. **Use consistent Redis keys** - Always use `RedisKeys` helper methods
10. **Handle errors gracefully** - Use appropriate exception types

## Performance Considerations

- **Source-generated JSON serialization** provides up to 2x performance improvement
- **BCrypt work factor** of 12 provides good security/performance balance
- **Pagination** prevents loading large datasets into memory
- **Redis key prefixing** enables efficient key pattern matching and cleanup
- **Async password operations** prevent blocking threads during CPU-intensive hashing
