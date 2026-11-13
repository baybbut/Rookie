using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Admin;

public class ApiException(HttpStatusCode? status, string message) : Exception(message) { public HttpStatusCode? Status { get; } = status; }

// App không mở rookie.db nữa: mọi dữ liệu đi qua Web API (/api/admin/...) của web, kèm JWT lấy lúc đăng nhập.
// Nghiệp vụ, xác thực và phân quyền do server kiểm tra; ẩn/hiện nút trong app chỉ để tiện dùng.
// Địa chỉ server mặc định http://localhost:5000 (cổng của `dotnet run`), đổi bằng biến môi trường ROOKIE_API.
static class Api {
 public static readonly string Base = (Environment.GetEnvironmentVariable("ROOKIE_API") ?? "http://localhost:5000").TrimEnd('/');
 static readonly HttpClient http = new() { BaseAddress = new Uri(Base + "/"), Timeout = TimeSpan.FromSeconds(20) };
 static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
 public static string? Token; // chỉ giữ trong bộ nhớ, mất khi đóng app
 // Phát ra khi server từ chối token (hết hạn, tài khoản bị khóa/xóa hoặc bị đổi vai trò) để app quay về màn đăng nhập.
 public static event Action? Expired;

 public static T Get<T>(string path) => Read<T>(Send(HttpMethod.Get, path, null));
 public static T Post<T>(string path, object? body = null) => Read<T>(Send(HttpMethod.Post, path, body ?? new { }));
 public static T Put<T>(string path, object body) => Read<T>(Send(HttpMethod.Put, path, body));
 public static void Post(string path, object? body = null) => Send(HttpMethod.Post, path, body ?? new { }).Dispose();
 public static void Delete(string path) => Send(HttpMethod.Delete, path, null).Dispose();
 public static string Q(string s) => Uri.EscapeDataString(s);

 static T Read<T>(HttpResponseMessage res) { using (res) return JsonSerializer.Deserialize<T>(res.Content.ReadAsStream(), json)!; }

 // shortcut: gọi đồng bộ (HttpClient.Send) nên giao diện đứng trong lúc chờ; đủ nhanh khi server cùng máy/mạng LAN, đổi sang async nếu server ở xa.
 static HttpResponseMessage Send(HttpMethod method, string path, object? body) {
  using var req = new HttpRequestMessage(method, path.TrimStart('/'));
  if (Token is not null) req.Headers.Authorization = new("Bearer", Token);
  if (body is not null) req.Content = JsonContent.Create(body, options: json);
  HttpResponseMessage res;
  try { res = http.Send(req); }
  catch (HttpRequestException) { throw new ApiException(null, $"Không kết nối được máy chủ {Base}.\nHãy chạy web (lệnh `dotnet run` ở thư mục dự án) rồi thử lại."); }
  catch (TaskCanceledException) { throw new ApiException(null, "Máy chủ phản hồi quá lâu, vui lòng thử lại."); }
  if (res.IsSuccessStatusCode) return res;
  using (res) {
   if (res.StatusCode == HttpStatusCode.Unauthorized && Token is not null) {
    Token = null; Expired?.Invoke();
    throw new ApiException(res.StatusCode, "Phiên đăng nhập đã hết hiệu lực, hoặc tài khoản vừa bị khóa / đổi quyền.\nVui lòng đăng nhập lại.");
   }
   throw new ApiException(res.StatusCode, Message(res));
  }
 }

 // Lỗi nghiệp vụ: { "error": "..." }; lỗi dữ liệu nhập: ProblemDetails { "errors": { "Field": ["..."] } }.
 static string Message(HttpResponseMessage res) {
  try {
   using var doc = JsonDocument.Parse(res.Content.ReadAsStream()); var root = doc.RootElement;
   if (root.TryGetProperty("error", out var e)) return e.GetString() ?? "";
   if (root.TryGetProperty("errors", out var errs)) return string.Join("\n", errs.EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString())));
  } catch (JsonException) { }
  return res.StatusCode switch {
   HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
   HttpStatusCode.NotFound => "Không tìm thấy dữ liệu (có thể đã bị xóa).",
   HttpStatusCode.TooManyRequests => "Thử quá nhiều lần. Vui lòng đợi một phút rồi thử lại.",
   _ => $"Máy chủ báo lỗi ({(int)res.StatusCode}).",
  };
 }
}

// Nhân viên đang đăng nhập (lấy từ phản hồi đăng nhập). Quyền thật nằm trong token và được server kiểm tra.
static class Session {
 public static Staff User = new();
 public static bool IsAdmin => User.Role == StaffRole.Admin;
 public static string Name => User.FullName;
}

// Danh mục trả về từ GET /api/admin/categories.
record CategoryRow(int Id, string Name, int Count, bool Hidden);

static class Svc {
 public const int LowStock = 10; // cùng ngưỡng "sắp hết" với trang /admin
 public static bool Has(string? hay, string? term) => string.IsNullOrWhiteSpace(term) || (hay ?? "").Contains(term.Trim(), StringComparison.CurrentCultureIgnoreCase);
 public static bool InDate(DateTime d, int? day, int? month, int? year) => (day is null || d.Day == day) && (month is null || d.Month == month) && (year is null || d.Year == year);

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
