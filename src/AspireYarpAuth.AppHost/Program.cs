var builder = DistributedApplication.CreateBuilder(args);

// JWT settings shared across services
var jwtKey = builder.AddParameter("jwt-key", secret: true);

// Add the API service (no auth required - YARP handles it)
var apiService = builder.AddProject<Projects.AspireYarpAuth_Api>("api");

// Add the custom YARP Gateway with JWT validation
var authGateway = builder.AddProject<Projects.AspireYarpAuth_Gateway>("auth-gateway")
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("ReverseProxy__Clusters__api-cluster__Destinations__api__Address", apiService.GetEndpoint("http"))
    .WithReference(apiService)
    .WaitFor(apiService);

// Add the Auth service for token generation (as child of auth-gateway)
var authService = builder.AddProject<Projects.AspireYarpAuth_Auth>("auth")
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithParentRelationship(authGateway);

builder.Build().Run();
