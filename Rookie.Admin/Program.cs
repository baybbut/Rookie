using Rookie.Models;
namespace Rookie.Admin;

// App quản trị không mở database: đăng nhập và mọi thao tác đi qua Web API, nên web (`dotnet run`) phải đang chạy.
static class Program {
 [STAThread]
 static void Main(string[] args) {
  ApplicationConfiguration.Initialize();
  // Lỗi khi tải trang (mất kết nối, hết phiên…) hiện thành hộp thoại thay vì làm app thoát.
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); Application.ThreadException += (_, e) => Ui.Err(e.Exception.Message);
  if (args is ["--screenshots", var dir]) { Shots(dir); return; }
  while (true) {
   using var login = new LoginForm(); if (login.ShowDialog() != DialogResult.OK) return;
   var main = new MainForm(); Application.Run(main); Api.Token = null; if (!main.LoggedOut) return;
  }
 }

 // Chế độ kiểm tra giao diện: `RookieAdmin.exe --screenshots <thư mục>` chụp từng trang rồi thoát.
 // Vẫn phải đăng nhập thật qua API bằng tài khoản trong biến môi trường ROOKIE_USER / ROOKIE_PASS (không có đường tắt bỏ qua mật khẩu).
 static void Shots(string dir) {
  try { LoginForm.SignIn(Environment.GetEnvironmentVariable("ROOKIE_USER") ?? "", Environment.GetEnvironmentVariable("ROOKIE_PASS") ?? ""); }
  catch (ApiException e) { Console.Error.WriteLine("Đăng nhập thất bại: " + e.Message); Environment.ExitCode = 1; return; }
  Directory.CreateDirectory(dir); using var m = new MainForm(); m.Show();
  foreach (var k in new[] { "dash", "cats", "prods", "custs", "orders", "stock", "staff", "reports" }.Where(k => k != "staff" || Session.IsAdmin)) {
   m.Go(k); Application.DoEvents(); Thread.Sleep(300); Application.DoEvents();
   using var bmp = new Bitmap(m.Width, m.Height); m.DrawToBitmap(bmp, new Rectangle(0, 0, m.Width, m.Height)); bmp.Save(Path.Combine(dir, $"{k}.png"));
  }

 }
}

class LoginForm : Form {
 readonly TextBox user = FieldForm.Txt(null, 30), pass = FieldForm.Txt(null, 64, true); readonly Label err = new() { AutoSize = true, ForeColor = Ui.Danger, Font = Ui.Base, MaximumSize = new Size(300, 0) };
 public LoginForm() {
  Text = "Rookie Admin — Đăng nhập"; Font = Ui.Base; BackColor = Ui.Cream; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(380, 400);
  var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(40, 28, 40, 20) };
  user.Width = pass.Width = 300; user.Margin = pass.Margin = new Padding(0, 4, 0, 14);
  var go = Ui.Btn("Vào quản trị", Submit, primary: true); go.Width = 300; go.Height = 40;
  p.Controls.AddRange([Ui.Lbl("rookie", Ui.Logo), Ui.Lbl("WELCOME BACK, BAKER", Ui.Small, Ui.Muted), Ui.Lbl("Đăng nhập quản trị", new Font("Georgia", 17f, FontStyle.Bold)), new Label { Height = 12 }, Ui.Lbl("TÊN ĐĂNG NHẬP", Ui.Small, Ui.Muted), user, Ui.Lbl("MẬT KHẨU", Ui.Small, Ui.Muted), pass, err, go]);
  Controls.Add(p); AcceptButton = go; Shown += (_, _) => user.Focus();
 }
 void Submit() {
  if (user.Text.Trim() == "" || pass.Text == "") { err.Text = "Hãy nhập tên đăng nhập và mật khẩu."; return; }
  try { SignIn(user.Text, pass.Text); DialogResult = DialogResult.OK; Close(); }
  catch (ApiException e) { err.Text = e.Message; pass.Clear(); pass.Focus(); }
 }
 // Server kiểm tra mật khẩu, tài khoản bị khóa và giới hạn số lần thử; trả về JWT chứa vai trò của nhân viên.
 public static void SignIn(string username, string password) {
  var r = Api.Post<LoginRes>("api/auth/staff", new { username = username.Trim(), password });
  Api.Token = r.Token; Session.User = new Staff { Id = r.Id, FullName = r.Name, Role = r.Role, Username = r.Username ?? "" };
 }
 record LoginRes(string Token, string Name, string Role, int Id, string? Username);
}

