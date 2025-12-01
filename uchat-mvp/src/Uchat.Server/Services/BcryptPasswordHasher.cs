namespace Uchat.Server.Services
{
    using System.Threading.Tasks;
    using BCrypt.Net;
    using Microsoft.Extensions.Options;
    using Uchat.Server.Configuration.Options;
    using Uchat.Shared.Abstractions;

    /// <summary>
    /// BCrypt implementation of password hashing.
    /// </summary>
    public class BcryptPasswordHasher : IPasswordHasher
    {
        private readonly BcryptOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="BcryptPasswordHasher"/> class.
        /// </summary>
        /// <param name="options">BCrypt configuration options.</param>
        public BcryptPasswordHasher(IOptions<BcryptOptions> options)
        {
            this.options = options.Value;
        }

        /// <inheritdoc/>
        public Task<string> HashPasswordAsync(string password)
        {
            var hash = BCrypt.HashPassword(password, this.options.WorkFactor);
            return Task.FromResult(hash);
        }

        /// <inheritdoc/>
        public Task<bool> VerifyPasswordAsync(string password, string hash)
        {
            var isValid = BCrypt.Verify(password, hash);
            return Task.FromResult(isValid);
        }
    }
}
