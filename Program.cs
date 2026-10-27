using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
const int SchemaVersion = 2; // tăng khi đổi model: DB cũ được sao lưu thành rookie.db.bak-* rồi tạo lại (dự án không dùng EF migrations)
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
 var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>(); var cn = db.Database.GetDbConnection();
 if (File.Exists(cn.DataSource)) {
  long v; cn.Open(); using (var cmd = cn.CreateCommand()) { cmd.CommandText = "PRAGMA user_version"; v = (long)cmd.ExecuteScalar()!; } cn.Close();
  if (v < SchemaVersion) { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); var bak = $"{cn.DataSource}.bak-{DateTime.Now:yyyyMMddHHmmss}"; File.Move(cn.DataSource, bak); app.Logger.LogWarning("Schema cũ: đã sao lưu {Old} thành {Bak} và tạo lại cơ sở dữ liệu.", cn.DataSource, bak); }
 }
 db.Database.EnsureCreated(); db.Database.ExecuteSqlRaw($"PRAGMA user_version = {SchemaVersion}");
 // Mật khẩu quản trị lấy từ cấu hình Admin:Password; riêng môi trường Development có mặc định "admin".
 DemoData.SeedStaff(db, app.Configuration["Admin:Password"] ?? (app.Environment.IsDevelopment() ? "admin" : null), app.Environment.IsDevelopment()); DemoData.Seed(db);
}
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
