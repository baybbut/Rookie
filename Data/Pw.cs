using System.Security.Cryptography;
namespace Rookie.Data;
// Băm mật khẩu PBKDF2 (muối ngẫu nhiên) — định dạng "muối.băm" đều ở dạng base64.
public static class Pw {
 const int Iter = 100_000;
 public static string Hash(string p) { var salt = RandomNumberGenerator.GetBytes(16); return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(p, salt, Iter, HashAlgorithmName.SHA256, 32))}"; }
 public static bool Verify(string p, string? stored) {
  var a = stored?.Split('.');
  if (a is not { Length: 2 }) return false;
  try { return CryptographicOperations.FixedTimeEquals(Rfc2898DeriveBytes.Pbkdf2(p, Convert.FromBase64String(a[0]), Iter, HashAlgorithmName.SHA256, 32), Convert.FromBase64String(a[1])); }
  catch (FormatException) { return false; }
 }
}
