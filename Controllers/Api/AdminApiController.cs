using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers.Api;
public record CategoryReq([Required, StringLength(40)] string Name, bool IsHidden);
public record StatusReq([Required] string Status);
public record AdminOrderReq(int CustomerId, [Required, MinLength(1), MaxLength(50)] List<CartLine> Items, [StringLength(500)] string? Note);
public record PurchaseReq([Required, StringLength(80)] string Supplier, [StringLength(500)] string? Note, [Required, MinLength(1)] List<PurchaseRow> Items);

// Cho app quản trị (WinForms): mọi endpoint yêu cầu token role Admin. Nghiệp vụ giống trang /admin.
[ApiController, Route("api/admin"), Authorize(AuthenticationSchemes = Roles.Scheme, Roles = Roles.Admin)]
public class AdminApiController(ShopDbContext db) : ControllerBase {
 [HttpGet("dashboard")] public Task<DashboardVm> Dashboard() => AdminController.BuildDashboard(db);
 [HttpGet("reports")] public Task<ReportVm> Reports(int days = 30) => AdminController.BuildReport(db, days);

 // ---- Sản phẩm ----
 [HttpGet("products")] public Task<List<Product>> Products() => db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
 [HttpGet("products/{id:int}")] public async Task<IActionResult> Product(int id) => await db.Products.FindAsync(id) is { } p ? Ok(p) : NotFound();

 [HttpPost("products")]
 public async Task<IActionResult> CreateProduct(Product input) {
  if (!await db.Categories.AnyAsync(c => c.Name == input.Category)) return BadRequest(new { error = "Hãy chọn một danh mục có sẵn." });
  var p = new Product(); Copy(input, p);
  db.Products.Add(p); await db.SaveChangesAsync();
  return CreatedAtAction(nameof(Product), new { id = p.Id }, p);
 }

 [HttpPut("products/{id:int}")]
 public async Task<IActionResult> UpdateProduct(int id, Product input) {
  var p = await db.Products.FindAsync(id);
  if (p is null) return NotFound();
  if (!await db.Categories.AnyAsync(c => c.Name == input.Category)) return BadRequest(new { error = "Hãy chọn một danh mục có sẵn." });
  Copy(input, p); await db.SaveChangesAsync();
  return Ok(p);
 }

 [HttpDelete("products/{id:int}")]
 public async Task<IActionResult> DeleteProduct(int id) {
  var p = await db.Products.FindAsync(id);
  if (p is null) return NotFound();
  db.Products.Remove(p); await db.SaveChangesAsync();
  return NoContent();
 }

 // Chỉ chép các trường được phép sửa (chống overposting Id).
 static void Copy(Product s, Product d) => (d.Name, d.Description, d.Image, d.Category, d.Price, d.IsNew, d.IsBestSeller) = (s.Name, s.Description, s.Image, s.Category, s.Price, s.IsNew, s.IsBestSeller);

 // ---- Danh mục ----
 [HttpGet("categories")]
 public async Task<List<CategoryRow>> Categories() {
  var counts = await db.Products.AsNoTracking().GroupBy(p => p.Category).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
  return (await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync()).Select(c => new CategoryRow(c.Id, c.Name, counts.GetValueOrDefault(c.Name), c.IsHidden)).ToList();
 }

