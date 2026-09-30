using System.ComponentModel.DataAnnotations;

namespace Backend.Entities;

public class RefreshToken
{
    public int Id { get; set; }

    [Required]
    public string TokenHash { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Token kullanımdan kaldırılırsa (örneğin Rotation sırasında veya çıkış yapıldığında) bu alan dolacak
    public DateTime? RevokedAt { get; set; }

    // Yardımcı özellik: Token hala geçerli mi? (İptal edilmemiş ve süresi dolmamış)
    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
}
