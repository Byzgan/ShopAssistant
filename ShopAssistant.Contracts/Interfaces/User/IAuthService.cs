namespace ShopAssistant.Contracts.Interfaces.User;

using ShopAssistant.Contracts.Models.User;

public interface IAuthService
{
    User Login(LoginModel model);
}
