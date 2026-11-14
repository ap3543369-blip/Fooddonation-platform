using System.Net;
using FoodDonationPlatform.Data;
using FoodDonationPlatform.Hubs;
using FoodDonationPlatform.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FoodDonationPlatform.Services;

// DB me save + SignalR real-time + email, teeno ek saath
public class Notifier(AppDbContext db, IHubContext<NotificationHub> hub, IEmailSender email)
{
    public async Task SendAsync(int userId, string message, string? emailSubject = null)
    {
        db.Notifications.Add(new Notification { UserId = userId, Message = message });
        await db.SaveChangesAsync();
        await hub.Clients.User(userId.ToString()).SendAsync("notification", message);

        var to = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
        if (to != null)
            _ = email.SendAsync(to, emailSubject ?? "Food Donation Platform - Update",
                EmailTemplates.Wrap(WebUtility.HtmlEncode(message)));   // fire-and-forget
    }
}
