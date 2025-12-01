namespace Uchat.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using Uchat.Shared.Dtos;

    /// <summary>
    /// Implementation of authentication service that communicates with the server API.
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly HttpClient httpClient;
        private readonly ICredentialStorageService credentialStorage;
        private readonly ILogger logger;

        private UserDto? currentUser;
        private string? accessToken;
        private string? refreshToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationService"/> class.
        /// </summary>
        /// <param name="httpClient">HTTP client for API requests.</param>
        /// <param name="credentialStorage">Credential storage service.</param>
        /// <param name="logger">Logger instance.</param>
        public AuthenticationService(HttpClient httpClient, ICredentialStorageService credentialStorage, ILogger logger)
        {
            this.httpClient = httpClient;
            this.credentialStorage = credentialStorage;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public UserDto? CurrentUser => this.currentUser;

        /// <inheritdoc/>
        public bool IsAuthenticated => this.currentUser != null && !string.IsNullOrEmpty(this.accessToken);

        /// <inheritdoc/>
        public string? AccessToken => this.accessToken;

        /// <inheritdoc/>
        public async Task<LoginResponse> LoginAsync(string usernameOrEmail, string password, bool rememberMe, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new LoginRequest
                {
                    UsernameOrEmail = usernameOrEmail,
                    Password = password,
                };

                var response = await this.httpClient.PostAsJsonAsync("/api/auth/login", request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
                    if (loginResponse == null)
                    {
                        throw new InvalidOperationException("Login response was null");
                    }

                    this.accessToken = loginResponse.AccessToken;
                    this.refreshToken = loginResponse.RefreshToken;
                    this.currentUser = loginResponse.User;

                    this.credentialStorage.SaveTokens(loginResponse.AccessToken, loginResponse.RefreshToken, loginResponse.User);

                    if (rememberMe)
                    {
                        this.credentialStorage.SaveCredentials(usernameOrEmail, password);
                    }
                    else
                    {
                        this.credentialStorage.ClearCredentials();
                    }

                    this.logger.Information("User logged in successfully: {Username}", loginResponse.User.Username);
                    return loginResponse;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    this.logger.Warning("Login failed: {StatusCode} - {Error}", response.StatusCode, errorContent);

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        this.logger.Warning("Unauthorized response received. Content: {Content}", errorContent);
                        var errorMessage = this.ExtractErrorMessageFromResponse(errorContent);
                        if (string.IsNullOrWhiteSpace(errorMessage))
                        {
                            errorMessage = "Invalid Login or Password";
                        }

                        this.logger.Warning("Login unauthorized: {Message}", errorMessage);
                        throw new InvalidOperationException(errorMessage);
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        var errorResponse = await this.TryDeserializeErrorResponseAsync(errorContent, cancellationToken);
                        var errorMessage = errorResponse?.Error ?? "Too many login attempts";
                        if (!string.IsNullOrEmpty(errorResponse?.Details))
                        {
                            errorMessage += $". {errorResponse.Details}";
                        }

                        throw new InvalidOperationException(errorMessage);
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        var validationMessage = this.ExtractValidationErrorMessage(errorContent) ?? "Invalid login request. Please check your input.";
                        throw new InvalidOperationException(validationMessage);
                    }

                    throw new InvalidOperationException("Unable to connect to server. Please check your network connection.");
                }
            }
            catch (HttpRequestException ex)
            {
                this.logger.Error(ex, "Network error during login");
                throw new InvalidOperationException("Unable to connect to server. Please check your network connection.", ex);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                this.logger.Error(ex, "Unexpected error during login");
                throw new InvalidOperationException("An unexpected error occurred during login.", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<RegisterResponse> RegisterAsync(string username, string email, string password, string displayName, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new RegisterRequest
                {
                    Username = username,
                    Email = email,
                    Password = password,
                    DisplayName = displayName,
                };

                var response = await this.httpClient.PostAsJsonAsync("/api/auth/register", request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var registerResponse = await response.Content.ReadFromJsonAsync<RegisterResponse>(cancellationToken);
                    if (registerResponse == null)
                    {
                        throw new InvalidOperationException("Register response was null");
                    }

                    this.logger.Information("User registered successfully: {Username}", username);
                    return registerResponse;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    this.logger.Warning("Registration failed: {StatusCode} - {Error}", response.StatusCode, errorContent);

                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        // Try to parse RegisterResponse first (for business logic errors)
                        var registerResponse = await this.TryDeserializeRegisterResponseAsync(errorContent, cancellationToken);
                        if (registerResponse != null && !string.IsNullOrEmpty(registerResponse.ErrorMessage))
                        {
                            return registerResponse;
                        }

                        // Try to parse validation errors from ModelState
                        var validationMessage = this.ExtractValidationErrorMessage(errorContent);
                        if (!string.IsNullOrEmpty(validationMessage))
                        {
                            return new RegisterResponse
                            {
                                Success = false,
                                ErrorMessage = validationMessage,
                            };
                        }
                    }

                    // Try to parse RegisterResponse for other errors
                    var errorRegisterResponse = await this.TryDeserializeRegisterResponseAsync(errorContent, cancellationToken);
                    if (errorRegisterResponse != null && !string.IsNullOrEmpty(errorRegisterResponse.ErrorMessage))
                    {
                        return errorRegisterResponse;
                    }

                    return new RegisterResponse
                    {
                        Success = false,
                        ErrorMessage = "Registration failed. Please check your input and try again.",
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                this.logger.Error(ex, "Network error during registration");
                return new RegisterResponse
                {
                    Success = false,
                    ErrorMessage = "Unable to connect to server. Please check your network connection.",
                };
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Unexpected error during registration");
                return new RegisterResponse
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred during registration.",
                };
            }
        }

        /// <inheritdoc/>
        public Task LogoutAsync()
        {
            this.currentUser = null;
            this.accessToken = null;
            this.refreshToken = null;
            this.credentialStorage.ClearAll();

            this.logger.Information("User logged out");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var savedTokens = this.credentialStorage.GetSavedTokens();
                if (savedTokens.HasValue)
                {
                    this.accessToken = savedTokens.Value.AccessToken;
                    this.refreshToken = savedTokens.Value.RefreshToken;
                    this.currentUser = savedTokens.Value.User;

                    this.logger.Information("Session restored from saved tokens");
                    return true;
                }

                var savedCredentials = this.credentialStorage.GetSavedCredentials();
                if (savedCredentials.HasValue)
                {
                    try
                    {
                        await this.LoginAsync(
                            savedCredentials.Value.UsernameOrEmail,
                            savedCredentials.Value.Password,
                            true,
                            cancellationToken);

                        this.logger.Information("Session restored using saved credentials");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        this.logger.Warning(ex, "Failed to restore session using saved credentials");
                        this.credentialStorage.ClearCredentials();
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Error restoring session");
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            this.logger.Information("Token refresh not yet implemented");
            return Task.FromResult(false);
        }

        private string ExtractErrorMessageFromResponse(string errorContent)
        {
            if (string.IsNullOrWhiteSpace(errorContent))
            {
                this.logger.Debug("Error content is empty");
                return string.Empty;
            }

            try
            {
                this.logger.Debug("Attempting to parse error content: {Content}", errorContent);
                var errorObject = JsonSerializer.Deserialize<JsonElement>(errorContent);

                // Try to extract from ErrorResponse format (Error property) - lowercase
                if (errorObject.TryGetProperty("error", out var errorProp))
                {
                    var errorValue = errorProp.GetString();
                    if (!string.IsNullOrWhiteSpace(errorValue))
                    {
                        this.logger.Debug("Extracted error message from 'error' property: {Message}", errorValue);
                        return errorValue;
                    }
                }

                // Try to extract from ErrorResponse format (Error property) - uppercase (case-insensitive fallback)
                if (errorObject.TryGetProperty("Error", out var errorPropUpper))
                {
                    var errorValue = errorPropUpper.GetString();
                    if (!string.IsNullOrWhiteSpace(errorValue))
                    {
                        this.logger.Debug("Extracted error message from 'Error' property: {Message}", errorValue);
                        return errorValue;
                    }
                }

                // Try to extract from Message format
                if (errorObject.TryGetProperty("message", out var messageProp))
                {
                    var messageValue = messageProp.GetString();
                    if (!string.IsNullOrWhiteSpace(messageValue))
                    {
                        this.logger.Debug("Extracted error message from 'message' property: {Message}", messageValue);
                        return messageValue;
                    }
                }

                // Try to extract from Detail format
                if (errorObject.TryGetProperty("detail", out var detailProp))
                {
                    var detailValue = detailProp.GetString();
                    if (!string.IsNullOrWhiteSpace(detailValue))
                    {
                        this.logger.Debug("Extracted error message from 'detail' property: {Message}", detailValue);
                        return detailValue;
                    }
                }

                // Try to extract from Title format (ASP.NET Core ProblemDetails)
                if (errorObject.TryGetProperty("title", out var titleProp))
                {
                    var titleValue = titleProp.GetString();
                    if (!string.IsNullOrWhiteSpace(titleValue))
                    {
                        this.logger.Debug("Extracted error message from 'title' property: {Message}", titleValue);
                        return titleValue;
                    }
                }

                this.logger.Warning("Could not extract error message from response. Content: {Content}", errorContent);
            }
            catch (JsonException ex)
            {
                this.logger.Warning(ex, "Failed to parse error response as JSON: {Content}", errorContent);

                // If it's not JSON, return the content as-is (might be plain text)
                if (!string.IsNullOrWhiteSpace(errorContent))
                {
                    return errorContent.Trim();
                }
            }
            catch (Exception ex)
            {
                this.logger.Warning(ex, "Failed to parse error response: {Content}", errorContent);
            }

            return string.Empty;
        }

        private async Task<ErrorResponse?> TryDeserializeErrorResponseAsync(string content, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
                return await JsonSerializer.DeserializeAsync<ErrorResponse>(stream, cancellationToken: cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        private async Task<RegisterResponse?> TryDeserializeRegisterResponseAsync(string content, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
                return await JsonSerializer.DeserializeAsync<RegisterResponse>(stream, cancellationToken: cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        private string? ExtractValidationErrorMessage(string errorContent)
        {
            if (string.IsNullOrWhiteSpace(errorContent))
            {
                return null;
            }

            try
            {
                var errorObject = JsonSerializer.Deserialize<JsonElement>(errorContent);

                // Try to extract from ModelState format (ASP.NET Core validation)
                if (errorObject.TryGetProperty("errors", out var errorsProp))
                {
                    var errorMessages = new List<string>();
                    foreach (var error in errorsProp.EnumerateObject())
                    {
                        var fieldName = error.Name;
                        foreach (var message in error.Value.EnumerateArray())
                        {
                            var msg = message.GetString();
                            if (!string.IsNullOrWhiteSpace(msg))
                            {
                                // Format field-specific errors
                                if (fieldName.Equals("Password", StringComparison.OrdinalIgnoreCase))
                                {
                                    errorMessages.Add($"Password: {msg}");
                                }
                                else if (fieldName.Equals("Username", StringComparison.OrdinalIgnoreCase))
                                {
                                    errorMessages.Add($"Username: {msg}");
                                }
                                else if (fieldName.Equals("Email", StringComparison.OrdinalIgnoreCase))
                                {
                                    errorMessages.Add($"Email: {msg}");
                                }
                                else if (fieldName.Equals("DisplayName", StringComparison.OrdinalIgnoreCase))
                                {
                                    errorMessages.Add($"Display Name: {msg}");
                                }
                                else
                                {
                                    errorMessages.Add(msg);
                                }
                            }
                        }
                    }

                    if (errorMessages.Count > 0)
                    {
                        return string.Join(" ", errorMessages);
                    }
                }

                // Try to extract from ErrorResponse format
                if (errorObject.TryGetProperty("error", out var errorProp))
                {
                    var errorValue = errorProp.GetString();
                    if (!string.IsNullOrWhiteSpace(errorValue))
                    {
                        return errorValue;
                    }
                }

                // Try to extract from Message format
                if (errorObject.TryGetProperty("message", out var messageProp))
                {
                    var messageValue = messageProp.GetString();
                    if (!string.IsNullOrWhiteSpace(messageValue))
                    {
                        return messageValue;
                    }
                }

                // Try to extract from Detail format
                if (errorObject.TryGetProperty("detail", out var detailProp))
                {
                    var detailValue = detailProp.GetString();
                    if (!string.IsNullOrWhiteSpace(detailValue))
                    {
                        return detailValue;
                    }
                }

                // Try to extract from Title format (ASP.NET Core ProblemDetails)
                if (errorObject.TryGetProperty("title", out var titleProp))
                {
                    var titleValue = titleProp.GetString();
                    if (!string.IsNullOrWhiteSpace(titleValue))
                    {
                        return titleValue;
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return null;
        }

        private class ErrorResponse
        {
            public string Error { get; set; } = string.Empty;

            public string? Details { get; set; }
        }
    }
}