 [HttpPost("categories")]
 public async Task<IActionResult> CreateCategory(CategoryReq r) {
  var name = r.Name.Trim();
  if (name.Length == 0) return BadRequest(new { error = "Tên danh mục không hợp lệ." });
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
  if (name.Length == 0) return BadRequest(new { error = "Tên danh mục không hợp lệ." });
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
  var term = q?.Trim();
  return string.IsNullOrEmpty(term) ? list : list.Where(c => $"{c.FullName} {c.Email} {c.Phone}".Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList();
 }

 [HttpGet("customers/{id:int}")] public async Task<IActionResult> Customer(int id) => await db.Customers.FindAsync(id) is { } c ? Ok(c) : NotFound();

 [HttpPost("customers")]
 public async Task<IActionResult> CreateCustomer(Customer input) {
  var c = new Customer { FullName = input.FullName, Email = input.Email.Trim().ToLowerInvariant(), Phone = input.Phone, Address = input.Address };
  db.Customers.Add(c); await db.SaveChangesAsync();
  return CreatedAtAction(nameof(Customer), new { id = c.Id }, c);
 }

 [HttpPut("customers/{id:int}")]
 public async Task<IActionResult> UpdateCustomer(int id, Customer input) {
  var c = await db.Customers.FindAsync(id);
  if (c is null) return NotFound();
  (c.FullName, c.Email, c.Phone, c.Address) = (input.FullName, input.Email.Trim().ToLowerInvariant(), input.Phone, input.Address);
  await db.SaveChangesAsync();
  return Ok(c);
 }

 [HttpPost("customers/{id:int}/lock")]
 public async Task<IActionResult> ToggleCustomerLock(int id) {
  var c = await db.Customers.FindAsync(id);
  if (c is null) return NotFound();
  c.IsLocked = !c.IsLocked; await db.SaveChangesAsync();
  return Ok(c);
 }

 // ---- Đơn hàng ----
 [HttpGet("orders")]
 public Task<List<Order>> Orders(string? status, int? customerId) =>
  db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).Where(o => (status == null || o.Status == status) && (customerId == null || o.CustomerId == customerId)).OrderByDescending(o => o.CreatedAt).ToListAsync();

 [HttpGet("orders/{id:int}")]
 public async Task<IActionResult> Order(int id) => await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == id) is { } o ? Ok(o) : NotFound();

 [HttpGet("order-statuses")] public string[] Statuses() => OrderStatus.All;

 [HttpPut("orders/{id:int}/status")]
 public async Task<IActionResult> SetOrderStatus(int id, StatusReq r) {
  if (!OrderStatus.All.Contains(r.Status)) return BadRequest(new { error = $"Trạng thái hợp lệ: {string.Join(", ", OrderStatus.All)}." });
  var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
  if (o is null) return NotFound();
  o.Status = r.Status; await db.SaveChangesAsync();
  return Ok(o);
 }

 [HttpPost("orders")]
 public async Task<IActionResult> CreateOrder(AdminOrderReq r) {
  var (o, err) = await ShopApiController.BuildOrder(db, await db.Customers.FindAsync(r.CustomerId), r.Items, r.Note, db.Products.AsNoTracking());
  if (o is null) return BadRequest(new { error = err });
  db.Orders.Add(o); await db.SaveChangesAsync();
  return CreatedAtAction(nameof(Order), new { id = o.Id }, o);
 }

 // ---- Nhập hàng ----
 [HttpGet("purchases")] public Task<List<Purchase>> Purchases() => db.Purchases.AsNoTracking().Include(p => p.Items).OrderByDescending(p => p.CreatedAt).ToListAsync();
 [HttpGet("purchases/{id:int}")] public async Task<IActionResult> Purchase(int id) => await db.Purchases.AsNoTracking().Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id) is { } p ? Ok(p) : NotFound();

 [HttpPost("purchases")]
 public async Task<IActionResult> CreatePurchase(PurchaseReq r) {
  var p = new Purchase { Supplier = r.Supplier.Trim(), Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim() };
  foreach (var x in r.Items) if (!string.IsNullOrWhiteSpace(x.ItemName) && x.Quantity > 0 && x.UnitCost >= 0) p.Items.Add(new PurchaseItem { ItemName = x.ItemName.Trim(), Unit = x.Unit?.Trim() ?? "", Quantity = x.Quantity, UnitCost = x.UnitCost });
  if (p.Supplier.Length == 0 || p.Items.Count == 0) return BadRequest(new { error = "Cần tên nhà cung cấp và ít nhất một dòng hàng hợp lệ (tên, số lượng > 0)." });
  db.Purchases.Add(p); await db.SaveChangesAsync();
  return CreatedAtAction(nameof(Purchase), new { id = p.Id }, p);
 }
}
