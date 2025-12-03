namespace Uchat.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// Service for managing real-time messaging via WebSocket and REST API.
    /// </summary>
    public interface IMessagingService
    {
        /// <summary>
        /// Gets a value indicating whether the WebSocket is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Occurs when a new message is received.
        /// </summary>
        event EventHandler<MessageDto>? MessageReceived;

        /// <summary>
        /// Occurs when a message is edited.
        /// </summary>
        event EventHandler<MessageDto>? MessageEdited;

        /// <summary>
        /// Occurs when a message is deleted.
        /// </summary>
        event EventHandler<string>? MessageDeleted;

        /// <summary>
        /// Occurs when a user starts typing.
        /// </summary>
        event EventHandler<string>? UserTyping;

        /// <summary>
        /// Occurs when a user's status changes.
        /// </summary>
        event EventHandler<StatusUpdateDto>? UserStatusChanged;

        /// <summary>
        /// Occurs when the connection state changes.
        /// </summary>
        event EventHandler<bool>? ConnectionStateChanged;

        /// <summary>
        /// Connects to the WebSocket server.
        /// </summary>
        /// <param name="accessToken">Authentication token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ConnectAsync(string accessToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the WebSocket server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a new message via WebSocket.
        /// </summary>
        /// <param name="chatId">The chat ID.</param>
        /// <param name="content">The message content.</param>
        /// <param name="replyToId">Optional ID of message being replied to.</param>
        /// <param name="attachmentIds">Optional list of attachment IDs.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendMessageAsync(string chatId, string content, string? replyToId = null, List<string>? attachmentIds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a new message via HTTP API (for attachments support).
        /// </summary>
        /// <param name="chatId">The chat ID.</param>
        /// <param name="content">The message content.</param>
        /// <param name="replyToId">Optional ID of message being replied to.</param>
        /// <param name="attachmentIds">Optional list of attachment IDs.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created message DTO.</returns>
        Task<MessageDto> SendMessageViaHttpAsync(string chatId, string content, string? replyToId = null, List<string>? attachmentIds = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Edits an existing message.
        /// </summary>
        /// <param name="messageId">The message ID.</param>
        /// <param name="content">The new content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EditMessageAsync(string messageId, string content, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a message.
        /// </summary>
        /// <param name="messageId">The message ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends typing indicator.
        /// </summary>
        /// <param name="chatId">The chat ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendTypingIndicatorAsync(string chatId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves message history for a chat.
        /// </summary>
        /// <param name="chatId">The chat ID.</param>
        /// <param name="limit">Number of messages to retrieve.</param>
        /// <param name="offset">Number of messages to skip.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of messages.</returns>
        Task<List<MessageDto>> GetMessageHistoryAsync(string chatId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for messages within a specific chat.
        /// </summary>
        /// <param name="chatId">The chat ID.</param>
        /// <param name="query">The search query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of matching messages.</returns>
        Task<List<MessageDto>> SearchMessagesAsync(string chatId, string query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the list of chats for the current user.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of chats.</returns>
        Task<List<ChatDto>> GetChatsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a specific chat by ID.
        /// </summary>
        /// <param name="chatId">The chat ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat DTO.</returns>
        Task<ChatDto> GetChatByIdAsync(string chatId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new chat.
        /// </summary>
        /// <param name="name">The chat name.</param>
        /// <param name="participantIds">The list of participant user IDs.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created chat DTO.</returns>
        Task<ChatDto> CreateChatAsync(string name, List<string> participantIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Blocks a user.
        /// </summary>
        /// <param name="userId">The user ID to block.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BlockUserAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a chat.
        /// </summary>
        /// <param name="chatId">The chat ID to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteChatAsync(string chatId, CancellationToken cancellationToken = default);
    }
}
