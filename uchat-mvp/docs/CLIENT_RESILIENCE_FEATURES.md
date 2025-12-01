# Client Resilience Features

This document describes the client resilience features implemented in the Uchat application to provide a robust and reliable user experience.

## Overview

The following resilience features have been implemented:

1. **Auto-reconnection with Exponential Backoff**
2. **Offline Message Queueing with Persistence**
3. **Online/Offline Presence Indicators**
4. **Global Error Handling Service**

## 1. Auto-reconnection with Exponential Backoff

### Overview
The client automatically attempts to reconnect when the WebSocket connection is lost, using an exponential backoff strategy to avoid overwhelming the server.

### Components

#### ReconnectionStrategy
- **Location**: `Uchat.Client/Services/ReconnectionStrategy.cs`
- **Purpose**: Calculates delays between reconnection attempts using exponential backoff
- **Configuration**:
  - `maxRetries`: Maximum number of retry attempts (default: 10)
  - `initialDelay`: Initial delay before first retry (default: 1 second)
  - `maxDelay`: Maximum delay between retries (default: 60 seconds)
  - `exponentialBase`: Base for exponential calculation (default: 2.0)

#### Key Features
- Configurable retry behavior
- Exponential backoff to reduce server load
- Cancellation support via `CancellationToken`
- UI notifications for connection state changes

### Usage Example
```csharp
var strategy = new ReconnectionStrategy(
    maxRetries: 10,
    initialDelay: TimeSpan.FromSeconds(1),
    maxDelay: TimeSpan.FromSeconds(60),
    exponentialBase: 2.0
);

// Get delay for attempt 3: 1 * 2^3 = 8 seconds
var delay = strategy.GetDelay(3);

// Check if should retry
if (strategy.ShouldRetry(attemptNumber))
{
    // Perform retry
}
```

### Backoff Timing Examples
With default settings (initial: 1s, base: 2.0, max: 60s):
- Attempt 0: 1 second
- Attempt 1: 2 seconds
- Attempt 2: 4 seconds
- Attempt 3: 8 seconds
- Attempt 4: 16 seconds
- Attempt 5: 32 seconds
- Attempt 6+: 60 seconds (capped at max)

## 2. Offline Message Queueing

### Overview
Messages sent while offline are queued locally and automatically sent when the connection is restored.

### Components

#### IOfflineMessageQueue
- **Location**: `Uchat.Client/Services/IOfflineMessageQueue.cs`
- **Purpose**: Interface for offline message queue operations

#### OfflineMessageQueue
- **Location**: `Uchat.Client/Services/OfflineMessageQueue.cs`
- **Storage**: LiteDB for persistent local storage
- **Database Location**: `%LocalAppData%/Uchat/Data/offline_messages.db`

#### QueuedMessage
- **Location**: `Uchat.Client/Services/QueuedMessage.cs`
- **Properties**:
  - `MessageId`: Unique message identifier
  - `ChatId`: Target chat
  - `Content`: Message content
  - `ReplyToId`: Optional reply-to message ID
  - `QueuedAt`: Timestamp when queued
  - `Attempts`: Number of send attempts

### Key Features
- Persistent storage across application restarts
- Automatic flush on reconnection
- Duplication prevention using message IDs
- Retry tracking for failed sends
- Ordered by queue time (FIFO)

### Queue Operations
```csharp
// Enqueue a message
await offlineQueue.EnqueueAsync(new QueuedMessage
{
    MessageId = Guid.NewGuid().ToString(),
    ChatId = "chat123",
    Content = "Hello",
    QueuedAt = DateTime.UtcNow,
    Attempts = 0
});

// Get all queued messages
var messages = await offlineQueue.GetAllAsync();

// Remove a message after successful send
await offlineQueue.RemoveAsync(messageId);

// Get queue count
var count = await offlineQueue.GetCountAsync();

// Clear all messages
await offlineQueue.ClearAsync();
```

### Automatic Behavior
When `SendMessageAsync` is called:
1. If **online**: Send immediately via WebSocket
2. If **offline**: Add to queue and notify user
3. On **reconnect**: Automatically flush all queued messages

## 3. Online/Offline Presence Indicators

### Overview
Real-time presence indicators show user online/offline status, synced via Redis-backed server updates.

### Components

#### UserStatus Enum
- **Location**: `Uchat.Shared/Enums/UserStatus.cs`
- **Values**:
  - `Offline`: User is offline
  - `Online`: User is online and available
  - `Away`: User is away from keyboard
  - `DoNotDisturb`: User is in DND mode

#### StatusUpdateDto
- **Location**: `Uchat.Shared/Dtos/StatusUpdateDto.cs`
- **Properties**:
  - `UserId`: User whose status changed
  - `Status`: New user status

### Integration
Status updates are received via WebSocket and exposed through the `IMessagingService.UserStatusChanged` event:

```csharp
messagingService.UserStatusChanged += (sender, statusUpdate) =>
{
    // Update UI to reflect new user status
    UpdateUserPresenceIndicator(statusUpdate.UserId, statusUpdate.Status);
};
```

### Server Integration
The server uses Redis to track and broadcast user presence:
- User connects: Status set to Online
- User disconnects: Status set to Offline
- User activity: Updates last seen timestamp
- Status changes: Broadcast to all relevant users

## 4. Global Error Handling Service

### Overview
Centralized error handling with user feedback and comprehensive logging.

### Components

#### IErrorHandlingService
- **Location**: `Uchat.Client/Services/IErrorHandlingService.cs`
- **Purpose**: Global error handling interface

