using Backend.Entities;

namespace Backend.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
}
