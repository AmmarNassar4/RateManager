using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.Services;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private const string LastUserNameCookieName = "RateManager.LastUserName";
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var model = new LoginViewModel { ReturnUrl = returnUrl };

        if (Request.Cookies.TryGetValue(LastUserNameCookieName, out var rememberedUserName)
            && !string.IsNullOrWhiteSpace(rememberedUserName))
        {
            model.UserName = rememberedUserName;
            model.RememberMe = true;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.UserName == model.UserName && x.IsActive);
        if (user == null || !PasswordHasher.VerifyPassword(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var expiresUtc = model.RememberMe
            ? DateTimeOffset.UtcNow.AddDays(30)
            : DateTimeOffset.UtcNow.AddHours(8);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = expiresUtc,
                AllowRefresh = true
            });

        if (model.RememberMe)
        {
            Response.Cookies.Append(LastUserNameCookieName, user.UserName, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }
        else
        {
            Response.Cookies.Delete(LastUserNameCookieName);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
