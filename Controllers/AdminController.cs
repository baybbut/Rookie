using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers;
[Authorize(AuthenticationSchemes = Auth.Staff), Route("admin")]
public partial class AdminController(ShopDbContext db, IWebHostEnvironment env) : Controller {
 // Tên nhân viên đang đăng nhập, ghi lại trên đơn/phiếu mình xử lý.
 string Me => User.Identity?.Name ?? "—";

 [AllowAnonymous, HttpGet("login")]
 public async Task<IActionResult> Login(string? returnUrl) { ViewBag.ReturnUrl = returnUrl; ViewBag.Disabled = !await db.Staffs.AnyAsync(); return View(); }

 [AllowAnonymous, HttpPost("login"), ValidateAntiForgeryToken]
 public async Task<IActionResult> Login(string? username, string? password, string? returnUrl) {
  username = username?.Trim().ToLowerInvariant();
  var s = string.IsNullOrEmpty(username) ? null : await db.Staffs.FirstOrDefaultAsync(x => x.Username == username);
  if (s is not null && !s.IsLocked && Pw.Verify(password ?? "", s.PasswordHash)) {
   var id = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, s.Id.ToString()), new Claim(ClaimTypes.Name, s.FullName), new Claim(ClaimTypes.Role, s.Role), new Claim("username", s.Username)], CookieAuthenticationDefaults.AuthenticationScheme);
   await HttpContext.SignInAsync(Auth.Staff, new ClaimsPrincipal(id));
   return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
  }
  ViewBag.ReturnUrl = returnUrl; ViewBag.Disabled = !await db.Staffs.AnyAsync(); ViewBag.Username = username;
  ViewBag.Error = s is not null && s.IsLocked && Pw.Verify(password ?? "", s.PasswordHash) ? "Tài khoản này đã bị khóa." : "Sai tên đăng nhập hoặc mật khẩu, thử lại nhé!";
  return View();
 }

 [HttpPost("logout"), ValidateAntiForgeryToken]
 public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(Auth.Staff); return RedirectToAction(nameof(Login)); }

 [HttpGet("")]
 public async Task<IActionResult> Index() => View(await BuildDashboard(db));

 internal static async Task<DashboardVm> BuildDashboard(ShopDbContext db) {
  var orders = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).ToListAsync();
  var purchases = await db.Purchases.AsNoTracking().Include(p => p.Items).Where(p => p.Status == PurchaseStatus.Received).ToListAsync();
  var today = DateTime.Today; var month = new DateTime(today.Year, today.Month, 1);
  var live = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
  return new DashboardVm {
   RevenueToday = live.Where(o => o.CreatedAt >= today).Sum(o => o.Total),
   RevenueMonth = live.Where(o => o.CreatedAt >= month).Sum(o => o.Total),
   PurchaseMonth = purchases.Where(p => p.CreatedAt >= month).Sum(p => p.Total),
   NewOrders = orders.Count(o => o.Status == OrderStatus.New),
   OpenOrders = orders.Count(o => o.Status is OrderStatus.New or OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Shipping),
   Customers = await db.Customers.CountAsync(), Products = await db.Products.CountAsync(),
   Days = Series(live, today.AddDays(-6), 7),
   Recent = orders.OrderByDescending(o => o.CreatedAt).Take(6).ToList(),
   TopProducts = TopProducts(live.Where(o => o.CreatedAt >= today.AddDays(-29)), 5),
  };
 }

 static List<DayPoint> Series(IEnumerable<Order> live, DateTime from, int days) {
  var byDay = live.Where(o => o.CreatedAt >= from).GroupBy(o => o.CreatedAt.Date).ToDictionary(g => g.Key, g => g.ToList());
  return Enumerable.Range(0, days).Select(i => { var d = from.AddDays(i); var l = byDay.GetValueOrDefault(d) ?? []; return new DayPoint(d, l.Sum(o => o.Total), l.Count); }).ToList();
 }

 static List<NamedValue> TopProducts(IEnumerable<Order> live, int take) =>
  live.SelectMany(o => o.Items).GroupBy(i => i.ProductName).Select(g => new NamedValue(g.Key, g.Sum(i => i.Subtotal), g.Sum(i => i.Quantity))).OrderByDescending(n => n.Value).Take(take).ToList();

 static bool Has(string? hay, string? term) => string.IsNullOrWhiteSpace(term) || (hay ?? "").Contains(term.Trim(), StringComparison.CurrentCultureIgnoreCase);

 [HttpGet("products")]
 public async Task<IActionResult> Products(string? code, string? name, string? category, string? status) {
  IEnumerable<Product> r = await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
  r = r.Where(p => Has(p.Code, code) && Has(p.Name, name) && (string.IsNullOrEmpty(category) || p.Category == category) && (string.IsNullOrEmpty(status) || (status == "best" ? p.IsBestSeller : status == "new" ? p.IsNew : !p.IsBestSeller && !p.IsNew)));
  ViewBag.Code = code; ViewBag.Name = name; ViewBag.Category = category; ViewBag.Status = status;
  await LoadCategories();
  return View(r.ToList());
 }

 // ---- Danh mục ----
 [Authorize(Roles = StaffRole.Admin), HttpGet("categories")]
 public async Task<IActionResult> Categories() {
  var cats = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
  var counts = await db.Products.AsNoTracking().GroupBy(p => p.Category).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
  return View(cats.Select(c => new CategoryRow(c.Id, c.Name, counts.GetValueOrDefault(c.Name), c.IsHidden)).ToList());
 }

 [Authorize(Roles = StaffRole.Admin), HttpPost("categories/toggle/{id:int}"), ValidateAntiForgeryToken]
 public async Task<IActionResult> ToggleCategory(int id) {
  var c = await db.Categories.FindAsync(id);
  if (c is null) return NotFound();
  c.IsHidden = !c.IsHidden; await db.SaveChangesAsync();
  TempData["Msg"] = c.IsHidden ? $"Đã ẩn “{c.Name}” khỏi trang bán bánh." : $"Đã hiện lại “{c.Name}” trên trang bán bánh.";
  return RedirectToAction(nameof(Categories));
 }

 [Authorize(Roles = StaffRole.Admin), HttpPost("categories/create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CreateCategory(string? name) {
  name = name?.Trim();
  if (string.IsNullOrEmpty(name) || name.Length > 40) TempData["Msg"] = "Tên danh mục không hợp lệ (1–40 ký tự).";
  else if (await db.Categories.AnyAsync(c => c.Name == name)) TempData["Msg"] = $"Danh mục “{name}” đã tồn tại.";
  else { db.Categories.Add(new Category { Name = name }); await db.SaveChangesAsync(); TempData["Msg"] = $"Đã thêm danh mục “{name}”."; }
  return RedirectToAction(nameof(Categories));
 }

 [Authorize(Roles = StaffRole.Admin), HttpPost("categories/edit/{id:int}"), ValidateAntiForgeryToken]
 public async Task<IActionResult> EditCategory(int id, string? name) {
  var c = await db.Categories.FindAsync(id);
  if (c is null) return NotFound();
  name = name?.Trim();
  if (string.IsNullOrEmpty(name) || name.Length > 40) TempData["Msg"] = "Tên danh mục không hợp lệ (1–40 ký tự).";
  else if (name != c.Name && await db.Categories.AnyAsync(x => x.Name == name)) TempData["Msg"] = $"Danh mục “{name}” đã tồn tại.";
  else if (name != c.Name) {
   var old = c.Name; c.Name = name;
   var n = await db.Products.Where(p => p.Category == old).ExecuteUpdateAsync(s => s.SetProperty(p => p.Category, name));
   await db.SaveChangesAsync();
   TempData["Msg"] = $"Đã đổi “{old}” thành “{name}” ({n} sản phẩm).";
  }
  return RedirectToAction(nameof(Categories));
 }

 [Authorize(Roles = StaffRole.Admin), HttpPost("categories/delete/{id:int}"), ValidateAntiForgeryToken]
 public async Task<IActionResult> DeleteCategory(int id) {
  var c = await db.Categories.FindAsync(id);
  if (c is null) return NotFound();
  if (await db.Products.AnyAsync(p => p.Category == c.Name)) TempData["Msg"] = $"Không xóa được “{c.Name}” vì vẫn còn bánh trong danh mục này.";
  else { db.Categories.Remove(c); await db.SaveChangesAsync(); TempData["Msg"] = $"Đã xóa danh mục “{c.Name}”."; }
  return RedirectToAction(nameof(Categories));
 }

 // ---- Sản phẩm ----
 [HttpGet("create")]
 public async Task<IActionResult> Create() { await LoadCategories(); return View("Form", new Product()); }

 [HttpPost("create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> Create([Bind("Code,Name,Description,Image,Category,Unit,Margin")] Product p, string? status, int? priceId, IFormFile? upload) {
  // Bánh mới: chọn bánh trong danh sách của xưởng (giá nhập lấy đúng giá xưởng, không nhập tay); giá bán tự tính từ tỉ lệ lợi nhuận.
  var sp = await db.SupplierPrices.FirstOrDefaultAsync(x => x.Id == priceId && x.ProductId == null);
  if (sp is null || !await db.Suppliers.AnyAsync(s => s.Id == sp.SupplierId && s.IsActive)) ModelState.AddModelError(nameof(Product.SupplierId), "Hãy chọn một bánh trong danh sách của xưởng đang hợp tác.");
  else (p.SupplierId, p.CostPrice) = (sp.SupplierId, sp.Price);
  await SaveUpload(p, upload); await CheckProduct(p, 0); ProductStatus.Apply(p, status);
  if (!ModelState.IsValid) { ViewBag.PriceId = priceId; await LoadCategories(); return View("Form", p); }
  using var tx = await db.Database.BeginTransactionAsync();
  db.Products.Add(p); await db.SaveChangesAsync();
  sp!.ProductId = p.Id; await db.SaveChangesAsync(); await tx.CommitAsync();
  TempData["Msg"] = $"Đã thêm “{p.Name}” (mã {p.Code}).";
  return RedirectToAction(nameof(Products));
 }

 [HttpGet("edit/{id:int}")]
 public async Task<IActionResult> Edit(int id) {
  var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
  if (p is null) return NotFound();
  await LoadCategories(); return View("Form", p);
 }

 [HttpPost("edit/{id:int}"), ValidateAntiForgeryToken]
 public async Task<IActionResult> Edit(int id, [Bind("Code,Name,Description,Image,Category,Unit,Margin")] Product input, string? status, IFormFile? upload) {
  var p = await db.Products.FindAsync(id);
  if (p is null) return NotFound();
  (input.CostPrice, input.SupplierId) = (p.CostPrice, p.SupplierId); // giá nhập đi theo bảng giá xưởng, không sửa ở đây
  await SaveUpload(input, upload); await CheckProduct(input, id); ProductStatus.Apply(input, status);
  if (!ModelState.IsValid) { input.Id = id; await LoadCategories(); return View("Form", input); }
  (p.Code, p.Name, p.Description, p.Image, p.Category, p.Unit, p.Price, p.CostPrice, p.Margin, p.IsNew, p.IsBestSeller) = (input.Code, input.Name, input.Description, input.Image, input.Category, input.Unit, input.Price, input.CostPrice, input.Margin, input.IsNew, input.IsBestSeller);
  await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã cập nhật “{p.Name}”.";
  return RedirectToAction(nameof(Products));
 }

 [HttpPost("delete/{id:int}"), ValidateAntiForgeryToken]
 public async Task<IActionResult> Delete(int id) {
  var p = await db.Products.FindAsync(id);
  if (p is null) return NotFound();
  db.Products.Remove(p); await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã xóa “{p.Name}”.";
  return RedirectToAction(nameof(Products));
 }

 static readonly string[] ImageExts = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
 // Ảnh tải lên lưu vào wwwroot/uploads với tên ngẫu nhiên (không dùng tên file người dùng gửi) rồi ghi đường dẫn vào p.Image.
 // shortcut: ảnh cũ khi thay ảnh/xóa bánh vẫn nằm trong thư mục; dọn tay nếu thư mục phình to.
 // Dùng chung cho trang /admin và API của app WinForms. Trả về (đường dẫn /uploads/..., null) hoặc (null, lỗi).
 public static async Task<(string? Path, string? Error)> StoreImage(IWebHostEnvironment env, IFormFile file) {
  var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
  if (!ImageExts.Contains(ext)) return (null, "Chỉ nhận ảnh .jpg, .jpeg, .png, .webp, .gif.");
  if (file.Length == 0 || file.Length > 5 * 1024 * 1024) return (null, "Ảnh phải có dung lượng từ 1 byte đến 5 MB.");
  var dir = Path.Combine(env.WebRootPath, "uploads"); Directory.CreateDirectory(dir);
  var name = Guid.NewGuid().ToString("N") + ext;
  await using (var fs = System.IO.File.Create(Path.Combine(dir, name))) await file.CopyToAsync(fs);
  return ("/uploads/" + name, null);
 }

 async Task SaveUpload(Product p, IFormFile? file) {
  if (file is null || file.Length == 0) return;
  var (path, error) = await StoreImage(env, file);
  if (error is not null) { ModelState.AddModelError(nameof(Product.Image), error); return; }
  p.Image = path!; ModelState.Remove(nameof(Product.Image));
 }

 async Task LoadCategories() {
  ViewBag.Categories = await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
  ViewBag.Suppliers = await db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
  // Bánh xưởng làm mà tiệm chưa bán (xưởng đang hợp tác): nguồn chọn khi thêm bánh mới.
  ViewBag.Catalog = await (from x in db.SupplierPrices join s in db.Suppliers on x.SupplierId equals s.Id where x.ProductId == null && s.IsActive orderby s.Name, x.Name select new SupplierPriceRow(x.Id, s.Id, s.Name, true, null, null, x.Name, x.Unit, x.Price, false)).ToListAsync();
 }

 async Task CheckProduct(Product p, int selfId) {
  if (!string.IsNullOrEmpty(p.Category) && !await db.Categories.AnyAsync(c => c.Name == p.Category)) ModelState.AddModelError(nameof(Product.Category), "Hãy chọn một danh mục có sẵn.");
  if (!ProductStatus.Units.Contains(p.Unit)) ModelState.AddModelError(nameof(Product.Unit), "Hãy chọn đơn vị có sẵn.");
  p.Price = ProductStatus.PriceOf(p.CostPrice, p.Margin); ModelState.Remove(nameof(Product.Price));
  if (p.Price < 1000) ModelState.AddModelError(nameof(Product.CostPrice), "Giá bán tính ra phải từ 1.000 ₫: hãy nhập giá nhập.");
  p.Code = p.Code?.Trim().ToUpperInvariant();
  if (string.IsNullOrEmpty(p.Code)) { var n = await db.Products.MaxAsync(x => (int?)x.Id) ?? 0; do p.Code = $"SP{++n:000}"; while (await db.Products.AnyAsync(x => x.Code == p.Code && x.Id != selfId)); ModelState.Remove(nameof(Product.Code)); }
  else if (await db.Products.AnyAsync(x => x.Code == p.Code && x.Id != selfId)) ModelState.AddModelError(nameof(Product.Code), $"Mã “{p.Code}” đã được dùng cho bánh khác.");
 }
}

public record CategoryRow(int Id, string Name, int Count, bool Hidden);
