using Microsoft.EntityFrameworkCore;
using Npgsql;
using UrlShortener.Data;
using UrlShortener.Infrastructure;
using UrlShortener.Service.Services;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    var password = builder.Configuration["POSTGRES_PASSWORD"];
    if (string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException("Set POSTGRES_PASSWORD in the local .env file or process environment.");
    }

    defaultConnection = new NpgsqlConnectionStringBuilder
    {
        Host = builder.Configuration["POSTGRES_HOST"] ?? "localhost",
        Port = int.TryParse(builder.Configuration["POSTGRES_PORT"], out var port) ? port : 5432,
        Database = builder.Configuration["POSTGRES_DB"] ?? "url_shortener",
        Username = builder.Configuration["POSTGRES_USER"] ?? "postgres",
        Password = password
    }.ConnectionString;
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler(_ => { });
builder.Services.AddDbContext<UrlShortenerDbContext>(options =>
    options.UseNpgsql(defaultConnection));
builder.Services.AddInfrastructure();
builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<UrlShortenerOptions>(builder.Configuration.GetSection("Shortener"));
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("FrontendOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("create-url", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
