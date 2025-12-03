namespace Uchat.Server.Services.Abstractions
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// Service for chat operations.
    /// </summary>
    public interface IChatService
    {
        /// <summary>
        /// Gets all chats for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of chat DTOs.</returns>
        Task<IEnumerable<ChatDto>> GetUserChatsAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific chat by ID.
        /// </summary>
        /// <param name="chatId">The chat ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The chat DTO if found.</returns>
        Task<ChatDto?> GetChatByIdAsync(string chatId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new chat.
        /// </summary>
        /// <param name="request">The create chat request data.</param>
        /// <param name="creatorUserId">The user ID of the chat creator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created chat DTO with participant details.</returns>
        Task<ChatDto> CreateChatAsync(CreateChatRequest request, string creatorUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a chat.
        /// </summary>
        /// <param name="chatId">The chat ID to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteChatAsync(string chatId, CancellationToken cancellationToken = default);
    }
}
