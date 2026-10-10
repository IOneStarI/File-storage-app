using FileStorage.Data;
using FileStorage.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

// Initializes the application entry point and the local database directory.
var builder = WebApplication.CreateBuilder(args);
var dataPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
Directory.CreateDirectory(dataPath);
// Configures the database context to store account and file metadata in SQLite.
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(dataPath, "files.db")}"));
// Configures account password requirements, unique emails, and failed-login lockout.
builder.Services.AddIdentity<AppUser, IdentityRole>(o => {
    o.Password.RequiredLength = 8;
    o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
// Configures login routes, cookie protections, and the sign-in lifetime.
builder.Services.ConfigureApplicationCookie(o => {
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
// Sets the multipart parser limit with room for upload form overhead.
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = StorageOptions.MaxBatchBytes + 2 * 1024 * 1024);
// Sets the HTTP server request-size limit to match the upload batch allowance.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = StorageOptions.MaxBatchBytes + 2 * 1024 * 1024);
// Registers MVC and validates anti-forgery tokens on state-changing requests.
builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
var app = builder.Build();
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Account/Error");
// Adds security headers to each response before continuing through the request pipeline.
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; frame-ancestors 'none'; form-action 'self'";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Files}/{action=Dashboard}/{id?}");
app.Run();
