using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

// Add problem details for exception handling
builder.Services.AddProblemDetails();

// Load scopes from YAML file
var scopesFilePath = Path.Combine(AppContext.BaseDirectory, "scopes.yml");
var scopeConfig = LoadScopesConfig(scopesFilePath);
builder.Services.AddSingleton(scopeConfig);

// Get JWT settings from configuration
var jwtKey = builder.Configuration["Jwt__Key"] ?? builder.Configuration["Jwt:Key"] ?? "ThisIsASecretKeyForDevelopmentOnly123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AspireYarpAuth";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AspireYarpAuth.Api";

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Add Authorization with a dynamic scope-based handler
builder.Services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ScopePolicy", policy =>
        policy.Requirements.Add(new ScopeRequirement()));
});

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseExceptionHandler();

// Map service default endpoints (health checks)
app.MapDefaultEndpoints();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map reverse proxy
app.MapReverseProxy();

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

// Authorization requirement and handler for dynamic scope validation
public class ScopeRequirement : IAuthorizationRequirement { }

public class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    private readonly ScopesConfig _scopesConfig;
    private readonly ILogger<ScopeAuthorizationHandler> _logger;

    public ScopeAuthorizationHandler(ScopesConfig scopesConfig, ILogger<ScopeAuthorizationHandler> logger)
    {
        _scopesConfig = scopesConfig;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            _logger.LogWarning("Authorization context resource is not HttpContext");
            return Task.CompletedTask;
        }

        // Get the request path (remove /api prefix as that's the gateway prefix)
        var requestPath = httpContext.Request.Path.Value ?? "/";
        if (requestPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            requestPath = requestPath[4..]; // Remove "/api"
            if (string.IsNullOrEmpty(requestPath)) requestPath = "/";
        }
        
        var requestMethod = httpContext.Request.Method;

        _logger.LogInformation("Checking authorization for path: {Path}, method: {Method}", requestPath, requestMethod);

        // Get user's scopes from the token
        var scopeClaim = context.User.FindFirst("scope") ?? context.User.FindFirst("scp");
        if (scopeClaim == null)
        {
            _logger.LogWarning("No scope claim found in token");
            return Task.CompletedTask;
        }

        var userScopes = scopeClaim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        _logger.LogInformation("User scopes: {Scopes}", string.Join(", ", userScopes));

        // Check if any of the user's scopes grant access to this endpoint
        foreach (var userScope in userScopes)
        {
            var scopeDefinition = _scopesConfig.Scopes
                .FirstOrDefault(s => s.Name.Equals(userScope, StringComparison.OrdinalIgnoreCase));

            if (scopeDefinition == null) continue;

            foreach (var endpoint in scopeDefinition.Endpoints)
            {
                if (MatchesEndpoint(requestPath, requestMethod, endpoint))
                {
                    _logger.LogInformation("Access granted by scope: {Scope}", userScope);
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }
        }

        _logger.LogWarning("No scope grants access to {Path} {Method}", requestPath, requestMethod);
        return Task.CompletedTask;
    }

    private static bool MatchesEndpoint(string requestPath, string requestMethod, EndpointDefinition endpoint)
    {
        // Check if method matches
        if (!endpoint.Methods.Any(m => m.Equals(requestMethod, StringComparison.OrdinalIgnoreCase) || m == "*"))
        {
            return false;
        }

        // Check if path matches (support wildcards)
        var pattern = endpoint.Path
            .Replace("/*", "/.*")
            .Replace("/", "\\/");
        
        if (pattern == "\\/.*") pattern = ".*"; // Handle /* at root
        
        return Regex.IsMatch(requestPath, $"^{pattern}$", RegexOptions.IgnoreCase);
    }
}

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
