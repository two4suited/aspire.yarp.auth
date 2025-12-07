# AspireYarpAuth

A .NET Aspire solution demonstrating YARP (Yet Another Reverse Proxy) integration with API authentication patterns.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) or [Podman](https://podman.io/) (for container runtime)
- An IDE like Visual Studio 2022, VS Code with C# Dev Kit, or JetBrains Rider

## Project Structure

```
AspireYarpAuth/
├── src/
│   ├── AspireYarpAuth.AppHost/        # Aspire orchestration host with YARP gateway
│   ├── AspireYarpAuth.Api/            # Backend API service
│   └── AspireYarpAuth.ServiceDefaults/ # Shared Aspire service defaults
├── Directory.Build.props               # Shared build configuration
├── Directory.Packages.props            # Central package management
└── global.json                         # SDK version configuration
```

## Technologies

- **.NET 10** - Latest .NET runtime
- **Aspire 13** - Cloud-native application orchestration
- **YARP** - Microsoft's high-performance reverse proxy

## Getting Started

### Run the application

```bash
cd src/AspireYarpAuth.AppHost
dotnet run
```

This will:
1. Launch the Aspire Dashboard (opens automatically in your browser)
2. Start the API service
3. Start the YARP gateway container on port 5000

### Access the services

- **Aspire Dashboard**: Automatically opens in browser (check console for URL)
- **YARP Gateway**: `http://localhost:5000`
- **API via Gateway**: `http://localhost:5000/api/weatherforecast`

## Architecture

```
                    ┌─────────────────┐
                    │   YARP Gateway  │
                    │   (Port 5000)   │
                    └────────┬────────┘
                             │
                    /api/*   │
                             ▼
                    ┌─────────────────┐
                    │   API Service   │
                    │  (weatherforecast)│
                    └─────────────────┘
```

## API Endpoints

### Direct API Endpoints
- `GET /` - Hello message
- `GET /weatherforecast` - Sample weather data
- `GET /health` - Health check endpoint
- `GET /alive` - Liveness check endpoint

### Via YARP Gateway (port 5000)
- `GET /api/` - Proxied hello message
- `GET /api/weatherforecast` - Proxied weather data

## Configuration

### YARP Routes

YARP is configured programmatically in the AppHost:

```csharp
var gateway = builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/api/{**catch-all}", apiService)
            .WithTransformPathRemovePrefix("/api");
    })
    .WithHostPort(5000);
```

### Service Defaults

The ServiceDefaults project provides:
- **OpenTelemetry** - Distributed tracing and metrics
- **Health Checks** - Liveness and readiness probes
- **Service Discovery** - Automatic service resolution
- **HTTP Resilience** - Retry policies and circuit breakers

## Package Versions

| Package | Version |
|---------|---------|
| Aspire.Hosting.AppHost | 13.0.2 |
| Aspire.Hosting.Yarp | 13.0.2 |
| Microsoft.Extensions.ServiceDiscovery | 10.0.0 |
| OpenTelemetry.* | 1.11.x |

## Learn More

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Aspire YARP Integration](https://aspire.dev/integrations/reverse-proxies/yarp/)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)