using Microsoft.EntityFrameworkCore;
using Rookie.Models;
namespace Rookie.Data;
public class CommerceDbContext(DbContextOptions<CommerceDbContext> options):DbContext(options) {
 public DbSet<Customer> Customers => Set<Customer>();
 public DbSet<Order> Orders => Set<Order>();
 protected override void OnModelCreating(ModelBuilder m) {
 m.Entity<Customer>().HasIndex(c=>c.Email).IsUnique();
 m.Entity<Order>().HasIndex(o=>new{o.CustomerId,o.RequestKey}).IsUnique();
 m.Entity<Order>().Ignore(o=>o.Total);
 m.Entity<Order>().HasMany(o=>o.Items).WithOne().HasForeignKey(i=>i.OrderId);
 }
}
