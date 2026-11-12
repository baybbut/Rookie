using Microsoft.EntityFrameworkCore;
using Rookie.Models;
namespace Rookie.Data;
public class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options) {
 public DbSet<Product> Products => Set<Product>();
 public DbSet<Category> Categories => Set<Category>();
 public DbSet<Customer> Customers => Set<Customer>();
 public DbSet<Order> Orders => Set<Order>();
 public DbSet<Purchase> Purchases => Set<Purchase>();
 protected override void OnModelCreating(ModelBuilder m) {
 m.Entity<Category>().HasIndex(c => c.Name).IsUnique();
 m.Entity<Category>().HasData(new Category {Id=1,Name="Bánh kem"}, new Category {Id=2,Name="Bánh ngọt"}, new Category {Id=3,Name="Bánh tart"}, new Category {Id=4,Name="Cookies"});
 m.Entity<Product>().HasData(
 new Product {Id=1,Name="Strawberry cloud",Description="Bông lan vani · kem tươi · dâu ngọt",Category="Bánh kem",Price=285000,IsBestSeller=true,Image="photo-1464305795204-6f5bbfc7fb81"},
 new Product {Id=2,Name="Butter, baby!",Description="Ngàn lớp giòn thơm, bơ Pháp nguyên chất",Category="Bánh ngọt",Price=45000,IsBestSeller=true,Image="photo-1555507036-ab1f4038808a"},
 new Product {Id=3,Name="Chocolate crush",Description="Chocolate đậm vị · một chút yêu chiều",Category="Bánh kem",Price=320000,IsBestSeller=true,Image="photo-1578985545062-69928b1d9587"},
 new Product {Id=4,Name="Berry little tart",Description="Tart bơ giòn · trái mọng theo mùa",Category="Bánh tart",Price=65000,IsNew=true,Image="photo-1488477181946-6428a0291777"},
 new Product {Id=5,Name="A little tiramisu",Description="Cà phê thơm · mascarpone mềm mịn",Category="Bánh ngọt",Price=75000,IsNew=true,Image="photo-1571877227200-a0d98ea607e9"},
 new Product {Id=6,Name="Cookie cuddle",Description="Cookie chocolate chip nướng mỗi ngày",Category="Cookies",Price=35000,IsNew=true,Image="photo-1499636136210-6f4ee915583e"});
 }
}