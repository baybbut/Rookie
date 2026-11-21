using System.ComponentModel.DataAnnotations;
namespace Rookie.Models;
public class Product {
 public int Id { get; set; }
 [StringLength(20)] public string? Code { get; set; }
 [Required, StringLength(80)] public string Name { get; set; } = "";
 [Required, StringLength(200)] public string Description { get; set; } = "";
 [Required, StringLength(120)] public string Image { get; set; } = "";
 [Required, StringLength(40)] public string Category { get; set; } = "";
 [Required, StringLength(20)] public string Unit { get; set; } = "chiếc";
 // Giá bán = Giá nhập × (1 + Tỉ lệ lợi nhuận %), làm tròn tới 1.000 ₫ (ProductStatus.PriceOf); server tự tính khi lưu.
 [Range(1000, 100000000)] public decimal Price { get; set; }
 // Giá nhập cố định = giá trong bảng giá của xưởng chính (SupplierId); chỉ đổi qua bảng giá xưởng, không nhập tay khi sửa bánh.
 [Range(0, 100000000)] public decimal CostPrice { get; set; }
 public int? SupplierId { get; set; }
 [Range(0, 1000)] public decimal Margin { get; set; }
 public bool IsNew { get; set; }
 public bool IsBestSeller { get; set; }
 // Bánh bị ẩn không hiện trên trang bán bánh và không đặt được, nhưng vẫn giữ trong đơn/phiếu cũ.
 public bool IsHidden { get; set; }
 // Image là mã ảnh Unsplash, hoặc đường dẫn /uploads/... nếu ảnh được tải lên từ trang quản trị.
 public static string Url(string image, int w) => image.StartsWith("/uploads/") ? image : $"https://images.unsplash.com/{image}?auto=format&fit=crop&w={w}&q=85";
}
// Trạng thái bánh trên trang bán: chọn một trong ba, lưu vào hai cờ IsBestSeller/IsNew mà trang chủ đang dùng.
public static class ProductStatus {
 public static readonly (string Key, string Label)[] Options = [("", "Bình thường"), ("best", "Bán chạy nhất"), ("new", "Mới ra lò")];
 public static readonly string[] Units = ["chiếc", "hộp", "khay", "cái"];
 public static string Key(Product p) => p.IsBestSeller ? "best" : p.IsNew ? "new" : "";
 public static void Apply(Product p, string? key) => (p.IsBestSeller, p.IsNew) = (key == "best", key == "new");
 public static decimal PriceOf(decimal cost, decimal margin) => Math.Round(cost * (1 + margin / 100) / 1000, MidpointRounding.AwayFromZero) * 1000;
}
