using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Admin;

// Tìm rookie.db của web (đi ngược từ thư mục chạy tới khi gặp rookie.db hoặc Rookie.csproj); đặt ROOKIE_DB để chỉ định file khác.
static class Db {
 public static readonly string Path = Locate();
 static string Locate() {
  var env = Environment.GetEnvironmentVariable("ROOKIE_DB"); if (!string.IsNullOrEmpty(env)) return env;
  for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) {
   var f = System.IO.Path.Combine(d.FullName, "rookie.db");
   if (File.Exists(f) || File.Exists(System.IO.Path.Combine(d.FullName, "Rookie.csproj"))) return f;
  }
  return System.IO.Path.Combine(AppContext.BaseDirectory, "rookie.db");
 }
 public static ShopDbContext New() => new(new DbContextOptionsBuilder<ShopDbContext>().UseSqlite($"Data Source={Path}").Options);
}

static class Session {
 public static Staff User = new();
 public static bool IsAdmin => User.Role == StaffRole.Admin;
 public static string Name => User.FullName;
}

// Nghiệp vụ dùng chung giữa các trang (tồn kho, báo cáo, xuất Excel) — cùng công thức với bản web.
static class Svc {
 public const int LowStock = 10;
 public static bool Has(string? hay, string? term) => string.IsNullOrWhiteSpace(term) || (hay ?? "").Contains(term.Trim(), StringComparison.CurrentCultureIgnoreCase);
 public static bool InDate(DateTime d, int? day, int? month, int? year) => (day is null || d.Day == day) && (month is null || d.Month == month) && (year is null || d.Year == year);

