using Microsoft.EntityFrameworkCore;
using Rookie.Data;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ShopDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Shop") ?? "Data Source=rookie.db"));
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => { o.LoginPath = "/admin/login"; o.Cookie.Name = "rookie-admin"; o.ExpireTimeSpan = TimeSpan.FromHours(8); });
builder.Services.AddAuthorization();
var app = builder.Build();
using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>(); db.Database.EnsureCreated(); DemoData.Seed(db); }
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();