using Microsoft.AspNetCore.Mvc;
using Passwords.Models;
using Passwords.Services;

namespace Passwords.Controllers;

public class EntriesController : Controller
{
    private readonly JsonDataStore _store;

    public EntriesController(JsonDataStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!IsLoggedIn)
        {
            return RedirectToLogin();
        }

        var entries = _store.GetEntries()
            .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View(entries);
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        if (!IsLoggedIn)
        {
            return RedirectToLogin();
        }

        var entry = _store.GetEntry(id);
        if (entry == null)
        {
            return NotFound();
        }

        _store.LogEntryOpened(CurrentUser, entry);
        return View(entry);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsLoggedIn)
        {
            return RedirectToLogin();
        }

        return View(new EntryCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(EntryCreateViewModel model)
    {
        if (!IsLoggedIn)
        {
            return RedirectToLogin();
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
            return View(model);
        }

        var entry = _store.AddEntry(model.Title.Trim(), model.Details ?? string.Empty, CurrentUser);
        return RedirectToAction("Details", new { id = entry.Id });
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        if (!IsLoggedIn)
        {
            return RedirectToLogin();
        }

        var entry = _store.GetEntry(id);
        if (entry == null)
        {
            return NotFound();
        }

        var model = new EntryEditViewModel
        {
            Id = entry.Id,
            Title = entry.Title,
            Details = entry.Details
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(EntryEditViewModel model)
    {
        if (!IsLoggedIn)
        {
            return RedirectToLogin();
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
            return View(model);
        }

        var updated = _store.UpdateEntry(model.Id, model.Title.Trim(), model.Details ?? string.Empty, CurrentUser);
        if (!updated)
        {
            return NotFound();
        }

        return RedirectToAction("Details", new { id = model.Id });
    }

    private string CurrentUser => HttpContext.Session.GetString("username") ?? "";

    private bool IsLoggedIn => !string.IsNullOrEmpty(CurrentUser);

    private IActionResult RedirectToLogin() => RedirectToAction("Login", "Account");
}
