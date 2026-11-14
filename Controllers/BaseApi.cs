using System.Security.Claims;
using FoodDonationPlatform.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodDonationPlatform.Controllers;

public class ListQuery
{
    public string? Q { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 6;
}

public static class DonationFilters
{
    public static IQueryable<Donation> Apply(IQueryable<Donation> q, ListQuery f, bool searchPeople = false)
    {
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var p = $"%{f.Q.Trim()}%";
            q = searchPeople
                ? q.Where(d => EF.Functions.Like(d.FoodItem, p) || EF.Functions.Like(d.PickupAddress, p)
                               || EF.Functions.Like(d.Donor!.Name, p) || (d.Ngo != null && EF.Functions.Like(d.Ngo.Name, p)))
                : q.Where(d => EF.Functions.Like(d.FoodItem, p) || EF.Functions.Like(d.PickupAddress, p)
                               || (d.Description != null && EF.Functions.Like(d.Description, p)));
        }
        if (!string.IsNullOrWhiteSpace(f.Category)) q = q.Where(d => d.Category == f.Category);
        if (Enum.TryParse<DonationStatus>(f.Status, true, out var st)) q = q.Where(d => d.Status == st);
        return f.Sort == "expiry" ? q.OrderBy(d => d.ExpiryTime) : q.OrderByDescending(d => d.CreatedAt);
    }
}

public abstract class BaseApi : ControllerBase
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected static async Task<object> PageAsync<T>(IQueryable<T> q, ListQuery f, Func<T, object> map)
    {
        var size = Math.Clamp(f.PageSize, 1, 50);
        var total = await q.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        var page = Math.Clamp(f.Page, 1, pages);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return new { items = items.Select(map).ToList(), total, page, pages };
    }
}
