using Azure.Identity;
using CalendarApi.Contracts;
using CalendarApi.Data;
using CalendarApi.Services;
using CalendarApi.Stores;
using Microsoft.Graph;
using MongoDB.Driver;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

//// --- Kestrel setup ---
//builder.WebHost.ConfigureKestrel(serverOptions =>
//{
//    var env = builder.Environment;

//    // Only use HTTPS when not running in a container
//    var runningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

//    if (!runningInContainer && env.IsDevelopment())
//    {
//        serverOptions.ListenAnyIP(7248, listenOptions =>
//        {
//            listenOptions.UseHttps(); // Use HTTPS on host dev
//        });
//    }

//    // Always enable HTTP so Docker can access it
//    serverOptions.ListenAnyIP(7248);
//});

builder.Configuration.AddEnvironmentVariables();

// --- Redis Setup ---
var redisConnectionString = builder.Configuration["REDIS_CONNECTION"]
    ?? throw new InvalidOperationException("Missing REDIS_CONNECTION in environment variables.");

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString, true);
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddScoped<GraphServiceClient>(provider =>
{
    var config = builder.Configuration;

    var clientId = config["AzureAd:ClientId"];
    var clientSecret = config["AzureAd:ClientSecret"];
    var tenantId = config["AzureAd:TenantId"];

    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

    return new GraphServiceClient(credential);
});

builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration["REDIS_CONNECTION"]);

// --- MongoDB Setup ---
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    // Use service name "mongo" inside Docker
    var connectionString = configuration["MongoDb:ConnectionString"]
        ?? "mongodb://mongo:27017";
    return new MongoClient(connectionString);
});

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var databaseName = configuration["MongoDb:DatabaseName"] ?? "CalendarApiDb";
    return client.GetDatabase(databaseName);
});

builder.Services.AddSingleton<MongoDbContext>();

// --- Services ---
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<CalendarUpdateService>();
builder.Services.AddSingleton<GraphSubscriptionService>();
builder.Services.AddSingleton<IAuthService, MicrosoftAuthService>();
builder.Services.AddScoped<QrCodeService>();
builder.Services.AddSingleton<SignalRTokenService>();
builder.Services.AddSingleton<SubscriptionStore>();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddHostedService<CleanupService>();

// --- CORS ---
var allowedOrigins = "AllowedOrigins";
var config = builder.Configuration;
var apiTunnelUrl = config["Urls:Backend"];
var frontendTunnelUrl = config["Urls:Frontend"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: allowedOrigins, policy =>
    {
        policy.WithOrigins(
                "https://localhost:7248",
                "http://localhost:7248",
                "http://localhost:8080",
                "https://localhost:8080",
                "http://host.docker.internal:7248",
                "http://host.docker.internal:8080",
                apiTunnelUrl,
                frontendTunnelUrl
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// --- Middleware ---
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only redirect to HTTPS if not running in container
if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    app.UseHttpsRedirection();
}

app.UseCors(allowedOrigins);

app.MapHub<CalendarHub>("/hubs/calendar");
app.MapControllers();

app.Run();
