# Uchat-Develop

A real-time chat application built with .NET 8, ASP.NET Core, and WPF.

## MVP Features

### Core Functionality
- ✅ **User Authentication**: Secure registration and login with JWT tokens
- ✅ **Real-Time Messaging**: WebSocket-based instant message delivery
- ✅ **Chat Types**: Support for direct messages, group chats, and channels
- ✅ **Message Management**: Send, edit, and delete messages with audit trails
- ✅ **User Presence**: Online/offline/away/DND status tracking
- ✅ **Message Status**: Sent, delivered, and read receipts
- ✅ **Message Threading**: Reply to specific messages
- ✅ **User Preferences**: Customizable themes, notifications, and privacy settings

### Technical Highlights
- **Multi-Database Architecture**: Oracle (primary data), MongoDB (preferences), Redis (cache/sessions)
- **Scalable Design**: Stateless API servers with horizontal scaling support
- **Security First**: BCrypt password hashing, JWT authentication, rate limiting
- **Audit Logging**: Complete message edit and delete history
- **Real-Time Updates**: WebSocket connections with automatic reconnection
- **Performance**: Indexed queries, connection pooling, intelligent caching

## Solution Structure

```
uchat-mvp/
├── src/                    # Source code
│   ├── Uchat.Shared/      # Shared library (DTOs, models, utilities)
│   ├── Uchat.Server/      # ASP.NET Core WebAPI server
│   ├── Uchat.Client/      # WPF desktop client
├── docs/                   # Documentation
├── scripts/                # Build and deployment scripts
├── config/                 # Configuration files
├── Directory.Build.props   # Common MSBuild properties
├── StyleCop.ruleset       # StyleCop analyzer configuration
└── stylecop.json          # StyleCop settings
```

## Projects

### Uchat.Shared
Class library containing shared code, models, and utilities used by both client and server.

**Target Framework:** .NET 8.0  
**Key Dependencies:**
- BCrypt.Net-Next (password hashing)
- Microsoft.Extensions.Options (configuration)
- System.Text.Json (serialization)

**Contents:**
- **Domain Models**: `User`, `Chat`, `Message` entities
- **DTOs**: Data transfer objects for API communication (`UserDto`, `ChatDto`, `MessageDto`, etc.)
- **Enumerations**: `MessageStatus`, `UserStatus`, `ChatType`
- **Contracts**: WebSocket message contracts for real-time communication (see [MESSAGE_CONTRACTS.md](uchat-mvp/docs/MESSAGE_CONTRACTS.md))
- **Abstractions**: Interfaces for DI (`IClock`, `IIdGenerator`, `IPasswordHasher`)
- **Helpers**: Password hashing, Redis key generation, pagination utilities
- **Configuration**: `JwtSettings`, `PasswordHashingOptions`
- **Serialization**: Source-generated JSON serializer context for high performance
- **Exceptions**: Domain and validation exceptions

See [MESSAGE_CONTRACTS.md](uchat-mvp/docs/MESSAGE_CONTRACTS.md) for detailed WebSocket message specifications.

### Uchat.Server
ASP.NET Core Web API server providing real-time chat functionality with WebSocket support.

**Target Framework:** .NET 8.0  
**Key Dependencies:**
- Serilog (logging)
- Microsoft.AspNetCore.WebSockets
- Oracle.ManagedDataAccess.Core
- MongoDB.Driver
- StackExchange.Redis
- BCrypt.Net-Next
- Microsoft.IdentityModel.Tokens
- System.IdentityModel.Tokens.Jwt

### Uchat.Client
WPF desktop application providing the user interface for the chat client.

**Target Framework:** .NET 8.0-windows  
**Key Dependencies:**
- CommunityToolkit.Mvvm (MVVM framework)
- System.Net.WebSockets.Client
- Serilog (logging)
- Microsoft.Extensions.Hosting

## Tooling and Standards

### Code Quality
- **StyleCop Analyzers**: Enforces consistent coding standards across all projects
- **Nullable Reference Types**: Enabled on all projects
- **TreatWarningsAsErrors**: Enabled to maintain code quality

### Coding Standards
The solution enforces the following StyleCop rules:
- **SA1200**: Using directives must be placed inside namespaces
- **SA1309**: Field names must not begin with underscore
- **SA1101**: Prefix local calls with `this.`
- **SA1649**: File name must match first type name (one public type per file)
- **Implicit usings**: Disabled to make dependencies explicit

## Prerequisites

### Development Requirements
- .NET 8.0 SDK or later
- Visual Studio 2022 / JetBrains Rider / VS Code
- Windows OS (for WPF client development)

### Database Requirements
- Oracle Database 12c+ (Express Edition supported)
- MongoDB 4.0+
- Redis 6.0+

See [DEPLOYMENT.md](uchat-mvp/docs/DEPLOYMENT.md) for detailed setup instructions.

## Quick Start

### 1. Database Setup

```bash
# Configure environment variables
cd uchat-mvp/scripts/powershell
cp .env.template .env
# Edit .env with your database credentials

# Provision databases (PowerShell)
pwsh ./Provision-Database.ps1

# Or use bash wrapper
chmod +x provision-database.sh
./provision-database.sh
```

### 2. Seed Demo Data (Optional)

