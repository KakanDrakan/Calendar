using Azure.Identity;
using CalendarApi.Contracts;
using CalendarApi.Data;
using CalendarApi.Services;
using CalendarApi.Stores;
using Microsoft.Graph;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(7248, listenOptions =>
    {
        listenOptions.UseHttps();
    });
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

// Other services
builder.Services.AddControllers();
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

// --- MongoDB Setup ---
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["MongoDb:ConnectionString"]
        ?? throw new InvalidOperationException("Missing MongoDb connection string in configuration.");
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

builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<CalendarUpdateService>();
builder.Services.AddScoped<GraphSubscriptionService>();
builder.Services.AddSingleton<IAuthService, MicrosoftAuthService>();
builder.Services.AddScoped<QrCodeService>();
builder.Services.AddSingleton<SignalRTokenService>();

builder.Services.AddSingleton<SubscriptionStore>();
builder.Services.AddSingleton<RecentlyUpdatedResourceStore>();
builder.Services.AddSingleton<CalendarStore>();
builder.Services.AddSingleton<SessionStore>();

builder.Services.AddHostedService<CleanupService>();

// Cors configuration
var allowedOrigins = "AllowedOrigins";
var config = builder.Configuration;
var apiTunnelUrl = config["Urls:Backend"];
var frontendTunnelUrl = config["Urls:Frontend"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: allowedOrigins,
        policy =>
        {
            policy.WithOrigins("https://localhost:7248", "http://localhost:8080", apiTunnelUrl, frontendTunnelUrl)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

// Middleware pipeline configuration
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(allowedOrigins);


app.MapHub<CalendarHub>("/hubs/calendar");
app.MapControllers();

app.Run();