 // Tồn = đã nhập kho (phiếu đã xác nhận) − đã bán (đơn chưa hủy) − bánh hủy.
 public static List<StockRow> Stock(ShopDbContext db) {
  var products = db.Products.AsNoTracking().OrderBy(p => p.Id).ToList();
  var inn = db.Purchases.AsNoTracking().Include(p => p.Items).Where(p => p.Status == PurchaseStatus.Received).ToList().SelectMany(p => p.Items).GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
  var sold = db.Orders.AsNoTracking().Include(o => o.Items).Where(o => o.Status != OrderStatus.Cancelled).ToList().SelectMany(o => o.Items).GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
  var waste = db.Discards.AsNoTracking().ToList().GroupBy(d => d.ProductId).ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));
  return products.Select(p => new StockRow(p, inn.GetValueOrDefault(p.Id), sold.GetValueOrDefault(p.Id), waste.GetValueOrDefault(p.Id))).ToList();
 }

 public static List<NamedValue> TopProducts(IEnumerable<Order> live, int take) =>
  live.SelectMany(o => o.Items).GroupBy(i => i.ProductName).Select(g => new NamedValue(g.Key, g.Sum(i => i.Subtotal), g.Sum(i => i.Quantity))).OrderByDescending(n => n.Value).Take(take).ToList();

 // Báo cáo theo khoảng ngày ("range"), một tháng ("month") hoặc một năm ("year", gộp theo tháng).
 public static ReportVm Report(ShopDbContext db, string type, DateTime? df, DateTime? dt, DateTime? month, int? year) {
  var today = DateTime.Today; var vm = new ReportVm { Type = type }; DateTime start, end;
  var m = month ?? new DateTime(today.Year, today.Month, 1); var y = year is >= 2000 and <= 2100 ? year.Value : today.Year;
  if (type == "month") { start = new(m.Year, m.Month, 1); end = start.AddMonths(1).AddDays(-1); vm.Title = $"Tháng {start:MM/yyyy} ({Fmt.Day(start)} – {Fmt.Day(end)})"; }
  else if (type == "year") { start = new(y, 1, 1); end = new(y, 12, 31); vm.Title = $"Năm {y} (theo từng tháng)"; vm.PeriodUnit = "tháng"; }
  else { end = (dt ?? today).Date; start = (df ?? end.AddDays(-29)).Date; if (start > end) (start, end) = (end, start); if ((end - start).TotalDays > 365) start = end.AddDays(-365); vm.Title = $"{Fmt.Day(start)} – {Fmt.Day(end)}"; }
  (vm.From, vm.To, vm.Year) = (start, end, y);
  var orders = db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).Where(o => o.CreatedAt >= start && o.CreatedAt < end.AddDays(1) && o.Status != OrderStatus.Cancelled).ToList();
  var discards = db.Discards.AsNoTracking().Where(d => d.CreatedAt >= start && d.CreatedAt < end.AddDays(1)).ToList();
  vm.Revenue = orders.Sum(o => o.Total); vm.OrderCount = orders.Count; vm.AvgOrder = orders.Count == 0 ? 0 : vm.Revenue / orders.Count;
  vm.Wasted = discards.Sum(d => d.Quantity); vm.WastedValue = discards.Sum(d => d.Value);
  vm.TopProducts = TopProducts(orders, 5);
  vm.TopCustomers = orders.GroupBy(o => o.Customer?.FullName ?? "—").Select(g => new NamedValue(g.Key, g.Sum(o => o.Total), g.Count())).OrderByDescending(n => n.Value).Take(5).ToList();
  var yearly = type == "year";
  Func<DateTime, DateTime> key = yearly ? d => new DateTime(d.Year, d.Month, 1) : d => d.Date;
  Func<DateTime, string> label = yearly ? d => $"Tháng {d:MM/yyyy}" : Fmt.Day;
  var byOrder = orders.GroupBy(o => key(o.CreatedAt)).ToDictionary(g => g.Key, g => g.ToList());
  var byWaste = discards.GroupBy(d => key(d.CreatedAt)).ToDictionary(g => g.Key, g => g.ToList());
  for (var d = start; d <= end; d = yearly ? d.AddMonths(1) : d.AddDays(1)) {
   var l = byOrder.GetValueOrDefault(d) ?? []; vm.Series.Add(new DayPoint(d, l.Sum(o => o.Total), l.Count, label(d)));
   if (byWaste.TryGetValue(d, out var w)) vm.Discards.Add(new DiscardPoint(label(d), w.Sum(x => x.Quantity), w.Sum(x => x.Value)));
  }
  return vm;
 }

 public static byte[] ReportXlsx(ReportVm r, string who) {
  var per = r.PeriodUnit == "tháng" ? "Tháng" : "Ngày";
  return Xlsx.Build(
   new XSheet("Tổng quan", ["Chỉ tiêu", "Giá trị"], [["Kỳ báo cáo", r.Title], ["Doanh thu (₫)", r.Revenue], ["Số đơn (không tính đã hủy)", r.OrderCount], ["Giá trị TB / đơn (₫)", Math.Round(r.AvgOrder)], ["Số bánh hủy", r.Wasted], ["Giá trị bánh hủy (₫)", r.WastedValue], ["Người xuất", who], ["Xuất lúc", Fmt.Date(DateTime.Now)]]),
   new XSheet($"Doanh thu theo {r.PeriodUnit}", [per, "Số đơn", "Doanh thu (₫)"], r.Series.Select(d => new object?[] { d.Label ?? Fmt.Day(d.Day), d.Orders, d.Revenue }).ToList()),
   new XSheet("Bánh bán chạy", ["Bánh", "Số lượng", "Doanh thu (₫)"], r.TopProducts.Select(t => new object?[] { t.Name, t.Count, t.Value }).ToList()),
   new XSheet("Khách hàng", ["Khách hàng", "Số đơn", "Chi tiêu (₫)"], r.TopCustomers.Select(t => new object?[] { t.Name, t.Count, t.Value }).ToList()),
   new XSheet($"Bánh hủy theo {r.PeriodUnit}", [per, "Số bánh hủy", "Giá trị (₫)"], r.Discards.Select(d => new object?[] { d.Label, d.Quantity, d.Value }).ToList()));
 }
}
