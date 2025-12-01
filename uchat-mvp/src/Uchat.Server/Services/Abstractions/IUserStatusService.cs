namespace Uchat.Server.Services.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;
    using Uchat.Shared.Enums;

    /// <summary>
    /// Service for managing user online/offline status in Redis.
    /// </summary>
    public interface IUserStatusService
    {
        /// <summary>
        /// Gets the current status of a user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The user status, or Offline if not found.</returns>
        Task<UserStatus> GetStatusAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the status of a user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="status">The new status.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetStatusAsync(string userId, UserStatus status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the status of a user (sets to offline).
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemoveStatusAsync(string userId, CancellationToken cancellationToken = default);
    }
}
