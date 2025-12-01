# Security Guide

This document outlines security considerations, best practices, and implementation details for the Uchat MVP application.

## Table of Contents

1. [Authentication](#authentication)
2. [Authorization](#authorization)
3. [Password Security](#password-security)
4. [JWT Token Management](#jwt-token-management)
5. [API Security](#api-security)
6. [Database Security](#database-security)
7. [WebSocket Security](#websocket-security)
8. [Data Protection](#data-protection)
9. [Threat Mitigation](#threat-mitigation)
10. [Security Audit](#security-audit)
11. [Compliance](#compliance)

## Authentication

### Overview

Uchat uses JWT (JSON Web Token) based authentication for stateless, scalable authentication across multiple server instances.

### Authentication Flow

```
┌────────┐                ┌────────┐                ┌──────────┐
│ Client │                │ Server │                │ Database │
└───┬────┘                └───┬────┘                └────┬─────┘
    │                         │                          │
    │ POST /auth/login        │                          │
    │ {username, password}    │                          │
    ├────────────────────────>│                          │
    │                         │ Query user by username   │
    │                         ├─────────────────────────>│
    │                         │ User record              │
    │                         │<─────────────────────────┤
    │                         │                          │
    │                         │ Verify password hash     │
    │                         │ (BCrypt.Verify)          │
    │                         │                          │
    │                         │ Generate JWT token       │
    │                         │                          │
    │ 200 OK                  │                          │
    │ {token, expiresAt}      │                          │
    │<────────────────────────┤                          │
    │                         │                          │
    │ GET /api/users/me       │                          │
    │ Authorization: Bearer token                        │
    ├────────────────────────>│                          │
    │                         │ Validate token           │
    │                         │ Extract userId           │
    │                         │                          │
    │                         │ Query user data          │
    │                         ├─────────────────────────>│
    │                         │ User data                │
    │                         │<─────────────────────────┤
    │ 200 OK {user}           │                          │
    │<────────────────────────┤                          │
```

### Implementation Details

#### Registration

```csharp
// Uchat.Server/Controllers/AuthController.cs
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterDto request)
{
    // 1. Validate input
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    // 2. Check if user already exists
    var existingUser = await userRepository.GetByUsernameAsync(request.Username);
    if (existingUser != null)
        return Conflict("Username already exists");

    // 3. Hash password
    var passwordHash = passwordHasher.HashPassword(request.Password);

    // 4. Create user
    var user = new User
    {
        Id = idGenerator.GenerateId(),
        Username = request.Username,
        Email = request.Email,
        PasswordHash = passwordHash,
        DisplayName = request.DisplayName,
        CreatedAt = clock.UtcNow,
        UpdatedAt = clock.UtcNow
    };

    await userRepository.CreateAsync(user);

    // 5. Generate token
    var token = jwtService.GenerateToken(user);

    return Ok(new { Token = token, ExpiresAt = token.ExpiresAt });
}
```

#### Login

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginDto request)
{
    // 1. Get user by username
    var user = await userRepository.GetByUsernameAsync(request.Username);
    if (user == null)
        return Unauthorized("Invalid credentials");

    // 2. Verify password
    if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        return Unauthorized("Invalid credentials");

    // 3. Generate JWT token
    var token = jwtService.GenerateToken(user);

    // 4. Store session in Redis
    await sessionService.CreateSessionAsync(user.Id, token.Id);

    return Ok(new { Token = token.Value, ExpiresAt = token.ExpiresAt });
}
```

#### Logout

```csharp
[HttpPost("logout")]
[Authorize]
public async Task<IActionResult> Logout()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var tokenId = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

    // Blacklist token in Redis
    await tokenBlacklistService.BlacklistTokenAsync(tokenId);

    // Remove session
    await sessionService.DeleteSessionAsync(userId);

    return Ok();
}
```

## Authorization

### Access Control

Current implementation uses resource-based authorization:

#### User Resources

- Users can only access their own profile
- Users can view other users' basic public information

```csharp
[HttpGet("{id}")]
[Authorize]
public async Task<IActionResult> GetUser(string id)
{
    var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    var user = await userRepository.GetByIdAsync(id);
    if (user == null)
        return NotFound();

    // Return full profile only for own user
    if (id == currentUserId)
        return Ok(mapper.Map<UserDto>(user));
    
    // Return limited public profile for others
    return Ok(mapper.Map<PublicUserDto>(user));
}
```

#### Chat Resources

- Users can only access chats they are participants in
- Only chat creator or message author can delete messages

```csharp
[HttpGet("{chatId}/messages")]
[Authorize]
public async Task<IActionResult> GetMessages(string chatId)
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
    // Verify user is participant
    var isParticipant = await chatRepository.IsUserParticipantAsync(chatId, userId);
    if (!isParticipant)
        return Forbid();

    var messages = await messageRepository.GetByChatIdAsync(chatId);
    return Ok(messages);
}
```

### Future: Role-Based Access Control (RBAC)

```csharp
public enum UserRole
{
    User = 0,
    Moderator = 1,
    Admin = 2
}

// Usage:
[Authorize(Roles = "Admin,Moderator")]
public async Task<IActionResult> DeleteAnyMessage(string messageId)
{
    // ...
}
```

## Password Security

### Password Hashing

**Algorithm**: BCrypt  
**Work Factor**: 12 (configurable)  
**Library**: BCrypt.Net-Next

#### Implementation

```csharp
// Uchat.Shared/Helpers/PasswordHasher.cs
public class PasswordHasher : IPasswordHasher
{
    private readonly int workFactor;

    public PasswordHasher(IOptions<PasswordHashingOptions> options)
    {
        this.workFactor = options.Value.WorkFactor;
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, this.workFactor);
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}
```

### Password Requirements

Enforce strong password policies:

```csharp
public class PasswordValidator
{
    public ValidationResult Validate(string password)
    {
        var errors = new List<string>();

        if (password.Length < 8)
            errors.Add("Password must be at least 8 characters long");

        if (!password.Any(char.IsUpper))
            errors.Add("Password must contain at least one uppercase letter");

        if (!password.Any(char.IsLower))
            errors.Add("Password must contain at least one lowercase letter");

        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit");

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            errors.Add("Password must contain at least one special character");

        // Check against common passwords
        if (CommonPasswords.Contains(password.ToLower()))
            errors.Add("Password is too common");

        return errors.Any() 
            ? ValidationResult.Failed(errors) 
            : ValidationResult.Success;
    }
}
```

### Password Storage

- **Never** store plaintext passwords
- Only store BCrypt hashed passwords in `users.password_hash` column
- Salt is automatically included in BCrypt hash
- Rainbow table attacks are mitigated by individual salts

## JWT Token Management

### Token Structure

```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "sub": "user_123",
    "username": "john.doe",
    "email": "john@example.com",
    "jti": "token_unique_id",
    "iat": 1234567890,
    "exp": 1234571490,
    "iss": "uchat-server",
    "aud": "uchat-client"
  },
  "signature": "..."
}
```

### Token Generation

```csharp
public class JwtService
{
    private readonly JwtSettings settings;
    private readonly SymmetricSecurityKey key;

    public JwtService(IOptions<JwtSettings> options)
    {
        this.settings = options.Value;
        this.key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(this.settings.Secret)
        );
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Iat, 
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var credentials = new SigningCredentials(
            this.key, 
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: this.settings.Issuer,
            audience: this.settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(this.settings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### Token Validation

```csharp
// Configure in Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret)
            ),
            ClockSkew = TimeSpan.Zero
        };
    });
```

### Token Blacklist

Revoked tokens are stored in Redis with TTL equal to token expiration:

```csharp
public class TokenBlacklistService
{
    private readonly IConnectionMultiplexer redis;
    private const string KeyPrefix = "uchat:token:blacklist:";

    public async Task BlacklistTokenAsync(string tokenId, TimeSpan expiration)
    {
        var db = this.redis.GetDatabase();
        var key = $"{KeyPrefix}{tokenId}";
        await db.StringSetAsync(key, "revoked", expiration);
    }

    public async Task<bool> IsBlacklistedAsync(string tokenId)
    {
        var db = this.redis.GetDatabase();
        var key = $"{KeyPrefix}{tokenId}";
        return await db.KeyExistsAsync(key);
    }
}
```

### Token Security Best Practices

1. **Use Strong Secrets**: Minimum 32 characters, cryptographically random
2. **Short Expiration**: 60 minutes maximum (15-30 minutes recommended)
3. **HTTPS Only**: Never transmit tokens over HTTP
4. **Secure Storage**: Client should store tokens securely (encrypted storage)
5. **Token Refresh**: Implement refresh tokens for better UX
6. **Blacklist on Logout**: Add to blacklist immediately
7. **Validate All Claims**: Check issuer, audience, expiration

## API Security

### Rate Limiting

Prevent abuse and DDoS attacks:

```csharp
public class RateLimitingMiddleware
{
    private readonly RequestDelegate next;
    private readonly IConnectionMultiplexer redis;

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var endpoint = context.Request.Path.Value;
        
        var key = $"uchat:ratelimit:{userId}:{endpoint}";
        var db = this.redis.GetDatabase();
        
        var current = await db.StringIncrementAsync(key);
        
        if (current == 1)
        {
            await db.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
        }
        
        if (current > 100) // 100 requests per minute
        {
            context.Response.StatusCode = 429; // Too Many Requests
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }
        
        await this.next(context);
    }
}
```

### CORS Configuration

Restrict cross-origin requests:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("UchatCorsPolicy", policy =>
    {
        policy.WithOrigins(
            configuration["Cors:AllowedOrigins"].Split(',')
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

app.UseCors("UchatCorsPolicy");
```

### Input Validation

Validate all user input:

```csharp
public class RegisterDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_.-]+$")]
    public string Username { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string DisplayName { get; set; }
}
```

### SQL Injection Prevention

Always use parameterized queries:

```csharp
// ✅ SAFE: Parameterized query
var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM users WHERE username = :username";
command.Parameters.Add(new OracleParameter("username", username));

// ❌ UNSAFE: String concatenation
var command = connection.CreateCommand();
command.CommandText = $"SELECT * FROM users WHERE username = '{username}'";
```

### XSS Prevention

Encode output and sanitize HTML:

```csharp
// In views/templates
@Html.Encode(user.DisplayName)

// Sanitize HTML content
public string SanitizeHtml(string html)
{
    return HttpUtility.HtmlEncode(html);
}
```

### CSRF Protection

Use anti-forgery tokens for state-changing operations:

```csharp
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult Delete(int id)
{
    // ...
}
```

## Database Security

### Connection Security

1. **Use SSL/TLS**: Enable encrypted connections
   ```
   Oracle: (DESCRIPTION=(ADDRESS=(PROTOCOL=TCPS)(HOST=...)))
   MongoDB: mongodb://...?ssl=true
   Redis: SSL=true in connection string
   ```

2. **Least Privilege**: Grant only required permissions
   ```sql
   -- Oracle
   GRANT SELECT, INSERT, UPDATE, DELETE ON users TO uchat_app;
   REVOKE ALL ON sys.* FROM uchat_app;
   ```

3. **Credential Management**: Never hardcode credentials
   - Use environment variables
   - Use secret management services (Azure Key Vault, AWS Secrets Manager)
   - Rotate credentials regularly

### Data Encryption

1. **At Rest**: Enable database encryption
   - Oracle: Transparent Data Encryption (TDE)
   - MongoDB: Encryption at Rest
   - Redis: Disk encryption at OS level

2. **In Transit**: Always use SSL/TLS

3. **Sensitive Fields**: Additional encryption for very sensitive data
   ```csharp
   public string EncryptSensitiveData(string data, string key)
   {
       using var aes = Aes.Create();
       aes.Key = Encoding.UTF8.GetBytes(key);
       // ... encryption logic
   }
   ```

### Audit Logging

Track all database changes:

```sql
-- Already implemented via triggers
SELECT * FROM message_edit_history 
WHERE message_id = 'msg_123';

SELECT * FROM message_delete_log 
WHERE deleted_by = 'user_456';
```

## WebSocket Security

### Authentication

Authenticate WebSocket connections:

```csharp
public class WebSocketAuthMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            // Get token from query string or first message
            var token = context.Request.Query["token"].ToString();
            
            // Validate token
            var principal = await ValidateTokenAsync(token);
            if (principal == null)
            {
                context.Response.StatusCode = 401;
                return;
            }
            
            context.User = principal;
            
            // Accept WebSocket
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await HandleWebSocketAsync(webSocket, context);
        }
        else
        {
            await this.next(context);
        }
    }
}
```

### Message Validation

Validate all incoming WebSocket messages:

```csharp
public async Task<bool> ValidateMessageAsync(WebSocketMessage message, string userId)
{
    // Verify user is participant of target chat
    if (message is MessageSentContract sent)
    {
        var isParticipant = await chatRepository.IsUserParticipantAsync(
            sent.ChatId, userId
        );
        
        if (!isParticipant)
            return false;
    }
    
    // Verify user owns the resource being modified
    if (message is MessageEditedContract edited)
    {
        var originalMessage = await messageRepository.GetByIdAsync(edited.MessageId);
        if (originalMessage.SenderId != userId)
            return false;
    }
    
    return true;
}
```

## Data Protection

### Personal Information

Minimize collection and storage of personal data:

- Username (required)
- Email (required)
- Display name (required)
- Avatar URL (optional)
- Status, timestamps (functional)

### Data Retention

Implement retention policies:

```sql
-- Delete old audit logs
DELETE FROM message_edit_history 
WHERE edited_at < SYSDATE - 90;

DELETE FROM message_delete_log 
WHERE deleted_at < SYSDATE - 90;
```

### Right to Be Forgotten

Implement user data deletion:

```csharp
public async Task DeleteUserDataAsync(string userId)
{
    // Soft delete messages (preserve chat history)
    await messageRepository.AnonymizeMessagesAsync(userId);
    
    // Delete preferences
    await preferencesRepository.DeleteAsync(userId);
    
    // Delete sessions
    await sessionService.DeleteAllUserSessionsAsync(userId);
    
    // Delete user record
    await userRepository.DeleteAsync(userId);
}
```

## Threat Mitigation

### Brute Force Protection

Implement account lockout:

```csharp
public class LoginAttemptTracker
{
    private const int MaxAttempts = 5;
    private const int LockoutMinutes = 15;

    public async Task<bool> IsLockedOutAsync(string username)
    {
        var key = $"uchat:login:attempts:{username}";
        var db = redis.GetDatabase();
        
        var attempts = await db.StringGetAsync(key);
        return attempts.HasValue && int.Parse(attempts) >= MaxAttempts;
    }

    public async Task RecordFailedAttemptAsync(string username)
    {
        var key = $"uchat:login:attempts:{username}";
        var db = redis.GetDatabase();
        
        await db.StringIncrementAsync(key);
        await db.KeyExpireAsync(key, TimeSpan.FromMinutes(LockoutMinutes));
    }

    public async Task ResetAttemptsAsync(string username)
    {
        var key = $"uchat:login:attempts:{username}";
        var db = redis.GetDatabase();
        
        await db.KeyDeleteAsync(key);
    }
}
```

### Session Hijacking

Mitigate session hijacking:

1. **Bind to IP**: Validate client IP hasn't changed
2. **User Agent**: Check user agent consistency
3. **Token Rotation**: Rotate tokens periodically
4. **Secure Cookies**: Use HttpOnly, Secure, SameSite flags

### Man-in-the-Middle (MITM)

Prevent MITM attacks:

1. **Enforce HTTPS**: Redirect all HTTP to HTTPS
2. **HSTS Header**: `Strict-Transport-Security: max-age=31536000`
3. **Certificate Pinning**: Pin SSL certificates in client

## Security Audit

### Checklist

- [ ] All passwords hashed with BCrypt (work factor 12+)
- [ ] JWT secret is strong (32+ characters)
- [ ] HTTPS enabled in production
- [ ] Database connections use SSL/TLS
- [ ] Input validation on all endpoints
- [ ] Output encoding to prevent XSS
- [ ] Parameterized queries to prevent SQL injection
- [ ] Rate limiting enabled
- [ ] CORS configured restrictively
- [ ] Authentication required on protected endpoints
- [ ] Authorization checks enforce access control
- [ ] Tokens blacklisted on logout
- [ ] Sensitive data encrypted
- [ ] Audit logging enabled
- [ ] Error messages don't leak sensitive info
- [ ] Security headers configured
- [ ] Dependencies up to date
- [ ] Secrets not in source control

### Security Headers

Configure security headers:

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add(
        "Content-Security-Policy", 
        "default-src 'self'; script-src 'self'"
    );
    context.Response.Headers.Add(
        "Strict-Transport-Security", 
        "max-age=31536000; includeSubDomains"
    );
    
    await next();
});
```

## Compliance

### GDPR

Requirements for EU users:

1. **Consent**: Obtain explicit consent for data collection
2. **Right to Access**: Provide user data on request
3. **Right to Erasure**: Implement user deletion
4. **Data Portability**: Allow data export
5. **Breach Notification**: Report breaches within 72 hours

### HIPAA (if handling health data)

Additional requirements:

1. **Encryption**: All data must be encrypted
2. **Access Logs**: Maintain comprehensive audit logs
3. **BAA**: Business Associate Agreements required
4. **Minimum Necessary**: Only access needed data

### Best Practices

1. **Privacy Policy**: Clearly state data usage
2. **Terms of Service**: Define acceptable use
3. **Cookie Consent**: Obtain consent for cookies
4. **Age Verification**: Comply with COPPA (13+ in US)
5. **Data Classification**: Classify and protect accordingly
