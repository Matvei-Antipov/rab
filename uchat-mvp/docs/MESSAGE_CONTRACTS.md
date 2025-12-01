# WebSocket Message Contracts

This document describes the WebSocket message contracts used for real-time communication between the Uchat client and server.

## Message Format

All WebSocket messages use JSON format with a common structure:

```json
{
  "type": "message_type_identifier",
  "...": "message-specific fields"
}
```

The `type` field is used to discriminate between different message types during deserialization.

## Message Types

### 1. Chat Message (`chat_message`)

Sent when a new message is created or received in a chat.

**Contract:** `ChatMessageContract`

**Direction:** Bidirectional (client → server, server → client)

**Schema:**
```json
{
  "type": "chat_message",
  "messageId": "string (UUID)",
  "chatId": "string (UUID)",
  "senderId": "string (UUID)",
  "content": "string",
  "timestamp": "string (ISO 8601)",
  "replyToId": "string (UUID, optional)"
}
```

**Example:**
```json
{
  "type": "chat_message",
  "messageId": "123e4567-e89b-12d3-a456-426614174000",
  "chatId": "987fcdeb-51a2-43f7-8a3c-123456789abc",
  "senderId": "user-123",
  "content": "Hello, world!",
  "timestamp": "2024-01-15T10:30:00Z",
  "replyToId": null
}
```

---

### 2. User Joined (`user_joined`)

Sent when a user joins a chat or comes online.

**Contract:** `UserJoinedContract`

**Direction:** Server → Client

**Schema:**
```json
{
  "type": "user_joined",
  "userId": "string (UUID)",
  "chatId": "string (UUID, optional)",
  "username": "string",
  "displayName": "string",
  "timestamp": "string (ISO 8601)"
}
```

**Example:**
```json
{
  "type": "user_joined",
  "userId": "user-456",
  "chatId": "987fcdeb-51a2-43f7-8a3c-123456789abc",
  "username": "alice",
  "displayName": "Alice Smith",
  "timestamp": "2024-01-15T10:31:00Z"
}
```

---

### 3. User Left (`user_left`)

Sent when a user leaves a chat or goes offline.

**Contract:** `UserLeftContract`

**Direction:** Server → Client

**Schema:**
```json
{
  "type": "user_left",
  "userId": "string (UUID)",
  "chatId": "string (UUID, optional)",
  "username": "string",
  "timestamp": "string (ISO 8601)"
}
```

**Example:**
```json
{
  "type": "user_left",
  "userId": "user-456",
  "chatId": "987fcdeb-51a2-43f7-8a3c-123456789abc",
  "username": "alice",
  "timestamp": "2024-01-15T11:00:00Z"
}
```

---

### 4. Typing Indicator (`typing_indicator`)

Sent when a user starts or stops typing in a chat.

**Contract:** `TypingIndicatorContract`

**Direction:** Bidirectional (client → server, server → client)

**Schema:**
```json
{
  "type": "typing_indicator",
  "userId": "string (UUID)",
  "chatId": "string (UUID)",
  "username": "string",
  "isTyping": "boolean"
}
```

**Example:**
```json
{
  "type": "typing_indicator",
  "userId": "user-789",
  "chatId": "987fcdeb-51a2-43f7-8a3c-123456789abc",
  "username": "bob",
  "isTyping": true
}
```

---

### 5. Message Delivered (`message_delivered`)

Sent when a message has been delivered to recipient(s).

**Contract:** `MessageDeliveredContract`

**Direction:** Server → Client

**Schema:**
```json
{
  "type": "message_delivered",
  "messageId": "string (UUID)",
  "chatId": "string (UUID)",
  "deliveredAt": "string (ISO 8601)"
}
```

**Example:**
```json
{
  "type": "message_delivered",
  "messageId": "123e4567-e89b-12d3-a456-426614174000",
  "chatId": "987fcdeb-51a2-43f7-8a3c-123456789abc",
  "deliveredAt": "2024-01-15T10:30:01Z"
}
```

---

### 6. Message Read (`message_read`)

Sent when a message has been read by a recipient.

**Contract:** `MessageReadContract`

**Direction:** Bidirectional (client → server, server → client)

