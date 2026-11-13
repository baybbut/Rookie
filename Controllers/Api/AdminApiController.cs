using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers.Api;
public record CategoryReq([Required, StringLength(40)] string Name, bool IsHidden);
public record CustomerReq([Required, StringLength(80)] string FullName, [Required, EmailAddress, StringLength(120)] string Email, [Required, Phone, StringLength(20)] string Phone, [StringLength(200)] string? Address, [StringLength(64, MinimumLength = 6)] string? Password);
public record StatusReq([Required] string Status);
public record AdminOrderReq(int CustomerId, [Required, MinLength(1), MaxLength(50)] List<ItemReq> Items, [StringLength(500)] string? Note);
public record PurchaseReq([Required] string Supplier, DateTime? Date, [StringLength(500)] string? Note, [Required, MinLength(1)] List<PurchaseRow> Items);
public record DiscardReq(int ProductId, [Range(1, 100000)] int Quantity, [Required] string Reason, [StringLength(200)] string? Note, DateTime? Date);
public record StaffReq([Required, StringLength(80)] string FullName, [RegularExpression("^[a-z0-9._]{3,30}$")] string? Username, [Phone, StringLength(20)] string? Phone, [Required] string Role, [StringLength(64, MinimumLength = 6)] string? Password);

// Cho app quản trị (WinForms): cần token của nhân viên ("Quản trị" hoặc "Nhân viên"); quản lý nhân sự chỉ dành cho "Quản trị".
// Nghiệp vụ giống trang /admin và dùng chung các hàm BuildDashboard/BuildReport/StockRows của AdminController.
[ApiController, IgnoreAntiforgeryToken, Route("api/admin"), Authorize(AuthenticationSchemes = Auth.Api, Roles = Roles.Staff)]
public class AdminApiController(ShopDbContext db) : ControllerBase {
 string Me => User.Identity?.Name ?? "—";
 int MyId => int.Parse(User.FindFirstValue("sub")!);
 static BadRequestObjectResult Err(string e) => new(new { error = e });
 static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

 [HttpGet("dashboard")] public Task<DashboardVm> Dashboard() => AdminController.BuildDashboard(db);
 // type = range (from, to) | month (month=yyyy-MM) | year (year).
 [HttpGet("reports")] public Task<ReportVm> Reports(string? type, DateTime? from, DateTime? to, string? month, int? year) => AdminController.BuildReport(db, type, from, to, month, year);

 // ---- Sản phẩm ----
 [HttpGet("products")] public Task<List<Product>> Products() => db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
 [HttpGet("products/{id:int}")] public async Task<IActionResult> Product(int id) => await db.Products.FindAsync(id) is { } p ? Ok(p) : NotFound();

 [HttpPost("products")]
 public async Task<IActionResult> CreateProduct(Product input) {
  var p = new Product(); Copy(input, p);
  if (await CheckProduct(p, 0) is { } e) return Err(e);
  db.Products.Add(p); await db.SaveChangesAsync();
  return CreatedAtAction(nameof(Product), new { id = p.Id }, p);
 }

 [HttpPut("products/{id:int}")]
 public async Task<IActionResult> UpdateProduct(int id, Product input) {
  var p = await db.Products.FindAsync(id);
  if (p is null) return NotFound();
  Copy(input, p);
  if (await CheckProduct(p, id) is { } e) return Err(e);
  await db.SaveChangesAsync();
  return Ok(p);
 }

 [HttpDelete("products/{id:int}")]
 public async Task<IActionResult> DeleteProduct(int id) {
  var p = await db.Products.FindAsync(id);
  if (p is null) return NotFound();
  db.Products.Remove(p); await db.SaveChangesAsync();
  return NoContent();
 }

 // Chỉ chép các trường được sửa (chống overposting Id).
 static void Copy(Product s, Product d) => (d.Code, d.Name, d.Description, d.Image, d.Category, d.Unit, d.Price, d.IsNew, d.IsBestSeller) = (s.Code, s.Name, s.Description, s.Image, s.Category, s.Unit, s.Price, s.IsNew, s.IsBestSeller);

