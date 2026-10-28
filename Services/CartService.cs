using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Services;
public class CartService(IHttpContextAccessor accessor,ShopDbContext db) {
 ISession Session => accessor.HttpContext!.Session;
 string CartKey => "cart:"+(accessor.HttpContext!.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??"guest");
 Dictionary<int,int> Read() { try { return JsonSerializer.Deserialize<Dictionary<int,int>>(Session.GetString(CartKey)??"{}")??[]; } catch { return []; } }
 public async Task<List<CartLine>> Lines() {
 var cart=Read();var products=await db.Products.AsNoTracking().Where(p=>cart.Keys.Contains(p.Id)).ToListAsync();
 return products.Select(p=>new CartLine(p,cart[p.Id])).ToList();
 }
 public async Task<bool> Change(int id,int quantity,bool add=false) {
 if(!await db.Products.AnyAsync(p=>p.Id==id)||quantity<0||quantity>99)return false;
 var cart=Read();if(add)quantity=Math.Min(99,cart.GetValueOrDefault(id)+quantity);
 if(quantity==0)cart.Remove(id);else cart[id]=quantity;
 Session.SetString(CartKey,JsonSerializer.Serialize(cart));return true;
 }
 public void Clear()=>Session.Remove(CartKey);
}
