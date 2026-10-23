using Microsoft.EntityFrameworkCore;
using Rookie.Data;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ShopDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Shop") ?? "Data Source=rookie.db"));
var app = builder.Build();
using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>(); db.Database.EnsureCreated(); }
app.UseStaticFiles(); app.UseRouting();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();