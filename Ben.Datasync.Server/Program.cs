// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Datasync.Server;
using CommunityToolkit.Datasync.Server.NSwag;
using CommunityToolkit.Datasync.Server.OpenApi;
using CommunityToolkit.Datasync.Server.Swashbuckle;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sample.Datasync.Server;
using Sample.Datasync.Server.Db;
using System.IdentityModel.Tokens.Jwt;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new ApplicationException("DefaultConnection is not set");

string? swaggerDriver = builder.Configuration["Swagger:Driver"];

// Cache configuration managers for token validation to avoid deadlocks
var microsoftConfigManager = new Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(
    "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration",
    new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever(),
    new Microsoft.IdentityModel.Protocols.HttpDocumentRetriever());

var googleConfigManager = new Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(
    "https://accounts.google.com/.well-known/openid-configuration",
    new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever(),
    new Microsoft.IdentityModel.Protocols.HttpDocumentRetriever());

var appleConfigManager = new Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(
    "https://appleid.apple.com/.well-known/openid-configuration",
    new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever(),
    new Microsoft.IdentityModel.Protocols.HttpDocumentRetriever());
bool nswagEnabled = swaggerDriver?.Equals("NSwag", StringComparison.InvariantCultureIgnoreCase) == true;
bool swashbuckleEnabled = swaggerDriver?.Equals("Swashbuckle", StringComparison.InvariantCultureIgnoreCase) == true;
bool openApiEnabled = swaggerDriver?.Equals("NET9", StringComparison.InvariantCultureIgnoreCase) == true;

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddDatasyncServices();

// Add HTTP context accessor for access control providers
builder.Services.AddHttpContextAccessor();

// Configure authentication
// Support for multiple authentication providers: Microsoft (Personal & Work/School), Google, and Apple
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        // Configure token validation to support multiple issuers
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            
            // Support multiple issuers: Microsoft, Google, and Apple
            ValidIssuers = new[]
            {
                // Microsoft personal accounts (this is the Microsoft-assigned tenant ID for all personal accounts)
                "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0",
                // Microsoft work/school accounts (common tenant)
                $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/v2.0",
                // Microsoft alternative format
                $"{builder.Configuration["AzureAd:Instance"]}{builder.Configuration["AzureAd:TenantId"]}/v2.0",
                // Google accounts
                "https://accounts.google.com",
                "accounts.google.com",
                // Apple accounts
                "https://appleid.apple.com"
            },
            
            // Support multiple audiences (client IDs)
            ValidAudiences = new[]
            {
                builder.Configuration["AzureAd:ClientId"] ?? "",
                builder.Configuration["GoogleAuth:ClientId"] ?? "",
                builder.Configuration["AppleAuth:ClientId"] ?? ""
            },
            
            // Map name claim for consistent user identification
            NameClaimType = "name",
            RoleClaimType = "roles",
            
            // Custom issuer signing key resolver to fetch keys from multiple providers
            IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
            {
                // Determine the issuer from the token
                var jsonToken = securityToken as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;
                var issuer = jsonToken?.Issuer;
                
                if (string.IsNullOrEmpty(issuer))
                {
                    return Enumerable.Empty<SecurityKey>();
                }
                
                // Use cached configuration managers to avoid blocking async calls
                try
                {
                    Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration> configManager;
                    
                    if (issuer.Contains("login.microsoftonline.com"))
                    {
                        configManager = microsoftConfigManager;
                    }
                    else if (issuer.Contains("accounts.google.com") || issuer == "accounts.google.com")
                    {
                        configManager = googleConfigManager;
                    }
                    else if (issuer.Contains("appleid.apple.com") || issuer == "https://appleid.apple.com")
                    {
                        configManager = appleConfigManager;
                    }
                    else
                    {
                        return Enumerable.Empty<SecurityKey>();
                    }
                    
                    // Use Task.Run to avoid deadlocks from sync-over-async
                    var config = Task.Run(async () => await configManager.GetConfigurationAsync(CancellationToken.None)).GetAwaiter().GetResult();
                    return config.SigningKeys;
                }
                catch
                {
                    return Enumerable.Empty<SecurityKey>();
                }
            }
        };
        
        // Add event handlers for better debugging and multi-provider support
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception != null)
                {
                    context.Response.Headers.Append("Token-Error", context.Exception.Message);
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // Token successfully validated
                return Task.CompletedTask;
            }
        };
    });

// Add authorization
builder.Services.AddAuthorization();

// Register the PersonalAccessControlProvider for user-scoped data
builder.Services.AddScoped<IAccessControlProvider<NoteItem>, PersonalAccessControlProvider<NoteItem>>();
builder.Services.AddScoped<IAccessControlProvider<TaskItem>, PersonalAccessControlProvider<TaskItem>>();

builder.Services.AddControllers();

if (nswagEnabled)
{
    _ = builder.Services.AddOpenApiDocument(options => options.AddDatasyncProcessor());
}

if (swashbuckleEnabled)
{
    _ = builder.Services.AddEndpointsApiExplorer();
    _ = builder.Services.AddSwaggerGen(options => options.AddDatasyncControllers());
}

if (openApiEnabled)
{
    // Explicit API Explorer configuration
    _ = builder.Services.AddEndpointsApiExplorer();
    _ = builder.Services.AddOpenApi(options => options.AddDatasyncTransformers());
}

WebApplication app = builder.Build();

var startupLogger = app.Logger;
AppDomain.CurrentDomain.UnhandledException += (_, args) =>
    startupLogger.LogCritical(args.ExceptionObject as Exception, "Unhandled exception caused process termination.");
TaskScheduler.UnobservedTaskException += (_, args) =>
{
    startupLogger.LogError(args.Exception, "Unobserved task exception.");
    args.SetObserved();
};
app.Lifetime.ApplicationStarted.Register(() =>
    startupLogger.LogInformation("Application started. Environment: {EnvironmentName}", app.Environment.EnvironmentName));
app.Lifetime.ApplicationStopping.Register(() => startupLogger.LogInformation("Application stopping."));
app.Lifetime.ApplicationStopped.Register(() => startupLogger.LogInformation("Application stopped."));

// Initialize the database
try
{
    using (IServiceScope scope = app.Services.CreateScope())
    {
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.InitializeDatabaseAsync();
    }
}
catch (Exception ex)
{
    // Log the error but don't crash the application
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to initialize database. The application will start but database operations may fail.");
}

app.UseHttpsRedirection();

if (nswagEnabled)
{
    _ = app.UseOpenApi().UseSwaggerUI();
}

if (swashbuckleEnabled)
{
    _ = app.UseSwagger().UseSwaggerUI();
}

// Enable authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (openApiEnabled)
{
    _ = app.MapOpenApi(pattern: "swagger/{documentName}/swagger.json");
    _ = app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Sample.Datasync.Server v1"));
}

app.Run();
