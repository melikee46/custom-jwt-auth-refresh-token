using System.ComponentModel.DataAnnotations;

namespace Backend.Entities;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
    
    [Required]
    public string PasswordHash { get; set; } = null!;

    // Bire-Çok (1-N) ilişki: Bir kullanıcının birden fazla Refresh Token'ı (farklı cihazlardan oturumu) olabilir
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
