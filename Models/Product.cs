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
 [Range(1000, 100000000)] public decimal Price { get; set; }
 public bool IsNew { get; set; }
 public bool IsBestSeller { get; set; }
}
// Trạng thái bánh trên trang bán: chọn một trong ba, lưu vào hai cờ IsBestSeller/IsNew mà trang chủ đang dùng.
public static class ProductStatus {
 public static readonly (string Key, string Label)[] Options = [("", "Bình thường"), ("best", "Bán chạy nhất"), ("new", "Mới ra lò")];
 public static readonly string[] Units = ["chiếc", "hộp", "khay", "cái"];
 public static string Key(Product p) => p.IsBestSeller ? "best" : p.IsNew ? "new" : "";
 public static void Apply(Product p, string? key) => (p.IsBestSeller, p.IsNew) = (key == "best", key == "new");
}
