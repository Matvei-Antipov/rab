namespace Uchat.Server.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using Uchat.Server.Data.Repositories;
    using Uchat.Server.Services.Abstractions;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// Controller for user-related endpoints.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository userRepository;
        private readonly ILogger logger;
        private readonly IUserStatusService userStatusService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="userRepository">User repository.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="userStatusService">User status service.</param>
        public UsersController(IUserRepository userRepository, ILogger logger, IUserStatusService userStatusService)
        {
            this.userRepository = userRepository;
            this.logger = logger;
            this.userStatusService = userStatusService;
        }

        /// <summary>
        /// Gets all available users for participant selection.
        /// Excludes the current authenticated user from the results.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of user DTOs.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<UserDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers(CancellationToken cancellationToken)
        {
            var userId = this.HttpContext.Items["UserId"] as string;
            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized();
            }

            this.logger.Information("Getting available users for user {UserId}", userId);

            var allUsers = await this.userRepository.GetAllAsync(cancellationToken);
            var filteredUsers = allUsers.Where(u => u.Id != userId);

            var userDtos = new List<UserDto>();
            foreach (var u in filteredUsers)
            {
                // Get status from Redis instead of Oracle
                var status = await this.userStatusService.GetStatusAsync(u.Id, cancellationToken);
                userDtos.Add(new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    Status = status,
                    LastSeenAt = u.LastSeenAt,
                });
            }

            return this.Ok(userDtos);
        }
    }
}
