using FileStorage.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
namespace FileStorage.Controllers;
public class AccountController(UserManager<AppUser> users, SignInManager<AppUser> signIn) : Controller {
    /// <summary>
    /// Displays the registration form with an empty account model.
    /// </summary>
    [HttpGet, AllowAnonymous] public IActionResult Register() => View(new RegisterModel());
    /// <summary>
    /// Validates registration details, creates an Identity account, and signs in the new user. Returns validation errors when account creation fails.
    /// </summary>
    [HttpPost, AllowAnonymous] public async Task<IActionResult> Register(RegisterModel model) {
        if (!ModelState.IsValid) return View(model);
        var user = new AppUser { UserName = model.Username, Email = model.Email, DisplayName = model.DisplayName.Trim() };
        var result = await users.CreateAsync(user, model.Password);
        if (!result.Succeeded) {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }
        await signIn.SignInAsync(user, false);
        TempData["Success"] = "Welcome! Your personal file space is ready.";
        return RedirectToAction("Dashboard", "Files");
    }
    /// <summary>
    /// Displays the login form and preserves the requested destination for a successful sign-in.
    /// </summary>
    [HttpGet, AllowAnonymous] public IActionResult Login(string? returnUrl) => View(new LoginModel { ReturnUrl = returnUrl });
    /// <summary>
    /// Authenticates the user with failed-attempt lockout and optional persistent sign-in. Redirects only to a local return URL or the dashboard.
    /// </summary>
    [HttpPost, AllowAnonymous] public async Task<IActionResult> Login(LoginModel model) {
        if (!ModelState.IsValid) return View(model);
        var result = await signIn.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded) {
            ModelState.AddModelError("", result.IsLockedOut ? "Too many attempts. Try again in five minutes." : "Incorrect username or password.");
            return View(model);
        }
        return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Dashboard", "Files");
    }
    /// <summary>
    /// Signs out the current user and redirects to the login page.
    /// </summary>
    [HttpPost, Authorize] public async Task<IActionResult> Logout() {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
    /// <summary>
    /// Displays the general error page with an HTTP 500 response.
    /// </summary>
    [AllowAnonymous] public IActionResult Error() { Response.StatusCode = 500; return View(); }
}
