using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Models;
namespace Rookie.Controllers;
// Quản lý kho: phiếu nhập bánh từ xưởng (xác nhận mới cộng tồn), tồn kho, bánh hủy.
// Tồn = đã nhập kho − đã bán (đơn chưa hủy) − bánh hủy.
public partial class AdminController {
 static bool InDate(DateTime d, int? day, int? month, int? year) => (day is null || d.Day == day) && (month is null || d.Month == month) && (year is null || d.Year == year);

 async Task<List<StockRow>> StockRows() {
  var products = await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
  var inn = (await db.Purchases.AsNoTracking().Include(p => p.Items).Where(p => p.Status == PurchaseStatus.Received).ToListAsync()).SelectMany(p => p.Items).GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
  var sold = (await db.Orders.AsNoTracking().Include(o => o.Items).Where(o => o.Status != OrderStatus.Cancelled).ToListAsync()).SelectMany(o => o.Items).GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
  var waste = (await db.Discards.AsNoTracking().ToListAsync()).GroupBy(d => d.ProductId).ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));
  return products.Select(p => new StockRow(p, inn.GetValueOrDefault(p.Id), sold.GetValueOrDefault(p.Id), waste.GetValueOrDefault(p.Id))).ToList();
 }

 // ---- Phiếu nhập ----
 [HttpGet("purchases")]
 public async Task<IActionResult> Purchases(string? q, string? status, int? day, int? month, int? year) {
  var all = await db.Purchases.AsNoTracking().Include(p => p.Items).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToListAsync();
  var t = q?.Trim().TrimStart('#');
  ViewBag.Q = q; ViewBag.Status = status; ViewBag.Date = new DateFilter(day, month, year);
  return View(all.Where(p => InDate(p.CreatedAt, day, month, year) && (string.IsNullOrEmpty(status) || p.Status == status)
   && (string.IsNullOrEmpty(t) || p.Id.ToString() == t || Has(p.Supplier, t) || p.Items.Any(i => Has(i.ItemName, t)))).ToList());
 }

 [HttpGet("purchases/{id:int}")]
 public async Task<IActionResult> PurchaseDetail(int id) {
  var p = await db.Purchases.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
  return p is null ? NotFound() : View(p);
 }

 [HttpGet("purchases/create")]
 public async Task<IActionResult> CreatePurchase() { await LoadPurchaseForm(); return View("PurchaseForm"); }

 [HttpPost("purchases/create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CreatePurchase(string? supplier, DateTime? date, string? note, List<PurchaseRow>? rows) {
  var products = await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id);
  var p = new Purchase { Supplier = supplier?.Trim() ?? "", CreatedAt = (date ?? DateTime.Today).Date, CreatedBy = Me, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() };
  foreach (var r in rows ?? []) if (products.TryGetValue(r.ProductId, out var pr) && r.Quantity > 0 && r.UnitCost >= 0) p.Items.Add(new PurchaseItem { ProductId = pr.Id, ItemName = pr.Name, Unit = pr.Unit, Quantity = Math.Min(r.Quantity, 100000), UnitCost = r.UnitCost });
  string? err = !await db.Suppliers.AnyAsync(s => s.Name == p.Supplier) ? "Hãy chọn xưởng bánh cung cấp." : date is null ? "Hãy chọn ngày lập phiếu." : p.CreatedAt > DateTime.Today ? "Ngày lập phiếu không được ở tương lai." : p.Items.Count == 0 ? "Hãy chọn ít nhất một loại bánh với số lượng > 0." : null;
  if (err is not null) { ViewBag.Error = err; ViewBag.Supplier = supplier; ViewBag.Date = date?.ToString("yyyy-MM-dd"); ViewBag.Note = note; ViewBag.Rows = rows; await LoadPurchaseForm(); return View("PurchaseForm"); }
  db.Purchases.Add(p); await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã lưu phiếu nhập #{p.Id}. Hãy kiểm hàng và xác nhận để cộng vào tồn kho.";
  return RedirectToAction(nameof(PurchaseDetail), new { id = p.Id });
 }

 async Task LoadPurchaseForm() {
  ViewBag.Suppliers = await db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
  ViewBag.Products = await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
 }

 // Chỉ phiếu "Chờ nhập kho" mới xác nhận/hủy được; xác nhận xong số bánh mới được cộng vào tồn kho.
 [HttpPost("purchases/{id:int}/receive"), ValidateAntiForgeryToken]
 public async Task<IActionResult> ReceivePurchase(int id) {
  var p = await db.Purchases.FindAsync(id);
  if (p is null) return NotFound();
  if (p.Status != PurchaseStatus.Pending) TempData["Msg"] = $"Phiếu #{p.Id} đang ở trạng thái “{p.Status}”, không xác nhận được.";
  else { (p.Status, p.ReceivedAt, p.ReceivedBy) = (PurchaseStatus.Received, DateTime.Now, Me); await db.SaveChangesAsync(); TempData["Msg"] = $"{Me} đã xác nhận nhập kho phiếu #{p.Id}."; }
  return RedirectToAction(nameof(PurchaseDetail), new { id });
 }

 [HttpPost("purchases/{id:int}/cancel"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CancelPurchase(int id) {
  var p = await db.Purchases.FindAsync(id);
  if (p is null) return NotFound();
  if (p.Status != PurchaseStatus.Pending) TempData["Msg"] = $"Phiếu #{p.Id} đang ở trạng thái “{p.Status}”, không hủy được.";
  else { p.Status = PurchaseStatus.Cancelled; await db.SaveChangesAsync(); TempData["Msg"] = $"Đã hủy phiếu #{p.Id}."; }
  return RedirectToAction(nameof(PurchaseDetail), new { id });
 }

 // ---- Tồn kho ----
 [HttpGet("stock")]
 public async Task<IActionResult> Stock(string? q, string? level) {
  var rows = (await StockRows()).Where(s => (Has(s.Product.Name, q) || Has(s.Product.Code, q)) && (level != "low" || s.Left <= LowStock)).ToList();
  ViewBag.Q = q; ViewBag.Level = level; ViewBag.Low = LowStock;
  return View(rows);
 }
 const int LowStock = 10;

 // ---- Bánh hủy ----
 [HttpGet("discards")]
 public async Task<IActionResult> Discards(string? q, int? day, int? month, int? year) {
  var all = await db.Discards.AsNoTracking().OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id).ToListAsync();
  ViewBag.Q = q; ViewBag.Date = new DateFilter(day, month, year); ViewBag.Stock = await StockRows();
  return View(all.Where(d => InDate(d.CreatedAt, day, month, year) && (Has(d.ProductName, q) || Has(d.Reason, q))).ToList());
 }

 [HttpPost("discards/create"), ValidateAntiForgeryToken]
 public async Task<IActionResult> CreateDiscard(int productId, int quantity, string? reason, string? note, DateTime? date) {
  var stock = (await StockRows()).FirstOrDefault(s => s.Product.Id == productId);
  var day = (date ?? DateTime.Today).Date;
  string? err = stock is null ? "Hãy chọn loại bánh." : quantity < 1 ? "Số lượng hủy phải từ 1 trở lên." : quantity > stock.Left ? $"Chỉ còn {stock.Left} {stock.Product.Unit} “{stock.Product.Name}” trong kho." : !DiscardReasons.All.Contains(reason ?? "") ? "Hãy chọn lý do hủy." : day > DateTime.Today ? "Ngày hủy không được ở tương lai." : null;
  if (err is not null) { TempData["Err"] = err; return RedirectToAction(nameof(Discards)); }
  var n = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
  db.Discards.Add(new Discard { ProductId = stock!.Product.Id, ProductName = stock.Product.Name, Unit = stock.Product.Unit, Quantity = quantity, UnitPrice = stock.Product.Price, Reason = n is null ? reason! : $"{reason}: {n}", CreatedAt = day == DateTime.Today ? DateTime.Now : day.AddHours(12), CreatedBy = Me });
  await db.SaveChangesAsync();
  TempData["Msg"] = $"Đã ghi nhận hủy {quantity} {stock.Product.Unit} “{stock.Product.Name}”.";
  return RedirectToAction(nameof(Discards));
 }
}
