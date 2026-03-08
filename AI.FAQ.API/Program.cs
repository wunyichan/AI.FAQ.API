using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

if (builder.Environment.IsDevelopment())
{
    string fileName = $"logs/log-{DateTime.Now:yyyyMMdd-HHmmss}.txt";

    builder.Logging.AddFile(fileName);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Enable the modern Scalar UI at /scalar/v1
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("My .NET 10 API")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
