using Microsoft.EntityFrameworkCore;
namespace Rookie.Data;
// Khởi tạo DB dùng chung cho web và app WinForms. Dự án không dùng EF migrations: khi đổi model thì tăng SchemaVersion,
// DB cũ được đổi tên thành *.bak-* rồi tạo lại (kèm dữ liệu mẫu).
public static class DbInit {
 public const int SchemaVersion = 2;
 public static void Prepare(ShopDbContext db, string? adminPassword, bool demo, Action<string>? log = null) {
  var cn = db.Database.GetDbConnection();
  if (File.Exists(cn.DataSource)) {
   long v; cn.Open(); using (var cmd = cn.CreateCommand()) { cmd.CommandText = "PRAGMA user_version"; v = (long)cmd.ExecuteScalar()!; } cn.Close();
   if (v < SchemaVersion) { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); var bak = $"{cn.DataSource}.bak-{DateTime.Now:yyyyMMddHHmmss}"; File.Move(cn.DataSource, bak); log?.Invoke($"Schema cũ: đã sao lưu {cn.DataSource} thành {bak} và tạo lại cơ sở dữ liệu."); }
  }
  db.Database.EnsureCreated(); db.Database.ExecuteSqlRaw($"PRAGMA user_version = {SchemaVersion}");
  DemoData.SeedStaff(db, adminPassword, demo); DemoData.Seed(db);
 }
}
