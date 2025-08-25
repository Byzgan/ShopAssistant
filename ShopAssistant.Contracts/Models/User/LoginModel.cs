using System.ComponentModel.DataAnnotations;

namespace ShopAssistant.Contracts.Models.User;

public class LoginModel
{
    [Required(ErrorMessage = "User Name is required")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    public string PasswordHash { get; set; } = null!;
}
