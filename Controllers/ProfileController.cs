using FoodDonationPlatform.Data;
using FoodDonationPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodDonationPlatform.Controllers;

[ApiController, Route("api/profile"), Authorize]
public class ProfileController(AppDbContext db, IPasswordHasher<User> hasher) : BaseApi
{
    public record UpdateDto(string Name, string? Phone, string? Address, string? OrgName);
    public record PasswordDto(string CurrentPassword, string NewPassword);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var u = await db.Users.FindAsync(UserId);
        if (u == null) return NotFound();
        return Ok(new { u.Id, u.Name, u.Email, Role = u.Role.ToString(), u.Phone, u.Address, u.OrgName, u.RegistrationNo, u.CreatedAt });
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateDto dto)
    {
        var u = await db.Users.FindAsync(UserId);
        if (u == null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Naam khali nahi ho sakta." });
        u.Name = dto.Name.Trim(); u.Phone = dto.Phone?.Trim(); u.Address = dto.Address?.Trim();
        if (u.Role == Role.Ngo && !string.IsNullOrWhiteSpace(dto.OrgName)) u.OrgName = dto.OrgName.Trim();
        await db.SaveChangesAsync();
        return Ok(new { message = "Profile update ho gaya.", user = new { u.Id, u.Name, u.Email, Role = u.Role.ToString(), u.OrgName } });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(PasswordDto dto)
    {
        var u = await db.Users.FindAsync(UserId);
        if (u == null) return NotFound();
        if (hasher.VerifyHashedPassword(u, u.PasswordHash, dto.CurrentPassword ?? "") == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Current password galat hai." });
        if ((dto.NewPassword?.Length ?? 0) < 6)
            return BadRequest(new { message = "Naya password kam se kam 6 character ka hona chahiye." });
        u.PasswordHash = hasher.HashPassword(u, dto.NewPassword!);
        await db.SaveChangesAsync();
        return Ok(new { message = "Password badal diya gaya." });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var role = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Ngo") ? "Ngo" : "Donor";
        var cards = new List<object>();
        void Add(string icon, string label, int value) => cards.Add(new { icon, label, value });

        if (role == "Donor")
        {
            var mine = db.Donations.Where(d => d.DonorId == UserId);
            Add("📦", "Total Donations", await mine.CountAsync());
            Add("⏳", "Pending", await mine.CountAsync(d => d.Status == DonationStatus.Pending));
            Add("🚚", "In Progress", await mine.CountAsync(d => d.Status == DonationStatus.Accepted || d.Status == DonationStatus.PickedUp));
            Add("✅", "Delivered", await mine.CountAsync(d => d.Status == DonationStatus.Delivered));
        }
        else if (role == "Ngo")
        {
            var mine = db.Donations.Where(d => d.NgoId == UserId);
            Add("🆕", "Available Now", await db.Donations.CountAsync(d => d.Status == DonationStatus.Pending && d.ExpiryTime > DateTime.UtcNow));
            Add("🤝", "Total Accepted", await mine.CountAsync());
            Add("🚚", "In Progress", await mine.CountAsync(d => d.Status == DonationStatus.Accepted || d.Status == DonationStatus.PickedUp));
            Add("✅", "Delivered", await mine.CountAsync(d => d.Status == DonationStatus.Delivered));
        }
        else
        {
            Add("🙋", "Donors", await db.Users.CountAsync(u => u.Role == Role.Donor));
            Add("🏢", "Approved NGOs", await db.Users.CountAsync(u => u.Role == Role.Ngo && u.IsApproved));
            Add("🕒", "NGO Approvals Pending", await db.Users.CountAsync(u => u.Role == Role.Ngo && !u.IsApproved));
            Add("📦", "Total Donations", await db.Donations.CountAsync());
            Add("✅", "Delivered", await db.Donations.CountAsync(d => d.Status == DonationStatus.Delivered));
        }
        return Ok(new { cards });
    }
}
