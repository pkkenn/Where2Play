using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.ResponseCompression;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Register clients and service
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Where2Play.Services.Clients.MusicBrainzClient>();
builder.Services.AddSingleton<Where2Play.Services.Clients.SetlistClient>();
builder.Services.AddScoped<Where2Play.Services.IMusicService, Where2Play.Services.MusicService>();

// Application query layer (shared between Razor Pages and API)
builder.Services.AddScoped<Where2Play.Application.Queries.RecommendationQueries>();

// Add memory cache for caching API responses
builder.Services.AddMemoryCache();

// Keep compression only; remove response caching to avoid serving stale/empty pages
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opt =>
{
    opt.Level = System.IO.Compression.CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(opt =>
{
    opt.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Configure named HttpClients for external APIs with a configurable User-Agent
var userAgent = builder.Configuration["ApiHeaders:UserAgent"] ?? "Where2Play/1.0 (dickendd@mail.uc.edu)";
var setlistApiKey = builder.Configuration["ApiKeys:SetlistFm"] ?? string.Empty;

builder.Services.AddHttpClient("setlist", client =>
{
    client.BaseAddress = new Uri("https://api.setlist.fm/rest/1.0/");
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
    if (!string.IsNullOrEmpty(setlistApiKey))
    {
        client.DefaultRequestHeaders.Add("x-api-key", setlistApiKey);
    }
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient("musicbrainz", client =>
{
    client.BaseAddress = new Uri("https://musicbrainz.org/ws/2/");
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

// Register background recommendation refresher
builder.Services.AddHostedService<Where2Play.Services.RecommendationBackgroundService>();

// Controllers + JSON options for API
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// API Versioning + Explorer
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV"; // v1, v1.0
    options.SubstituteApiVersionInUrl = true;
});

// SwaggerGen with XML comments
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (!string.IsNullOrWhiteSpace(xmlPath) && File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Enable Swagger publicly (Development and Production)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.DocumentTitle = "Where2Play API";

    var apiVersionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    foreach (var description in apiVersionProvider.ApiVersionDescriptions)
    {
        c.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", $"Where2Play {description.GroupName.ToUpperInvariant()}");
    }

    c.DefaultModelsExpandDepth(-1);
    c.DisplayRequestDuration();
    c.InjectStylesheet("/swagger-custom.css");
});

app.UseHttpsRedirection();

// Enable compression only
app.UseResponseCompression();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Map attribute-routed controllers (for API)
app.MapControllers();

app.MapAreaControllerRoute(
    name: "API",
    areaName: "API",
    pattern: "api/{controller=Values}/{action=Get}/{id?}");

app.Run();
