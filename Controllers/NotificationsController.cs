using FoodDonationPlatform.Data;
using FoodDonationPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodDonationPlatform.Controllers;

[ApiController, Route("api")]
public class NotificationsController(AppDbContext db) : BaseApi
{
    [HttpGet("notifications"), Authorize]
    public async Task<IActionResult> Mine() =>
        Ok(await db.Notifications.Where(n => n.UserId == UserId).OrderByDescending(n => n.CreatedAt).Take(30).ToListAsync());

    [HttpPost("notifications/read-all"), Authorize]
    public async Task<IActionResult> ReadAll()
    {
        await db.Notifications.Where(n => n.UserId == UserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return Ok();
    }

    // Landing page ke liye public stats
    [HttpGet("stats")]
    public async Task<IActionResult> Stats() => Ok(new
    {
        totalDonations = await db.Donations.CountAsync(),
        delivered = await db.Donations.CountAsync(d => d.Status == DonationStatus.Delivered),
        ngos = await db.Users.CountAsync(u => u.Role == Role.Ngo && u.IsApproved),
        donors = await db.Users.CountAsync(u => u.Role == Role.Donor)
    });
}
