using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FoodDonationPlatform.Models;
using Microsoft.IdentityModel.Tokens;

namespace FoodDonationPlatform.Services;

public class TokenService(IConfiguration config)
{
    public string Create(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