 // Cùng quy tắc với AdminController.CheckProduct: danh mục/đơn vị có sẵn, mã bánh duy nhất, bỏ trống mã thì tự sinh SPxxx.
 async Task<string?> CheckProduct(Product p, int selfId) {
  if (!await db.Categories.AnyAsync(c => c.Name == p.Category)) return "Hãy chọn một danh mục có sẵn.";
  if (!ProductStatus.Units.Contains(p.Unit)) return $"Đơn vị phải là: {string.Join(", ", ProductStatus.Units)}.";
  p.Code = p.Code?.Trim().ToUpperInvariant();
  if (string.IsNullOrEmpty(p.Code)) { var n = await db.Products.MaxAsync(x => (int?)x.Id) ?? 0; do p.Code = $"SP{++n:000}"; while (await db.Products.AnyAsync(x => x.Code == p.Code && x.Id != selfId)); }
  else if (await db.Products.AnyAsync(x => x.Code == p.Code && x.Id != selfId)) return $"Mã “{p.Code}” đã được dùng cho bánh khác.";
  return null;
 }

 // ---- Danh mục ----
 [HttpGet("categories")]
 public async Task<List<CategoryRow>> Categories() {
  var counts = await db.Products.AsNoTracking().GroupBy(p => p.Category).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
  return (await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync()).Select(c => new CategoryRow(c.Id, c.Name, counts.GetValueOrDefault(c.Name), c.IsHidden)).ToList();
 }

 [HttpPost("categories")]
 public async Task<IActionResult> CreateCategory(CategoryReq r) {
  var name = r.Name.Trim();
  if (name.Length == 0) return Err("Tên danh mục không hợp lệ.");
  if (await db.Categories.AnyAsync(c => c.Name == name)) return Conflict(new { error = $"Danh mục “{name}” đã tồn tại." });
  var c = new Category { Name = name, IsHidden = r.IsHidden };
  db.Categories.Add(c); await db.SaveChangesAsync();
  return Ok(c);
 }

 // Đổi tên thì cập nhật luôn chuỗi Category của các sản phẩm (không có khóa ngoại).
 [HttpPut("categories/{id:int}")]
 public async Task<IActionResult> UpdateCategory(int id, CategoryReq r) {
  var c = await db.Categories.FindAsync(id);
  if (c is null) return NotFound();
  var name = r.Name.Trim();
  if (name.Length == 0) return Err("Tên danh mục không hợp lệ.");
  if (name != c.Name && await db.Categories.AnyAsync(x => x.Name == name)) return Conflict(new { error = $"Danh mục “{name}” đã tồn tại." });
  using var tx = await db.Database.BeginTransactionAsync();
  if (name != c.Name) { var old = c.Name; c.Name = name; await db.Products.Where(p => p.Category == old).ExecuteUpdateAsync(s => s.SetProperty(p => p.Category, name)); }
  c.IsHidden = r.IsHidden;
  await db.SaveChangesAsync(); await tx.CommitAsync();
  return Ok(c);
 }

 [HttpDelete("categories/{id:int}")]
 public async Task<IActionResult> DeleteCategory(int id) {
  var c = await db.Categories.FindAsync(id);
  if (c is null) return NotFound();
  if (await db.Products.AnyAsync(p => p.Category == c.Name)) return Conflict(new { error = $"Không xóa được “{c.Name}” vì vẫn còn bánh trong danh mục này." });
  db.Categories.Remove(c); await db.SaveChangesAsync();
  return NoContent();
 }

 // ---- Khách hàng ----
 [HttpGet("customers")]
 public async Task<List<Customer>> Customers(string? q) {
  var list = await db.Customers.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
  var t = q?.Trim();
  return string.IsNullOrEmpty(t) ? list : list.Where(c => $"{c.FullName} {c.Email} {c.Phone}".Contains(t, StringComparison.CurrentCultureIgnoreCase)).ToList();
 }

