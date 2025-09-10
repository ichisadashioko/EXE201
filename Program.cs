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

Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console()
    .WriteTo.File("logs/tinder_logs.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("logger started!");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>();

// TODO test in production environment
var GOOGLE_CLOUD_STORAGE_BUCKET_NAME = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_STORAGE_BUCKET_NAME");
Console.WriteLine($"GOOGLE_CLOUD_STORAGE_BUCKET_NAME: {GOOGLE_CLOUD_STORAGE_BUCKET_NAME}");
if (string.IsNullOrWhiteSpace(GOOGLE_CLOUD_STORAGE_BUCKET_NAME))
{
    string log_message = "GOOGLE_CLOUD_STORAGE_BUCKET_NAME is not configured!";
    Console.WriteLine(log_message);
    throw new Exception(log_message);
}
{
    GoogleCloudStorageConfig config = new GoogleCloudStorageConfig() { BUCKET_NAME = GOOGLE_CLOUD_STORAGE_BUCKET_NAME };
    //builder.Services.Configure<GoogleCloudStorageConfig>(config);
    builder.Services.AddSingleton(config);
}

// register Google Cloud Storage client
var storage_client = StorageClient.Create();

var GOOGLE_STORAGE_CLIENT_PROXY = Environment.GetEnvironmentVariable("GOOGLE_STORAGE_CLIENT_PROXY");
Console.WriteLine($"GOOGLE_STORAGE_CLIENT_PROXY: {GOOGLE_STORAGE_CLIENT_PROXY}");
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

    Console.WriteLine("Environment.GetEnvironmentVariable");
    Console.WriteLine(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));

    //using FileStream fs = File.OpenRead("C:\\programs\\downloads\\ProjectilePea.png");
    //var retval = storage_client.UploadObject(GOOGLE_CLOUD_STORAGE_BUCKET_NAME, $"{DateTime.UtcNow.ToFileTimeUtc()}", null, fs);
    //Console.WriteLine(retval);
    //Console.WriteLine(retval.SelfLink);
    //Console.WriteLine(retval.MediaLink);
}

{
    Console.WriteLine("test google cloud storage client config");
    // test for upload and other required permissions
    var retval = storage_client.ListObjects(GOOGLE_CLOUD_STORAGE_BUCKET_NAME);
    foreach (var item in retval)
    {
        Console.WriteLine($"MediaLink: {item.MediaLink}");
        Console.WriteLine($"SelfLink: {item.SelfLink}");
    }
}

builder.Services.AddSingleton(storage_client);

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

var app = builder.Build();

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
app.UseStaticFiles();
app.MapControllers();
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
    Console.WriteLine($"Request Path: {context.Request.Path}");
    // if the request is for an API or a static file other than index.html, let it pass through
    if (context.Request.Path.StartsWithSegments("/index.html"))
    {
        Console.WriteLine("Redirecting /index.html to /");
        context.Response.Redirect("/");
        return;
    }

    if (Utils.ShouldServeIndexHtmlContent(context.Request.Path))
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
