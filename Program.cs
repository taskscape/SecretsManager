using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Passwords.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<JsonDataStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
})
.AddOpenIdConnect(options =>
{
    var authSection = builder.Configuration.GetSection("Authentication:Microsoft");
    options.Authority = "https://login.microsoftonline.com/common/v2.0";
    options.ClientId = authSection["ClientId"] ?? string.Empty;
    options.ClientSecret = authSection["ClientSecret"] ?? string.Empty;
    options.CallbackPath = authSection["CallbackPath"] ?? "/signin-oidc";
    options.ResponseType = "code";
    options.SaveTokens = false;
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "preferred_username"
    };
    options.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = context =>
        {
            var store = context.HttpContext.RequestServices.GetRequiredService<JsonDataStore>();
            var identifier = UserIdentifier.GetUserIdentifier(context.Principal)?.Trim();
            if (string.IsNullOrWhiteSpace(identifier) || !store.IsAllowedUser(identifier))
            {
                context.Fail("User is not allowed.");
                return Task.CompletedTask;
            }

            store.LogLogin(identifier);
            return Task.CompletedTask;
        },
        OnRemoteFailure = context =>
        {
            context.HandleResponse();
            var message = context.Failure?.Message?.Contains("not allowed", StringComparison.OrdinalIgnoreCase) == true
                ? "Your Microsoft account is not authorized for this app."
                : "Microsoft sign-in failed.";
            var encoded = Uri.EscapeDataString(message);
            context.Response.Redirect($"/Account/Login?error={encoded}");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (!string.IsNullOrEmpty(path) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Entries}/{action=Index}/{id?}");

app.Run();
