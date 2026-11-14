using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FoodDonationPlatform.Hubs;

[Authorize]
public class NotificationHub : Hub { }
