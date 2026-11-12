using System.ComponentModel.DataAnnotations;
namespace Rookie.Models;
public class Category {
 public int Id { get; set; }
 [Required, StringLength(40)] public string Name { get; set; } = "";
 public bool IsHidden { get; set; }
}
