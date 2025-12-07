using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

// Load scopes from YAML file
var scopesFilePath = Path.Combine(AppContext.BaseDirectory, "scopes.yml");
var scopeConfig = LoadScopesConfig(scopesFilePath);
builder.Services.AddSingleton(scopeConfig);

var app = builder.Build();

app.UseExceptionHandler();

// Map service default endpoints (health checks)
app.MapDefaultEndpoints();

// Get JWT settings from configuration
var jwtKey = app.Configuration["Jwt__Key"] ?? app.Configuration["Jwt:Key"] ?? "ThisIsASecretKeyForDevelopmentOnly123!";
var jwtIssuer = app.Configuration["Jwt:Issuer"] ?? "AspireYarpAuth";
var jwtAudience = app.Configuration["Jwt:Audience"] ?? "AspireYarpAuth.Api";

// Token generation endpoint
app.MapPost("/token", (TokenRequest request, ScopesConfig config) =>
{
    // Validate that requested scopes exist in the configuration
    var requestedScopes = request.Scopes ?? [];
    var validScopes = config.Scopes.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var invalidScopes = requestedScopes.Where(s => !validScopes.Contains(s)).ToArray();
    
    if (invalidScopes.Length > 0)
    {
        return Results.BadRequest(new 
        { 
            Error = "invalid_scope",
            Message = $"Invalid scopes requested: {string.Join(", ", invalidScopes)}",
            ValidScopes = validScopes
        });
    }
    
    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, request.Username ?? "demo-user"),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
    };
    
    // Add scopes as a space-separated claim (standard OAuth2 format)
    if (requestedScopes.Length > 0)
    {
        claims.Add(new Claim("scope", string.Join(" ", requestedScopes)));
    }
    
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    
    var token = new JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: creds
    );
    
    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
    
    return Results.Ok(new TokenResponse(
        tokenString,
        "Bearer",
        3600,
        requestedScopes
    ));
});

// Endpoint to show available scopes from the YAML config
app.MapGet("/scopes", (ScopesConfig config) =>
{
    return Results.Ok(new
    {
        AvailableScopes = config.Scopes.Select(s => new
        {
            s.Name,
            s.Description,
            Endpoints = s.Endpoints.Select(e => new { e.Path, e.Methods })
        })
    });
});

app.MapGet("/", () => "Auth Service - POST /token to get a bearer token, GET /scopes to see available scopes");

app.Run();

// Helper to load YAML config
static ScopesConfig LoadScopesConfig(string filePath)
{
    if (!File.Exists(filePath))
    {
        Console.WriteLine($"Warning: Scopes file not found at {filePath}, using empty config");
        return new ScopesConfig { Scopes = [] };
    }
    
    var yaml = File.ReadAllText(filePath);
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();
    
    return deserializer.Deserialize<ScopesConfig>(yaml);
}

record TokenRequest(string? Username, string[]? Scopes);
record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string[] Scopes);

// YAML config models
public class ScopesConfig
{
    public List<ScopeDefinition> Scopes { get; set; } = [];
}

public class ScopeDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<EndpointDefinition> Endpoints { get; set; } = [];
}

public class EndpointDefinition
{
    public string Path { get; set; } = "";
    public List<string> Methods { get; set; } = [];
}