 [HttpGet("customers/{id:int}")] public async Task<IActionResult> Customer(int id) => await db.Customers.FindAsync(id) is { } c ? Ok(c) : NotFound();

 [HttpPost("customers")]
 public async Task<IActionResult> CreateCustomer(CustomerReq r) {
  if (string.IsNullOrEmpty(r.Password)) return Err("Hãy nhập mật khẩu cho khách hàng.");
  var c = new Customer();
  if (await SetCustomer(c, r, 0) is { } e) return Err(e);
  db.Customers.Add(c); await db.SaveChangesAsync();
  return CreatedAtAction(nameof(Customer), new { id = c.Id }, c);
 }

 [HttpPut("customers/{id:int}")]
 public async Task<IActionResult> UpdateCustomer(int id, CustomerReq r) {
  var c = await db.Customers.FindAsync(id);
  if (c is null) return NotFound();
  if (await SetCustomer(c, r, id) is { } e) return Err(e);
  await db.SaveChangesAsync();
  return Ok(c);
 }

 async Task<string?> SetCustomer(Customer c, CustomerReq r, int selfId) {
  var email = r.Email.Trim().ToLowerInvariant();
  if (await db.Customers.AnyAsync(x => x.Email.ToLower() == email && x.Id != selfId)) return "Email này đã có khách hàng khác dùng.";
  (c.FullName, c.Email, c.Phone, c.Address) = (r.FullName.Trim(), email, r.Phone.Trim(), Trim(r.Address));
  if (!string.IsNullOrEmpty(r.Password)) c.PasswordHash = Pw.Hash(r.Password);
  return null;
 }

 [HttpPost("customers/{id:int}/lock")]
 public async Task<IActionResult> ToggleCustomerLock(int id) {
  var c = await db.Customers.FindAsync(id);
  if (c is null) return NotFound();
  c.IsLocked = !c.IsLocked; await db.SaveChangesAsync();
  return Ok(c);
 }

 // ---- Đơn hàng ----
 [HttpGet("order-statuses")] public string[] Statuses() => OrderStatus.All;

