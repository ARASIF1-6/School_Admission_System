using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Services;

// 1. Automatically load .env file if present in current directory, parent directory, or app directory
LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// 2. Configure Kestrel to listen on port 8080 (from .env or PORT env var)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

// 3. Resolve Database connection info directly from .env file:
// Priority:
// a) Explicit DATABASE_CONNECTION_STRING in .env
// b) Individual DB variables in .env (DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD)
// c) ConnectionStrings:DefaultConnection in appsettings / environment fallback
var connectionString = GetDatabaseConnectionStringFromEnv(builder.Configuration);

// Populate into builder configuration so EF Core and any consumers see the .env connection string
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // If testing or specifically requested InMemory
    if (connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
    {
        options.UseInMemoryDatabase(connectionString[9..]);
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

// Register services
builder.Services.AddScoped<IAdmissionService, AdmissionService>();
builder.Services.AddHostedService<HoldExpirationService>();

// CORS configuration - Allows X-Student-Id header
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Student-Id");
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "School Admission System API",
        Version = "v1",
        Description = "API for the online school admission system with seat hold & first-come first-served mechanism"
    });
    // Add custom header definition for Swagger UI
    c.AddSecurityDefinition("X-Student-Id", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "X-Student-Id",
        Type = SecuritySchemeType.ApiKey,
        Description = "17-digit Birth Registration Number of the student"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "X-Student-Id"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Ensure database is created and seeded automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        if (db.Database.IsRelational())
        {
            db.Database.EnsureCreated();
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not automatically create database at startup. It will retry when database is reachable.");
    }
}

// Enable Swagger in all environments for testing convenience
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "School Admission API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");

// Custom error handling middleware to ensure 4xx/5xx return JSON matching spec
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (BadHttpRequestException ex)
    {
        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled server error.");
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = "An internal server error occurred." });
    }
});

app.MapControllers();

app.Run();

// Method to get or construct Database Connection String from .env variables
static string GetDatabaseConnectionStringFromEnv(IConfiguration configuration)
{
    // 1. Check full connection string from .env
    var connStr = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
    if (!string.IsNullOrWhiteSpace(connStr))
    {
        return connStr;
    }

    // 2. Check individual components from .env
    var host = Environment.GetEnvironmentVariable("DB_HOST");
    var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
    var database = Environment.GetEnvironmentVariable("DB_NAME");
    var user = Environment.GetEnvironmentVariable("DB_USER");
    var pass = Environment.GetEnvironmentVariable("DB_PASSWORD");

    if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(database) && !string.IsNullOrWhiteSpace(user))
    {
        return $"Host={host};Port={port};Database={database};Username={user};Password={pass};Include Error Detail=true";
    }

    // 3. Fallback to ConnectionStrings__DefaultConnection or configuration
    var fromConfig = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? configuration.GetConnectionString("DefaultConnection");

    if (!string.IsNullOrWhiteSpace(fromConfig))
    {
        return fromConfig;
    }

    // 4. Default fallback
    return "Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=postgres";
}

// Helper method to load .env key-value pairs into environment
static void LoadDotEnv()
{
    var searchPaths = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
        Path.Combine(AppContext.BaseDirectory, ".env"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env")
    };

    foreach (var path in searchPaths)
    {
        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex > 0)
                {
                    var key = trimmed[..separatorIndex].Trim();
                    var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"').Trim('\'');
                    // Always load or override from .env
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
            break;
        }
    }
}

// Needed for integration tests
public partial class Program { }
