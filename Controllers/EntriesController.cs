using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Passwords.Models;
using Passwords.Services;

namespace Passwords.Controllers;

[Authorize]
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
        var entries = _store.GetEntries()
            .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View(entries);
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
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
        return View(new EntryCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(EntryCreateViewModel model)
    {
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

    private string CurrentUser => UserIdentifier.GetUserIdentifier(User) ?? "Unknown";
}