**Schema:**
```json
{
  "type": "message_read",
  "messageId": "string (UUID)",
  "chatId": "string (UUID)",
  "userId": "string (UUID)",
  "readAt": "string (ISO 8601)"
}
```

**Example:**
```json
{
  "type": "message_read",
  "messageId": "123e4567-e89b-12d3-a456-426614174000",
  "chatId": "987fcdeb-51a2-43f7-8a3c-123456789abc",
  "userId": "user-456",
  "readAt": "2024-01-15T10:31:30Z"
}
```

---

### 7. User Status Changed (`user_status_changed`)

Sent when a user changes their online/away/DND status.

**Contract:** `UserStatusChangedContract`

**Direction:** Server → Client

**Schema:**
```json
{
  "type": "user_status_changed",
  "userId": "string (UUID)",
  "username": "string",
  "status": "number (UserStatus enum)",
  "timestamp": "string (ISO 8601)"
}
```

**UserStatus Enum Values:**
- `0` - Offline
- `1` - Online
- `2` - Away
- `3` - DoNotDisturb

**Example:**
```json
{
  "type": "user_status_changed",
  "userId": "user-789",
  "username": "bob",
  "status": 2,
  "timestamp": "2024-01-15T10:45:00Z"
}
```

---

### 8. Error (`error`)

Sent when an error occurs during WebSocket communication.

**Contract:** `ErrorContract`

**Direction:** Server → Client

**Schema:**
```json
{
  "type": "error",
  "code": "string",
  "message": "string",
  "details": "string (optional)"
}
```

**Common Error Codes:**
- `UNAUTHORIZED` - Authentication failed or token expired
- `INVALID_MESSAGE` - Message format or content is invalid
- `CHAT_NOT_FOUND` - Specified chat does not exist
- `PERMISSION_DENIED` - User lacks permission for the action
- `RATE_LIMIT_EXCEEDED` - Too many messages sent too quickly

**Example:**
```json
{
  "type": "error",
  "code": "CHAT_NOT_FOUND",
  "message": "The specified chat does not exist or you don't have access to it.",
  "details": "chatId: 987fcdeb-51a2-43f7-8a3c-123456789abc"
}
```

---

## Serialization

All messages are serialized using System.Text.Json with the following settings:

- **Naming Policy:** camelCase
- **Null Handling:** Null values are omitted from output
- **Enum Serialization:** Enums are serialized as numbers by default
- **Date Format:** ISO 8601 (UTC)

For optimal performance, use the `UchatJsonSerializerContext` source-generated serializer context:

```csharp
using Uchat.Shared.Serialization;

var options = JsonSerializerOptionsFactory.WebSocket;
var json = JsonSerializer.Serialize(message, options);
```

## Implementation Guidelines

### Client Implementation

1. Establish WebSocket connection to the server endpoint
2. Send authentication token in initial handshake or first message
3. Deserialize incoming messages using the `type` field
4. Implement handlers for each message type
5. Send appropriate responses (e.g., message_read when displaying messages)
6. Handle reconnection and message queue on disconnect

### Server Implementation

1. Accept WebSocket connections and authenticate users
2. Maintain a registry of connected users and their WebSocket connections
3. Route messages to appropriate recipients based on chat membership
4. Broadcast presence updates (user_joined, user_left, user_status_changed)
5. Persist messages to database before broadcasting
6. Handle graceful disconnection and cleanup

## Message Flow Examples

### Sending a Chat Message

1. Client sends `chat_message` to server
2. Server validates message and stores in database
3. Server broadcasts `chat_message` to all chat participants
4. Server sends `message_delivered` to original sender
5. Recipients send `message_read` when they view the message
6. Server broadcasts `message_read` to all chat participants

### User Joining a Chat

1. Client establishes WebSocket connection
2. Server authenticates user
3. Server sends `user_joined` to all users in shared chats
4. Server sends initial chat state (recent messages, participants) to new user

### Typing Indicator

1. Client detects user typing and sends `typing_indicator` with `isTyping: true`
2. Server broadcasts to other chat participants
3. Client sends `typing_indicator` with `isTyping: false` when user stops typing
4. Server broadcasts stop typing to other participants
5. Typing indicators should expire after 3-5 seconds of inactivity
