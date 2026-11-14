namespace FoodDonationPlatform.Models;

public enum Role { Donor, Ngo, Admin }
public enum DonationStatus { Pending, Accepted, PickedUp, Delivered, Cancelled }

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Role Role { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? OrgName { get; set; }
    public string? RegistrationNo { get; set; }
    public bool IsApproved { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Donation
{
    public int Id { get; set; }
    public int DonorId { get; set; }
    public User? Donor { get; set; }
    public int? NgoId { get; set; }
    public User? Ngo { get; set; }
    public string FoodItem { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string? Description { get; set; }
    public string PickupAddress { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTime ExpiryTime { get; set; }
    public DonationStatus Status { get; set; } = DonationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
