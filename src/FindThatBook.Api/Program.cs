using FindThatBook.Api.Extensions;
using FindThatBook.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenLibrary(builder.Configuration);
builder.Services.AddLanguageModels(builder.Configuration);
builder.Services.AddScoped<IBookSearchService, BookSearchService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("Frontend");
app.MapControllers();

app.Run();
