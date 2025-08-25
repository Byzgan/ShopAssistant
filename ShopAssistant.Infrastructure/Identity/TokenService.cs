namespace ShopAssistant.Infrastructure.Identity;

using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShopAssistant.Contracts.Models.User;
using ShopAssistant.Contracts.Interfaces.User;

public class TokenService(IConfiguration configuration) : ITokenService
{
    public string GenerateToken(User user)
    {
        var key = Encoding.ASCII.GetBytes(configuration["JWT:Key"] ?? throw new InvalidOperationException("JWT key not configured."));
        var tokenHandler = new JwtSecurityTokenHandler();

        var claims = new List<Claim>
        {
            new Claim("userId", user.Id.ToString() ?? string.Empty),
            new Claim("userName",user.Name ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("language", user.Language ?? string.Empty),
            new Claim("systemName", user.ExternalSystem),
            new Claim("session_id", user.UniqueKey),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(Convert.ToInt32(configuration["JWT:ExpirationMinutes"])),
            Issuer = configuration["JWT:Issuer"],
            Audience = configuration["JWT:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}