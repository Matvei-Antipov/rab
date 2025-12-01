# Security Hardening Implementation

This document describes the security enhancements implemented for the Uchat server.

## Changes Overview

### 1. Input Validation with FluentValidation

**Dependencies Added:**
- `FluentValidation` (v11.9.0)
- `FluentValidation.AspNetCore` (v11.3.0)

**Validators Created:**
- `RegisterRequestValidator` - Validates user registration requests
  - Username: 3-50 characters, alphanumeric with underscores/hyphens
  - Email: Valid email format, max 255 characters
  - Password: Min 8 characters, must contain uppercase, lowercase, and digit
  - DisplayName: 1-100 characters

- `LoginRequestValidator` - Validates login requests
  - Username/Email: Required, max 255 characters
  - Password: Required, max 100 characters

- `SendMessageRequestValidator` - Validates message sending
  - ChatId: Required
  - Content: Required, max 10,000 characters
  - ReplyToId: Optional, max 26 characters

- `UserPreferenceDtoValidator` - Validates user preference updates
  - Theme: Must be 'light' or 'dark'
  - Language: 2-5 characters
  - MutedChats: Max 1000 items

**Integration:**
- Validators automatically registered via `AddValidators()` extension method
- Controllers check `ModelState.IsValid` before processing requests
- Consistent error responses via ModelState

### 2. Rate Limiting

**New Services:**
- `IRateLimitService` - Interface for rate limiting operations
- `RateLimitService` - Redis-based rate limiting implementation
- `RateLimitOptions` - Configuration options
- `RateLimitOptionsValidator` - Configuration validation

**Configuration:**
```json
"RateLimit": {
  "LoginMaxAttempts": 5,
  "LoginWindowSeconds": 300,
  "MessageMaxAttempts": 60,
  "MessageWindowSeconds": 60
}
```

**Rate Limiting Applied To:**
- Login endpoint: 5 attempts per 5 minutes (per identifier)
- WebSocket message sending: 60 messages per minute (per user)

**Features:**
- Returns HTTP 429 (Too Many Requests) when limit exceeded
- Includes retry-after time in error response
- Graceful degradation: Returns true on Redis errors to avoid blocking legitimate requests

### 3. Security Headers Middleware

**New Middleware:**
- `SecurityHeadersMiddleware` - Adds security headers to all responses

**Headers Added:**
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: geolocation=(), microphone=(), camera=()`
- `Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:;`

### 4. CORS Configuration

**Custom Method:**
- `AddCorsServices()` - Custom extension method to avoid conflicts with framework methods

**New Configuration Options:**
- `CorsOptions` - Custom CORS configuration class
- `CorsOptionsValidator` - Configuration validation

**Configuration:**
```json
"Cors": {
  "AllowedOrigins": ["http://localhost:5000", "https://localhost:5001"],
  "AllowCredentials": true,
  "PolicyName": "UchatCorsPolicy"
}
```

**Features:**
- Configurable allowed origins from appsettings.json
- Optional credentials support
- Named policy to avoid conflicts

### 5. Disposal Patterns

**Enhanced Services:**
- `RedisMultiplexer` - Now implements `IDisposable` with proper disposal pattern
  - Disposes `IConnectionMultiplexer` on cleanup
  - Prevents resource leaks

**DI Lifetimes:**
- `RedisMultiplexer`: Singleton (with disposal)
- `RateLimitService`: Singleton
- Other services maintain appropriate lifetimes

### 6. API Documentation

**XML Documentation:**
- Enabled XML documentation generation in `Uchat.Server.csproj`
- All public APIs documented with XML comments
- Suppressed CS1591 warnings for undocumented internal members

**Swagger Configuration:**
- Enhanced with comprehensive API information
- JWT Bearer authentication scheme added to Swagger UI
- Automatic inclusion of XML documentation comments
- ProducesResponseType attributes on all controller actions

**Swagger Features:**
- Title: "Uchat Server API"
- Version: "v1"
- Description included
- JWT Bearer token support in UI
- Response type documentation

### 7. Error Response Models

**New Models:**
- `ErrorResponse` - Generic error response with optional details
- `ValidationErrorResponse` - Validation error response with field-level errors

**Usage:**
- Consistent error responses across all endpoints
- Field-level validation errors
- Clear error messages with details

### 8. Testing

**New Test Files:**
- `RegisterRequestValidatorTests` - 6 tests for registration validation
- `LoginRequestValidatorTests` - 3 tests for login validation
- `RateLimitServiceTests` - 9 tests for rate limiting functionality

**Updated Tests:**
- `AuthControllerTests` - Updated to test rate limiting and validation
  - Added rate limit exceeded test case
  - Updated validation tests to use ModelState

**Test Coverage:**
- Validator tests cover valid and invalid inputs
- Rate limit tests cover normal operation and Redis failures
- Controller tests verify rate limiting integration

## Configuration Updates

### appsettings.json
Added new sections:
- `RateLimit` - Rate limiting configuration
- `Cors` - CORS policy configuration

## Breaking Changes

None. All changes are backward compatible with the existing API.

## Migration Guide

### For Developers

1. Ensure all new requests pass validation
2. Handle HTTP 429 responses with retry logic
3. Include JWT Bearer token in requests to authenticated endpoints
4. Update client applications to handle CORS properly

### For Deployment

1. Update `appsettings.json` with CORS allowed origins
2. Adjust rate limit thresholds as needed for your environment
3. Ensure Redis is available for rate limiting
4. Review security headers for compatibility with your infrastructure

## Security Benefits

1. **Prevents Brute Force Attacks**: Rate limiting on login prevents credential stuffing
2. **Prevents Spam**: Message rate limiting prevents message flooding
3. **Input Validation**: Prevents injection attacks and data corruption
4. **Security Headers**: Protects against XSS, clickjacking, and MIME-sniffing attacks
5. **CORS Protection**: Prevents unauthorized cross-origin requests
6. **API Documentation**: Facilitates security audits and testing

## Performance Considerations

- Rate limiting uses Redis counters (O(1) operations)
- Validation occurs before expensive database operations
- Security headers add negligible overhead
- CORS handled by ASP.NET Core middleware

## Future Enhancements

Potential additions:
- API key authentication for service-to-service calls
- Request signing for critical operations
- IP-based rate limiting in addition to user-based
- Configurable security header policies
- HSTS (HTTP Strict Transport Security) headers
- Advanced CSP policies with nonces
