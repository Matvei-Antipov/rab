namespace Uchat.Server.Services.Abstractions
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// Service for message operations.
    /// </summary>
    public interface IMessageService
    {
        /// <summary>
        /// Sends a new message.
        /// </summary>
        /// <param name="request">The message request.</param>
        /// <param name="senderId">The sender's user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created message DTO.</returns>
        Task<MessageDto> SendMessageAsync(SendMessageRequest request, string senderId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Edits an existing message.
        /// </summary>
        /// <param name="messageId">The message ID to edit.</param>
        /// <param name="newContent">The new content.</param>
        /// <param name="userId">The requesting user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated message DTO.</returns>
        Task<MessageDto> EditMessageAsync(string messageId, string newContent, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a message.
        /// </summary>
        /// <param name="messageId">The message ID to delete.</param>
        /// <param name="userId">The requesting user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat ID that the deleted message belonged to.</returns>
        Task<string> DeleteMessageAsync(string messageId, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets messages for a specific chat.
        /// </summary>
        /// <param name="chatId">The chat ID.</param>
        /// <param name="limit">Maximum number of messages.</param>
        /// <param name="offset">Number of messages to skip.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of message DTOs.</returns>
        Task<IEnumerable<MessageDto>> GetChatMessagesAsync(string chatId, int limit, int offset, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a message as delivered.
        /// </summary>
        /// <param name="messageId">The message ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task MarkAsDeliveredAsync(string messageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a message as read.
        /// </summary>
        /// <param name="messageId">The message ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for messages matching a query within a chat.
        /// </summary>
        /// <param name="chatId">The chat ID.</param>
        /// <param name="query">The search query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of matching message DTOs.</returns>
        Task<IEnumerable<MessageDto>> SearchMessagesAsync(string chatId, string query, CancellationToken cancellationToken = default);
    }
}
