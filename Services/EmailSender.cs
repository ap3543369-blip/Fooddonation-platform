using System.Net;
using System.Net.Mail;

namespace FoodDonationPlatform.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody);
}

// Smtp:Host khali ho to email sirf console log me dikhegi (development mode).
public class EmailSender(IConfiguration cfg, ILogger<EmailSender> log) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = cfg["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            log.LogInformation("[DEV EMAIL] To: {To} | Subject: {Subject}\n{Body}", to, subject, htmlBody);
            return;
        }
        try
        {
            using var client = new SmtpClient(host, cfg.GetValue("Smtp:Port", 587))
            {
                EnableSsl = cfg.GetValue("Smtp:EnableSsl", true),
                Credentials = new NetworkCredential(cfg["Smtp:User"], cfg["Smtp:Password"])
            };
            var from = cfg["Smtp:From"] ?? cfg["Smtp:User"] ?? "no-reply@fooddonation.local";
            using var msg = new MailMessage(from, to, subject, htmlBody) { IsBodyHtml = true };
            await client.SendMailAsync(msg);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Email bhejne me error: {To}", to);   // email fail ho to app na ruke
        }
    }
}

public static class EmailTemplates
{
    public static string Wrap(string bodyHtml, string? buttonText = null, string? buttonUrl = null)
    {
        var btn = buttonUrl == null ? "" :
            "<p><a href='" + buttonUrl + "' style='display:inline-block;background:#16a34a;color:#fff;padding:11px 22px;border-radius:8px;text-decoration:none;font-weight:600'>" + buttonText + "</a></p>";
        return "<div style='font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:auto;border:1px solid #e1ebe4;border-radius:12px;overflow:hidden'>" +
               "<div style='background:#16a34a;color:#fff;padding:16px 22px;font-size:18px;font-weight:600'>🍲 Food Donation Platform</div>" +
               "<div style='padding:22px;color:#12261a;font-size:15px;line-height:1.6'>" + bodyHtml + btn + "</div>" +
               "<div style='padding:12px 22px;background:#f3f7f4;color:#64786b;font-size:12px'>Ye ek automated email hai.</div></div>";
    }
}
