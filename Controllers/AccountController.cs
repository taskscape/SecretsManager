using Microsoft.AspNetCore.Mvc;
using Passwords.Models;
using Passwords.Services;

namespace Passwords.Controllers;

public class AccountController : Controller
{
    private readonly JsonDataStore _store;

    public AccountController(JsonDataStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (IsLoggedIn)
        {
            return RedirectToAction("Index", "Entries");
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            ViewData["Error"] = "Username and password are required.";
            return View(model);
        }

        if (!_store.ValidateUser(model.Username, model.Password))
        {
            ViewData["Error"] = "Invalid username or password.";
            return View(model);
        }

        HttpContext.Session.SetString("username", model.Username);
        _store.LogLogin(model.Username);

        return RedirectToAction("Index", "Entries");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    private bool IsLoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("username"));
}
