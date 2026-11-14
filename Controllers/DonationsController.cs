using FoodDonationPlatform.Data;
using FoodDonationPlatform.Models;
using FoodDonationPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodDonationPlatform.Controllers;

[ApiController, Route("api/donations"), Authorize]
public class DonationsController(AppDbContext db, Notifier notifier, IWebHostEnvironment env) : BaseApi
{
    public class CreateDto
    {
        public string? FoodItem { get; set; }
        public string? Quantity { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? PickupAddress { get; set; }
        public DateTime ExpiryTime { get; set; }
        public IFormFile? Image { get; set; }
    }
    public record StatusDto(string Status);

    static readonly string[] AllowedExt = [".jpg", ".jpeg", ".png", ".webp"];

    static object Map(Donation d) => new
    {
        d.Id, d.FoodItem, d.Quantity, d.Category, d.Description, d.PickupAddress, d.ImageUrl,
        d.ExpiryTime, d.CreatedAt, d.UpdatedAt, Status = d.Status.ToString(),
        DonorName = d.Donor?.Name, DonorPhone = d.Donor?.Phone,
        NgoName = d.Ngo == null ? null : (d.Ngo.OrgName ?? d.Ngo.Name), NgoPhone = d.Ngo?.Phone
    };

    IQueryable<Donation> Base() => db.Donations.Include(d => d.Donor).Include(d => d.Ngo);

    static bool LooksLikeImage(IFormFile f)
    {
        using var s = f.OpenReadStream();
        var b = new byte[12];
        var n = s.Read(b, 0, 12);
        if (n < 4) return false;
        var jpg = b[0] == 0xFF && b[1] == 0xD8;
        var png = b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
        var webp = n >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50;
        return jpg || png || webp;
    }

    // ---------- DONOR ----------
    [HttpPost, Authorize(Roles = "Donor"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FoodItem) || string.IsNullOrWhiteSpace(dto.Quantity) || string.IsNullOrWhiteSpace(dto.PickupAddress))
            return BadRequest(new { message = "Food item, quantity aur pickup address zaruri hain." });
        if (dto.ExpiryTime.ToUniversalTime() <= DateTime.UtcNow)
            return BadRequest(new { message = "Expiry time future me hona chahiye." });

        string? imageUrl = null;
        if (dto.Image is { Length: > 0 })
        {
            var ext = Path.GetExtension(dto.Image.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext) || !LooksLikeImage(dto.Image))
                return BadRequest(new { message = "Sirf JPG, PNG ya WEBP image allowed hai." });
            if (dto.Image.Length > 3 * 1024 * 1024)
                return BadRequest(new { message = "Image 3 MB se chhoti honi chahiye." });

            var dir = Path.Combine(env.WebRootPath, "uploads");
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid():N}{ext}";
            await using var fs = System.IO.File.Create(Path.Combine(dir, name));
            await dto.Image.CopyToAsync(fs);
            imageUrl = "/uploads/" + name;
        }

        var cat = string.IsNullOrWhiteSpace(dto.Category) ? "Other" : dto.Category.Trim();
        var d = new Donation
        {
            DonorId = UserId, FoodItem = dto.FoodItem.Trim(), Quantity = dto.Quantity.Trim(),
            Category = cat.Length > 40 ? cat[..40] : cat, Description = dto.Description?.Trim(),
            PickupAddress = dto.PickupAddress.Trim(), ExpiryTime = dto.ExpiryTime.ToUniversalTime(), ImageUrl = imageUrl
        };
        db.Donations.Add(d);
        await db.SaveChangesAsync();

        var ngoIds = await db.Users.Where(u => u.Role == Role.Ngo && u.IsApproved).Select(u => u.Id).ToListAsync();
        foreach (var id in ngoIds)
            await notifier.SendAsync(id, $"Nayi food donation available: {d.FoodItem} ({d.Quantity})", "Nayi food donation available");

        return Ok(new { message = "Donation request bhej di gayi!", id = d.Id });
    }

    [HttpGet("mine"), Authorize(Roles = "Donor")]
    public async Task<IActionResult> Mine([FromQuery] ListQuery f) =>
        Ok(await PageAsync(DonationFilters.Apply(Base().Where(d => d.DonorId == UserId), f), f, Map));

    [HttpPost("{id}/cancel"), Authorize(Roles = "Donor")]
    public async Task<IActionResult> Cancel(int id)
    {
        var d = await db.Donations.FirstOrDefaultAsync(x => x.Id == id && x.DonorId == UserId);
        if (d == null) return NotFound();
        if (d.Status != DonationStatus.Pending) return BadRequest(new { message = "Sirf pending donation cancel ho sakti hai." });
        d.Status = DonationStatus.Cancelled; d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "Donation cancel ho gayi." });
    }

    // ---------- NGO ----------
    [HttpGet("available"), Authorize(Roles = "Ngo")]
    public async Task<IActionResult> Available([FromQuery] ListQuery f)
    {
        f.Status = null; // sirf Pending dikhani hain
        var q = Base().Where(d => d.Status == DonationStatus.Pending && d.ExpiryTime > DateTime.UtcNow);
        return Ok(await PageAsync(DonationFilters.Apply(q, f), f, Map));
    }

    [HttpGet("ngo/mine"), Authorize(Roles = "Ngo")]
    public async Task<IActionResult> NgoMine([FromQuery] ListQuery f) =>
        Ok(await PageAsync(DonationFilters.Apply(Base().Where(d => d.NgoId == UserId), f), f, Map));

    [HttpPost("{id}/accept"), Authorize(Roles = "Ngo")]
    public async Task<IActionResult> Accept(int id)
    {
        var d = await db.Donations.FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();
        if (d.Status != DonationStatus.Pending) return BadRequest(new { message = "Ye donation ab available nahi hai." });

        var ngo = await db.Users.FindAsync(UserId);
        d.Status = DonationStatus.Accepted; d.NgoId = UserId; d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await notifier.SendAsync(d.DonorId, $"Aapki donation '{d.FoodItem}' ko {ngo!.OrgName ?? ngo.Name} ne accept kar liya!", "Aapki donation accept ho gayi");
        return Ok(new { message = "Donation accept ho gayi." });
    }

    [HttpPost("{id}/status"), Authorize(Roles = "Ngo")]
    public async Task<IActionResult> UpdateStatus(int id, StatusDto dto)
    {
        var d = await db.Donations.FirstOrDefaultAsync(x => x.Id == id && x.NgoId == UserId);
        if (d == null) return NotFound();
        if (!Enum.TryParse<DonationStatus>(dto.Status, true, out var next)) return BadRequest(new { message = "Invalid status." });

        var ok = (d.Status == DonationStatus.Accepted && next == DonationStatus.PickedUp)
              || (d.Status == DonationStatus.PickedUp && next == DonationStatus.Delivered);
        if (!ok) return BadRequest(new { message = "Ye status change allowed nahi hai." });

        d.Status = next; d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await notifier.SendAsync(d.DonorId, $"Aapki donation '{d.FoodItem}' ka status ab: {next}", $"Donation status: {next}");
        return Ok(new { message = $"Status updated: {next}" });
    }
}
