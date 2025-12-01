namespace Uchat.Server.Controllers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using Uchat.Server.Models;
    using Uchat.Server.Services.Abstractions;
    using Uchat.Shared.Dtos;
    using Uchat.Shared.Exceptions;

    /// <summary>
    /// Controller for authentication endpoints.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService authService;
        private readonly IRateLimitService rateLimitService;
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="authService">Authentication service.</param>
        /// <param name="rateLimitService">Rate limit service.</param>
        /// <param name="logger">Logger instance.</param>
        public AuthController(IAuthService authService, IRateLimitService rateLimitService, ILogger logger)
        {
            this.authService = authService;
            this.rateLimitService = rateLimitService;
            this.logger = logger;
        }

        /// <summary>
        /// Registers a new user account.
        /// </summary>
        /// <param name="request">Registration request data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Registration response.</returns>
        [HttpPost("register")]
        [ProducesResponseType(typeof(RegisterResponse), 200)]
        [ProducesResponseType(typeof(RegisterResponse), 400)]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
        {
            if (!this.ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            var response = await this.authService.RegisterAsync(request, cancellationToken);

            if (!response.Success)
            {
                return this.BadRequest(response);
            }

            this.logger.Information("User registration successful via API");
            return this.Ok(response);
        }

        /// <summary>
        /// Authenticates a user and returns JWT tokens.
        /// </summary>
        /// <param name="request">Login request data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Login response with access and refresh tokens.</returns>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 401)]
        [ProducesResponseType(typeof(ErrorResponse), 429)]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            if (!this.ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            var identifier = request.UsernameOrEmail;
            var isAllowed = await this.rateLimitService.IsLoginAllowedAsync(identifier, cancellationToken);
            if (!isAllowed)
            {
                var resetTime = await this.rateLimitService.GetLoginRateLimitResetTimeAsync(identifier, cancellationToken);
                this.logger.Warning("Rate limit exceeded for login attempt: {Identifier}", identifier);
                return this.StatusCode(429, new ErrorResponse
                {
                    Error = "Too many login attempts",
                    Details = $"Please try again in {resetTime} seconds",
                });
            }

            try
            {
                var response = await this.authService.LoginAsync(request, cancellationToken);
                this.logger.Information("User login successful via API");
                return this.Ok(response);
            }
            catch (AuthenticationException ex)
            {
                this.logger.Warning(ex, "Authentication failed");
                return this.Unauthorized(new ErrorResponse { Error = ex.Message });
            }
        }
    }
}
