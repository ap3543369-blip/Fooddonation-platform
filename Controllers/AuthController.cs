using System.Security.Cryptography;
using System.Text;
using FoodDonationPlatform.Data;
using FoodDonationPlatform.Models;
using FoodDonationPlatform.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace FoodDonationPlatform.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens,
    Notifier notifier, IEmailSender emailSender, IConfiguration config) : ControllerBase
{
    public record RegisterDto(string Name, string Email, string Password, string Role,
        string? Phone, string? Address, string? OrgName, string? RegistrationNo);
    public record LoginDto(string Email, string Password);
    public record ForgotDto(string Email);
    public record ResetDto(string Email, string Token, string NewPassword);

    static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email) || (dto.Password?.Length ?? 0) < 6)
            return BadRequest(new { message = "Name, email aur kam se kam 6 character ka password chahiye." });

        if (!Enum.TryParse<Role>(dto.Role, true, out var role) || role == Role.Admin)
            return BadRequest(new { message = "Role Donor ya Ngo hona chahiye." });

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Ye email pehle se registered hai." });

        if (role == Role.Ngo && string.IsNullOrWhiteSpace(dto.OrgName))
            return BadRequest(new { message = "NGO ka naam zaruri hai." });

        var user = new User
        {
            Name = dto.Name.Trim(), Email = email, Role = role, Phone = dto.Phone, Address = dto.Address,
            OrgName = role == Role.Ngo ? dto.OrgName : null,
            RegistrationNo = role == Role.Ngo ? dto.RegistrationNo : null,
            IsApproved = role == Role.Donor
        };
        user.PasswordHash = hasher.HashPassword(user, dto.Password!);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var safeName = System.Net.WebUtility.HtmlEncode(user.Name);
        if (role == Role.Ngo)
        {
            _ = emailSender.SendAsync(user.Email, "Registration received",
                EmailTemplates.Wrap($"Namaste {safeName},<br>Aapka NGO registration mil gaya hai. Admin approval ke baad aap login kar sakte hain."));
            var admins = await db.Users.Where(u => u.Role == Role.Admin).Select(u => u.Id).ToListAsync();
            foreach (var id in admins)
                await notifier.SendAsync(id, $"Naya NGO approval ke liye: {user.OrgName}", "Naya NGO approval request");
            return Ok(new { message = "Registration ho gaya. Admin approval ke baad aap login kar sakte hain." });
        }

        _ = emailSender.SendAsync(user.Email, "Welcome to Food Donation Platform",
            EmailTemplates.Wrap($"Namaste {safeName},<br>Aapka account ban gaya hai. Ab aap khana donate kar sakte hain!"));
        return Ok(new { message = "Registration successful! Ab login karein." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password ?? "") == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Email ya password galat hai." });

        if (!user.IsApproved)
            return StatusCode(403, new { message = "Aapka NGO account abhi admin approval ke liye pending hai." });

        return Ok(new
        {
            token = tokens.Create(user),
            user = new { user.Id, user.Name, user.Email, Role = user.Role.ToString(), user.OrgName }
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> Forgot(ForgotDto dto)
    {
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        string? devLink = null;

        if (user != null)
        {
            var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            user.PasswordResetTokenHash = Hash(raw);
            user.PasswordResetExpiry = DateTime.UtcNow.AddMinutes(30);
            await db.SaveChangesAsync();

            var link = $"{Request.Scheme}://{Request.Host}/reset.html?email={Uri.EscapeDataString(email)}&token={raw}";
            _ = emailSender.SendAsync(user.Email, "Password reset link",
                EmailTemplates.Wrap("Aapne password reset ka request kiya hai. Ye link 30 minute tak valid hai.<br>Agar aapne request nahi kiya to is email ko ignore karein.",
                    "Password Reset Karein", link));

            // Sirf development ke liye: SMTP set na ho to link response me bhi aata hai
            if (config.GetValue<bool>("App:DevShowResetLink") && string.IsNullOrWhiteSpace(config["Smtp:Host"]))
                devLink = link;
        }
        return Ok(new { message = "Agar ye email registered hai to reset link bhej diya gaya hai.", devResetLink = devLink });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetDto dto)
    {
        if ((dto.NewPassword?.Length ?? 0) < 6)
            return BadRequest(new { message = "Naya password kam se kam 6 character ka hona chahiye." });

        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        var ok = user?.PasswordResetTokenHash != null && user.PasswordResetExpiry > DateTime.UtcNow &&
                 CryptographicOperations.FixedTimeEquals(
                     Encoding.UTF8.GetBytes(user.PasswordResetTokenHash), Encoding.UTF8.GetBytes(Hash(dto.Token ?? "")));
        if (!ok) return BadRequest(new { message = "Reset link invalid ya expire ho chuka hai." });

        user!.PasswordHash = hasher.HashPassword(user, dto.NewPassword!);
        user.PasswordResetTokenHash = null; user.PasswordResetExpiry = null;
        await db.SaveChangesAsync();
        return Ok(new { message = "Password badal diya gaya. Ab login karein." });
    }
}
