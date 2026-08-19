using RBI_Malaysia.Services;
using RBI_Malaysia.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
//builder.Services.AddRazorComponents()
//    .AddInteractiveServerComponents();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true;
    });

builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProcessAreaService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
