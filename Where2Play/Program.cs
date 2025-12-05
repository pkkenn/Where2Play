var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Where2Play API",
        Version = "v1",
        Description = "API for searching and discovering concert events across multiple cities. Combines pre-seeded demo data with live concert information from Setlist.fm and artist details from MusicBrainz.",
        Contact = new()
        {
            Name = "Where2Play",
            Email = "dickendd@mail.uc.edu"
        }
    });

    // Enable XML comments for Swagger documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});
builder.Services.AddScoped<Where2Play.Services.CitySearchService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Enable Swagger UI in all environments (Dev and Prod)
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Map attribute-routed controllers so Swagger can discover endpoints
app.MapControllers();

app.MapAreaControllerRoute(
    name: "API",
    areaName: "API",
    pattern: "api/{controller=Values}/{action=Get}/{id?}");

app.Run();
