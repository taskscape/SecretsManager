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
        if (!IsLoggedIn) return RedirectToLogin();

        var currentUser = _store.GetUser(CurrentUser);
        var items = _store.GetEntries()
            .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .Select(e => new EntryListItemViewModel
            {
                Entry   = e,
                CanRead = _store.CanUserReadEntry(e, currentUser)
            })
            .ToList();

        return View(items);
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        if (!IsLoggedIn) return RedirectToLogin();

        var entry = _store.GetEntry(id);
        if (entry == null) return NotFound();

        var currentUser = _store.GetUser(CurrentUser);
        var canRead     = _store.CanUserReadEntry(entry, currentUser);

        if (canRead)
            _store.LogEntryOpened(CurrentUser, entry);

        var vm = new EntryDetailsViewModel
        {
            Entry             = entry,
            CanRead           = canRead,
            HasPendingRequest = !canRead && _store.HasPendingRequest(CurrentUser, id),
            OwnerUsername     = entry.CreatedBy
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsLoggedIn) return RedirectToLogin();
        return View(new EntryCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(EntryCreateViewModel model)
    {
        if (!IsLoggedIn) return RedirectToLogin();

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
            return View(model);
        }

        var entry = _store.AddEntry(
            model.Title.Trim(),
            model.Details ?? string.Empty,
            string.IsNullOrWhiteSpace(model.Users) ? null : model.Users.Trim(),
            CurrentUser);
        return RedirectToAction("Details", new { id = entry.Id });
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        if (!IsLoggedIn) return RedirectToLogin();

        var entry = _store.GetEntry(id);
        if (entry == null) return NotFound();

        var currentUser = _store.GetUser(CurrentUser);
        if (!_store.CanUserReadEntry(entry, currentUser)) return Forbid();

        var model = new EntryEditViewModel
        {
            Id      = entry.Id,
            Title   = entry.Title,
            Details = entry.Details,
            Users   = entry.Users
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(EntryEditViewModel model)
    {
        if (!IsLoggedIn) return RedirectToLogin();

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
            return View(model);
        }

        var entry = _store.GetEntry(model.Id);
        if (entry == null) return NotFound();
        var currentUser = _store.GetUser(CurrentUser);
        if (!_store.CanUserReadEntry(entry, currentUser)) return Forbid();

        var updated = _store.UpdateEntry(
            model.Id,
            model.Title.Trim(),
            model.Details ?? string.Empty,
            string.IsNullOrWhiteSpace(model.Users) ? null : model.Users.Trim(),
            CurrentUser);
        if (!updated) return NotFound();

        return RedirectToAction("Details", new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RequestAccess(int id)
    {
        if (!IsLoggedIn) return RedirectToLogin();

        var entry = _store.GetEntry(id);
        if (entry == null) return NotFound();

        var currentUser = _store.GetUser(CurrentUser);
        if (_store.CanUserReadEntry(entry, currentUser))
            return RedirectToAction("Details", new { id });

        _store.CreateAccessRequest(CurrentUser, id);
        return RedirectToAction("Details", new { id });
    }

    private string CurrentUser => HttpContext.Session.GetString("username") ?? "";
    private bool IsLoggedIn    => !string.IsNullOrEmpty(CurrentUser);
    private IActionResult RedirectToLogin() => RedirectToAction("Login", "Account");
}
