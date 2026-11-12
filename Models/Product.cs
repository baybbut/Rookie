using System.ComponentModel.DataAnnotations;
namespace Rookie.Models;
public class Product {
 public int Id { get; set; }
 [Required, StringLength(80)] public string Name { get; set; } = "";
 [Required, StringLength(200)] public string Description { get; set; } = "";
 [Required, StringLength(120)] public string Image { get; set; } = "";
 [Required, StringLength(40)] public string Category { get; set; } = "";
 [Range(1000, 100000000)] public decimal Price { get; set; }
 public bool IsNew { get; set; }
 public bool IsBestSeller { get; set; }
}