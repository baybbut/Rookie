using System.ComponentModel.DataAnnotations;
namespace Rookie.Models;
public static class OrderStatus {
 public const string New = "Mới", Confirmed = "Đã xác nhận", Preparing = "Đang làm", Shipping = "Đang giao", Done = "Hoàn thành", Cancelled = "Đã hủy";
 public static readonly string[] All = [New, Confirmed, Preparing, Shipping, Done, Cancelled];
 // Quy trình: Mới → Đã xác nhận (nhân viên) → Đang làm → Đang giao → Hoàn thành; hủy được cho tới trước khi hoàn thành.
 public static string? Next(string s) => s switch { New => Confirmed, Confirmed => Preparing, Preparing => Shipping, Shipping => Done, _ => null };
 public static bool CanCancel(string s) => s is New or Confirmed or Preparing or Shipping;
 public static bool Allowed(string from, string to) => to == Cancelled ? CanCancel(from) : Next(from) == to;
}
public static class PurchaseStatus {
 public const string Pending = "Chờ nhập kho", Received = "Đã nhập kho", Cancelled = "Đã hủy";
 public static readonly string[] All = [Pending, Received, Cancelled];
}
public class Customer {
 public int Id { get; set; }
 [Required, StringLength(80)] public string FullName { get; set; } = "";
 [Required, EmailAddress, StringLength(120)] public string Email { get; set; } = "";
 [Required, Phone, StringLength(20)] public string Phone { get; set; } = "";
 [StringLength(200)] public string? Address { get; set; }
 public string? PasswordHash { get; set; }
 public bool IsLocked { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public List<Order> Orders { get; set; } = [];
}
public class Order {
 public int Id { get; set; }
 public int CustomerId { get; set; }
 public Customer? Customer { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public string Status { get; set; } = OrderStatus.New;
 public string? Note { get; set; }
 public string? CreatedBy { get; set; }
 public string? ConfirmedBy { get; set; }
 public DateTime? ConfirmedAt { get; set; }
 public List<OrderItem> Items { get; set; } = [];
 public decimal Total => Items.Sum(i => i.Subtotal);
}
public class OrderItem {
 public int Id { get; set; }
 public int OrderId { get; set; }
 public int ProductId { get; set; }
 public string ProductName { get; set; } = "";
 public decimal UnitPrice { get; set; }
 public int Quantity { get; set; }
 public decimal Subtotal => UnitPrice * Quantity;
}
public class Supplier {
 public int Id { get; set; }
 [Required, StringLength(80)] public string Name { get; set; } = "";
 public string? Phone { get; set; }
 public string? Address { get; set; }
}
public class Purchase {
 public int Id { get; set; }
 public string Supplier { get; set; } = "";
 public string Status { get; set; } = PurchaseStatus.Pending;
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public string? CreatedBy { get; set; }
 public DateTime? ReceivedAt { get; set; }
 public string? ReceivedBy { get; set; }
 public string? Note { get; set; }
 public List<PurchaseItem> Items { get; set; } = [];
 public decimal Total => Items.Sum(i => i.Subtotal);
}
public class PurchaseItem {
 public int Id { get; set; }
 public int PurchaseId { get; set; }
 public int ProductId { get; set; }
 public string ItemName { get; set; } = "";
 public string Unit { get; set; } = "";
 public int Quantity { get; set; }
 public decimal UnitCost { get; set; }
 public decimal Subtotal => Quantity * UnitCost;
}
public class PurchaseRow { public int ProductId { get; set; } public int Quantity { get; set; } public decimal UnitCost { get; set; } }
// Bánh hủy (hết hạn, hỏng, làm lỗi…): trừ khỏi tồn kho, giá trị tính theo giá bán lúc hủy.
public class Discard {
 public int Id { get; set; }
 public int ProductId { get; set; }
 public string ProductName { get; set; } = "";
 public string Unit { get; set; } = "";
 public int Quantity { get; set; }
 public decimal UnitPrice { get; set; }
 public string Reason { get; set; } = "";
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public string? CreatedBy { get; set; }
 public decimal Value => UnitPrice * Quantity;
}
public static class DiscardReasons { public static readonly string[] All = ["Hết hạn", "Hỏng / rơi vỡ", "Làm lỗi", "Ế cuối ngày", "Khác"]; }

public record DayPoint(DateTime Day, decimal Revenue, int Orders, string? Label = null);
public record NamedValue(string Name, decimal Value, int Count);
public record DiscardPoint(string Label, int Quantity, decimal Value);
public record StockRow(Product Product, int In, int Sold, int Wasted) { public int Left => In - Sold - Wasted; }
public record DateFilter(int? Day, int? Month, int? Year);
public class DashboardVm {
 public decimal RevenueToday, RevenueMonth, PurchaseMonth;
 public int NewOrders, OpenOrders, Customers, Products;
 public List<DayPoint> Days = [];
 public List<Order> Recent = [];
 public List<NamedValue> TopProducts = [];
}
public class ReportVm {
 public string Type = "range", Title = "", Month = "", PeriodUnit = "ngày";
 public DateTime From, To;
 public int Year;
 public decimal Revenue, AvgOrder, WastedValue;
 public int OrderCount, Wasted;
 public List<DayPoint> Series = [];
 public List<NamedValue> TopProducts = [], TopCustomers = [];
 public List<DiscardPoint> Discards = [];
}
public static class Fmt {
 static readonly System.Globalization.CultureInfo Vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
 public static string Vnd(decimal v) => v.ToString("N0", Vn) + " ₫";
 public static string Date(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Vn);
 public static string Day(DateTime d) => d.ToString("dd/MM/yyyy", Vn);
 public static string StatusClass(string s) => s switch { OrderStatus.New => "st-new", OrderStatus.Confirmed => "st-conf", OrderStatus.Preparing => "st-prep", OrderStatus.Shipping => "st-ship", OrderStatus.Done => "st-done", _ => "st-cancel" };
 public static string PurchaseClass(string s) => s switch { PurchaseStatus.Pending => "st-new", PurchaseStatus.Received => "st-done", _ => "st-cancel" };
}
