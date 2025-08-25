namespace ShopAssistant.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Contracts.Interfaces.User;
using Contracts.Models.User;

/// <summary>
/// Controller for issuing JWT tokens for development and testing purposes.
/// Not for production use without additional security measures.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, ITokenService tokenService) : ControllerBase
{
    /// <summary>
    /// Authenticates a user and returns a JWT token if credentials are valid.
    /// </summary>
    /// <param name="model">The login credentials (username and password hash).</param>
    /// <returns>JWT token in JSON format.</returns>
    [HttpPost("token")]
    public IActionResult GetToken(LoginModel model)
    {
        User user = authService.Login(model);

        string userToken = tokenService.GenerateToken(user);

        return Ok(new { token = userToken });
    }
}