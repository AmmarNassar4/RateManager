using RateManager.Net10.Models;

namespace RateManager.Net10.ViewModels;

public class LoginViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class CreateUserInput
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public AppUserRole Role { get; set; } = AppUserRole.Viewer;
}
