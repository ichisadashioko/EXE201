using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Shioko;
using Shioko.Models;
using Shioko.Services;
using Serilog;
using Google.Cloud.Storage.V1;
using System.Net;
using Google.Apis.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Shioko.Tinder.Hubs;
using Microsoft.Extensions.FileProviders;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/tinder_logs.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("logger started!");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddSignalR();

bool isDevelopment = Utils.IsDevelopment();
Log.Information($"isDevelopment : {isDevelopment}");


// TODO test in production environment
var GOOGLE_CLOUD_STORAGE_BUCKET_NAME = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_STORAGE_BUCKET_NAME");
Log.Information($"GOOGLE_CLOUD_STORAGE_BUCKET_NAME: {GOOGLE_CLOUD_STORAGE_BUCKET_NAME}");
if (string.IsNullOrWhiteSpace(GOOGLE_CLOUD_STORAGE_BUCKET_NAME))
{
    string log_message = "GOOGLE_CLOUD_STORAGE_BUCKET_NAME is not configured!";
    Log.Information(log_message);
    if (!isDevelopment)
    {
        throw new Exception(log_message);
    }

    // config upload service
    builder.Services.AddSingleton<IImageUploadService>(sp =>
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        return new LocalImageUploadService(env.WebRootPath);
    });
}
else
{
    {
        GoogleCloudStorageConfig config = new GoogleCloudStorageConfig() { BUCKET_NAME = GOOGLE_CLOUD_STORAGE_BUCKET_NAME };
        //builder.Services.Configure<GoogleCloudStorageConfig>(config);
        builder.Services.AddSingleton(config);
    }

    // register Google Cloud Storage client
    var storage_client = StorageClient.Create();

    //Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    if (isDevelopment)
    {
        var GOOGLE_STORAGE_CLIENT_PROXY = Environment.GetEnvironmentVariable("GOOGLE_STORAGE_CLIENT_PROXY");
        Log.Information($"GOOGLE_STORAGE_CLIENT_PROXY: {GOOGLE_STORAGE_CLIENT_PROXY}");
        if (!string.IsNullOrWhiteSpace(GOOGLE_STORAGE_CLIENT_PROXY))
        {
            HttpMessageHandler handler = storage_client.Service.HttpClient.MessageHandler;
            //HttpClientHandler http_client_handler = null;
            while (true)
            {
                if (handler is DelegatingHandler)
                {
                    handler = ((DelegatingHandler)handler).InnerHandler;
                }
                else if (handler is HttpClientHandler)
                {
                    //http_client_handler = handler;
                    break;
                }
                else
                {
                    throw new Exception("unexpected handler type");
                }
            }

            var clientHandler = (HttpClientHandler)handler;
            clientHandler.Proxy = new WebProxy(new Uri(GOOGLE_STORAGE_CLIENT_PROXY), true);
            clientHandler.UseProxy = true;

            Log.Information("Environment.GetEnvironmentVariable");
            Log.Information($"{Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")}");

            //using FileStream fs = File.OpenRead("C:\\programs\\downloads\\ProjectilePea.png");
            //var retval = storage_client.UploadObject(GOOGLE_CLOUD_STORAGE_BUCKET_NAME, $"{DateTime.UtcNow.ToFileTimeUtc()}", null, fs);
            //Log.Information(retval);
            //Log.Information(retval.SelfLink);
            //Log.Information(retval.MediaLink);
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SKIP_GCS_TEST")))
        {
            Log.Information("test google cloud storage client config");
            // test for upload and other required permissions
            // TODO
            var retval = storage_client.ListObjects(GOOGLE_CLOUD_STORAGE_BUCKET_NAME);
            foreach (var item in retval)
            {
                Log.Information($"MediaLink: {item.MediaLink}");
                Log.Information($"SelfLink: {item.SelfLink}");
                break;
            }
        }
    }
    builder.Services.AddSingleton(storage_client);
    builder.Services.AddSingleton<IImageUploadService>(sp =>
    {
        var storageClient = sp.GetRequiredService<StorageClient>();
        var gcsConfig = sp.GetRequiredService<GoogleCloudStorageConfig>();
        return new GoogleCloudImageUploadService(storageClient, gcsConfig);
    });
}

// Add services to the container.
// builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// logging
builder.Services.AddHttpLogging(o => { });

// JWT AUTHENTICATION
var jwtSettings_1 = builder.Configuration.GetSection("Jwt");
var jwtSettings = jwtSettings_1.Get<JwtSettings>();

if (jwtSettings == null)
{
    // TODO get JWT secret from a more secure place
    throw new InvalidOperationException("JWT settings are not configured properly.");
}

builder.Services.Configure<JwtSettings>(jwtSettings_1);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
    };
});
builder.Services.AddSingleton<TokenService>();
//builder.Services.AddSingleton<RateLimitingService>();
builder.Services.AddScoped<RateLimitingService>();

