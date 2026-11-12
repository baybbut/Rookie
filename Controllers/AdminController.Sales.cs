using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Models;
namespace Rookie.Controllers;
public partial class AdminController {
 // ---- Khách hàng ----
 [HttpGet("customers")]
 public async Task<IActionResult> Customers(string? q) {
  var list = await db.Customers.AsNoTracking().Include(c => c.Orders).ThenInclude(o => o.Items).OrderBy(c => c.Id).ToListAsync();
  ViewBag.Q = q; var term = q?.Trim();
  return View(string.IsNullOrEmpty(term) ? list : list.Where(c => $"{c.FullName} {c.Email} {c.Phone}".Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList());
 }

 [HttpGet("customers/create")]
 public IActionResult CreateCustomer() => View("CustomerForm", new Customer());

 [HttpPost("customers/create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CreateCustomer([Bind("FullName,Email,Phone,Address")] Customer c) {
  if (!ModelState.IsValid) return View("CustomerForm", c);
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
 public async Task<IActionResult> EditCustomer(int id, [Bind("FullName,Email,Phone,Address")] Customer input) {
  var c = await db.Customers.FindAsync(id);
  if (c is null) return NotFound();
  if (!ModelState.IsValid) { input.Id = id; return View("CustomerForm", input); }
  (c.FullName, c.Email, c.Phone, c.Address) = (input.FullName, input.Email, input.Phone, input.Address);
  await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã cập nhật “{c.FullName}”.";
  return RedirectToAction(nameof(Customers));
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
 public async Task<IActionResult> Orders(string? status, int? customer) {
  var all = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).OrderByDescending(o => o.CreatedAt).ToListAsync();
  ViewBag.Counts = OrderStatus.All.ToDictionary(s => s, s => all.Count(o => o.Status == s));
  ViewBag.Total = all.Count; ViewBag.Status = status; ViewBag.CustomerId = customer;
  IEnumerable<Order> r = all;
  if (!string.IsNullOrEmpty(status)) r = r.Where(o => o.Status == status);
  if (customer is int cid) r = r.Where(o => o.CustomerId == cid);
  return View(r.ToList());
 }

 [HttpGet("orders/{id:int}")]
 public async Task<IActionResult> OrderDetail(int id) {
  var o = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
  return o is null ? NotFound() : View(o);
 }

 [HttpPost("orders/{id:int}/status"), ValidateAntiForgeryToken]
 public async Task<IActionResult> SetOrderStatus(int id, string status) {
  var o = await db.Orders.FindAsync(id);
  if (o is null) return NotFound();
  if (!OrderStatus.All.Contains(status)) return BadRequest();
  o.Status = status; await db.SaveChangesAsync();
  TempData["Msg"] = $"Đơn #{o.Id} → {status}.";
  return RedirectToAction(nameof(OrderDetail), new { id });
 }

 [HttpGet("orders/create")]
 public async Task<IActionResult> CreateOrder() { await LoadOrderForm(); return View("OrderForm"); }

 [HttpPost("orders/create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CreateOrder(int customerId, string? note, Dictionary<int, int>? qty) {
  var customer = await db.Customers.FindAsync(customerId);
  var products = await db.Products.AsNoTracking().ToListAsync();
  var o = new Order { CustomerId = customerId, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() };
  foreach (var p in products) if (qty is not null && qty.TryGetValue(p.Id, out var n) && n > 0) o.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = Math.Min(n, 99) });
  string? err = customer is null ? "Hãy chọn khách hàng." : customer.IsLocked ? "Tài khoản khách này đang bị khóa." : o.Items.Count == 0 ? "Hãy chọn ít nhất một chiếc bánh." : null;
  if (err is not null) { ViewBag.Error = err; ViewBag.Selected = customerId; ViewBag.Note = note; await LoadOrderForm(); return View("OrderForm"); }
  db.Orders.Add(o); await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã tạo đơn #{o.Id}.";
  return RedirectToAction(nameof(OrderDetail), new { id = o.Id });
 }

 async Task LoadOrderForm() {
  ViewBag.Customers = await db.Customers.AsNoTracking().Where(c => !c.IsLocked).OrderBy(c => c.FullName).ToListAsync();
  ViewBag.Products = await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
 }

 // ---- Nhập hàng ----
 [HttpGet("purchases")]
 public async Task<IActionResult> Purchases() => View(await db.Purchases.AsNoTracking().Include(p => p.Items).OrderByDescending(p => p.CreatedAt).ToListAsync());

 [HttpGet("purchases/{id:int}")]
 public async Task<IActionResult> PurchaseDetail(int id) {
  var p = await db.Purchases.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
  return p is null ? NotFound() : View(p);
 }

 [HttpGet("purchases/create")]
 public IActionResult CreatePurchase() => View("PurchaseForm");

 [HttpPost("purchases/create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CreatePurchase(string? supplier, string? note, List<PurchaseRow>? rows) {
  var p = new Purchase { Supplier = supplier?.Trim() ?? "", Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() };
  foreach (var r in rows ?? []) if (!string.IsNullOrWhiteSpace(r.ItemName) && r.Quantity > 0 && r.UnitCost >= 0) p.Items.Add(new PurchaseItem { ItemName = r.ItemName.Trim(), Unit = r.Unit?.Trim() ?? "", Quantity = r.Quantity, UnitCost = r.UnitCost });
  string? err = p.Supplier.Length is 0 or > 80 ? "Nhập tên nhà cung cấp (tối đa 80 ký tự)." : p.Items.Count == 0 ? "Hãy nhập ít nhất một dòng hàng hợp lệ (tên, số lượng > 0)." : null;
  if (err is not null) { ViewBag.Error = err; ViewBag.Supplier = supplier; ViewBag.Note = note; ViewBag.Rows = rows; return View("PurchaseForm"); }
  db.Purchases.Add(p); await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã lưu phiếu nhập #{p.Id}.";
  return RedirectToAction(nameof(PurchaseDetail), new { id = p.Id });
 }

 // ---- Báo cáo & thống kê ----
 [HttpGet("reports")]
 public async Task<IActionResult> Reports(int days = 30) {
  days = days is 7 or 30 or 90 ? days : 30;
  var from = DateTime.Today.AddDays(-(days - 1));
  var orders = (await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).ToListAsync()).Where(o => o.CreatedAt >= from).ToList();
  var live = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
  var purchases = (await db.Purchases.AsNoTracking().Include(p => p.Items).ToListAsync()).Where(p => p.CreatedAt >= from).ToList();
  var revenue = live.Sum(o => o.Total); var cost = purchases.Sum(p => p.Total);
  return View(new ReportVm {
   Days = days, Revenue = revenue, Cost = cost, Profit = revenue - cost, OrderCount = live.Count, AvgOrder = live.Count == 0 ? 0 : revenue / live.Count,
   Series = Series(live, from, days), TopProducts = TopProducts(live, 5),
   TopCustomers = live.GroupBy(o => o.Customer?.FullName ?? "—").Select(g => new NamedValue(g.Key, g.Sum(o => o.Total), g.Count())).OrderByDescending(n => n.Value).Take(5).ToList(),
   ByStatus = OrderStatus.All.Select(s => new NamedValue(s, orders.Where(o => o.Status == s).Sum(o => o.Total), orders.Count(o => o.Status == s))).ToList(),
   BySupplier = purchases.GroupBy(p => p.Supplier).Select(g => new NamedValue(g.Key, g.Sum(p => p.Total), g.Count())).OrderByDescending(n => n.Value).ToList(),
  });
 }
}
