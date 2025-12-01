namespace Uchat.Server.Middleware
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Serilog;
    using Uchat.Server.Services.Abstractions;
    using Uchat.Shared.Dtos;
    using Uchat.Shared.Enums;

    /// <summary>
    /// Handles WebSocket connections and message routing.
    /// </summary>
    public class WebSocketHandler
    {
        private readonly RequestDelegate next;
        private readonly IWebSocketConnectionManager connectionManager;
        private readonly IJwtTokenService jwtTokenService;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketHandler"/> class.
        /// </summary>
        /// <param name="next">Next request delegate.</param>
        /// <param name="connectionManager">WebSocket connection manager.</param>
        /// <param name="jwtTokenService">JWT token service.</param>
        /// <param name="logger">Logger.</param>
        public WebSocketHandler(
            RequestDelegate next,
            IWebSocketConnectionManager connectionManager,
            IJwtTokenService jwtTokenService,
            ILogger logger)
        {
            this.next = next;
            this.connectionManager = connectionManager;
            this.jwtTokenService = jwtTokenService;
            this.logger = logger;
        }

        /// <summary>
        /// Invokes the WebSocket handler middleware.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                if (context.Request.Path == "/ws")
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await this.HandleWebSocketAsync(webSocket, context);
                    return;
                }
            }

            await this.next(context);
        }

        private async Task HandleWebSocketAsync(WebSocket webSocket, HttpContext context)
        {
            string? userId = null;
            string? connectionId = null;

            // Get scoped services from HttpContext
            var sessionService = context.RequestServices.GetRequiredService<ISessionService>();
            var messageService = context.RequestServices.GetRequiredService<IMessageService>();
            var rateLimitService = context.RequestServices.GetRequiredService<IRateLimitService>();
            var chatService = context.RequestServices.GetRequiredService<IChatService>();
            var userStatusService = context.RequestServices.GetRequiredService<IUserStatusService>();

            WebSocketReceiveResult? receiveResult = null;

            try
            {
                var buffer = new byte[1024 * 4];

                using var authTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                try
                {
                    if (webSocket.State != WebSocketState.Open)
                    {
                        this.logger.Debug("WebSocket is not in Open state: {State}", webSocket.State);
                        return;
                    }

                    receiveResult = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        authTimeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    this.logger.Warning("WebSocket authentication timeout - no message received within 10 seconds");
                    await this.CloseWebSocketSafelyAsync(webSocket, WebSocketCloseStatus.PolicyViolation, "Authentication timeout");
                    return;
                }
                catch (WebSocketException wsEx) when (webSocket.State == WebSocketState.Aborted || webSocket.State == WebSocketState.Closed)
                {
                    this.logger.Debug("WebSocket closed during authentication: {State}, {Message}", webSocket.State, wsEx.Message);
                    return;
                }

                if (receiveResult == null)
                {
                    return;
                }

                while (webSocket.State == WebSocketState.Open && !receiveResult.CloseStatus.HasValue)
                {
                    var messageText = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);

                    try
                    {
                        var envelope = JsonSerializer.Deserialize<WebSocketMessageEnvelope>(messageText);

                        if (envelope != null)
                        {
                                    if (userId == null && envelope.Type == Uchat.Shared.Enums.WebSocketMessageType.Authenticate)
                                {
                                    if (webSocket.State == WebSocketState.Open)
                                    {
                                        var authResult = await this.AuthenticateAsync(envelope, webSocket, sessionService, context);
                                        if (authResult != null)
                                        {
                                            userId = authResult.Value.UserId;
                                            connectionId = authResult.Value.ConnectionId;
                                            this.logger.Information("WebSocket authenticated for user {UserId}", userId);

                                            // Send queued messages to newly connected client
                                            try
                                            {
                                            var queuedMessages = await sessionService.GetQueuedMessagesAsync(userId);
                                            foreach (var queuedMsg in queuedMessages)
                                            {
                                                if (webSocket.State == WebSocketState.Open)
                                                {
                                                    await this.connectionManager.SendMessageAsync(connectionId, queuedMsg, CancellationToken.None);
                                                }
                                            }

                                            await sessionService.ClearMessageQueueAsync(userId);
                                        }
                                        catch (WebSocketException)
                                        {
                                            // Connection closed while sending queued messages
                                            break;
                                        }
                                    }
                                }
                            }
                            else if (userId != null && webSocket.State == WebSocketState.Open)
                            {
                                await this.HandleMessageAsync(envelope, userId, connectionId!, sessionService, messageService, rateLimitService, chatService, userStatusService);
                            }
                            else
                            {
                                if (webSocket.State == WebSocketState.Open)
                                {
                                    await this.SendErrorAsync(webSocket, "Not authenticated");
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        this.logger.Warning(ex, "Failed to parse WebSocket message");
                        if (webSocket.State == WebSocketState.Open)
                        {
                            await this.SendErrorAsync(webSocket, "Invalid message format");
                        }
                    }
                    catch (WebSocketException)
                    {
                        // Connection was closed during message processing
                        if (webSocket.State == WebSocketState.Aborted || webSocket.State == WebSocketState.Closed)
                        {
                            this.logger.Debug("WebSocket closed during message processing: {State}", webSocket.State);
                            break;
                        }

                        throw; // Re-throw to be caught by outer handler
                    }
                    catch (Exception ex)
                    {
                        this.logger.Error(ex, "Error processing WebSocket message");
                        if (webSocket.State == WebSocketState.Open)
                        {
                            await this.SendErrorAsync(webSocket, "Internal server error");
                        }
                    }

                    if (webSocket.State != WebSocketState.Open)
                    {
                        break;
                    }

                    try
                    {
                        receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    }
                    catch (WebSocketException wsEx) when (webSocket.State == WebSocketState.Aborted || webSocket.State == WebSocketState.Closed)
                    {
                        this.logger.Debug("WebSocket closed during message receive: {State}, {Message}", webSocket.State, wsEx.Message);
                        break;
                    }
                }

                // Clean up connection on disconnect
                if (connectionId != null)
                {
                    this.connectionManager.RemoveConnection(connectionId);
                }

                if (userId != null)
                {
                    // Check if user has any remaining connections
                    var remainingConnections = this.connectionManager.GetConnectionIdsByUserId(userId);
                    if (!remainingConnections.Any())
                    {
                        // No more connections, set status to offline
                        await userStatusService.SetStatusAsync(userId, Uchat.Shared.Enums.UserStatus.Offline, CancellationToken.None);
                        await this.BroadcastStatusUpdateAsync(userId, Uchat.Shared.Enums.UserStatus.Offline, chatService);
                    }

                    await sessionService.RemoveSessionAsync(userId);
                }

                if (receiveResult != null && receiveResult.CloseStatus.HasValue)
                {
                    await this.CloseWebSocketSafelyAsync(
                        webSocket,
                        receiveResult.CloseStatus.Value,
                        receiveResult.CloseStatusDescription ?? "Connection closed");
                }
                else if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                {
                    // Client closed connection without proper close handshake
                    await this.CloseWebSocketSafelyAsync(webSocket, WebSocketCloseStatus.NormalClosure, "Connection closed");
                }
            }
            catch (WebSocketException wsEx)
            {
                // This is expected when client closes connection abruptly - log as debug, not warning
                if (webSocket.State == WebSocketState.Aborted || webSocket.State == WebSocketState.Closed)
                {
                    this.logger.Debug("WebSocket connection closed by client: {State}", webSocket.State);
                }
                else
                {
                    this.logger.Warning(wsEx, "WebSocket error: {State}, {Message}", webSocket.State, wsEx.Message);
                }

                if (connectionId != null)
                {
                    this.connectionManager.RemoveConnection(connectionId);
                }

                if (userId != null)
                {
                    try
                    {
                        // Check if user has any remaining connections
                        var remainingConnections = this.connectionManager.GetConnectionIdsByUserId(userId);
                        if (!remainingConnections.Any())
                        {
                            // No more connections, set status to offline
                            await userStatusService.SetStatusAsync(userId, Uchat.Shared.Enums.UserStatus.Offline, CancellationToken.None);
                            await this.BroadcastStatusUpdateAsync(userId, Uchat.Shared.Enums.UserStatus.Offline, chatService);
                        }

                        await sessionService.RemoveSessionAsync(userId);
                    }
                    catch (Exception ex)
                    {
                        this.logger.Warning(ex, "Error removing session for user {UserId}", userId);
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "WebSocket connection error");

                if (connectionId != null)
                {
                    this.connectionManager.RemoveConnection(connectionId);
                }

                if (userId != null)
                {
                    // Check if user has any remaining connections
                    var remainingConnections = this.connectionManager.GetConnectionIdsByUserId(userId);
                    if (!remainingConnections.Any())
                    {
                        // No more connections, set status to offline
                        await userStatusService.SetStatusAsync(userId, Uchat.Shared.Enums.UserStatus.Offline, CancellationToken.None);
                        await this.BroadcastStatusUpdateAsync(userId, Uchat.Shared.Enums.UserStatus.Offline, chatService);
                    }

                    await sessionService.RemoveSessionAsync(userId);
                }

                await this.CloseWebSocketSafelyAsync(webSocket, WebSocketCloseStatus.InternalServerError, "Server error");
            }
        }

        private async Task CloseWebSocketSafelyAsync(
            WebSocket webSocket,
            WebSocketCloseStatus closeStatus,
            string? statusDescription)
        {
            if (webSocket.State == WebSocketState.Open ||
                webSocket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await webSocket.CloseAsync(
                        closeStatus,
                        statusDescription,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    this.logger.Warning(ex, "Failed to close WebSocket gracefully");
                }
            }
        }

        private async Task<(string UserId, string ConnectionId)?> AuthenticateAsync(
            WebSocketMessageEnvelope envelope,
            WebSocket webSocket,
            ISessionService sessionService,
            HttpContext httpContext)
        {
            try
            {
                if (envelope.Payload.HasValue)
                {
                    var authPayload = JsonSerializer.Deserialize<AuthPayload>(envelope.Payload.Value.GetRawText());
                    if (authPayload?.Token != null)
                    {
                        var userId = await this.jwtTokenService.ValidateTokenAsync(authPayload.Token);

                        if (!string.IsNullOrEmpty(userId))
                        {
                            var connectionId = Guid.NewGuid().ToString();
                            this.connectionManager.AddConnection(userId, webSocket, connectionId);

                            var resumeToken = await sessionService.CreateSessionAsync(userId, connectionId);

                            // Set user status to online in Redis
                            var statusService = httpContext.RequestServices.GetRequiredService<IUserStatusService>();
                            await statusService.SetStatusAsync(userId, Uchat.Shared.Enums.UserStatus.Online, CancellationToken.None);

                            // Broadcast status update to other users
                            var chatSvc = httpContext.RequestServices.GetRequiredService<IChatService>();
                            await this.BroadcastStatusUpdateAsync(userId, Uchat.Shared.Enums.UserStatus.Online, chatSvc);

                            var response = new WebSocketMessageEnvelope
                            {
                                Type = Uchat.Shared.Enums.WebSocketMessageType.Authenticate,
                                Payload = JsonSerializer.SerializeToElement(new { Success = true, ResumeToken = resumeToken }),
                            };

                            await this.SendMessageAsync(webSocket, response);

                            return (userId, connectionId);
                        }
                    }
                }

                await this.SendErrorAsync(webSocket, "Authentication failed");
                return null;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Authentication error");
                await this.SendErrorAsync(webSocket, "Authentication error");
                return null;
            }
        }

        private async Task HandleMessageAsync(
            WebSocketMessageEnvelope envelope,
            string userId,
            string connectionId,
            ISessionService sessionService,
            IMessageService messageService,
            IRateLimitService rateLimitService,
            IChatService chatService,
            IUserStatusService userStatusService)
        {
            switch (envelope.Type)
            {
                case Uchat.Shared.Enums.WebSocketMessageType.NewMessage:
                    await this.HandleNewMessageAsync(envelope, userId, messageService, rateLimitService, chatService);
                    break;

                case Uchat.Shared.Enums.WebSocketMessageType.EditMessage:
                    await this.HandleEditMessageAsync(envelope, userId, messageService, chatService);
                    break;

                case Uchat.Shared.Enums.WebSocketMessageType.DeleteMessage:
                    await this.HandleDeleteMessageAsync(envelope, userId, messageService, chatService);
                    break;

                case Uchat.Shared.Enums.WebSocketMessageType.Heartbeat:
                    await sessionService.UpdateHeartbeatAsync(userId);

                    // Refresh status expiration in Redis
                    await userStatusService.SetStatusAsync(userId, await userStatusService.GetStatusAsync(userId, CancellationToken.None), CancellationToken.None);
                    break;

                default:
                    this.logger.Warning("Unknown message type: {MessageType}", envelope.Type);
                    break;
            }
        }

        private async Task HandleNewMessageAsync(
            WebSocketMessageEnvelope envelope,
            string userId,
            IMessageService messageService,
            IRateLimitService rateLimitService,
            IChatService chatService)
        {
            if (!envelope.Payload.HasValue)
            {
                return;
            }

            // Check rate limit
            var isAllowed = await rateLimitService.IsMessageSendAllowedAsync(userId, CancellationToken.None);
            if (!isAllowed)
            {
                this.logger.Warning("Rate limit exceeded for message send: {UserId}", userId);
                var errorResponse = new WebSocketMessageEnvelope
                {
                    Type = Uchat.Shared.Enums.WebSocketMessageType.Error,
                    Payload = JsonSerializer.SerializeToElement(new { Error = "Rate limit exceeded", Message = "Too many messages sent. Please slow down." }),
                };
                var errorJson = JsonSerializer.Serialize(errorResponse);
                await this.connectionManager.BroadcastToUserAsync(userId, errorJson, CancellationToken.None);

                return;
            }

            var request = JsonSerializer.Deserialize<SendMessageRequest>(envelope.Payload.Value.GetRawText());
            if (request != null)
            {
                var messageDto = await messageService.SendMessageAsync(request, userId);

                var response = new WebSocketMessageEnvelope
                {
                    Type = Uchat.Shared.Enums.WebSocketMessageType.NewMessage,
                    MessageId = messageDto.Id,
                    Payload = JsonSerializer.SerializeToElement(messageDto),
                };

                // Broadcast to all chat participants
                await this.BroadcastToChatAsync(request.ChatId, response, chatService);
            }
        }

        private async Task HandleEditMessageAsync(
            WebSocketMessageEnvelope envelope,
            string userId,
            IMessageService messageService,
            IChatService chatService)
        {
            if (!envelope.Payload.HasValue)
            {
                return;
            }

            var request = JsonSerializer.Deserialize<EditMessageRequest>(envelope.Payload.Value.GetRawText());
            if (request != null)
            {
                var messageDto = await messageService.EditMessageAsync(request.MessageId, request.Content, userId);

                var response = new WebSocketMessageEnvelope
                {
                    Type = Uchat.Shared.Enums.WebSocketMessageType.EditMessage,
                    MessageId = messageDto.Id,
                    Payload = JsonSerializer.SerializeToElement(messageDto),
                };

                // Broadcast edited message to all chat participants
                await this.BroadcastToChatAsync(messageDto.ChatId, response, chatService);
            }
        }

        private async Task HandleDeleteMessageAsync(
            WebSocketMessageEnvelope envelope,
            string userId,
            IMessageService messageService,
            IChatService chatService)
        {
            if (!envelope.Payload.HasValue)
            {
                return;
            }

            var request = JsonSerializer.Deserialize<DeleteMessageRequest>(envelope.Payload.Value.GetRawText());
            if (request == null)
            {
                return;
            }

            try
            {
                this.logger.Information("Processing delete request for message {MessageId} from user {UserId}", request.MessageId, userId);

                var chatId = await messageService.DeleteMessageAsync(request.MessageId, userId);

                var response = new WebSocketMessageEnvelope
                {
                    Type = Uchat.Shared.Enums.WebSocketMessageType.DeleteMessage,
                    MessageId = request.MessageId,
                    Payload = JsonSerializer.SerializeToElement(new { ChatId = chatId }),
                };

                this.logger.Information("Message {MessageId} deleted successfully, broadcasting to all participants in chat {ChatId}", request.MessageId, chatId);

                await this.BroadcastToChatAsync(chatId, response, chatService);
                this.logger.Debug("Delete notification broadcasted to all participants in chat {ChatId}", chatId);
            }
            catch (Uchat.Shared.Exceptions.ValidationException vex)
            {
                this.logger.Warning(vex, "Validation error deleting message {MessageId} by user {UserId}: {Message}", request.MessageId, userId, vex.Message);

                var errorResponse = new WebSocketMessageEnvelope
                {
                    Type = Uchat.Shared.Enums.WebSocketMessageType.Error,
                    Error = vex.Message,
                    Payload = JsonSerializer.SerializeToElement(new { MessageId = request.MessageId, ErrorType = "ValidationError" }),
                };

                await this.connectionManager.BroadcastToUserAsync(userId, JsonSerializer.Serialize(errorResponse), CancellationToken.None);
            }
            catch (Uchat.Shared.Exceptions.DomainException dex)
            {
                this.logger.Error(dex, "Domain error deleting message {MessageId} by user {UserId}: {Message}", request.MessageId, userId, dex.Message);

                var errorResponse = new WebSocketMessageEnvelope
                {
                    Type = Uchat.Shared.Enums.WebSocketMessageType.Error,
                    Error = "Failed to delete message. Please try again later.",
                    Payload = JsonSerializer.SerializeToElement(new { MessageId = request.MessageId, ErrorType = "DatabaseError" }),
                };

                await this.connectionManager.BroadcastToUserAsync(userId, JsonSerializer.Serialize(errorResponse), CancellationToken.None);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Unexpected error deleting message {MessageId} by user {UserId}", request.MessageId, userId);

                var errorResponse = new WebSocketMessageEnvelope
                {
                    Type = Uchat.Shared.Enums.WebSocketMessageType.Error,
                    Error = "An unexpected error occurred while deleting the message.",
                    Payload = JsonSerializer.SerializeToElement(new { MessageId = request.MessageId, ErrorType = "UnexpectedError" }),
                };

                await this.connectionManager.BroadcastToUserAsync(userId, JsonSerializer.Serialize(errorResponse), CancellationToken.None);
            }
        }

        private async Task BroadcastToChatAsync(string chatId, WebSocketMessageEnvelope envelope, IChatService chatService)
        {
            var message = JsonSerializer.Serialize(envelope);
            this.logger.Debug("Broadcasting message to chat {ChatId}", chatId);

            try
            {
                // Get chat participants and send message to each one
                var chat = await chatService.GetChatByIdAsync(chatId, CancellationToken.None);
                if (chat != null)
                {
                    var tasks = new List<Task>();
                    foreach (var participantId in chat.ParticipantIds)
                    {
                        tasks.Add(this.connectionManager.BroadcastToUserAsync(
                            participantId,
                            message,
                            CancellationToken.None));
                    }

                    await Task.WhenAll(tasks);
                    this.logger.Debug("Message broadcasted to {Count} participants in chat {ChatId}", chat.ParticipantIds.Count, chatId);
                }
                else
                {
                    this.logger.Warning("Chat {ChatId} not found for broadcast", chatId);
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to broadcast message to chat {ChatId}", chatId);
            }
        }

        private async Task BroadcastStatusUpdateAsync(string userId, Uchat.Shared.Enums.UserStatus status, IChatService chatService)
        {
            try
            {
                var statusUpdate = new StatusUpdateDto
                {
                    UserId = userId,
                    Status = status,
                };

                var envelope = new WebSocketMessageEnvelope
                {
                    Type = Uchat.Shared.Enums.WebSocketMessageType.StatusUpdate,
                    Payload = JsonSerializer.SerializeToElement(statusUpdate),
                };

                var message = JsonSerializer.Serialize(envelope);

                // Get all chats where this user is a participant
                var userChats = await chatService.GetUserChatsAsync(userId, CancellationToken.None);
                var allParticipantIds = new HashSet<string>();

                foreach (var chat in userChats)
                {
                    foreach (var participantId in chat.ParticipantIds)
                    {
                        if (participantId != userId)
                        {
                            allParticipantIds.Add(participantId);
                        }
                    }
                }

                // Broadcast status update to all participants
                var tasks = new List<Task>();
                foreach (var participantId in allParticipantIds)
                {
                    tasks.Add(this.connectionManager.BroadcastToUserAsync(
                        participantId,
                        message,
                        CancellationToken.None));
                }

                await Task.WhenAll(tasks);
                this.logger.Debug("Status update broadcasted to {Count} users for user {UserId}", allParticipantIds.Count, userId);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Failed to broadcast status update for user {UserId}", userId);
            }
        }

        private async Task SendMessageAsync(WebSocket socket, WebSocketMessageEnvelope envelope)
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }

            try
            {
                var message = JsonSerializer.Serialize(envelope);
                var bytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(bytes), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // Connection closed - ignore
            }
        }

        private async Task SendErrorAsync(WebSocket socket, string error)
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }

            try
            {
                var envelope = new WebSocketMessageEnvelope
                {
                    Type = Uchat.Shared.Enums.WebSocketMessageType.Error,
                    Error = error,
                };

                await this.SendMessageAsync(socket, envelope);
            }
            catch (WebSocketException)
            {
                // Connection closed - ignore
            }
        }

        private class AuthPayload
        {
            public string? Token { get; set; }
        }
    }
}
