using Microsoft.AspNetCore.Mvc;
using Passwords.Models;
using Passwords.Services;

namespace Passwords.Controllers;

public class RequestsController : Controller
{
    private readonly JsonDataStore _store;

    public RequestsController(JsonDataStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!IsLoggedIn) return RedirectToLogin();

        var requests = _store.GetPendingRequestsForOwner(CurrentUser);
        var items = requests.Select(r => new AccessRequestViewModel
        {
            Request    = r,
            EntryTitle = _store.GetEntry(r.EntryId)?.Title ?? "(deleted)"
        }).ToList();

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Approve(int id)
    {
        if (!IsLoggedIn) return RedirectToLogin();
        _store.ApproveRequest(id, CurrentUser);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Decline(int id)
    {
        if (!IsLoggedIn) return RedirectToLogin();
        _store.DeclineRequest(id, CurrentUser);
        return RedirectToAction("Index");
    }

    private string CurrentUser => HttpContext.Session.GetString("username") ?? "";
    private bool IsLoggedIn    => !string.IsNullOrEmpty(CurrentUser);
    private IActionResult RedirectToLogin() => RedirectToAction("Login", "Account");
}
