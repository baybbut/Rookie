using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ShopDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Shop") ?? "Data Source=rookie.db"));
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
 o.LoginPath = "/admin/login"; o.AccessDeniedPath = "/admin"; o.Cookie.Name = "rookie-staff"; o.ExpireTimeSpan = TimeSpan.FromHours(8);
 // Nhân viên bị xóa/khóa thì phiên đang mở bị hủy ngay.
 o.Events.OnValidatePrincipal = async ctx => {
  var s = int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? await ctx.HttpContext.RequestServices.GetRequiredService<ShopDbContext>().Staffs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) : null;
  if (s is null || s.IsLocked) { ctx.RejectPrincipal(); await ctx.HttpContext.SignOutAsync(); }
 };
});
builder.Services.AddAuthorization();
var app = builder.Build();
using (var scope = app.Services.CreateScope()) {
 // Mật khẩu quản trị lấy từ cấu hình Admin:Password; riêng môi trường Development có mặc định "admin".
 DbInit.Prepare(scope.ServiceProvider.GetRequiredService<ShopDbContext>(), app.Configuration["Admin:Password"] ?? (app.Environment.IsDevelopment() ? "admin" : null), app.Environment.IsDevelopment(), m => app.Logger.LogWarning("{Msg}", m));
}
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
