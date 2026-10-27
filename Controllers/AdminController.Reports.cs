using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Controllers;
public partial class AdminController {
 // ---- Báo cáo & thống kê: theo khoảng ngày / theo tháng / theo năm ----
 [HttpGet("reports")]
 public async Task<IActionResult> Reports(string? type, [FromQuery(Name = "from")] DateTime? df, [FromQuery(Name = "to")] DateTime? dt, string? month, int? year, bool print = false) { ViewBag.Print = print; return View(await BuildReport(type, df, dt, month, year)); }

 [HttpGet("reports/export")]
 public async Task<IActionResult> ExportReport(string? type, [FromQuery(Name = "from")] DateTime? df, [FromQuery(Name = "to")] DateTime? dt, string? month, int? year) {
  var r = await BuildReport(type, df, dt, month, year);
  var per = r.PeriodUnit == "tháng" ? "Tháng" : "Ngày";
  var bytes = Xlsx.Build(
   new XSheet("Tổng quan", ["Chỉ tiêu", "Giá trị"], [["Kỳ báo cáo", r.Title], ["Doanh thu (₫)", r.Revenue], ["Số đơn (không tính đã hủy)", r.OrderCount], ["Giá trị TB / đơn (₫)", Math.Round(r.AvgOrder)], ["Số bánh hủy", r.Wasted], ["Giá trị bánh hủy (₫)", r.WastedValue], ["Người xuất", Me], ["Xuất lúc", Fmt.Date(DateTime.Now)]]),
   new XSheet($"Doanh thu theo {r.PeriodUnit}", [per, "Số đơn", "Doanh thu (₫)"], r.Series.Select(d => new object?[] { d.Label ?? Fmt.Day(d.Day), d.Orders, d.Revenue }).ToList()),
   new XSheet("Bánh bán chạy", ["Bánh", "Số lượng", "Doanh thu (₫)"], r.TopProducts.Select(t => new object?[] { t.Name, t.Count, t.Value }).ToList()),
   new XSheet("Khách hàng", ["Khách hàng", "Số đơn", "Chi tiêu (₫)"], r.TopCustomers.Select(t => new object?[] { t.Name, t.Count, t.Value }).ToList()),
   new XSheet($"Bánh hủy theo {r.PeriodUnit}", [per, "Số bánh hủy", "Giá trị (₫)"], r.Discards.Select(d => new object?[] { d.Label, d.Quantity, d.Value }).ToList()));
  return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"bao-cao-{r.From:yyyyMMdd}-{r.To:yyyyMMdd}.xlsx");
 }

 async Task<ReportVm> BuildReport(string? type, DateTime? df, DateTime? dt, string? month, int? year) {
  var today = DateTime.Today; type = type is "month" or "year" ? type : "range";
  var vm = new ReportVm { Type = type }; DateTime start, end;
  var m = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mm) ? mm : new DateTime(today.Year, today.Month, 1);
  var y = year is >= 2000 and <= 2100 ? year.Value : today.Year;
  if (type == "month") { start = new(m.Year, m.Month, 1); end = start.AddMonths(1).AddDays(-1); vm.Title = $"Tháng {start:MM/yyyy} ({Fmt.Day(start)} – {Fmt.Day(end)})"; }
  else if (type == "year") { start = new(y, 1, 1); end = new(y, 12, 31); vm.Title = $"Năm {y} (theo từng tháng)"; vm.PeriodUnit = "tháng"; }
  else { end = (dt ?? today).Date; start = (df ?? end.AddDays(-29)).Date; if (start > end) (start, end) = (end, start); if ((end - start).TotalDays > 365) start = end.AddDays(-365); vm.Title = $"{Fmt.Day(start)} – {Fmt.Day(end)}"; }
  (vm.From, vm.To, vm.Month, vm.Year) = (start, end, m.ToString("yyyy-MM"), type == "year" ? y : today.Year);

  var orders = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).Where(o => o.CreatedAt >= start && o.CreatedAt < end.AddDays(1) && o.Status != OrderStatus.Cancelled).ToListAsync();
  var discards = await db.Discards.AsNoTracking().Where(d => d.CreatedAt >= start && d.CreatedAt < end.AddDays(1)).ToListAsync();
  vm.Revenue = orders.Sum(o => o.Total); vm.OrderCount = orders.Count; vm.AvgOrder = orders.Count == 0 ? 0 : vm.Revenue / orders.Count;
  vm.Wasted = discards.Sum(d => d.Quantity); vm.WastedValue = discards.Sum(d => d.Value);
  vm.TopProducts = TopProducts(orders, 5);
  vm.TopCustomers = orders.GroupBy(o => o.Customer?.FullName ?? "—").Select(g => new NamedValue(g.Key, g.Sum(o => o.Total), g.Count())).OrderByDescending(n => n.Value).Take(5).ToList();

  // Một "ô" là một ngày (kỳ ngày/tháng) hoặc một tháng (kỳ năm); nhãn luôn ghi ngày/tháng/năm đầy đủ.
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
}