// HttpClient for downloading remote images
//HttpClient? image_downloader_http_client = null;
//if (isDevelopment)
//{
//    string? proxy_url = Environment.GetEnvironmentVariable("HTTP_PROXY");
//    if (proxy_url != null)
//    {
//        var handler = new HttpClientHandler
//        {
//            Proxy = new WebProxy(proxy_url),
//            UseProxy = true,
//            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
//        };
//        image_downloader_http_client = new HttpClient(handler);
//    }
//}


//if (image_downloader_http_client == null)
//{
//    image_downloader_http_client = new HttpClient();
//}

builder.Services.AddHttpClient(Utils.DOWNLOAD_REMOTE_IMAGE_HTTP_CLIENT_NAME).ConfigurePrimaryHttpMessageHandler(() =>
{
    if (isDevelopment)
    {
        string? proxy_url = Environment.GetEnvironmentVariable("HTTP_PROXY");
        if (!string.IsNullOrWhiteSpace(proxy_url))
        {
            Log.Debug("HTTP_PROXY:");
            Log.Debug(proxy_url);

            return new HttpClientHandler
            {
                Proxy = new WebProxy(proxy_url),
                UseProxy = true,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
        }
    }

    return new HttpClientHandler();
});

builder.Services.AddHttpClient(Utils.GOOGLE_GEMINI_HTTP_CLIENT_NAME).ConfigurePrimaryHttpMessageHandler(() =>
{
    if (isDevelopment)
    {
        string? proxy_url = Environment.GetEnvironmentVariable("HTTP_PROXY");
        if (!string.IsNullOrWhiteSpace(proxy_url))
        {
            Log.Debug("HTTP_PROXY:");
            Log.Debug(proxy_url);

            return new HttpClientHandler
            {
                Proxy = new WebProxy(proxy_url),
                UseProxy = true,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
        }
    }

    return new HttpClientHandler();
});

var GOOGLE_GEMINI_API_KEY = Environment.GetEnvironmentVariable("GOOGLE_GEMINI_API_KEY");
if (string.IsNullOrWhiteSpace(GOOGLE_GEMINI_API_KEY))
{
    string log_message = "GOOGLE_GEMINI_API_KEY is not configured!";
    Log.Information(log_message);
    if (!isDevelopment)
    {
        throw new Exception(log_message);
    }

    builder.Services.AddSingleton<IImageGenService>(sp =>
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        return new DummyImageGenService(env.WebRootPath);
    });
}
else
{
    builder.Services.AddSingleton(new GoogleGeminiApiKeyConfig { API_KEY = GOOGLE_GEMINI_API_KEY });
    builder.Services.AddSingleton<IImageGenService, GoogleGeminiApiImageGenService>();
}

var app = builder.Build();

// setup database
// TODO backup SQLite database file before applying migration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        //var logger = services.GetRequiredService<ILogger<Program>>();
        //logger.LogError(ex, "An error occurred while migrating the database.");
        Log.Error("An error occurred while migrating the database.");
        Log.Error(ex.Message);
        Log.Error(ex.ToString());
        throw;
    }
}

app.UseHttpLogging();

// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    // app.UseExceptionHandler("/Error");
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    // app.UseHsts();
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}
// TODO disable swagger in production
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
if (isDevelopment)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(app.Environment.WebRootPath),
        ServeUnknownFileTypes = true, // <-- Enable serving files without extensions
        DefaultContentType = "application/octet-stream" // or "image/jpeg" if you expect images
    });
}
else
{
    app.UseStaticFiles();
}
app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.UseRouting();
//app.UseAntiforgery();

// TODO: Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// app.MapRazorPages();

// TODO this does not block index.html
// redirect root to index.html

string INDEX_HTML_PATH = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
if (!File.Exists(INDEX_HTML_PATH))
{
    throw new FileNotFoundException("index.html not found in wwwroot folder. Please make sure you have built the frontend project and copied the files to wwwroot folder.", INDEX_HTML_PATH);
}

app.Use(async (context, next) =>
{
    Log.Information($"Request Path: {context.Request.Path}");
    // if the request is for an API or a static file other than index.html, let it pass through
    if (context.Request.Path.StartsWithSegments("/index.html"))
    {
        Log.Information("Redirecting /index.html to /");
        context.Response.Redirect("/");
        return;
    }

    var request_path = context.Request.Path;
    var filter_retval = Utils.ShouldServeIndexHtmlContent(request_path);
    Log.Debug($"Utils.ShouldServeIndexHtmlContent returns {filter_retval}");
    Log.Debug($"{request_path}");

    if (filter_retval)
    {
        if (File.Exists(INDEX_HTML_PATH))
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(INDEX_HTML_PATH);
            return;
        }
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("index.html not found");
        return;
    }
    else
    {
        await next();
        return;
    }

    // if (context.Request.Path == "/")
    // {
    //     context.Response.Redirect("/index.html");
    //     return;
    // }
    // await next();
});
// app.MapGet("/", async context =>
// {
//     context.Response.Redirect("/index.html");
// });

app.Run();
