namespace RateManager.Net10.Models;

public enum AppUserRole
{
    Viewer = 1,
    Admin = 2
}

public class AppUser
{
    public int AppUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public AppUserRole Role { get; set; } = AppUserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
