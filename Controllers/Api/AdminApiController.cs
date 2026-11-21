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
public record SupplierReq([Required, StringLength(80)] string Name, [Required(ErrorMessage = "Hãy nhập số điện thoại."), Phone(ErrorMessage = "Số điện thoại không hợp lệ."), StringLength(20)] string Phone, [StringLength(200)] string? Address);
public record SupplierPriceReq(int SupplierId, int? ProductId, [StringLength(80)] string? Name, [StringLength(20)] string? Unit, [Range(1000, 100000000)] decimal Price);
public record PaymentReq([StringLength(50)] string Method = "", [StringLength(200)] string? Note = null);
public record AdminOrderReq(int CustomerId, [Required, MinLength(1), MaxLength(50)] List<ItemReq> Items, [StringLength(500)] string? Note);
public record PurchaseReq([Required] string Supplier, DateTime? Date, [StringLength(500)] string? Note, [Required, MinLength(1)] List<PurchaseRow> Items);
public record DiscardLineReq(int ProductId, [Range(1, 100000)] int Quantity, [Required] string Reason, [StringLength(200)] string? Note);
public record DiscardSlipReq([Required, MinLength(1), MaxLength(50)] List<DiscardLineReq> Items, [StringLength(200)] string? Note, DateTime? Date);
public record StaffReq([Required, StringLength(80)] string FullName, [RegularExpression("^[a-z0-9._]{3,30}$")] string? Username, [Required(ErrorMessage = "Hãy nhập số điện thoại."), Phone(ErrorMessage = "Số điện thoại không hợp lệ."), StringLength(20)] string? Phone, [Required] string Role, [StringLength(64, MinimumLength = 6)] string? Password);

// Cho app quản trị (WinForms): cần token của nhân viên ("Quản trị" hoặc "Nhân viên"); quản lý nhân sự chỉ dành cho "Quản trị".
// Nghiệp vụ giống trang /admin và dùng chung các hàm BuildDashboard/BuildReport/StockRows của AdminController.
[ApiController, IgnoreAntiforgeryToken, Route("api/admin"), Authorize(AuthenticationSchemes = Auth.Api, Roles = Roles.Staff)]
public class AdminApiController(ShopDbContext db, IWebHostEnvironment env) : ControllerBase {
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

 // Bánh mới: chọn một bánh trong danh sách của xưởng (?priceId=, xưởng đang hợp tác, chưa gắn bánh nào). Giá nhập lấy đúng giá xưởng,
 // không nhận từ client; chỉ nhập tỉ lệ lợi nhuận, giá bán tự tính. Dòng giá đó được gắn với bánh vừa tạo.
 [HttpPost("products")]
 public async Task<IActionResult> CreateProduct(Product input, [FromQuery] int? priceId) {
  var sp = await db.SupplierPrices.FirstOrDefaultAsync(x => x.Id == priceId && x.ProductId == null);
  if (sp is null || !await db.Suppliers.AnyAsync(s => s.Id == sp.SupplierId && s.IsActive)) return Err("Hãy chọn một bánh trong danh sách của xưởng đang hợp tác (bánh chưa được bán ở tiệm).");
  var p = new Product(); Copy(input, p); (p.SupplierId, p.CostPrice) = (sp.SupplierId, sp.Price);
  if (await CheckProduct(p, 0) is { } e) return Err(e);
  using var tx = await db.Database.BeginTransactionAsync();
  db.Products.Add(p); await db.SaveChangesAsync();
  sp.ProductId = p.Id; await db.SaveChangesAsync(); await tx.CommitAsync();
  return CreatedAtAction(nameof(Product), new { id = p.Id }, p);
 }

