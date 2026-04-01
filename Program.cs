using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using RekovaBE_CSharp.Data;
using RekovaBE_CSharp.Services;

var builder = WebApplication.CreateBuilder(args);

// ==================== ENVIRONMENT VARIABLE SETUP ====================
// Load connection string from environment variables
var dbServer = Environment.GetEnvironmentVariable("DB_SERVER") ?? "127.0.0.1";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "rekovadb";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "IbraKonate@5";

var connectionString = $"Server={dbServer};Port={dbPort};Database={dbName};User={dbUser};Password={dbPassword};SslMode=None;AllowPublicKeyRetrieval=True;";

// Override with explicit connection string if provided
var explicitConnStr = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
if (!string.IsNullOrEmpty(explicitConnStr))
{
    connectionString = explicitConnStr;
}

// ==================== LOGGING SETUP ====================
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/rekova_.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ==================== DATABASE SETUP ====================
Log.Information("Using MySQL connection to database: {Database}", dbName);

// Configure DbContext with SHA256 support
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // Use MySQL 8.0 instead of AutoDetect to avoid connection during startup
    options.UseMySql(connectionString, ServerVersion.Parse("8.0.0"),
        mysqlOptions =>
        {
            mysqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
});

// ==================== CORS SETUP ====================
var corsOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "http://localhost:5173,http://localhost:3000").Split(',');

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", corsPolicyBuilder =>
    {
        corsPolicyBuilder
            .WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ==================== AUTHENTICATION SETUP ====================
// Generate default JWT key if not provided
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
if (string.IsNullOrEmpty(jwtKey))
{
    // Use a default key for development (MUST be overridden in production)
    jwtKey = "RekovaBE-CSharp-Development-Key-Change-In-Production-12345678901234567890";
    Log.Warning("JWT_KEY environment variable not set. Using default development key. CHANGE THIS IN PRODUCTION!");
}

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT Key must be at least 32 characters long. Current length: " + jwtKey.Length);
}

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "RekovaAPI";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "RekovaClient";

// Add JWT configuration to services for AuthService to access
builder.Services.Configure<JwtConfigOptions>(options =>
{
    options.Key = jwtKey;
    options.Issuer = jwtIssuer;
    options.Audience = jwtAudience;
});

var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// ==================== AUTHORIZATION SETUP ====================
builder.Services.AddAuthorization();

// ==================== DEPENDENCY INJECTION ====================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IDbMigrationService, DbMigrationService>();
builder.Services.AddHttpClient<IMpesaService, MpesaService>();

// ==================== CONTROLLERS AND SWAGGER ====================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Rekova API",
        Version = "v1",
        Description = "Loan Collection Management System"
    });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// ==================== BUILD APP ====================
var app = builder.Build();

// ==================== DATABASE MIGRATION ====================
try
{
    using (var scope = app.Services.CreateScope())
    {
        var migrationService = scope.ServiceProvider.GetRequiredService<IDbMigrationService>();
        await migrationService.MigrateAsync();
        Log.Information("✓ Database migration completed successfully");
    }
}
catch (Exception ex)
{
    Log.Warning(ex, "Database migration warning (non-fatal): {Message}. The app will continue but some features may not work properly. Make sure DB_PASSWORD and other DB_* environment variables are set.", ex.Message);
}

// ==================== MIDDLEWARE SETUP ====================
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

// ==================== CUSTOM MIDDLEWARE ====================
// Global error handling middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unhandled exception");
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "An internal server error occurred"
        });
    }
});

// Health check endpoint
app.MapGet("/api/health", () =>
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var isHealthy = dbContext.Database.CanConnect();

    return Results.Ok(new
    {
        success = isHealthy,
        status = isHealthy ? "healthy" : "unhealthy",
        timestamp = DateTime.UtcNow,
        database = isHealthy ? "connected" : "disconnected"
    });
});

app.MapControllers();

// ==================== DATABASE MIGRATION ====================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        db.Database.Migrate();
        Log.Information("Database migration completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Database migration failed. Please ensure MySQL is running and connection string is correct.");
    }
}

// ==================== RUN APP ====================
var port = builder.Configuration.GetValue<int>("ApiSettings:Port");
Log.Information("Starting application on port {Port}", port);
app.Run($"http://0.0.0.0:{port}");