using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using MediaFlows.Data;
using MediaFlows.Shared.Configuration;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Web.Auth;
using MediaFlows.Web.Hubs;
using MediaFlows.Web.Middleware;
using MediaFlows.Web.BackgroundServices;
using MediaFlows.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Serilog;

AWSSDKHandler.RegisterXRayForAllServices(); // Instrument ALL AWS SDK calls before anything else

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSQL"),
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("MediaFlows.Data");
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        }));

var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = jwtSettings["Authority"];
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false, // Cognito access tokens use client_id, not aud
        ValidateLifetime = true,
        NameClaimType = "cognito:username"
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",
        p => p.RequireRole("SystemAdmin"));
    options.AddPolicy("CanCreateContent",
        p => p.RequireRole("SystemAdmin", "ContentCreator"));
    options.AddPolicy("CanReview",
        p => p.RequireRole("SystemAdmin", "Editor"));
    options.AddPolicy("CanViewContent",
        p => p.RequireRole("SystemAdmin", "ContentCreator", "Editor", "Viewer"));
});

builder.Services.AddTransient<IClaimsTransformation, CognitoGroupsClaimsTransformation>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("NextJsFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000" }
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
});

builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<Amazon.S3.IAmazonS3>();
builder.Services.AddAWSService<Amazon.DynamoDBv2.IAmazonDynamoDB>();
builder.Services.AddAWSService<Amazon.SQS.IAmazonSQS>();
builder.Services.AddAWSService<Amazon.CloudWatch.IAmazonCloudWatch>();
builder.Services.AddAWSService<Amazon.CloudWatchLogs.IAmazonCloudWatchLogs>();
// Cost Explorer is a global service exposed only via the us-east-1 endpoint.
// Inheriting the app's default region (ap-southeast-1) silently returns no data.
builder.Services.AddAWSService<Amazon.CostExplorer.IAmazonCostExplorer>(
    new Amazon.Extensions.NETCore.Setup.AWSOptions { Region = Amazon.RegionEndpoint.USEast1 });
builder.Services.AddAWSService<Amazon.CognitoIdentityProvider.IAmazonCognitoIdentityProvider>();
builder.Services.AddAWSService<Amazon.Rekognition.IAmazonRekognition>();
builder.Services.AddAWSService<Amazon.EventBridge.IAmazonEventBridge>();

// DynamoDB high-level context with table name prefix.
// DisableFetchingTableMetadata avoids the DescribeTable call the mapper
// otherwise makes on first query — the EB instance role doesn't grant
// dynamodb:DescribeTable, and all our models declare [DynamoDBHashKey]/
// [DynamoDBRangeKey] statically so runtime schema discovery is unneeded.
var dynamoDbTablePrefix = builder.Configuration["DynamoDB:TableNamePrefix"] ?? "";
builder.Services.AddSingleton<Amazon.DynamoDBv2.DataModel.IDynamoDBContext>(sp =>
    new Amazon.DynamoDBv2.DataModel.DynamoDBContext(
        sp.GetRequiredService<Amazon.DynamoDBv2.IAmazonDynamoDB>(),
        new Amazon.DynamoDBv2.DataModel.DynamoDBContextConfig
        {
            TableNamePrefix = dynamoDbTablePrefix,
            DisableFetchingTableMetadata = true
        }));

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<MediaFlows.Shared.Configuration.RekognitionSettings>(builder.Configuration.GetSection("Rekognition"));
builder.Services.AddScoped<IRekognitionService, RekognitionService>();
builder.Services.AddScoped<IMediaAssetService, MediaAssetService>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IReviewEventPublisher, ReviewEventPublisher>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBookmarkService, BookmarkService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IS3StorageService, S3StorageService>();
builder.Services.AddScoped<IDynamoDbService, DynamoDbService>();
builder.Services.AddScoped<ICognitoAdminService, CognitoAdminService>();
builder.Services.AddHttpClient();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MediaFlows API",
        Version = "v1",
        Description = "REST API for MediaFlows DAM"
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Bearer token. Enter: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});
builder.Services.AddSignalR();
builder.Services.AddHostedService<AnalyticsWorker>();
builder.Services.AddHostedService<ScheduledPublisherWorker>();

// Rate limiting for auth endpoints (5 attempts/min per IP)
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.ContentType = "text/plain";
        await ctx.HttpContext.Response.WriteAsync(
            "Too many attempts. Please wait a minute before trying again.", token);
    };
});

// Data Protection for multi-instance cookie sharing
builder.Services.AddDataProtection()
    .PersistKeysToAWSSystemsManager("/MediaFlows/DataProtection");

// Options pattern for configuration sections
builder.Services.Configure<S3Settings>(builder.Configuration.GetSection("S3"));
builder.Services.Configure<CognitoSettings>(builder.Configuration.GetSection("Cognito"));
builder.Services.Configure<DynamoDbSettings>(builder.Configuration.GetSection("DynamoDB"));
builder.Services.Configure<EventBridgeSettings>(builder.Configuration.GetSection("EventBridge"));

var app = builder.Build();

// Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// 9a. X-Ray — MUST be first to trace entire request
app.UseXRay("MediaFlows");

// 9b. Error handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediaFlows API v1"));
}
else
{
    app.UseHsts();
}

// 9c. Core pipeline — Forwarded headers (ALB terminates TLS, forwards X-Forwarded-Proto)
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// HTTPS handling: EB's nginx overrides X-Forwarded-Proto to $scheme (always "http"),
// so we use X-Forwarded-Port from the ALB (which nginx does NOT override) to detect
// the original protocol. Port 443 = HTTPS, Port 80 = HTTP.
if (!app.Environment.IsDevelopment())
{
    app.Use((context, next) =>
    {
        var forwardedPort = context.Request.Headers["X-Forwarded-Port"].FirstOrDefault();
        if (forwardedPort == "80")
        {
            // Original request was HTTP — redirect to HTTPS before setting any cookies
            var httpsUrl = $"https://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            context.Response.Redirect(httpsUrl, permanent: true);
            return Task.CompletedTask;
        }
        // For HTTPS requests (port 443): force scheme to https
        context.Request.Scheme = "https";
        return next(context);
    });
}
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseCors("NextJsFrontend");

// 9d. Rate limiting + Auth pipeline
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 9e. Custom middleware — AFTER auth, BEFORE endpoints
app.UseMiddleware<EnsureUserExistsMiddleware>();

app.MapHub<NotificationHub>("/hubs/notifications");               // Real-time notifications
app.MapHub<AnalyticsHub>("/hubs/analytics");                      // Real-time analytics
app.MapControllers();

// Health check — lightweight endpoint for ALB and monitoring
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"
}))
.AllowAnonymous()
.WithTags("Health");

app.Run();

// Make Program class accessible for WebApplicationFactory in integration tests
public partial class Program { }