```bash
# Seed with default data
pwsh ./Seed-DemoData.ps1

# Or customize counts
pwsh ./Seed-DemoData.ps1 -UserCount 20 -ChatCount 10 -MessageCount 100
```

Demo credentials: All users have password `Password123!`

### 3. Build and Run

```bash
# Navigate to solution directory
cd uchat-mvp

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Run server
cd src/Uchat.Server
dotnet run

# Run client (in separate terminal)
cd src/Uchat.Client
dotnet run
```

## Documentation

- **[ARCHITECTURE.md](uchat-mvp/docs/ARCHITECTURE.md)** - System architecture and design patterns
- **[DEPLOYMENT.md](uchat-mvp/docs/DEPLOYMENT.md)** - Deployment guide for various environments
- **[SECURITY.md](uchat-mvp/docs/SECURITY.md)** - Security considerations and best practices
- **[SETUP.md](uchat-mvp/docs/SETUP.md)** - Initial setup and configuration guide
- **[TESTING.md](uchat-mvp/docs/TESTING.md)** - Test execution and coverage reporting guide
- **[MESSAGE_CONTRACTS.md](uchat-mvp/docs/MESSAGE_CONTRACTS.md)** - WebSocket message specifications
- **[SHARED_MODELS.md](uchat-mvp/docs/SHARED_MODELS.md)** - Detailed model documentation

### Database Documentation
- **[Oracle Scripts](uchat-mvp/config/oracle/README.md)** - SQL scripts and schema documentation
- **[MongoDB Scripts](uchat-mvp/config/mongodb/README.md)** - Collection setup and indexes
- **[Redis Setup](uchat-mvp/config/redis/README.md)** - Key structures and TTL policies

### Scripts Documentation
- **[PowerShell Scripts](uchat-mvp/scripts/powershell/README.md)** - Provisioning and seeding guide

## Development Workflow

### Branch Strategy

- `main` - Production-ready code
- `develop` - Integration branch for features
- `feat/*` - Feature branches
- `fix/*` - Bug fix branches
- `docs/*` - Documentation updates

### Pull Request Workflow

1. **Create Feature Branch**
   ```bash
   git checkout -b feat/your-feature-name
   ```

2. **Make Changes**
   - Follow StyleCop coding standards
   - Write unit tests for new code
   - Update documentation as needed

3. **Commit Changes**
   ```bash
   git add .
   git commit -m "feat: add your feature description"
   ```
   
   Commit message format:
   - `feat:` - New feature
   - `fix:` - Bug fix
   - `docs:` - Documentation changes
   - `refactor:` - Code refactoring
   - `test:` - Test additions/changes
   - `chore:` - Maintenance tasks

4. **Push and Create PR**
   ```bash
   git push origin feat/your-feature-name
   ```
   
   Then create a pull request on GitHub with:
   - Clear description of changes
   - Link to related issues
   - Screenshots (if UI changes)
   - Test results

5. **Code Review**
   - Address reviewer feedback
   - Ensure all CI checks pass
   - Maintain clean commit history

6. **Merge**
   - Squash and merge to keep history clean
   - Delete feature branch after merge

### Code Quality Checks

Before submitting PR, ensure:

```bash
# Build without errors
dotnet build

# All tests pass
dotnet test

# No StyleCop warnings
dotnet build /p:TreatWarningsAsErrors=true
```

### Pre-Commit Checklist

- [ ] Code follows StyleCop standards (SA1200, SA1309, SA1101, SA1649)
- [ ] All new code has unit tests
- [ ] Tests pass locally
- [ ] No hardcoded secrets or credentials
- [ ] Documentation updated (if needed)
- [ ] Commit message follows convention
- [ ] No merge conflicts

## Project Structure

### Key Directories

```
uchat-mvp/
├── config/                 # Database scripts and configuration
│   ├── oracle/            # Oracle SQL scripts with execution order
│   ├── mongodb/           # MongoDB collection setup
│   └── redis/             # Redis documentation and patterns
├── scripts/               # Automation scripts
│   └── powershell/        # PowerShell provisioning and seeding
├── docs/                  # Comprehensive documentation
├── src/                   # Source code
│   ├── Uchat.Shared/     # Shared models, DTOs, utilities
│   ├── Uchat.Server/     # ASP.NET Core Web API
│   ├── Uchat.Client/     # WPF desktop application
│   └── Uchat.Tests/      # Unit and integration tests
└── publish/               # Build output (gitignored)
```

## Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET | 8.0 |
| Server | ASP.NET Core | 8.0 |
| Client | WPF | 8.0-windows |
| Primary DB | Oracle Database | 12c+ |
| Document DB | MongoDB | 4.0+ |
| Cache/Sessions | Redis | 6.0+ |
| Authentication | JWT | - |
| Password Hashing | BCrypt | - |
| Logging | Serilog | Latest |
| Testing | xUnit | Latest |
| Code Quality | StyleCop | Latest |

## Contributing

We welcome contributions! Please follow these guidelines:

1. Review existing issues or create a new one
2. Fork the repository
3. Create a feature branch
4. Make your changes following our coding standards
5. Add/update tests and documentation
6. Submit a pull request

## License

See LICENSE file for details.
