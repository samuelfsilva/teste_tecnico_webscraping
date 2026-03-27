using LegalScraper.Application;
using LegalScraper.Infrastructure;
using LegalScraper.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Force Kestrel to always listen on port 5256 (HTTP)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5256);
});

// Add services to the container.
builder.Services.AddControllers();
// Add CORS policy to allow frontend during development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "LegalScraper API", Version = "v1" });
});

// Configure Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ensure database is created and migrations applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

// Enable CORS for the frontend
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
