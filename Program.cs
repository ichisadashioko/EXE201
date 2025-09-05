using Shioko;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// logging
builder.Services.AddHttpLogging(o => { });

var app = builder.Build();

app.UseHttpLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    // app.UseHsts();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.UseStaticFiles();

// app.UseRouting();

// TODO: Enable authentication and authorization
// app.UseAuthorization();

// app.MapRazorPages();

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
