namespace ShopAssistant.Contracts.Interfaces.User;

using ShopAssistant.Contracts.Models.User;

public interface ITokenService
{
    string GenerateToken(User user);
}
