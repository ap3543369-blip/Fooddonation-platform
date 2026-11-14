using System.Text;
using System.Text.Json.Serialization;
using FoodDonationPlatform.Data;
using FoodDonationPlatform.Hubs;
using FoodDonationPlatform.Models;
using FoodDonationPlatform.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<Notifier>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var key = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    db.Database.EnsureCreated();
    Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "uploads"));
    Seed(db, hasher);
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.Run();

// ---------- Demo data ----------
static User Make(IPasswordHasher<User> h, string name, string email, string pw, Role role,
    string? phone = null, string? address = null, string? org = null, string? reg = null, bool approved = true)
{
    var u = new User { Name = name, Email = email, Role = role, Phone = phone, Address = address, OrgName = org, RegistrationNo = reg, IsApproved = approved };
    u.PasswordHash = h.HashPassword(u, pw);
    return u;
}

static void Seed(AppDbContext db, IPasswordHasher<User> h)
{
    void AddIfMissing(User u) { if (!db.Users.Any(x => x.Email == u.Email)) db.Users.Add(u); }
    AddIfMissing(Make(h, "Admin", "admin@food.com", "Admin@123", Role.Admin));
    AddIfMissing(Make(h, "Rahul Sharma", "donor@food.com", "Donor@123", Role.Donor, "9876543210", "Rohini, Delhi"));
    AddIfMissing(Make(h, "Priya Verma", "ngo@food.com", "Ngo@123", Role.Ngo, "9811122233", "Karol Bagh, Delhi", "Hope Foundation", "NGO-DL-2021-045"));
    AddIfMissing(Make(h, "Amit Singh", "ngo2@food.com", "Ngo@123", Role.Ngo, "9899988877", "Dwarka, Delhi", "Helping Hands Trust", "NGO-DL-2023-112", approved: false));
    db.SaveChanges();

    if (db.Donations.Any()) return;
    var donor = db.Users.First(u => u.Email == "donor@food.com");
    var ngo = db.Users.First(u => u.Email == "ngo@food.com");
    var now = DateTime.UtcNow;
    db.Donations.AddRange(
        new Donation { DonorId = donor.Id, FoodItem = "Veg Biryani", Quantity = "25 plates", Category = "Cooked Food", PickupAddress = "Sector 15, Rohini, Delhi", Description = "Shaadi function ka bacha hua fresh khana.", ExpiryTime = now.AddHours(8), Status = DonationStatus.Pending },
        new Donation { DonorId = donor.Id, NgoId = ngo.Id, FoodItem = "Fresh Bread & Buns", Quantity = "40 pieces", Category = "Bakery", PickupAddress = "Connaught Place, Delhi", ExpiryTime = now.AddHours(10), Status = DonationStatus.Accepted, CreatedAt = now.AddHours(-3), UpdatedAt = now.AddHours(-2) },
        new Donation { DonorId = donor.Id, NgoId = ngo.Id, FoodItem = "Seasonal Fruits", Quantity = "15 kg", Category = "Fruits & Vegetables", PickupAddress = "Karol Bagh, Delhi", ExpiryTime = now.AddDays(-1), Status = DonationStatus.Delivered, CreatedAt = now.AddDays(-2), UpdatedAt = now.AddDays(-1) });
    db.SaveChanges();
}
