namespace Uchat.Server.Services
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using Uchat.Server.Services.Abstractions;
    using Uchat.Shared.Enums;
    using Uchat.Shared.Helpers;

    /// <summary>
    /// Redis-based implementation of the user status service.
    /// </summary>
    public class UserStatusService : IUserStatusService
    {
        private readonly IRedisMultiplexer redisMultiplexer;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserStatusService"/> class.
        /// </summary>
        /// <param name="redisMultiplexer">The Redis multiplexer.</param>
        /// <param name="logger">Logger instance.</param>
        public UserStatusService(IRedisMultiplexer redisMultiplexer, ILogger logger)
        {
            this.redisMultiplexer = redisMultiplexer;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task<UserStatus> GetStatusAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = this.redisMultiplexer.GetDatabase();
                var key = RedisKeys.UserStatus(userId);
                var value = await db.StringGetAsync(key);

                if (!value.HasValue)
                {
                    this.logger.Debug("Status not found in Redis for user {UserId}, returning Offline", userId);
                    return UserStatus.Offline;
                }

                if (int.TryParse(value.ToString(), out var statusValue) &&
                    Enum.IsDefined(typeof(UserStatus), statusValue))
                {
                    return (UserStatus)statusValue;
                }

                this.logger.Warning("Invalid status value in Redis for user {UserId}: {Value}, returning Offline", userId, value);
                return UserStatus.Offline;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Error getting status from Redis for user {UserId}, returning Offline", userId);
                return UserStatus.Offline;
            }
        }

        /// <inheritdoc/>
        public async Task SetStatusAsync(string userId, UserStatus status, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = this.redisMultiplexer.GetDatabase();
                var key = RedisKeys.UserStatus(userId);
                var statusValue = ((int)status).ToString();

                // Set status with expiration of 1 hour (will be refreshed on heartbeat)
                await db.StringSetAsync(key, statusValue, TimeSpan.FromHours(1));

                this.logger.Debug("Set status {Status} for user {UserId} in Redis", status, userId);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Error setting status in Redis for user {UserId}", userId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task RemoveStatusAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = this.redisMultiplexer.GetDatabase();
                var key = RedisKeys.UserStatus(userId);
                await db.KeyDeleteAsync(key);

                this.logger.Debug("Removed status for user {UserId} from Redis", userId);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Error removing status from Redis for user {UserId}", userId);
                throw;
            }
        }
    }
}
