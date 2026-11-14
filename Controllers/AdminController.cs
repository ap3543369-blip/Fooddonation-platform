using FoodDonationPlatform.Data;
using FoodDonationPlatform.Models;
using FoodDonationPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodDonationPlatform.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles = "Admin")]
public class AdminController(AppDbContext db, Notifier notifier) : BaseApi
{
    [HttpGet("ngos")]
    public async Task<IActionResult> Ngos([FromQuery] ListQuery f)
    {
        var q = db.Users.Where(u => u.Role == Role.Ngo);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var p = $"%{f.Q.Trim()}%";
            q = q.Where(u => EF.Functions.Like(u.Name, p) || EF.Functions.Like(u.Email, p) || (u.OrgName != null && EF.Functions.Like(u.OrgName, p)));
        }
        if (f.Status == "pending") q = q.Where(u => !u.IsApproved);
        else if (f.Status == "approved") q = q.Where(u => u.IsApproved);

        var proj = q.OrderBy(u => u.IsApproved).ThenByDescending(u => u.CreatedAt)
            .Select(u => new { u.Id, u.Name, u.Email, u.Phone, u.OrgName, u.RegistrationNo, u.Address, u.IsApproved, u.CreatedAt });
        return Ok(await PageAsync(proj, f, x => x));
    }

    [HttpPost("ngos/{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == Role.Ngo);
        if (u == null) return NotFound();
        u.IsApproved = true;
        await db.SaveChangesAsync();
        await notifier.SendAsync(u.Id, "Badhai ho! Aapka NGO account approve ho gaya hai. Ab aap donations accept kar sakte hain.", "NGO account approved");
        return Ok(new { message = "NGO approved." });
    }

    [HttpPost("ngos/{id}/reject")]
    public async Task<IActionResult> Reject(int id)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == Role.Ngo);
        if (u == null) return NotFound();
        u.IsApproved = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "NGO approval hata di gayi." });
    }

    [HttpGet("donations")]
    public async Task<IActionResult> Donations([FromQuery] ListQuery f)
    {
        var q = DonationFilters.Apply(db.Donations.Include(d => d.Donor).Include(d => d.Ngo), f, searchPeople: true);
        return Ok(await PageAsync(q, f, d => new
        {
            d.Id, d.FoodItem, d.Quantity, d.Category, d.CreatedAt, Status = d.Status.ToString(),
            DonorName = d.Donor?.Name, NgoName = d.Ngo == null ? null : (d.Ngo.OrgName ?? d.Ngo.Name)
        }));
    }
}
