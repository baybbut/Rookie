using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rookie.Services;
namespace Rookie.Controllers.Api;
public record CartChangeReq(int ProductId, [Range(0, 99)] int Quantity);

// Giỏ hàng cho các React island trên trang Razor: dùng cookie đăng nhập của khách (không dùng JWT) và giỏ trong session
// qua CartService, nên trang đặt hàng Razor đọc đúng giỏ này. Vì xác thực bằng cookie nên POST/PUT VẪN kiểm tra
// chống CSRF (bộ lọc AutoValidateAntiforgeryToken toàn cục): island gửi token trong header RequestVerificationToken.
[ApiController, Route("api/cart"), Authorize(AuthenticationSchemes = Auth.Customer)]
public class CartApiController(CartService cart) : ControllerBase {
 [HttpGet] public async Task<object> Get() => Shape(await cart.Lines());

 // Cộng thêm vào giỏ (tối đa 99 mỗi bánh).
 [HttpPost] public async Task<IActionResult> Add(CartChangeReq r) => r.Quantity >= 1 && await cart.Change(r.ProductId, r.Quantity, true) ? Ok(Shape(await cart.Lines())) : BadRequest(new { error = "Không thêm được bánh này vào giỏ." });

 // Đặt lại số lượng; 0 = xóa khỏi giỏ.
 [HttpPut] public async Task<IActionResult> Set(CartChangeReq r) => await cart.Change(r.ProductId, r.Quantity) ? Ok(Shape(await cart.Lines())) : BadRequest(new { error = "Không cập nhật được giỏ." });

 static object Shape(List<Models.CartLine> lines) => new {
  lines = lines.OrderBy(l => l.Product.Id).Select(l => new { productId = l.Product.Id, name = l.Product.Name, image = l.Product.Image, unit = l.Product.Unit, price = l.Product.Price, quantity = l.Quantity, subtotal = l.Product.Price * l.Quantity }),
  count = lines.Sum(l => l.Quantity), total = lines.Sum(l => l.Product.Price * l.Quantity),
 };
}
