# Uchat MVP Setup Guide

## Initial Bootstrap

This solution has been bootstrapped with the following structure:

### Directory Layout

```
uchat-mvp/
├── src/                    # Source code
│   ├── Uchat.Shared/      # Shared library
│   ├── Uchat.Server/      # ASP.NET Core server
│   ├── Uchat.Client/      # WPF client
│   └── Uchat.Tests/       # Test project
├── docs/                   # Documentation
├── scripts/                # Build and deployment scripts
├── config/                 # Configuration files
├── Directory.Build.props   # Solution-wide build properties
├── StyleCop.ruleset       # StyleCop rules
├── stylecop.json          # StyleCop settings
└── uchat-mvp.sln          # Solution file
```

## Project Details

### Uchat.Shared (Class Library - .NET 8.0)
- Shared models, DTOs, and utilities
- StyleCop.Analyzers configured

### Uchat.Server (ASP.NET Core Web API - .NET 8.0)
- WebSocket support for real-time communication
- Packages:
  - Serilog + Serilog.Sinks.Console
  - Microsoft.AspNetCore.WebSockets
  - Oracle.ManagedDataAccess.Core
  - MongoDB.Driver
  - StackExchange.Redis
  - BCrypt.Net-Next
  - Microsoft.IdentityModel.Tokens
  - System.IdentityModel.Tokens.Jwt
  - StyleCop.Analyzers

### Uchat.Client (WPF Application - .NET 8.0-windows)
- WPF desktop client
- MVVM pattern using CommunityToolkit.Mvvm
- Packages:
  - CommunityToolkit.Mvvm
  - System.Net.WebSockets.Client
  - Serilog + Serilog.Sinks.File
  - Microsoft.Extensions.Hosting
  - StyleCop.Analyzers

### Uchat.Tests (xUnit Test Project - .NET 8.0-windows)
- Unit and integration tests
- Packages:
  - xUnit
  - FluentAssertions
  - NSubstitute
  - StyleCop.Analyzers

## Project References

- `Uchat.Server` → references `Uchat.Shared`
- `Uchat.Client` → references `Uchat.Shared`
- `Uchat.Tests` → references all three projects

## StyleCop Configuration

The solution enforces the following coding standards:

1. **SA1200**: Using directives must be placed inside namespaces
2. **SA1309**: Field names must not begin with underscore
3. **SA1101**: Prefix local calls with `this.`
4. **SA1649**: File name must match first type name (one public type per file)
5. **Implicit usings**: Disabled (all usings must be explicit)

Configuration files:
- `StyleCop.ruleset` - Rule set definition
- `stylecop.json` - Additional settings (using placement, etc.)
- `Directory.Build.props` - Applied to all projects

## Build Configuration

- **Nullable Reference Types**: Enabled on all projects
- **TreatWarningsAsErrors**: Enabled for code quality enforcement
- **ImplicitUsings**: Disabled to make dependencies explicit

## Getting Started

1. Restore packages:
   ```bash
   dotnet restore
   ```

2. Build the solution:
   ```bash
   dotnet build
   ```

3. Run tests:
   ```bash
   dotnet test
   ```

4. Run the server:
   ```bash
   cd src/Uchat.Server
   dotnet run
   ```

5. Run the client (Windows only):
   ```bash
   cd src/Uchat.Client
   dotnet run
   ```

## Development Notes

- The WPF client requires `EnableWindowsTargeting=true` to build on non-Windows platforms
- All projects target .NET 8.0 (client and tests target net8.0-windows)
- StyleCop analyzers will report warnings as errors, so ensure code follows the standards
- Use explicit usings in all files, placed inside namespace declarations
