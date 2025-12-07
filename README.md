# AspireYarpAuth

A .NET Aspire solution demonstrating JWT bearer token authentication and scope-based authorization using a custom YARP (Yet Another Reverse Proxy) gateway.

## Features

- **JWT Authentication** - Bearer token generation and validation
- **Scope-Based Authorization** - YAML-configured endpoint permissions
- **YARP Reverse Proxy** - Custom gateway with authentication middleware
- **Service Orchestration** - .NET Aspire for multi-service management
- **Dynamic Configuration** - Scopes and endpoints defined in `scopes.yml`

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) or [Podman](https://podman.io/) (for container runtime)
- An IDE like Visual Studio 2022, VS Code with C# Dev Kit, or JetBrains Rider

## Project Structure

```
AspireYarpAuth/
├── src/
│   ├── AspireYarpAuth.AppHost/        # Aspire orchestration host
│   ├── AspireYarpAuth.Gateway/        # Custom YARP gateway with JWT auth
│   ├── AspireYarpAuth.Auth/           # JWT token generation service
│   ├── AspireYarpAuth.Api/            # Backend API service (no auth)
│   ├── AspireYarpAuth.ServiceDefaults/ # Shared Aspire service defaults
│   └── scopes.yml                     # Scope and endpoint definitions
├── api-tests.http                      # REST Client test scenarios
├── Directory.Build.props               # Shared build configuration
├── Directory.Packages.props            # Central package management
└── global.json                         # SDK version configuration
```

## Technologies

- **.NET 10** - Latest .NET runtime
- **Aspire 13.0.2** - Cloud-native application orchestration
- **YARP 2.3.0** - Microsoft's high-performance reverse proxy
- **JWT Bearer Authentication** - Token-based security
- **YamlDotNet 16.2.1** - YAML configuration parsing

## Getting Started

### Run the application

```bash
cd src/AspireYarpAuth.AppHost
dotnet run
```

This will:
1. Launch the Aspire Dashboard (opens automatically in your browser)
2. Start the Auth service for token generation
3. Start the API service (protected by gateway)
4. Start the YARP Auth Gateway with JWT validation

### Access the services

- **Aspire Dashboard**: Automatically opens in browser (check console for URL with token)
- **Auth Service**: Get JWT tokens at `/token` endpoint
- **Auth Gateway**: Access API through the gateway with valid JWT
- **API Service**: Direct access available but not recommended (use gateway)

## Authentication Flow

### 1. Get a JWT Token

Request a token from the Auth service with desired scopes:

```bash
POST http://localhost:{auth-port}/token
Content-Type: application/json

{
  "scopes": ["weatherforecast:read"]
}
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-12-06T03:00:00Z"
}
```

### 2. Call API via Gateway

Use the token in the Authorization header:

```bash
GET http://localhost:{gateway-port}/weatherforecast
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 3. Scope Validation

The gateway validates:
- Token signature and expiration
- Required scopes for the requested endpoint
- HTTP method permissions

## Architecture
## Architecture

```
┌──────────────────┐
│   User/Client    │
└────────┬─────────┘
         │
         │ 1. POST /token (request scopes)
         ▼
┌──────────────────┐
│   Auth Service   │ ← Validates scopes against scopes.yml
│  (Token Gen)     │
└────────┬─────────┘
         │ 2. Returns JWT token
         │
         │ 3. GET /weatherforecast + Bearer token
         ▼
┌──────────────────┐
│  Auth Gateway    │ ← YARP + JWT validation
│  (JWT + Scopes)  │ ← Scope-based authorization
└────────┬─────────┘
         │ 4. Proxies if authorized
         ▼
┌──────────────────┐
│   API Service    │ ← No authentication (trusts gateway)
│ (weatherforecast)│
└──────────────────┘
```

**Key Points:**
- Auth service validates requested scopes exist in `scopes.yml`
- Auth Gateway validates JWT and checks scopes for each request
- API service has no authentication (protected by gateway)
- Scopes are mapped to specific endpoints and HTTP methods

## Scope Configuration

Scopes are defined in `src/scopes.yml`:

```yaml
scopes:
  - name: "helloworld:read"
    description: "Read access to hello world endpoint"
    endpoints:
      - path: "/"
        methods: ["GET"]

  - name: "weatherforecast:read"
    description: "Read access to weather forecast"
    endpoints:
      - path: "/weatherforecast"
        methods: ["GET"]

  - name: "weatherforecast:write"
    description: "Write access to weather forecast"
    endpoints:
      - path: "/weatherforecast"
        methods: ["POST", "PUT", "DELETE"]

  - name: "api:full"
    description: "Full access to all API endpoints"
    endpoints:
      - path: "/*"
        methods: ["GET", "POST", "PUT", "DELETE", "PATCH"]
```

### Adding New Scopes

1. Edit `src/scopes.yml`
2. Add a new scope with name, description, and endpoints
3. Restart the Auth and Gateway services
4. Request tokens with the new scope

## API Endpoints

### Auth Service
- `POST /token` - Generate JWT token with requested scopes
  - Body: `{ "scopes": ["scope1", "scope2"] }`
  - Returns: `{ "token": "...", "expiresAt": "..." }`
- `GET /scopes` - List all available scopes from scopes.yml

### API Service (via Gateway)
- `GET /` - Hello message (requires `helloworld:read` scope)
- `GET /weatherforecast` - Weather data (requires `weatherforecast:read` scope)
- `POST /weatherforecast` - Create weather (requires `weatherforecast:write` scope)
- `GET /health` - Health check endpoint (no auth required)
- `GET /alive` - Liveness check endpoint (no auth required)

### Direct API Endpoints (not recommended)
The API service is accessible directly but should be accessed through the Auth Gateway for security.

## Testing

Use the `api-tests.http` file with REST Client extension in VS Code:

```http
### 1. Get token with weatherforecast:read scope
POST {{auth}}/token
Content-Type: application/json

{
  "scopes": ["weatherforecast:read"]
}

### 2. Get weather forecast (should succeed)
GET {{gateway}}/weatherforecast
Authorization: Bearer {{token}}

### 3. Get hello world (should fail - missing scope)
GET {{gateway}}/
Authorization: Bearer {{token}}

### 4. Get all available scopes
GET {{auth}}/scopes
```

## Configuration

### JWT Settings

JWT configuration is managed through Aspire parameters:
- `jwt-key` - Secret parameter shared between Auth and Gateway services
- Tokens expire after 1 hour
- Uses HS256 algorithm for signing

### YARP Configuration

The Gateway uses YARP with:
- **Route matching**: All requests proxied to API service
- **JWT Bearer authentication**: Validates token signature and expiration
- **Scope authorization**: Custom handler validates scopes against scopes.yml
- **Service discovery**: Automatic resolution of API service endpoint

### Service Defaults

The ServiceDefaults project provides:
- **OpenTelemetry** - Distributed tracing and metrics
- **Health Checks** - Liveness and readiness probes
- **Service Discovery** - Automatic service resolution
- **HTTP Resilience** - Retry policies and circuit breakers

### AppHost Configuration

```csharp
// JWT settings shared across services
var jwtKey = builder.AddParameter("jwt-key", secret: true);

// API service (no auth - trusts gateway)
var apiService = builder.AddProject<Projects.AspireYarpAuth_Api>("api");

// Auth Gateway with JWT validation and YARP
var authGateway = builder.AddProject<Projects.AspireYarpAuth_Gateway>("auth-gateway")
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithReference(apiService);

// Auth service for token generation (child of auth-gateway)
var authService = builder.AddProject<Projects.AspireYarpAuth_Auth>("auth")
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithParentRelationship(authGateway);
```

## Package Versions

| Package | Version |
|---------|---------|
| Aspire.Hosting.AppHost | 13.0.2 |
| Yarp.ReverseProxy | 2.3.0 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 |
| System.IdentityModel.Tokens.Jwt | 8.3.1 |
| YamlDotNet | 16.2.1 |
| Microsoft.Extensions.ServiceDiscovery | 10.0.0 |
| OpenTelemetry.* | 1.11.x |

## Security Considerations

- **JWT Secret**: The `jwt-key` parameter should be stored securely (use Azure Key Vault in production)
- **HTTPS**: Always use HTTPS in production for token transmission
- **Token Expiration**: Tokens expire after 1 hour; implement refresh token flow for production
- **Scope Validation**: Both Auth service and Gateway validate scopes independently
- **Gateway Trust**: API service trusts the gateway; ensure gateway is properly secured
- **Direct API Access**: In production, block direct API access (only allow gateway)

## Learn More

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [JWT Bearer Authentication](https://learn.microsoft.com/aspnet/core/security/authentication/)
- [Scope-Based Authorization](https://learn.microsoft.com/aspnet/core/security/authorization/)