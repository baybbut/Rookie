using System.ComponentModel.DataAnnotations;
namespace Rookie.Models;
public static class OrderStatus {
 public const string New = "Mới", Confirmed = "Đã xác nhận", Preparing = "Đang làm", Shipping = "Đang giao", Done = "Hoàn thành", Cancelled = "Đã hủy";
 public static readonly string[] All = [New, Confirmed, Preparing, Shipping, Done, Cancelled];
 // Quy trình: Mới → Đã xác nhận (nhân viên) → Đang làm → Đang giao → Hoàn thành; hủy được cho tới trước khi hoàn thành.
 public static string? Next(string s) => s switch { New => Confirmed, Confirmed => Preparing, Preparing => Shipping, Shipping => Done, _ => null };
 public static bool CanCancel(string s) => s is New or Confirmed or Preparing or Shipping;
 // Khách tự sửa thông tin giao bánh tới trước khi đơn "Đang giao".
 public static bool CustomerCanEdit(string s) => s is New or Confirmed or Preparing;
 public static bool Allowed(string from, string to) => to == Cancelled ? CanCancel(from) : Next(from) == to;
}
public static class PurchaseStatus {
 public const string Pending = "Chờ nhập kho", Received = "Đã nhập kho", Cancelled = "Đã hủy";
 public static readonly string[] All = [Pending, Received, Cancelled];
}
// Thanh toán: Chưa thanh toán → (ghi nhận) Chờ xác nhận → (xác nhận) Đã thanh toán → (hoàn tiền) Đã hoàn tiền.
public static class PaymentStatus {
 public const string Unpaid = "Chưa thanh toán", Pending = "Chờ xác nhận", Paid = "Đã thanh toán", Refunded = "Đã hoàn tiền";
 public static readonly string[] All = [Unpaid, Pending, Paid, Refunded];
 public static readonly string[] Methods = ["Tiền mặt (COD)", "Chuyển khoản", "Thẻ / ví điện tử"];
}
// Phiếu hủy: nhân viên lập ở trạng thái "Chờ duyệt"; chỉ phiếu "Đã duyệt" mới trừ tồn kho và tính vào bánh hủy. Quản trị lập thì duyệt luôn.
public static class DiscardStatus {
 public const string Pending = "Chờ duyệt", Approved = "Đã duyệt", Rejected = "Từ chối";
 public static readonly string[] All = [Pending, Approved, Rejected];
}
public class Customer {
 public int Id { get; set; }
 [StringLength(40)] public string? UserName { get; set; }
 [Required, StringLength(80)] public string FullName { get; set; } = "";
 [Required, EmailAddress, StringLength(120)] public string Email { get; set; } = "";
 [Required, Phone, StringLength(20)] public string Phone { get; set; } = "";
 [StringLength(200)] public string? Address { get; set; }
 [System.Text.Json.Serialization.JsonIgnore] public string? PasswordHash { get; set; }
 public bool IsLocked { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 [System.Text.Json.Serialization.JsonIgnore] public List<Order> Orders { get; set; } = [];
}
public class Order {
 public int Id { get; set; }
 public int CustomerId { get; set; }
 public Customer? Customer { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public string Status { get; set; } = OrderStatus.New;
 public string? Note { get; set; }
 // Người nhận do khách nhập khi đặt trên web (null với đơn nhân viên tạo: giao theo thông tin khách hàng).
 public string? ShipName { get; set; }
 public string? ShipPhone { get; set; }
 public string? ShipAddress { get; set; }
 // Khóa chống gửi trùng đơn khi khách bấm đặt hàng nhiều lần.
 public string? RequestKey { get; set; }
 public string? CreatedBy { get; set; }
 public string? ConfirmedBy { get; set; }
 public DateTime? ConfirmedAt { get; set; }
 public string PaymentStatus { get; set; } = Models.PaymentStatus.Unpaid;
 public string? PaymentMethod { get; set; }
 public string? PaymentNote { get; set; }
 public string? PaymentRecordedBy { get; set; }
 public DateTime? PaymentRecordedAt { get; set; }
 public string? PaymentConfirmedBy { get; set; }
 public DateTime? PaymentConfirmedAt { get; set; }
 public string? RefundedBy { get; set; }
 public DateTime? RefundedAt { get; set; }
 public List<OrderItem> Items { get; set; } = [];
 public decimal Total => Items.Sum(i => i.Subtotal);
}
// Cập nhật thông tin đơn (người nhận, địa chỉ, lời nhắn): khách sửa khi đơn còn "Mới", nhân viên/quản trị sửa khi đơn chưa hoàn thành/hủy.
public class OrderInfoInput {
 [Required(ErrorMessage = "Nhập tên người nhận"), StringLength(100)] public string Name { get; set; } = "";
 [Required(ErrorMessage = "Nhập số điện thoại"), Phone(ErrorMessage = "Số điện thoại không hợp lệ"), StringLength(30)] public string Phone { get; set; } = "";
 [Required(ErrorMessage = "Nhập địa chỉ giao bánh"), StringLength(500)] public string Address { get; set; } = "";
 [StringLength(1000)] public string? Note { get; set; }
 public void ApplyTo(Order o) => (o.ShipName, o.ShipPhone, o.ShipAddress, o.Note) = (Name.Trim(), Phone.Trim(), Address.Trim(), string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());
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
 // Ngừng hợp tác: không chọn được khi lập phiếu nhập mới, phiếu cũ giữ nguyên.
 public bool IsActive { get; set; } = true;
}
// Danh sách bánh + bảng giá cố định của xưởng. Mỗi dòng là một bánh xưởng làm với đơn giá nhập:
// ProductId = null → bánh tiệm chưa bán (chọn được khi "Thêm bánh mới"); có ProductId → gắn với bánh đang bán.
// Phiếu nhập lấy đơn giá từ đây, không nhập tay; giá nhập của bánh (Product.CostPrice) = giá của xưởng chính (Product.SupplierId).
public class SupplierPrice {
 public int Id { get; set; }
 public int SupplierId { get; set; }
 public int? ProductId { get; set; }
 [Required, StringLength(80)] public string Name { get; set; } = "";
 [Required, StringLength(20)] public string Unit { get; set; } = "chiếc";
 [Range(1000, 100000000)] public decimal Price { get; set; }
}
public record SupplierPriceRow(int Id, int SupplierId, string Supplier, bool SupplierActive, int? ProductId, string? Code, string Product, string Unit, decimal Price, bool IsMain);
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
// Phiếu hủy bánh (hết hạn, hỏng, làm lỗi…): khi được duyệt thì trừ khỏi tồn kho, giá trị tính theo giá bán lúc hủy.
// Mỗi dòng là một loại bánh; các dòng lập cùng lúc chung SlipId (số phiếu = Id dòng đầu) và được duyệt / từ chối cùng nhau.
public class Discard {
 public int SlipId { get; set; }
 public string Status { get; set; } = DiscardStatus.Pending;
 public string? ReviewedBy { get; set; }
 public DateTime? ReviewedAt { get; set; }
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
// Kiểm kê cuối ngày: số nhập / bán / hủy (đã duyệt) trong ngày, phiếu hủy còn chờ duyệt và tồn cuối ngày.
public record DailyRow(Product Product, int In, int Sold, int Wasted, decimal WastedValue, int PendingWaste, int Left);
public record DateFilter(int? Day, int? Month, int? Year);
// Property (không phải field) để Web API trả JSON và Swagger mô tả được cấu trúc.
public class DashboardVm {
 public decimal RevenueToday { get; set; } public decimal RevenueMonth { get; set; } public decimal PurchaseMonth { get; set; }
 public int NewOrders { get; set; } public int OpenOrders { get; set; } public int Customers { get; set; } public int Products { get; set; }
 public List<DayPoint> Days { get; set; } = [];
 public List<Order> Recent { get; set; } = [];
 public List<NamedValue> TopProducts { get; set; } = [];
}
public class ReportVm {
 public string Type { get; set; } = "range"; public string Title { get; set; } = ""; public string Month { get; set; } = ""; public string PeriodUnit { get; set; } = "ngày";
 public DateTime From { get; set; } public DateTime To { get; set; }
 public int Year { get; set; }
 public decimal Revenue { get; set; } public decimal AvgOrder { get; set; } public decimal WastedValue { get; set; }
 public int OrderCount { get; set; } public int Wasted { get; set; }
 public List<DayPoint> Series { get; set; } = [];
 public List<NamedValue> TopProducts { get; set; } = []; public List<NamedValue> TopCustomers { get; set; } = [];
 public List<DiscardPoint> Discards { get; set; } = [];
 // Thống kê đơn hàng trong kỳ (tính cả đơn đã hủy): Count = số đơn, Value = tổng tiền.
 public int AllOrders { get; set; } public int CancelledOrders { get; set; }
 public List<NamedValue> OrdersByStatus { get; set; } = []; public List<NamedValue> OrdersByPayment { get; set; } = []; public List<NamedValue> OrdersBySource { get; set; } = [];
}
public static class Fmt {
 static readonly System.Globalization.CultureInfo Vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
 public static string Vnd(decimal v) => v.ToString("N0", Vn) + " ₫";
 public static string Date(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Vn);
 public static string Day(DateTime d) => d.ToString("dd/MM/yyyy", Vn);
 public static string StatusClass(string s) => s switch { OrderStatus.New => "st-new", OrderStatus.Confirmed => "st-conf", OrderStatus.Preparing => "st-prep", OrderStatus.Shipping => "st-ship", OrderStatus.Done => "st-done", _ => "st-cancel" };
 public static string PurchaseClass(string s) => s switch { PurchaseStatus.Pending => "st-new", PurchaseStatus.Received => "st-done", _ => "st-cancel" };
}