#### ErrorHandlingService
- **Location**: `Uchat.Client/Services/ErrorHandlingService.cs`
- **Purpose**: Implementation with Serilog integration

#### ErrorSeverity Enum
- **Location**: `Uchat.Client/Services/ErrorSeverity.cs`
- **Values**:
  - `Info`: Informational message
  - `Warning`: Warning message
  - `Error`: Error message
  - `Critical`: Critical error message

#### ErrorNotificationEventArgs
- **Location**: `Uchat.Client/Services/ErrorNotificationEventArgs.cs`
- **Purpose**: Event arguments for error notifications

### Key Features
- User-friendly error messages
- Comprehensive error logging via Serilog
- Severity-based handling
- Event-driven notifications for UI updates
- Exception type-specific messages

### Usage Examples

```csharp
// Handle exception with custom message
errorHandler.HandleError(
    exception,
    "Failed to send message. Please try again.",
    showDialog: true
);

// Handle exception with auto-generated message
errorHandler.HandleError(exception);

// Show informational message
errorHandler.ShowInfo("Connected to server");

// Show warning
errorHandler.ShowWarning("Connection unstable. Reconnecting...");

// Subscribe to error events
errorHandler.ErrorOccurred += (sender, args) =>
{
    switch (args.Severity)
    {
        case ErrorSeverity.Info:
            ShowToast(args.Message, ToastType.Info);
            break;
        case ErrorSeverity.Warning:
            ShowToast(args.Message, ToastType.Warning);
            break;
        case ErrorSeverity.Error:
            ShowDialog(args.Message);
            break;
    }
};
```

### Exception Message Mapping
The service provides user-friendly messages for common exceptions:
- `InvalidOperationException`: "An operation could not be completed. Please try again."
- `UnauthorizedAccessException`: "You do not have permission to perform this action."
- `TimeoutException`: "The operation timed out. Please check your connection and try again."
- `HttpRequestException`: "Network error occurred. Please check your connection."
- `WebSocketException`: "Connection error occurred. Attempting to reconnect..."
- Default: "An unexpected error occurred. Please try again."

## Integration with MessagingService

The `MessagingService` has been enhanced to integrate all resilience features:

### Enhanced Features
1. **Auto-reconnection**: Automatically triggers on connection loss
2. **Queue integration**: Messages queued when offline, flushed on reconnect
3. **Error handling**: All errors logged and optionally shown to users
4. **Duplication prevention**: Tracks sent message IDs to avoid duplicates

### Connection Flow
```
User sends message
    ↓
Is connected?
    ├─ Yes → Send immediately via WebSocket
    │        Add to sent message IDs
    └─ No  → Add to offline queue
             Show "Message queued" notification
    ↓
Connection lost
    ↓
Trigger reconnection with exponential backoff
    ↓
Reconnection successful
    ↓
Flush offline queue
    ↓
For each queued message:
    ├─ Already sent? → Remove from queue
    └─ Not sent? → Send via WebSocket
                   Add to sent message IDs
                   Remove from queue on success
```

## Testing

### Unit Tests

#### ReconnectionStrategyTests
- **Location**: `Uchat.Tests/Client/Services/ReconnectionStrategyTests.cs`
- **Coverage**:
  - Constructor validation
  - Exponential backoff calculation
  - Max delay capping
  - Retry decision logic
  - Edge cases (negative values, out of range)

#### OfflineMessageQueueTests
- **Location**: `Uchat.Tests/Client/Services/OfflineMessageQueueTests.cs`
- **Coverage**:
  - Enqueue/dequeue operations
  - Persistence across instances
  - Queue ordering (FIFO)
  - Duplicate handling (upsert)
  - Count and clear operations

#### ErrorHandlingServiceTests
- **Location**: `Uchat.Tests/Client/Services/ErrorHandlingServiceTests.cs`
- **Coverage**:
  - Error logging
  - Event raising
  - Message generation
  - Severity handling
  - Exception type mapping

#### MessagingServiceTests
- **Location**: `Uchat.Tests/Client/Services/MessagingServiceTests.cs`
- **Coverage**:
  - Existing tests updated for new constructor
  - New test: Offline message queueing

### Running Tests
```bash
dotnet test
```

## Configuration

All services are registered in `Uchat.Client/Infrastructure/DependencyInjection.cs`:

```csharp
services.AddSingleton<IOfflineMessageQueue, OfflineMessageQueue>();
services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
services.AddSingleton<IMessagingService, MessagingService>();
```

## Dependencies

New dependencies added:
- **LiteDB** (v5.0.21): Lightweight NoSQL database for offline message persistence

Existing dependencies used:
- **Serilog**: Logging framework
- **System.Net.WebSockets.Client**: WebSocket client

## StyleCop Compliance

All new code follows StyleCop rules:
- ✅ Using directives inside namespaces (SA1200)
- ✅ No underscore prefixes for fields (SA1309)
- ✅ `this.` prefix for local calls (SA1101)
- ✅ Proper XML documentation
- ✅ Proper indentation and formatting
- ✅ Trailing commas in multi-line initializers (SA1413)

## Future Enhancements

Potential improvements:
1. **Configurable reconnection strategy** via app settings
2. **Message queue size limits** with overflow handling
3. **Priority queueing** for important messages
4. **Network quality detection** for adaptive behavior
5. **Offline mode indicator** in UI
6. **Message send retry limits** with user notification
7. **Queue statistics** for monitoring and debugging