 [HttpGet("orders")]
 public Task<List<Order>> Orders(string? status, int? customerId, DateTime? from, DateTime? to) =>
  db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer)
   .Where(o => (status == null || o.Status == status) && (customerId == null || o.CustomerId == customerId) && (from == null || o.CreatedAt >= from.Value.Date) && (to == null || o.CreatedAt < to.Value.Date.AddDays(1)))
   .OrderByDescending(o => o.CreatedAt).ToListAsync();

 [HttpGet("orders/{id:int}")]
 public async Task<IActionResult> Order(int id) => await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == id) is { } o ? Ok(o) : NotFound();

 // Quy trình: Mới → Đã xác nhận → Đang làm → Đang giao → Hoàn thành; hủy được tới trước khi hoàn thành (OrderStatus.Allowed).
 [HttpPut("orders/{id:int}/status")]
 public async Task<IActionResult> SetOrderStatus(int id, StatusReq r) {
  var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
  if (o is null) return NotFound();
  if (!OrderStatus.Allowed(o.Status, r.Status)) return Conflict(new { error = $"Không thể chuyển đơn #{o.Id} từ “{o.Status}” sang “{r.Status}”." });
  o.Status = r.Status;
  if (r.Status == OrderStatus.Confirmed) (o.ConfirmedBy, o.ConfirmedAt) = (Me, DateTime.Now);
  await db.SaveChangesAsync();
  return Ok(o);
 }

 [HttpPost("orders")]
 public async Task<IActionResult> CreateOrder(AdminOrderReq r) {
  var (o, err) = await ShopApiController.BuildOrder(db, await db.Customers.FindAsync(r.CustomerId), r.Items, r.Note, db.Products.AsNoTracking());
  if (o is null) return Err(err!);
  o.CreatedBy = Me;
  db.Orders.Add(o); await db.SaveChangesAsync();
  return CreatedAtAction(nameof(Order), new { id = o.Id }, o);
 }

 // ---- Kho: xưởng cung cấp, phiếu nhập, tồn kho, bánh hủy ----
 [HttpGet("suppliers")] public Task<List<Supplier>> Suppliers() => db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
 [HttpGet("stock")] public Task<List<StockRow>> Stock() => AdminController.StockRows(db);

 [HttpGet("purchases")] public Task<List<Purchase>> Purchases() => db.Purchases.AsNoTracking().Include(p => p.Items).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToListAsync();
 [HttpGet("purchases/{id:int}")] public async Task<IActionResult> Purchase(int id) => await db.Purchases.AsNoTracking().Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id) is { } p ? Ok(p) : NotFound();

 // Phiếu mới ở trạng thái "Chờ nhập kho"; chỉ khi xác nhận (receive) số bánh mới được cộng vào tồn.
 [HttpPost("purchases")]
 public async Task<IActionResult> CreatePurchase(PurchaseReq r) {
  var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id);
  var p = new Purchase { Supplier = r.Supplier.Trim(), CreatedAt = (r.Date ?? DateTime.Today).Date, CreatedBy = Me, Note = Trim(r.Note) };
  foreach (var x in r.Items) if (products.TryGetValue(x.ProductId, out var pr) && x.Quantity > 0 && x.UnitCost >= 0) p.Items.Add(new PurchaseItem { ProductId = pr.Id, ItemName = pr.Name, Unit = pr.Unit, Quantity = Math.Min(x.Quantity, 100000), UnitCost = x.UnitCost });
  var err = !await db.Suppliers.AnyAsync(s => s.Name == p.Supplier) ? "Hãy chọn xưởng bánh cung cấp có sẵn." : p.CreatedAt > DateTime.Today ? "Ngày lập phiếu không được ở tương lai." : p.Items.Count == 0 ? "Hãy chọn ít nhất một loại bánh với số lượng > 0." : null;
  if (err is not null) return Err(err);
  db.Purchases.Add(p); await db.SaveChangesAsync();
  return CreatedAtAction(nameof(Purchase), new { id = p.Id }, p);
 }

 [HttpPost("purchases/{id:int}/receive")] public Task<IActionResult> ReceivePurchase(int id) => SetPurchase(id, PurchaseStatus.Received);
 [HttpPost("purchases/{id:int}/cancel")] public Task<IActionResult> CancelPurchase(int id) => SetPurchase(id, PurchaseStatus.Cancelled);

 async Task<IActionResult> SetPurchase(int id, string status) {
  var p = await db.Purchases.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
  if (p is null) return NotFound();
  if (p.Status != PurchaseStatus.Pending) return Conflict(new { error = $"Phiếu #{p.Id} đang ở trạng thái “{p.Status}”." });
  p.Status = status;
  if (status == PurchaseStatus.Received) (p.ReceivedAt, p.ReceivedBy) = (DateTime.Now, Me);
  await db.SaveChangesAsync();
  return Ok(p);
 }

 [HttpGet("discard-reasons")] public string[] DiscardReasonList() => DiscardReasons.All;
 [HttpGet("discards")] public Task<List<Discard>> Discards() => db.Discards.AsNoTracking().OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id).ToListAsync();

 // Không cho hủy nhiều hơn số đang tồn.
 [HttpPost("discards")]
 public async Task<IActionResult> CreateDiscard(DiscardReq r) {
  var stock = (await AdminController.StockRows(db)).FirstOrDefault(s => s.Product.Id == r.ProductId);
  var day = (r.Date ?? DateTime.Today).Date;
  var err = stock is null ? "Không tìm thấy loại bánh." : r.Quantity > stock.Left ? $"Chỉ còn {stock.Left} {stock.Product.Unit} “{stock.Product.Name}” trong kho." : !DiscardReasons.All.Contains(r.Reason) ? $"Lý do phải là: {string.Join(", ", DiscardReasons.All)}." : day > DateTime.Today ? "Ngày hủy không được ở tương lai." : null;
  if (err is not null) return Err(err);
  var note = Trim(r.Note);
  var d = new Discard { ProductId = stock!.Product.Id, ProductName = stock.Product.Name, Unit = stock.Product.Unit, Quantity = r.Quantity, UnitPrice = stock.Product.Price, Reason = note is null ? r.Reason : $"{r.Reason}: {note}", CreatedAt = day == DateTime.Today ? DateTime.Now : day.AddHours(12), CreatedBy = Me };
  db.Discards.Add(d); await db.SaveChangesAsync();
  return Ok(d);
 }

 // ---- Nhân sự: chỉ "Quản trị"; luôn phải còn ít nhất một quản trị đang hoạt động ----
 Task<bool> OtherActiveAdmin(int id) => db.Staffs.AnyAsync(s => s.Id != id && s.Role == StaffRole.Admin && !s.IsLocked);

 [Authorize(Roles = StaffRole.Admin), HttpGet("staff")] public Task<List<Staff>> StaffList() => db.Staffs.AsNoTracking().OrderBy(s => s.Id).ToListAsync();

 [Authorize(Roles = StaffRole.Admin), HttpPost("staff")]
 public async Task<IActionResult> CreateStaff(StaffReq r) {
  var u = r.Username?.Trim().ToLowerInvariant() ?? "";
  var err = u.Length == 0 ? "Hãy nhập tên đăng nhập." : string.IsNullOrEmpty(r.Password) ? "Hãy nhập mật khẩu." : !StaffRole.All.Contains(r.Role) ? $"Vai trò phải là: {string.Join(", ", StaffRole.All)}." : await db.Staffs.AnyAsync(x => x.Username == u) ? "Tên đăng nhập đã tồn tại." : null;
  if (err is not null) return Err(err);
  var s = new Staff { FullName = r.FullName.Trim(), Username = u, Phone = Trim(r.Phone), Role = r.Role, PasswordHash = Pw.Hash(r.Password!) };
  db.Staffs.Add(s); await db.SaveChangesAsync();
  return Ok(s);
 }

 // Không đổi được tên đăng nhập; không hạ quyền người quản trị cuối cùng.
 [Authorize(Roles = StaffRole.Admin), HttpPut("staff/{id:int}")]
 public async Task<IActionResult> UpdateStaff(int id, StaffReq r) {
  var s = await db.Staffs.FindAsync(id);
  if (s is null) return NotFound();
  if (!StaffRole.All.Contains(r.Role)) return Err($"Vai trò phải là: {string.Join(", ", StaffRole.All)}.");
  if (s.Role == StaffRole.Admin && r.Role != StaffRole.Admin && !await OtherActiveAdmin(id)) return Conflict(new { error = "Phải còn ít nhất một người quản trị đang hoạt động." });
  (s.FullName, s.Phone, s.Role) = (r.FullName.Trim(), Trim(r.Phone), r.Role);
  if (!string.IsNullOrEmpty(r.Password)) s.PasswordHash = Pw.Hash(r.Password);
  await db.SaveChangesAsync();
  return Ok(s);
 }

 [Authorize(Roles = StaffRole.Admin), HttpPost("staff/{id:int}/lock")]
 public async Task<IActionResult> ToggleStaffLock(int id) {
  var s = await db.Staffs.FindAsync(id);
  if (s is null) return NotFound();
  if (!s.IsLocked && id == MyId) return Conflict(new { error = "Bạn không thể tự khóa tài khoản đang đăng nhập." });
  if (!s.IsLocked && s.Role == StaffRole.Admin && !await OtherActiveAdmin(id)) return Conflict(new { error = "Phải còn ít nhất một người quản trị đang hoạt động." });
  s.IsLocked = !s.IsLocked; await db.SaveChangesAsync();
  return Ok(s);
 }
}
