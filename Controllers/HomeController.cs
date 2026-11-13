using Microsoft.AspNetCore.Mvc;
namespace Rookie.Controllers;
// Danh sách bánh trên trang chủ do React island (Products.jsx) tải qua GET /api/products.
public class HomeController : Controller {
 public IActionResult Index() => View();
}
