using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers;
public partial class AdminController {
 // ---- Khách hàng ----
 [HttpGet("customers")]
 public async Task<IActionResult> Customers(string? name, string? contact, string? status) {
  var list = await db.Customers.AsNoTracking().Include(c => c.Orders).ThenInclude(o => o.Items).OrderBy(c => c.Id).ToListAsync();
  ViewBag.Name = name; ViewBag.Contact = contact; ViewBag.Status = status;
  return View(list.Where(c => Has(c.FullName, name) && (Has(c.Email, contact) || Has(c.Phone, contact)) && (string.IsNullOrEmpty(status) || c.IsLocked == (status == "locked"))).ToList());
 }

 [HttpGet("customers/create")]
 public IActionResult CreateCustomer() => View("CustomerForm", new Customer());

 [HttpPost("customers/create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CreateCustomer([Bind("FullName,Email,Phone,Address")] Customer c, string? password) {
  await CheckCustomer(c, password, true, 0);
  if (!ModelState.IsValid) return View("CustomerForm", c);
  c.PasswordHash = Pw.Hash(password!);
  db.Customers.Add(c); await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã thêm khách hàng “{c.FullName}”.";
  return RedirectToAction(nameof(Customers));
 }

 [HttpGet("customers/edit/{id:int}")]
 public async Task<IActionResult> EditCustomer(int id) {
  var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
  return c is null ? NotFound() : View("CustomerForm", c);
 }

 [HttpPost("customers/edit/{id:int}"), ValidateAntiForgeryToken]
 public async Task<IActionResult> EditCustomer(int id, [Bind("FullName,Email,Phone,Address")] Customer input, string? password) {
  var c = await db.Customers.FindAsync(id);
  if (c is null) return NotFound();
  await CheckCustomer(input, password, false, id);
  if (!ModelState.IsValid) { input.Id = id; return View("CustomerForm", input); }
  (c.FullName, c.Email, c.Phone, c.Address) = (input.FullName, input.Email, input.Phone, input.Address);
  if (!string.IsNullOrEmpty(password)) c.PasswordHash = Pw.Hash(password);
  await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã cập nhật “{c.FullName}”{(string.IsNullOrEmpty(password) ? "" : " và đổi mật khẩu")}.";
  return RedirectToAction(nameof(Customers));
 }

 async Task CheckCustomer(Customer c, string? password, bool create, int selfId) {
  if (create && string.IsNullOrEmpty(password)) ModelState.AddModelError("password", "Hãy nhập mật khẩu cho khách hàng.");
  else if (!string.IsNullOrEmpty(password) && (password.Length < 6 || password.Length > 64)) ModelState.AddModelError("password", "Mật khẩu dài 6–64 ký tự.");
  c.Email = c.Email?.Trim() ?? "";
  if (c.Email.Length > 0 && await db.Customers.AnyAsync(x => x.Email == c.Email && x.Id != selfId)) ModelState.AddModelError(nameof(Customer.Email), "Email này đã có khách hàng khác dùng.");
 }

 [HttpPost("customers/lock/{id:int}"), ValidateAntiForgeryToken]
 public async Task<IActionResult> ToggleCustomerLock(int id) {
  var c = await db.Customers.FindAsync(id);
  if (c is null) return NotFound();
  c.IsLocked = !c.IsLocked; await db.SaveChangesAsync();
  TempData["Msg"] = c.IsLocked ? $"Đã khóa tài khoản “{c.FullName}”." : $"Đã mở khóa “{c.FullName}”.";
  return RedirectToAction(nameof(Customers));
 }

 // ---- Đơn hàng ----
 [HttpGet("orders")]
 public async Task<IActionResult> Orders(string? q, string? status, int? customer, [FromQuery(Name = "from")] DateTime? df, [FromQuery(Name = "to")] DateTime? dt) {
  var all = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).OrderByDescending(o => o.CreatedAt).ToListAsync();
  IEnumerable<Order> r = all;
  if (!string.IsNullOrWhiteSpace(q)) { var t = q.Trim().TrimStart('#'); r = r.Where(o => o.Id.ToString() == t || Has(o.Customer?.FullName, t) || Has(o.Customer?.Phone, t)); }
  if (customer is int cid) { r = r.Where(o => o.CustomerId == cid); ViewBag.CustomerName = (await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid))?.FullName ?? $"#{cid}"; }
  if (df is DateTime a) r = r.Where(o => o.CreatedAt >= a.Date);
  if (dt is DateTime b) r = r.Where(o => o.CreatedAt < b.Date.AddDays(1));
  var scoped = r.ToList(); // đếm theo trạng thái trong phạm vi các bộ lọc khác
  ViewBag.Counts = OrderStatus.All.ToDictionary(s => s, s => scoped.Count(o => o.Status == s));
  ViewBag.Total = scoped.Count; ViewBag.Q = q; ViewBag.Status = status; ViewBag.CustomerId = customer; ViewBag.From = df?.ToString("yyyy-MM-dd"); ViewBag.To = dt?.ToString("yyyy-MM-dd");
  return View(string.IsNullOrEmpty(status) ? scoped : scoped.Where(o => o.Status == status).ToList());
 }

 [HttpGet("orders/{id:int}")]
 public async Task<IActionResult> OrderDetail(int id) {
  var o = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
  return o is null ? NotFound() : View(o);
 }

 // Nhân viên xác nhận đơn = chuyển "Mới" → "Đã xác nhận"; các bước sau đi tuần tự, hủy được tới trước khi hoàn thành.
 [HttpPost("orders/{id:int}/status"), ValidateAntiForgeryToken]
 public async Task<IActionResult> SetOrderStatus(int id, string status, string? back) {
  var o = await db.Orders.FindAsync(id);
  if (o is null) return NotFound();
  if (!OrderStatus.All.Contains(status)) return BadRequest();
  if (!OrderStatus.Allowed(o.Status, status)) TempData["Msg"] = $"Không thể chuyển đơn #{o.Id} từ “{o.Status}” sang “{status}”.";
  else {
   o.Status = status;
   if (status == OrderStatus.Confirmed) (o.ConfirmedBy, o.ConfirmedAt) = (Me, DateTime.Now);
   await db.SaveChangesAsync();
   TempData["Msg"] = status == OrderStatus.Confirmed ? $"{Me} đã xác nhận đơn #{o.Id}." : $"Đơn #{o.Id} → {status}.";
  }
  return Url.IsLocalUrl(back) ? Redirect(back!) : RedirectToAction(nameof(OrderDetail), new { id });
 }

 [HttpGet("orders/create")]
 public async Task<IActionResult> CreateOrder() { await LoadOrderForm(); return View("OrderForm"); }

 [HttpPost("orders/create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CreateOrder(int customerId, string? note, Dictionary<int, int>? qty) {
  var customer = await db.Customers.FindAsync(customerId);
  var products = await db.Products.AsNoTracking().ToListAsync();
  var o = new Order { CustomerId = customerId, CreatedBy = Me, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() };
  foreach (var p in products) if (qty is not null && qty.TryGetValue(p.Id, out var n) && n > 0) o.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = Math.Min(n, 99) });
  string? err = customer is null ? "Hãy chọn khách hàng." : customer.IsLocked ? "Tài khoản khách này đang bị khóa." : o.Items.Count == 0 ? "Hãy chọn ít nhất một chiếc bánh." : null;
  if (err is not null) { ViewBag.Error = err; ViewBag.Selected = customerId; ViewBag.Note = note; await LoadOrderForm(); return View("OrderForm"); }
  db.Orders.Add(o); await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã tạo đơn #{o.Id}. Đơn đang chờ nhân viên xác nhận.";
  return RedirectToAction(nameof(OrderDetail), new { id = o.Id });
 }

 async Task LoadOrderForm() {
  ViewBag.Customers = await db.Customers.AsNoTracking().Where(c => !c.IsLocked).OrderBy(c => c.FullName).ToListAsync();
  ViewBag.Products = await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
  ViewBag.Stock = (await StockRows()).ToDictionary(s => s.Product.Id, s => s.Left);
 }
}
