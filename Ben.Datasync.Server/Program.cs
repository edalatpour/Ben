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

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new ApplicationException("DefaultConnection is not set");

string? swaggerDriver = builder.Configuration["Swagger:Driver"];
bool nswagEnabled = swaggerDriver?.Equals("NSwag", StringComparison.InvariantCultureIgnoreCase) == true;
bool swashbuckleEnabled = swaggerDriver?.Equals("Swashbuckle", StringComparison.InvariantCultureIgnoreCase) == true;
bool openApiEnabled = swaggerDriver?.Equals("NET9", StringComparison.InvariantCultureIgnoreCase) == true;

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddDatasyncServices();

// Add HTTP context accessor for access control providers
builder.Services.AddHttpContextAccessor();

// Configure authentication
// Support for multiple authentication providers: Microsoft (Personal & Work/School) and Google
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        // Support Microsoft accounts (both personal and work/school)
        var microsoftAuthority = $"{builder.Configuration["AzureAd:Instance"]}{builder.Configuration["AzureAd:TenantId"]}/v2.0";
        options.Authority = microsoftAuthority;
        options.Audience = builder.Configuration["AzureAd:ClientId"];
        
        // Configure token validation to support multiple issuers
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            
            // Support multiple issuers: Microsoft and Google
            ValidIssuers = new[]
            {
                // Microsoft personal accounts
                "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0",
                // Microsoft work/school accounts (common tenant)
                $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/v2.0",
                // Microsoft alternative format
                $"{builder.Configuration["AzureAd:Instance"]}{builder.Configuration["AzureAd:TenantId"]}/v2.0",
                // Google accounts
                "https://accounts.google.com",
                "accounts.google.com"
            },
            
            // Support multiple audiences (client IDs)
            ValidAudiences = new[]
            {
                builder.Configuration["AzureAd:ClientId"] ?? "",
                builder.Configuration["GoogleAuth:ClientId"] ?? ""
            },
            
            // Map name claim for consistent user identification
            NameClaimType = "name",
            RoleClaimType = "roles"
        };
        
        // Configure metadata address to fetch signing keys from Microsoft
        options.MetadataAddress = $"{microsoftAuthority}/.well-known/openid-configuration";
        
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

// Initialize the database
using (IServiceScope scope = app.Services.CreateScope())
{
    AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.InitializeDatabaseAsync();
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
