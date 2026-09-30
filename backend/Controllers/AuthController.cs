using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Backend.Data;
using Backend.DTOs;
using Backend.Entities;
using Backend.Services;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;

    public AuthController(AppDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        // 1. Veritabanından e-posta ile kullanıcıyı bul
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            // Güvenlik kuralı: "Kullanıcı bulunamadı" demek yerine genel bir hata dönüyoruz (User Enumeration engellemek için)
            return Unauthorized("E-posta veya şifre hatalı.");
        }

        // 2. Şifreyi BCrypt ile doğrula (Kasıtlı yavaş işlem)
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Unauthorized("E-posta veya şifre hatalı.");
        }

        // 3. Kullanıcı doğrulandı, 15 dakikalık Access Token üret
        var accessToken = _tokenService.GenerateAccessToken(user);

        // 4. Uzun ömürlü (7 günlük) güvenli rastgele Refresh Token üret
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        var refreshToken = Convert.ToBase64String(randomNumber);

        // 5. Refresh Token'ı veritabanına koymadan önce SHA256 ile Hashle
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(refreshToken));
        var hashedToken = Convert.ToBase64String(hashedBytes);

        // 6. Hash'lenmiş token'ı veritabanına kaydet
        var refreshTokenEntity = new RefreshToken
        {
            TokenHash = hashedToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7) // 7 gün geçerli
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        // 7. Plaintext Refresh Token'ı tarayıcıya "HttpOnly" Cookie olarak set et
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true, // JavaScript (Frontend) tarafından okunamamasını garantiler (XSS Koruması)
            Secure = true,   // Sadece HTTPS üzerinden gönderilir
            SameSite = SameSiteMode.Strict, // CSRF (Cross-Site Request Forgery) koruması sağlar
            Expires = refreshTokenEntity.ExpiresAt
        };
        
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

        // 8. Access Token'ı Frontend'in memory'sine alabilmesi için JSON response olarak dön
        return Ok(new { accessToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        // 1. Tarayıcının gönderdiği HttpOnly Cookie'den refresh token'ı oku
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized("Refresh token bulunamadı.");
        }

        // 2. Gelen plaintext token'ı SHA256 ile Hash'le (DB'de sadece hash var)
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(refreshToken));
        var hashedToken = Convert.ToBase64String(hashedBytes);

        // 3. Veritabanından token'ı ve o token'ın sahibini (User) çek
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hashedToken);

        if (storedToken == null)
        {
            return Unauthorized("Geçersiz refresh token.");
        }

        // ==========================================
        // 4. REUSE DETECTION (YENİDEN KULLANIM TESPİTİ)
        // ==========================================
        
        // Eğer token önceden kullanılmış ve iptal edilmişse (RevokedAt doluysa), bir saldırı altındayız!
        if (storedToken.RevokedAt != null)
        {
            // NÜKLEER SEÇENEK: Kullanıcının veritabanındaki TÜM aktif Refresh Token'larını bul ve iptal et
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == storedToken.UserId && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow; // Hepsini düşür
            }

            await _context.SaveChangesAsync(); // Veritabanına kaydet
            
            // Gerçek kullanıcıya veya saldırgana hesabın kilitlendiğini bildiriyoruz
            return Unauthorized("Güvenlik ihlali tespit edildi! Kullanılmış bir token tespit edildiği için tüm oturumlarınız güvenlik amacıyla kapatıldı. Lütfen tekrar giriş yapın.");
        }

        // 5. Token'ın sadece normal süresi dolmuş mu?
        if (DateTime.UtcNow >= storedToken.ExpiresAt)
        {
            return Unauthorized("Refresh token süresi dolmuş. Lütfen tekrar giriş yapın.");
        }

        // ==========================================
        // 5. ROTATION (Döndürme) MANTIĞI BAŞLIYOR
        // ==========================================
        
        // Kullanılmış olan mevcut token'ı geçersiz kılıyoruz (RevokedAt)
        storedToken.RevokedAt = DateTime.UtcNow;

        // Yepyeni, taze bir Refresh Token üretiyoruz
        var newRandomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(newRandomNumber);
        var newRefreshTokenPlain = Convert.ToBase64String(newRandomNumber);

        // Yeni token'ı da DB'ye yazmak için Hash'liyoruz
        var newHashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(newRefreshTokenPlain));
        var newHashedToken = Convert.ToBase64String(newHashedBytes);

        var newRefreshTokenEntity = new RefreshToken
        {
            TokenHash = newHashedToken,
            UserId = storedToken.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        
        _context.RefreshTokens.Add(newRefreshTokenEntity);
        
        // Hem eskisinin güncellenmesini (Revoked) hem yenisinin eklenmesini aynı anda DB'ye yaz
        await _context.SaveChangesAsync();

        // ==========================================
        // 6. YENİ BİLGİLERİ MÜŞTERİYE GÖNDERME
        // ==========================================

        // Yeni Refresh Token'ı tarayıcıya yepyeni bir Cookie olarak set et (eski cookie'nin üzerine yazar)
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = newRefreshTokenEntity.ExpiresAt
        };
        Response.Cookies.Append("refreshToken", newRefreshTokenPlain, cookieOptions);

        // Yeni Access Token üret
        var newAccessToken = _tokenService.GenerateAccessToken(storedToken.User);

        // Sadece Access Token'ı JSON dön
        return Ok(new { accessToken = newAccessToken });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            // Veritabanından bul ve iptal et
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(refreshToken));
            var hashedToken = Convert.ToBase64String(hashedBytes);

            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == hashedToken);

            if (storedToken != null)
            {
                storedToken.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        // Tarayıcıdaki Cookie'yi temizle (Tarihini geçmiş zamana ayarlayarak)
        Response.Cookies.Append("refreshToken", "", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(-1)
        });

        return Ok(new { message = "Başarıyla çıkış yapıldı." });
    }
}
