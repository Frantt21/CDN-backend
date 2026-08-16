using System.Text;
using CDNBackend.API.Data;
using CDNBackend.API.Hubs;
using CDNBackend.API.Middleware;
using CDNBackend.API.Services;
using CDNBackend.API.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Tiempo real (SignalR)
builder.Services.AddSignalR();

// Datos
builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<UsersRepository>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<ImagesRepository>();
builder.Services.AddScoped<SavedImagesRepository>();

// Servicios
builder.Services.AddScoped<PasswordHasher>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ImageService>();
builder.Services.AddScoped<SavedImageService>();
builder.Services.AddScoped<RealtimeService>();

// Almacenamiento de imágenes (Local en dev, Azure CDN en producción)
var storageProvider = builder.Configuration["Storage:Provider"];
if (string.Equals(storageProvider, "Azure", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IImageStorage, AzureBlobStorage>();
else
    builder.Services.AddSingleton<IImageStorage, LocalDiskStorage>();

// Autenticación JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// CORS para el frontend React
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<FeedHub>("/hubs/feed");

app.Run();