class MainForm : Form {
 public bool LoggedOut;
 readonly Panel content = new() { Dock = DockStyle.Fill, BackColor = Ui.Cream };
 readonly Dictionary<string, Page> pages = []; readonly Dictionary<string, Button> nav = [];
 public MainForm() {
  Text = "Rookie Admin — Quản trị tiệm bánh"; Font = Ui.Base; BackColor = Ui.Cream; StartPosition = FormStartPosition.CenterScreen; Size = new Size(1280, 800); MinimumSize = new Size(1000, 640);
  var side = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = Ui.Side, Padding = new Padding(14, 18, 14, 14) };
  var items = new List<(string Key, string Text)> { ("dash", "Trang chủ"), ("cats", "Quản lý danh mục"), ("prods", "Quản lý sản phẩm"), ("custs", "Quản lý khách hàng"), ("orders", "Quản lý đơn hàng"), ("stock", "Quản lý kho") };
  if (Session.IsAdmin) items.Add(("staff", "Quản lý nhân sự")); items.Add(("reports", "Báo cáo & thống kê"));
  var menu = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 12, 0, 0) };
  foreach (var (k, t) in items) { var b = new Button { Text = t, Width = 220, Height = 42, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), Font = Ui.Base, Cursor = Cursors.Hand, UseMnemonic = false, TabStop = false, Margin = new Padding(0, 0, 0, 3), UseVisualStyleBackColor = false }; b.FlatAppearance.BorderSize = 0; var key = k; b.Click += (_, _) => Go(key); nav[k] = b; menu.Controls.Add(b); }
  var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
  top.Controls.Add(Ui.Lbl("rookie", Ui.Logo)); top.Controls.Add(Ui.Lbl("ADMIN · QUẢN TRỊ TIỆM BÁNH", Ui.Small, Ui.Muted));
  var who = new Panel { Dock = DockStyle.Bottom, Height = 118, BackColor = Ui.Side };
  var card = new Panel { Left = 0, Top = 0, Width = 222, Height = 62, BackColor = Ui.Card, BorderStyle = BorderStyle.FixedSingle };
  var initial = Session.Name.Trim().Split(' ')[^1][..1].ToUpper();
  var av = new Label { Text = initial, Left = 10, Top = 12, Width = 36, Height = 36, TextAlign = ContentAlignment.MiddleCenter, BackColor = Ui.Ink, ForeColor = Ui.Cream, Font = Ui.Big }; av.Paint += (_, _) => { };
  var nm = new Label { Text = Session.Name, Left = 54, Top = 10, Width = 160, Height = 20, Font = Ui.Bold, AutoEllipsis = true }; var rl = new Label { Text = $"{Session.User.Role} · @{Session.User.Username}", Left = 54, Top = 32, Width = 160, Height = 18, ForeColor = Ui.Muted, AutoEllipsis = true };
  card.Controls.AddRange([av, nm, rl]);
  var out_ = Ui.Btn("Đăng xuất", () => { if (Ui.Ask("Đăng xuất khỏi hệ thống?")) { LoggedOut = true; Close(); } }); out_.Left = 0; out_.Top = 72; out_.Width = 222;
  who.Controls.AddRange([card, out_]);
  side.Controls.Add(menu); side.Controls.Add(who); side.Controls.Add(top); Controls.Add(content); Controls.Add(side);
  Shown += (_, _) => Go("dash");
  // Server từ chối token (hết hạn, bị khóa, bị đổi vai trò): đóng cửa sổ chính và quay về màn đăng nhập.
  Action expired = () => BeginInvoke(() => { LoggedOut = true; Close(); }); Api.Expired += expired; FormClosed += (_, _) => Api.Expired -= expired;
 }
 Page Make(string key) => key switch { "dash" => new DashboardPage(), "cats" => new CategoriesPage(), "prods" => new ProductsPage(), "custs" => new CustomersPage(this), "orders" => new OrdersPage(), "stock" => new WarehousePage(), "staff" => new StaffPage(), _ => new ReportsPage() };
 public Page Go(string key) {
  if (!pages.TryGetValue(key, out var p)) { p = pages[key] = Make(key); content.Controls.Add(p); }
  foreach (var (k, b) in nav) { var on = k == key; b.BackColor = on ? Ui.Ink : Ui.Side; b.ForeColor = on ? Ui.Cream : Ui.Ink; }
  foreach (var q in pages.Values) q.Visible = q == p; p.BringToFront(); p.OnShow(); return p;
 }
 public void OpenOrdersFor(int customerId) { var p = (OrdersPage)Go("orders"); p.FilterCustomer(customerId); }
}
