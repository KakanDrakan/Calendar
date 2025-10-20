using Azure.Core;
using Azure.Identity;
using CalendarApi.Contracts;
using CalendarApi.Helpers;
using CalendarApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Graph;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Auth/Graph configuration

//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
//    .EnableTokenAcquisitionToCallDownstreamApi()
//    .AddInMemoryTokenCaches();

//var scopes = new[] { "https://graph.microsoft.com/.default" };

//builder.Services.AddScoped<GraphServiceClient>(provider =>
//{
//    var tokenAcquisition = provider.GetRequiredService<ITokenAcquisition>();

//    var credential = new DelegateCredential(async (context, cancel) =>
//    {
//        var token = await tokenAcquisition.GetAccessTokenForUserAsync(new[] { "https://graph.microsoft.com/.default" });
//        return new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1));
//    });

//    return new GraphServiceClient(credential);
//});

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

builder.Services.AddScoped<IEventService, EventService>();

// Cors configuration
var allowedOrigins = "AllowedOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: allowedOrigins,
        policy =>
        {
            policy.WithOrigins("https://localhost:7248", "http://localhost:8080")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
