using RBI_Malaysia.Services;
using RBI_Malaysia.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true;
    });

// Authentication
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RBI_Malaysia_Auth";

        options.LoginPath = "/login";
        options.LogoutPath = "/account/logout";

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();

// Application Services
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProcessAreaService>();
builder.Services.AddScoped<EquipmentService>();
builder.Services.AddScoped<ComponentService>();
builder.Services.AddScoped<InspectionService>();
builder.Services.AddScoped<COFFlammableService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();


// ============================================================
// LOGIN ENDPOINT
// ============================================================

app.MapPost("/account/login", async (
    HttpContext context,
    LoginService loginService) =>
{
    var form = await context.Request.ReadFormAsync();

    string username = form["Username"].ToString();
    string password = form["Password"].ToString();

    if (string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/login?error=required");
    }

    LoginResult? result =
        await loginService.ValidateLoginAsync(username, password);

    if (result == null)
    {
        return Results.Redirect("/login?error=invalid");
    }

    var claims = new List<Claim>
    {
        new Claim(
            ClaimTypes.NameIdentifier,
            result.UserID),

        new Claim(
            ClaimTypes.Name,
            result.UserName),

        new Claim(
            "CompanyID",
            result.CompanyID),

        new Claim(
            "CompanyName",
            result.CompanyName)
    };

    var identity = new ClaimsIdentity(
        claims,
        CookieAuthenticationDefaults.AuthenticationScheme);

    var principal = new ClaimsPrincipal(identity);

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            AllowRefresh = true
        });

    return Results.Redirect("/home");
})
.DisableAntiforgery();


// ============================================================
// LOGOUT ENDPOINT
// ============================================================

app.MapGet("/account/logout", async (
    HttpContext context,
    UserSession userSession) =>
{
    await context.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);

    userSession.Clear();

    return Results.Redirect("/login");
});


// ============================================================
// RAZOR COMPONENTS
// ============================================================

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();