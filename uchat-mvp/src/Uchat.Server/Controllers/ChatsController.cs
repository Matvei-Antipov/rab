namespace Uchat.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using Uchat.Server.Services.Abstractions;
    using Uchat.Shared.Dtos;
    using Uchat.Shared.Enums;

    /// <summary>
    /// Controller for chat-related endpoints.
    /// </summary>
    [ApiController]
    [Route("api/chats")]
    public class ChatsController : ControllerBase
    {
        private readonly IChatService chatService;
        private readonly IMessageService messageService;
        private readonly IWebSocketConnectionManager connectionManager;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatsController"/> class.
        /// </summary>
        /// <param name="chatService">Chat service.</param>
        /// <param name="messageService">Message service.</param>
        /// <param name="connectionManager">WebSocket connection manager.</param>
        /// <param name="logger">Logger.</param>
        public ChatsController(IChatService chatService, IMessageService messageService, IWebSocketConnectionManager connectionManager, ILogger logger)
        {
            this.chatService = chatService;
            this.messageService = messageService;
            this.connectionManager = connectionManager;
            this.logger = logger;
        }

        /// <summary>
        /// Gets all chats for the authenticated user.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of chat DTOs.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ChatDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<IEnumerable<ChatDto>>> GetChats(CancellationToken cancellationToken)
        {
            var userId = this.HttpContext.Items["UserId"] as string;
            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized();
            }

            var chats = await this.chatService.GetUserChatsAsync(userId, cancellationToken);
            return this.Ok(chats);
        }

        /// <summary>
        /// Gets a specific chat by ID.
        /// </summary>
        /// <param name="id">The chat ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat DTO.</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ChatDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ChatDto>> GetChat(string id, CancellationToken cancellationToken)
        {
            var chat = await this.chatService.GetChatByIdAsync(id, cancellationToken);
            if (chat == null)
            {
                return this.NotFound();
            }

            return this.Ok(chat);
        }

        /// <summary>
        /// Gets messages for a specific chat.
        /// </summary>
        /// <param name="id">The chat ID.</param>
        /// <param name="limit">Maximum number of messages to retrieve.</param>
        /// <param name="offset">Number of messages to skip.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of message DTOs.</returns>
        [HttpGet("{id}/messages")]
        [ProducesResponseType(typeof(IEnumerable<MessageDto>), 200)]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetChatMessages(
            string id,
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0,
            CancellationToken cancellationToken = default)
        {
            if (limit <= 0 || limit > 100)
            {
                limit = 50;
            }

            if (offset < 0)
            {
                offset = 0;
            }

            var messages = await this.messageService.GetChatMessagesAsync(id, limit, offset, cancellationToken);
            return this.Ok(messages);
        }

        /// <summary>
        /// Sends a message to a specific chat (HTTP endpoint for attachments).
        /// </summary>
        /// <param name="id">The chat ID.</param>
        /// <param name="request">The send message request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created message DTO.</returns>
        [HttpPost("{id}/messages")]
        [ProducesResponseType(typeof(MessageDto), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<MessageDto>> SendMessage(
            string id,
            [FromBody] SendMessageRequest request,
            CancellationToken cancellationToken)
        {
            var userId = this.HttpContext.Items["UserId"] as string;
            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized();
            }

            request.ChatId = id;
            var message = await this.messageService.SendMessageAsync(request, userId, cancellationToken);

            // Broadcast message to all chat participants via WebSocket
            try
            {
                var chat = await this.chatService.GetChatByIdAsync(id, cancellationToken);
                if (chat != null)
                {
                    var envelope = new WebSocketMessageEnvelope
                    {
                        Type = WebSocketMessageType.NewMessage,
                        MessageId = message.Id,
                        Payload = JsonSerializer.SerializeToElement(message),
                    };

                    var messageJson = JsonSerializer.Serialize(envelope);

                    // Send to all participants
                    var broadcastTasks = new List<Task>();
                    foreach (var participantId in chat.ParticipantIds)
                    {
                        broadcastTasks.Add(this.connectionManager.BroadcastToUserAsync(
                            participantId,
                            messageJson,
                            cancellationToken));
                    }

                    await Task.WhenAll(broadcastTasks);
                    this.logger.Debug("Message {MessageId} broadcasted to {Count} participants in chat {ChatId}", message.Id, chat.ParticipantIds.Count, id);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the request - message was already saved
                this.logger.Warning(ex, "Failed to broadcast message {MessageId} via WebSocket", message.Id);
            }

            return this.Created($"/api/chats/{id}/messages", message);
        }

        /// <summary>
        /// Creates a new chat.
        /// </summary>
        /// <param name="request">The create chat request data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created chat DTO.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ChatDto), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(409)]
        public async Task<ActionResult<ChatDto>> CreateChat([FromBody] CreateChatRequest? request, CancellationToken cancellationToken)
        {
            this.logger.Information("POST /api/chats - Creating new chat {ChatName}", request?.Name ?? "unknown");

            // Check for null request
            if (request == null)
            {
                this.logger.Warning("Null request body for chat creation");
                return this.BadRequest(new { error = "Request body is required." });
            }

            // Check authentication
            var userId = this.HttpContext.Items["UserId"] as string;
            if (string.IsNullOrEmpty(userId))
            {
                this.logger.Warning("Unauthorized chat creation attempt");
                return this.Unauthorized();
            }

            // Check ModelState for FluentValidation errors
            if (!this.ModelState.IsValid)
            {
                this.logger.Warning("Validation failed for chat creation: {ValidationErrors}", this.ModelState);
                return this.BadRequest(this.ModelState);
            }

            try
            {
                var chat = await this.chatService.CreateChatAsync(request, userId, cancellationToken);
                this.logger.Information("Chat {ChatId} created successfully by user {UserId}", chat.Id, userId);
                return this.CreatedAtAction(nameof(this.GetChat), new { id = chat.Id }, chat);
            }
            catch (Shared.Exceptions.DuplicateChatException ex)
            {
                this.logger.Warning("Duplicate chat name: {Message}", ex.Message);
                return this.Conflict(new { error = ex.Message });
            }
            catch (Shared.Exceptions.ValidationException ex)
            {
                this.logger.Warning("Validation error during chat creation: {Message}", ex.Message);
                return this.BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Deletes a chat.
        /// </summary>
        /// <param name="id">The chat ID to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Success response.</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteChat(string id, CancellationToken cancellationToken)
        {
            var userId = this.HttpContext.Items["UserId"] as string;
            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized();
            }

            var chat = await this.chatService.GetChatByIdAsync(id, cancellationToken);
            if (chat == null)
            {
                return this.NotFound(new { error = "Chat not found." });
            }

            if (!chat.ParticipantIds.Contains(userId))
            {
                return this.Forbid();
            }

            await this.chatService.DeleteChatAsync(id, cancellationToken);
            this.logger.Information("Chat {ChatId} deleted by user {UserId}", id, userId);

            return this.Ok(new { message = "Chat deleted successfully." });
        }
    }
}
