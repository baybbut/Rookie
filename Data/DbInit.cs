using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
namespace Rookie.Data;
// Khởi tạo DB SQL Server dùng chung cho web và app WinForms. Dự án không dùng EF migrations: khi đổi model thì tăng SchemaVersion;
// lần chạy sau DB cũ được BACKUP ra file .bak (thư mục backup mặc định của SQL Server) rồi xóa và tạo lại kèm dữ liệu mẫu.
public static class DbInit {
 public const int SchemaVersion = 3;
 public const string DefaultConnection = "Server=.;Database=RookieBakery;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

 public static void Prepare(ShopDbContext db, string? adminPassword, bool demo, Action<string>? log = null) {
  if (db.GetService<IRelationalDatabaseCreator>().Exists() && Version(db) is var (v, hasTables) && v < SchemaVersion && hasTables) {
   var name = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
   var file = $"{name}-{DateTime.Now:yyyyMMddHHmmss}.bak";
   db.Database.ExecuteSql($"BACKUP DATABASE {name} TO DISK = {file}");
   SqlConnection.ClearAllPools(); db.Database.EnsureDeleted();
   log?.Invoke($"Cấu trúc DB cũ (phiên bản {v}): đã backup {name} ra {file} trong thư mục backup của SQL Server rồi tạo lại.");
  }
  db.Database.EnsureCreated();
  db.Database.ExecuteSqlRaw("IF OBJECT_ID(N'dbo.__SchemaVersion') IS NULL CREATE TABLE dbo.__SchemaVersion (Version int NOT NULL); DELETE dbo.__SchemaVersion; INSERT dbo.__SchemaVersion VALUES ({0});", SchemaVersion);
  DemoData.SeedStaff(db, adminPassword, demo); DemoData.Seed(db);
 }

 // (phiên bản đã ghi, DB có bảng nào chưa); DB trống (vừa tạo tay) thì không cần backup.
 static (int, bool) Version(ShopDbContext db) {
  var cn = db.Database.GetDbConnection(); cn.Open();
  try {
   using var cmd = cn.CreateCommand();
   cmd.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.__SchemaVersion') IS NULL THEN 0 ELSE 1 END, (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0)";
   using (var r = cmd.ExecuteReader()) { r.Read(); if (r.GetInt32(0) == 0) return (0, r.GetInt32(1) > 0); }
   cmd.CommandText = "SELECT ISNULL(MAX(Version), 0) FROM dbo.__SchemaVersion";
   return ((int)cmd.ExecuteScalar()!, true);
  } finally { cn.Close(); }
 }
}