 // Sửa bánh: không nhập tay giá nhập; đổi xưởng chính thì giá nhập lấy theo bảng giá của xưởng đó.
 [HttpPut("products/{id:int}")]
 public async Task<IActionResult> UpdateProduct(int id, Product input) {
  var p = await db.Products.FindAsync(id);
  if (p is null) return NotFound();
  Copy(input, p);
  if (input.SupplierId is int sid && sid != p.SupplierId) {
   if (await db.SupplierPrices.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == sid && x.ProductId == id) is not { } sp) return Err("Xưởng này chưa có giá cho bánh, hãy thêm vào bảng giá của xưởng trước.");
   (p.SupplierId, p.CostPrice) = (sid, sp.Price);
  }
  if (await CheckProduct(p, id) is { } e) return Err(e);
  await db.SaveChangesAsync();
  return Ok(p);
 }

 // ---- Danh sách bánh + bảng giá của xưởng: xem (mọi nhân viên); thêm / sửa / bỏ (chỉ quản trị vì làm đổi giá bán) ----
 // unlinked=true: chỉ bánh tiệm chưa bán (dùng cho "Thêm bánh mới").
 [HttpGet("supplier-prices")]
 public Task<List<SupplierPriceRow>> SupplierPrices(int? supplierId, int? productId, bool unlinked = false) =>
  (from x in db.SupplierPrices join s in db.Suppliers on x.SupplierId equals s.Id
   join p0 in db.Products on x.ProductId equals p0.Id into pj from p in pj.DefaultIfEmpty()
   where (supplierId == null || x.SupplierId == supplierId) && (productId == null || x.ProductId == productId) && (!unlinked || x.ProductId == null)
   orderby s.Name, x.ProductId == null, x.Name
   select new SupplierPriceRow(x.Id, s.Id, s.Name, s.IsActive, x.ProductId, p == null ? null : p.Code, p == null ? x.Name : p.Name, p == null ? x.Unit : p.Unit, x.Price, p != null && p.SupplierId == s.Id)).AsNoTracking().ToListAsync();

 // Thêm vào danh sách của xưởng: bánh tiệm đang bán (ProductId) hoặc bánh mới của xưởng (Name + Unit). Bánh chưa có xưởng chính thì xưởng này thành xưởng chính.
 [Authorize(Roles = StaffRole.Admin), HttpPost("supplier-prices")]
 public async Task<IActionResult> AddSupplierPrice(SupplierPriceReq r) {
  if (!await db.Suppliers.AnyAsync(s => s.Id == r.SupplierId)) return NotFound();
  var x = new SupplierPrice { SupplierId = r.SupplierId, Price = r.Price };
  if (r.ProductId is int pid) {
   var p = await db.Products.FindAsync(pid);
   if (p is null) return NotFound();
   if (await db.SupplierPrices.AnyAsync(y => y.SupplierId == r.SupplierId && y.ProductId == pid)) return Conflict(new { error = $"Xưởng đã có giá cho “{p.Name}”." });
   (x.ProductId, x.Name, x.Unit) = (pid, p.Name, p.Unit);
   if (p.SupplierId is null) { if (ProductStatus.PriceOf(r.Price, p.Margin) > 100000000) return Err("Giá bán tính ra vượt 100.000.000 ₫."); (p.SupplierId, p.CostPrice, p.Price) = (r.SupplierId, r.Price, ProductStatus.PriceOf(r.Price, p.Margin)); }
  } else {
   var name = Trim(r.Name);
   if (name is null) return Err("Hãy nhập tên bánh của xưởng.");
   if (!ProductStatus.Units.Contains(r.Unit ?? "")) return Err($"Đơn vị phải là: {string.Join(", ", ProductStatus.Units)}.");
   if (await db.SupplierPrices.AnyAsync(y => y.SupplierId == r.SupplierId && y.ProductId == null && y.Name == name)) return Conflict(new { error = $"Xưởng đã có bánh “{name}” trong danh sách." });
   (x.Name, x.Unit) = (name, r.Unit!);
  }
  db.SupplierPrices.Add(x); await db.SaveChangesAsync();
  return Ok(x);
 }

 // Sửa giá (và tên / đơn vị nếu bánh chưa bán ở tiệm). Là xưởng chính của bánh thì giá nhập và giá bán của bánh đổi theo.
 [Authorize(Roles = StaffRole.Admin), HttpPut("supplier-prices/{id:int}")]
 public async Task<IActionResult> UpdateSupplierPrice(int id, SupplierPriceReq r) {
  var x = await db.SupplierPrices.FindAsync(id);
  if (x is null) return NotFound();
  if (x.ProductId is int pid) {
   var p = await db.Products.FindAsync(pid);
   if (p is not null && p.SupplierId == x.SupplierId) {
    if (ProductStatus.PriceOf(r.Price, p.Margin) > 100000000) return Err("Giá bán tính ra vượt 100.000.000 ₫.");
    (p.CostPrice, p.Price) = (r.Price, ProductStatus.PriceOf(r.Price, p.Margin));
   }
  } else {
   var name = Trim(r.Name) ?? x.Name;
   if (r.Unit is not null && !ProductStatus.Units.Contains(r.Unit)) return Err($"Đơn vị phải là: {string.Join(", ", ProductStatus.Units)}.");
   if (name != x.Name && await db.SupplierPrices.AnyAsync(y => y.SupplierId == x.SupplierId && y.ProductId == null && y.Name == name)) return Conflict(new { error = $"Xưởng đã có bánh “{name}” trong danh sách." });
   (x.Name, x.Unit) = (name, r.Unit ?? x.Unit);
  }
  x.Price = r.Price; await db.SaveChangesAsync();
  return Ok(x);
 }

 [Authorize(Roles = StaffRole.Admin), HttpDelete("supplier-prices/{id:int}")]
 public async Task<IActionResult> DeleteSupplierPrice(int id) {
  var x = await db.SupplierPrices.FindAsync(id);
  if (x is null) return NotFound();
  if (x.ProductId is int pid && await db.Products.AnyAsync(p => p.Id == pid && p.SupplierId == x.SupplierId)) return Conflict(new { error = "Đây là xưởng chính của bánh: hãy đổi xưởng chính (sửa bánh) trước khi bỏ." });
  db.SupplierPrices.Remove(x); await db.SaveChangesAsync();
  return NoContent();
 }

 // Tải ảnh bánh lên wwwroot/uploads (multipart, trường "file"); app gán đường dẫn trả về vào Product.Image rồi lưu bánh.
 [HttpPost("uploads")]
 public async Task<IActionResult> Upload(IFormFile file) {
  var (path, error) = await AdminController.StoreImage(env, file);
  return error is null ? Ok(new { path }) : Err(error);
 }

 [HttpDelete("products/{id:int}")]
 public async Task<IActionResult> DeleteProduct(int id) {
  var p = await db.Products.FindAsync(id);
  if (p is null) return NotFound();
  // Bánh bị xóa: các dòng giá của xưởng trở lại thành "bánh tiệm chưa bán".
  db.Products.Remove(p); await db.SupplierPrices.Where(x => x.ProductId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ProductId, (int?)null)); await db.SaveChangesAsync();
  return NoContent();
 }

 // Ẩn / hiện bánh trên trang bán bánh (bánh ẩn không đặt được nhưng vẫn giữ trong đơn, phiếu cũ).
 [HttpPost("products/{id:int}/hide")]
 public async Task<IActionResult> ToggleProductHidden(int id) {
  var p = await db.Products.FindAsync(id);
  if (p is null) return NotFound();
  p.IsHidden = !p.IsHidden; await db.SaveChangesAsync();
  return Ok(p);
 }

 // Chỉ chép các trường được sửa (chống overposting Id); giá bán do CheckProduct tính từ giá nhập + tỉ lệ lợi nhuận.
 // Giá nhập / xưởng chính không chép ở đây (đi theo bảng giá xưởng).
 static void Copy(Product s, Product d) => (d.Code, d.Name, d.Description, d.Image, d.Category, d.Unit, d.Margin, d.IsNew, d.IsBestSeller) = (s.Code, s.Name, s.Description, s.Image, s.Category, s.Unit, s.Margin, s.IsNew, s.IsBestSeller);

 // Cùng quy tắc với AdminController.CheckProduct: danh mục/đơn vị có sẵn, mã bánh duy nhất, bỏ trống mã thì tự sinh SPxxx.
 async Task<string?> CheckProduct(Product p, int selfId) {
  if (!await db.Categories.AnyAsync(c => c.Name == p.Category)) return "Hãy chọn một danh mục có sẵn.";
  if (!ProductStatus.Units.Contains(p.Unit)) return $"Đơn vị phải là: {string.Join(", ", ProductStatus.Units)}.";
  p.Price = ProductStatus.PriceOf(p.CostPrice, p.Margin);
  if (p.Price is < 1000 or > 100000000) return "Giá bán tính ra phải từ 1.000 ₫ đến 100.000.000 ₫: kiểm tra giá nhập và tỉ lệ lợi nhuận.";
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

 [Authorize(Roles = StaffRole.Admin), HttpPost("categories")]
 public async Task<IActionResult> CreateCategory(CategoryReq r) {
  var name = r.Name.Trim();
  if (name.Length == 0) return Err("Tên danh mục không hợp lệ.");
  if (await db.Categories.AnyAsync(c => c.Name == name)) return Conflict(new { error = $"Danh mục “{name}” đã tồn tại." });
  var c = new Category { Name = name, IsHidden = r.IsHidden };
  db.Categories.Add(c); await db.SaveChangesAsync();
  return Ok(c);
 }

 // Đổi tên thì cập nhật luôn chuỗi Category của các sản phẩm (không có khóa ngoại).
 [Authorize(Roles = StaffRole.Admin), HttpPut("categories/{id:int}")]
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

 [Authorize(Roles = StaffRole.Admin), HttpDelete("categories/{id:int}")]
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

 // Sửa người nhận / địa chỉ / ghi chú khi đơn chưa hoàn thành hoặc hủy.
 [HttpPut("orders/{id:int}/info")]
 public async Task<IActionResult> UpdateOrderInfo(int id, OrderInfoInput r) {
  var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
  if (o is null) return NotFound();
  if (o.Status is OrderStatus.Done or OrderStatus.Cancelled) return Conflict(new { error = $"Đơn #{o.Id} đã “{o.Status}”, không sửa thông tin được nữa." });
  r.ApplyTo(o); await db.SaveChangesAsync();
  return Ok(o);
 }

 // ---- Thanh toán: Chưa thanh toán → (ghi nhận) Chờ xác nhận → (xác nhận) Đã thanh toán → (hoàn tiền) Đã hoàn tiền ----
 [HttpPost("orders/{id:int}/payment/record")]
 public Task<IActionResult> RecordPayment(int id, PaymentReq r) => Pay(id, PaymentStatus.Unpaid, o => {
  if (o.Status == OrderStatus.Cancelled) return "Đơn đã hủy, không ghi nhận thanh toán.";
  if (!PaymentStatus.Methods.Contains(r.Method)) return $"Hình thức phải là: {string.Join(", ", PaymentStatus.Methods)}.";
  (o.PaymentStatus, o.PaymentMethod, o.PaymentNote, o.PaymentRecordedBy, o.PaymentRecordedAt) = (PaymentStatus.Pending, r.Method, Trim(r.Note), Me, DateTime.Now); return null;
 });
 [HttpPost("orders/{id:int}/payment/confirm")]
 public Task<IActionResult> ConfirmPayment(int id) => Pay(id, PaymentStatus.Pending, o => { (o.PaymentStatus, o.PaymentConfirmedBy, o.PaymentConfirmedAt) = (PaymentStatus.Paid, Me, DateTime.Now); return null; });
 [HttpPost("orders/{id:int}/payment/refund")]
 public Task<IActionResult> RefundPayment(int id, PaymentReq r) => Pay(id, PaymentStatus.Paid, o => {
  if (Trim(r.Note) is not { } note) return "Hãy nhập lý do hoàn tiền.";
  (o.PaymentStatus, o.PaymentNote, o.RefundedBy, o.RefundedAt) = (PaymentStatus.Refunded, note, Me, DateTime.Now); return null;
 });

 async Task<IActionResult> Pay(int id, string from, Func<Order, string?> apply) {
  var o = await db.Orders.Include(x => x.Items).Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
  if (o is null) return NotFound();
  if (o.PaymentStatus != from) return Conflict(new { error = $"Thanh toán của đơn #{o.Id} đang “{o.PaymentStatus}”." });
  if (apply(o) is { } e) return Err(e);
  await db.SaveChangesAsync();
  return Ok(o);
 }

 // ---- Kho: nhà cung cấp, phiếu nhập, tồn kho, phiếu hủy, kiểm kê cuối ngày ----
 [HttpGet("suppliers")] public Task<List<Supplier>> Suppliers() => db.Suppliers.AsNoTracking().OrderByDescending(s => s.IsActive).ThenBy(s => s.Name).ToListAsync();

 [HttpPost("suppliers")]
 public async Task<IActionResult> CreateSupplier(SupplierReq r) {
  var s = new Supplier();
  if (await SetSupplier(s, r) is { } e) return Err(e);
  db.Suppliers.Add(s); await db.SaveChangesAsync();
  return Ok(s);
 }

 // Đổi tên thì cập nhật luôn tên trên các phiếu nhập cũ (phiếu lưu tên, không có khóa ngoại).
 [HttpPut("suppliers/{id:int}")]
 public async Task<IActionResult> UpdateSupplier(int id, SupplierReq r) {
  var s = await db.Suppliers.FindAsync(id);
  if (s is null) return NotFound();
  var old = s.Name;
  if (await SetSupplier(s, r) is { } e) return Err(e);
  using var tx = await db.Database.BeginTransactionAsync();
  if (old != s.Name) await db.Purchases.Where(p => p.Supplier == old).ExecuteUpdateAsync(x => x.SetProperty(p => p.Supplier, s.Name));
  await db.SaveChangesAsync(); await tx.CommitAsync();
  return Ok(s);
 }

 // Ngừng / tiếp tục hợp tác.
 [HttpPost("suppliers/{id:int}/toggle")]
 public async Task<IActionResult> ToggleSupplier(int id) {
  var s = await db.Suppliers.FindAsync(id);
  if (s is null) return NotFound();
  s.IsActive = !s.IsActive; await db.SaveChangesAsync();
  return Ok(s);
 }

 async Task<string?> SetSupplier(Supplier s, SupplierReq r) {
  var name = r.Name.Trim();
  if (name.Length == 0) return "Hãy nhập tên nhà cung cấp.";
  if (await db.Suppliers.AnyAsync(x => x.Name == name && x.Id != s.Id)) return $"Nhà cung cấp “{name}” đã tồn tại.";
  (s.Name, s.Phone, s.Address) = (name, r.Phone.Trim(), Trim(r.Address));
  return null;
 }

 [HttpGet("stock")] public Task<List<StockRow>> Stock() => AdminController.StockRows(db);
 [HttpGet("daily")] public Task<List<DailyRow>> Daily(DateTime? date) => AdminController.DailyRows(db, (date ?? DateTime.Today).Date);

 [HttpGet("purchases")] public Task<List<Purchase>> Purchases() => db.Purchases.AsNoTracking().Include(p => p.Items).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToListAsync();
 [HttpGet("purchases/{id:int}")] public async Task<IActionResult> Purchase(int id) => await db.Purchases.AsNoTracking().Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id) is { } p ? Ok(p) : NotFound();

 // Phiếu mới ở trạng thái "Chờ nhập kho"; chỉ khi xác nhận (receive) số bánh mới được cộng vào tồn.
 [HttpPost("purchases")]
 public async Task<IActionResult> CreatePurchase(PurchaseReq r) {
  var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id);
  var p = new Purchase { Supplier = r.Supplier.Trim(), CreatedAt = (r.Date ?? DateTime.Today).Date, CreatedBy = Me, Note = Trim(r.Note) };
  foreach (var x in r.Items) if (products.TryGetValue(x.ProductId, out var pr) && x.Quantity > 0 && x.UnitCost >= 0) p.Items.Add(new PurchaseItem { ProductId = pr.Id, ItemName = pr.Name, Unit = pr.Unit, Quantity = Math.Min(x.Quantity, 100000), UnitCost = x.UnitCost });
  var err = !await db.Suppliers.AnyAsync(s => s.Name == p.Supplier && s.IsActive) ? "Hãy chọn nhà cung cấp đang hợp tác." : p.CreatedAt > DateTime.Today ? "Ngày lập phiếu không được ở tương lai." : p.Items.Count == 0 ? "Hãy chọn ít nhất một loại bánh với số lượng > 0." : await AdminController.PriceFromSupplier(db, p);
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

 // Lập một phiếu hủy gồm nhiều loại bánh. Tổng số hủy của mỗi bánh không vượt số đang tồn.
 [HttpPost("discards")]
 public async Task<IActionResult> CreateDiscard(DiscardSlipReq r) {
  var stock = (await AdminController.StockRows(db)).ToDictionary(s => s.Product.Id);
  var day = (r.Date ?? DateTime.Today).Date;
  var err = day > DateTime.Today ? "Ngày hủy không được ở tương lai." : r.Items.FirstOrDefault(i => !stock.ContainsKey(i.ProductId)) is not null ? "Không tìm thấy loại bánh."
   : r.Items.FirstOrDefault(i => !DiscardReasons.All.Contains(i.Reason)) is not null ? $"Lý do phải là: {string.Join(", ", DiscardReasons.All)}."
   : r.Items.GroupBy(i => i.ProductId).Select(g => (S: stock[g.Key], Q: g.Sum(i => i.Quantity))).FirstOrDefault(x => x.Q > x.S.Left) is { S: { } s } ? $"Chỉ còn {s.Left} {s.Product.Unit} “{s.Product.Name}” trong kho." : null;
  if (err is not null) return Err(err);
  // Nhân viên lập → "Chờ duyệt"; quản trị lập → duyệt luôn.
  var admin = User.IsInRole(StaffRole.Admin); var at = day == DateTime.Today ? DateTime.Now : day.AddHours(12); var note = Trim(r.Note);
  var lines = r.Items.Select(i => { var p = stock[i.ProductId].Product; var n = Trim(i.Note) ?? note;
   return new Discard { ProductId = p.Id, ProductName = p.Name, Unit = p.Unit, Quantity = i.Quantity, UnitPrice = p.Price, Reason = n is null ? i.Reason : $"{i.Reason}: {n}", CreatedAt = at, CreatedBy = Me,
    Status = admin ? DiscardStatus.Approved : DiscardStatus.Pending, ReviewedBy = admin ? Me : null, ReviewedAt = admin ? DateTime.Now : null }; }).ToList();
  // Số phiếu = Id dòng đầu tiên (không trùng, không cần bộ đếm riêng).
  using var tx = await db.Database.BeginTransactionAsync();
  db.Discards.AddRange(lines); await db.SaveChangesAsync();
  foreach (var d in lines) d.SlipId = lines[0].Id;
  await db.SaveChangesAsync(); await tx.CommitAsync();
  return Ok(lines);
 }

 // Duyệt / từ chối cả phiếu: chỉ quản trị. Lúc duyệt kiểm tra lại tồn kho từng bánh (có thể đã bán bớt từ lúc lập phiếu).
 [Authorize(Roles = StaffRole.Admin), HttpPost("discards/slips/{slip:int}/approve")]
 public Task<IActionResult> ApproveDiscard(int slip) => ReviewSlip(slip, DiscardStatus.Approved);
 [Authorize(Roles = StaffRole.Admin), HttpPost("discards/slips/{slip:int}/reject")]
 public Task<IActionResult> RejectDiscard(int slip) => ReviewSlip(slip, DiscardStatus.Rejected);

 async Task<IActionResult> ReviewSlip(int slip, string status) {
  var lines = await db.Discards.Where(d => d.SlipId == slip).ToListAsync();
  if (lines.Count == 0) return NotFound();
  if (lines.Any(d => d.Status != DiscardStatus.Pending)) return Conflict(new { error = $"Phiếu hủy #{slip} đã “{lines[0].Status}”." });
  if (status == DiscardStatus.Approved) {
   var stock = (await AdminController.StockRows(db)).ToDictionary(s => s.Product.Id);
   foreach (var g in lines.GroupBy(d => d.ProductId)) { var left = stock.GetValueOrDefault(g.Key)?.Left ?? 0; if (g.Sum(d => d.Quantity) > left) return Err($"Chỉ còn {left} {g.First().Unit} “{g.First().ProductName}” trong kho, không duyệt hủy {g.Sum(d => d.Quantity)} được."); }
  }
  foreach (var d in lines) (d.Status, d.ReviewedBy, d.ReviewedAt) = (status, Me, DateTime.Now);
  await db.SaveChangesAsync();
  return Ok(lines);
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
