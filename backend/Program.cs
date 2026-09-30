using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Backend.Data;
using Backend.Settings;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// ==========================================
// ADIM 10: GÜVENLİK (CORS & RATE LIMITING)
// ==========================================

// 1. CORS Ayarı: Sadece kendi frontend'imize izin veriyoruz (AllowAnyOrigin YASAK!)
builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Vite varsayılan portu
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // HttpOnly Cookie'lerin gidip gelmesi için ŞART
    });
});

// 2. Rate Limiting Ayarı: Brute Force saldırılarına karşı IP başına 1 dakikada 10 istek sınırı
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.RejectionStatusCode = 429; // 429 Too Many Requests
});

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Strongly Typed Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// Register Services
builder.Services.AddScoped<ITokenService, TokenService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Production'da HTTPS zorunlu olmalı (şu an geliştirme ortamı için esnek bırakabiliriz ama kural gereği sıkı tutuyoruz)
    options.RequireHttpsMetadata = false; 
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings!.Secret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        // Saat farkı (ClockSkew) toleransını sıfıra indiriyoruz. Token süresi bittiği saniye geçersiz olur.
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero 
    };
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Güvenlik middleware'lerini devreye al
app.UseCors("StrictCorsPolicy");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
