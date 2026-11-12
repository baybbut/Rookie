using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Rookie.Data;
using Rookie.Models;
namespace Rookie.Admin;

class CustomersPage : Page {
 readonly MainForm owner; readonly TextBox name, contact; readonly ComboBox status;
 public CustomersPage(MainForm owner) : base("Customers", "Quản lý khách hàng") {
  this.owner = owner; name = Add("Họ tên", new TextBox(), 200); contact = Add("Email / số điện thoại", new TextBox(), 200); status = Add("Trạng thái", Combo("Tất cả", ["Hoạt động", "Đã khóa"])); ClearButton();
  G.Setup("Khách hàng|1.5", "Liên hệ|2.4", ">Đơn|0.5", ">Đã chi|1", "Trạng thái|0.9");
  Act("Thêm khách hàng", () => Edit(null), primary: true); Act("Sửa", () => { if (G.Pick<Customer>() is { } c) Edit(c); }); Act("Khóa / mở khóa", Lock); Act("Xem đơn hàng", Orders, reload: false);
 }
 public override void Reload() {
  using var db = Db.New(); var st = status.SelectedIndex;
  var list = db.Customers.AsNoTracking().Include(c => c.Orders).ThenInclude(o => o.Items).OrderBy(c => c.Id).ToList().Where(c => Svc.Has(c.FullName, name.Text) && (Svc.Has(c.Email, contact.Text) || Svc.Has(c.Phone, contact.Text)) && (st == 0 || c.IsLocked == (st == 2))).ToList();
  G.Load(list, c => [$"{c.FullName}  (tham gia {c.CreatedAt:dd/MM/yyyy})", $"{c.Email} · {c.Phone}{(string.IsNullOrEmpty(c.Address) ? "" : " · " + c.Address)}", c.Orders.Count, Fmt.Vnd(c.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.Total)), c.IsLocked ? "Đã khóa" : "Hoạt động"],
   (r, c) => Grid.Tint(r, 4, c.IsLocked ? Color.FromArgb(246, 220, 217) : Color.FromArgb(200, 232, 205))); Note($"{list.Count} khách hàng");
 }
 void Orders() { if (G.Pick<Customer>() is { } c) owner.OpenOrdersFor(c.Id); }
 void Lock() {
  if (G.Pick<Customer>() is not { } c) return;
  if (!Ui.Ask(c.IsLocked ? $"Mở khóa tài khoản “{c.FullName}”?" : $"Khóa tài khoản “{c.FullName}”?\nKhách sẽ không thể đặt đơn mới khi còn bị khóa.")) return;
  using var db = Db.New(); var x = db.Customers.Find(c.Id)!; x.IsLocked = !x.IsLocked; db.SaveChanges(); Note(x.IsLocked ? $"Đã khóa tài khoản “{x.FullName}”." : $"Đã mở khóa “{x.FullName}”.");
 }
 void Edit(Customer? cur) {
  using var db = Db.New(); var isNew = cur is null; var c = isNew ? new Customer() : db.Customers.Find(cur!.Id)!;
  using var f = new FieldForm(isNew ? "Thêm khách hàng" : $"Sửa khách hàng — {c.FullName}", isNew ? "Thêm khách hàng" : "Lưu thay đổi", 320);
  var tName = f.Add("Họ và tên", FieldForm.Txt(c.FullName, 80)); var tMail = f.Add("Email", FieldForm.Txt(c.Email, 120)); var tPhone = f.Add("Số điện thoại", FieldForm.Txt(c.Phone, 20)); var tAddr = f.Add("Địa chỉ", FieldForm.Txt(c.Address, 200));
  var tPass = f.Add(isNew ? "Mật khẩu" : "Mật khẩu mới", FieldForm.Txt(null, 64, true)); f.AddWide(Ui.Lbl(isNew ? "Mật khẩu tối thiểu 6 ký tự." : "Để trống nếu không đổi mật khẩu. Mật khẩu cũ được lưu mã hóa nên không xem lại được.", Ui.Small, Ui.Muted));
  f.Check = () => {
   var mail = tMail.Text.Trim();
   if (tName.Text.Trim() == "") return "Hãy nhập họ tên."; if (!Regex.IsMatch(mail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) return "Email không hợp lệ."; if (!Regex.IsMatch(tPhone.Text.Trim(), @"^[0-9+()\-\s.]{8,20}$")) return "Số điện thoại không hợp lệ.";
   if (db.Customers.Any(x => x.Email == mail && x.Id != c.Id)) return "Email này đã có khách hàng khác dùng.";
   if (isNew && tPass.Text == "") return "Hãy nhập mật khẩu cho khách hàng."; if (tPass.Text != "" && tPass.Text.Length < 6) return "Mật khẩu dài 6–64 ký tự."; return null;
  };
  if (f.ShowDialog() != DialogResult.OK) return;
  (c.FullName, c.Email, c.Phone, c.Address) = (tName.Text.Trim(), tMail.Text.Trim(), tPhone.Text.Trim(), string.IsNullOrWhiteSpace(tAddr.Text) ? null : tAddr.Text.Trim());
  if (tPass.Text != "") c.PasswordHash = Pw.Hash(tPass.Text); if (isNew) db.Customers.Add(c); db.SaveChanges(); Note($"Đã lưu khách hàng “{c.FullName}”.");
 }
}

class OrdersPage : Page {
 readonly TextBox q; readonly DateTimePicker from, to; readonly ComboBox status; readonly Panel chip = new() { AutoSize = true, Visible = false, Margin = new Padding(0, 0, 0, 6) }; readonly Label chipText = Ui.Lbl("");
 readonly Button bConfirm, bNext, bCancel; int? customer;
 public OrdersPage() : base("Orders", "Quản lý đơn hàng") {
  q = Add("Tìm đơn hàng", new TextBox(), 230); from = Add("Từ ngày", new DateTimePicker(), 130); to = Add("Đến ngày", new DateTimePicker(), 130); status = Add("Trạng thái", Combo("Tất cả", OrderStatus.All)); ClearButton(() => customer = null);
  var cp = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 19, 0, 0) }; cp.Controls.Add(chipText); cp.Controls.Add(Ui.Btn("Bỏ lọc khách", () => { customer = null; Reload(); })); chip.Controls.Add(cp); Filters.Controls.Add(chip);
  G.Setup("Mã|0.45", "Khách hàng|1.7", "Thời gian|1.25", "Sản phẩm|2.4", ">Tổng|1", "Trạng thái|1", "Xác nhận bởi|1.3");
  bConfirm = Act("Xác nhận đơn", () => Advance(OrderStatus.Confirmed, "Xác nhận"), primary: true); bNext = Act("Chuyển bước tiếp", () => { if (G.Pick<Order>() is { } o && OrderStatus.Next(o.Status) is { } n) Advance(n, "Chuyển"); });
  bCancel = Act("Hủy đơn", () => Advance(OrderStatus.Cancelled, "Hủy"), danger: true); Act("Chi tiết", Detail, reload: false); G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Detail(); }; G.SelectionChanged += (_, _) => Buttons();
 }
 public void FilterCustomer(int id) { customer = id; Reload(); }
 void Buttons() { var s = G.Pick<Order>()?.Status; bConfirm.Enabled = s == OrderStatus.New; bNext.Enabled = s is not null && s != OrderStatus.New && OrderStatus.Next(s) is not null; bCancel.Enabled = s is not null && OrderStatus.CanCancel(s); }
 public override void Reload() {
  using var db = Db.New(); IEnumerable<Order> r = db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Customer).OrderByDescending(o => o.CreatedAt).ToList();
  if (!string.IsNullOrWhiteSpace(q.Text)) { var t = q.Text.Trim().TrimStart('#'); r = r.Where(o => o.Id.ToString() == t || Svc.Has(o.Customer?.FullName, t) || Svc.Has(o.Customer?.Phone, t)); }
  if (customer is int cid) { r = r.Where(o => o.CustomerId == cid); chipText.Text = $"Đang lọc theo khách hàng:  {db.Customers.Find(cid)?.FullName ?? "#" + cid}   "; chip.Visible = true; } else chip.Visible = false;
  if (Pick(from) is { } a) r = r.Where(o => o.CreatedAt >= a); if (Pick(to) is { } b) r = r.Where(o => o.CreatedAt < b.AddDays(1)); if (Sel(status) is { } s) r = r.Where(o => o.Status == s);
  var list = r.ToList();
  G.Load(list, o => [$"#{o.Id}", $"{o.Customer?.FullName}  ({o.Customer?.Phone})", Fmt.Date(o.CreatedAt), $"{o.Items.Sum(i => i.Quantity)} món: {string.Join(", ", o.Items.Select(i => i.ProductName))}", Fmt.Vnd(o.Total), o.Status, o.ConfirmedBy ?? "—"], (row, o) => Grid.Tint(row, 5, Ui.OrderColor(o.Status)));
  Note($"{list.Count} đơn · tổng {Fmt.Vnd(list.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.Total))} (không tính đơn hủy)"); Buttons();
 }
 // Nhân viên xác nhận đơn = "Mới" → "Đã xác nhận"; các bước sau đi tuần tự, hủy được tới trước khi hoàn thành.
 void Advance(string to, string verb) {
  if (G.Pick<Order>() is not { } sel) return; using var db = Db.New(); var o = db.Orders.Find(sel.Id)!;
  if (!OrderStatus.Allowed(o.Status, to)) { Ui.Err($"Không thể chuyển đơn #{o.Id} từ “{o.Status}” sang “{to}”."); return; }
  if (!Ui.Ask(to == OrderStatus.Cancelled ? $"Hủy đơn #{o.Id}? Không thể mở lại sau khi hủy." : to == OrderStatus.Confirmed ? $"Xác nhận đơn #{o.Id}?\nBạn xác nhận đã kiểm tra thông tin khách và sản phẩm." : $"Chuyển đơn #{o.Id} sang “{to}”?")) return;
  o.Status = to; if (to == OrderStatus.Confirmed) (o.ConfirmedBy, o.ConfirmedAt) = (Session.Name, DateTime.Now); db.SaveChanges();
  Note(to == OrderStatus.Confirmed ? $"{Session.Name} đã xác nhận đơn #{o.Id}." : $"Đơn #{o.Id} → {to}.");
 }
 void Detail() {
  if (G.Pick<Order>() is not { } o) return;
  using var f = new Form { Text = $"Đơn #{o.Id}", Font = Ui.Base, BackColor = Ui.Cream, StartPosition = FormStartPosition.CenterParent, Size = new Size(620, 520), MinimizeBox = false, Padding = new Padding(20) };
  var g = new Grid().Setup("Sản phẩm", ">Đơn giá", ">SL", ">Thành tiền"); g.Load(o.Items, i => [i.ProductName, Fmt.Vnd(i.UnitPrice), i.Quantity, Fmt.Vnd(i.Subtotal)]);
  var c = o.Customer; var info = Ui.Lbl($"{c?.FullName}\n{c?.Email} · {c?.Phone}\n{c?.Address}\n\nĐặt lúc {Fmt.Date(o.CreatedAt)}{(o.CreatedBy is null ? "" : " · tạo bởi " + o.CreatedBy)}\nTrạng thái: {o.Status}{(o.ConfirmedBy is null ? "  (chưa nhân viên nào xác nhận)" : $"  ·  xác nhận bởi {o.ConfirmedBy}{(o.ConfirmedAt is { } t ? " lúc " + Fmt.Date(t) : "")}")}{(string.IsNullOrEmpty(o.Note) ? "" : "\nGhi chú: " + o.Note)}"); info.Dock = DockStyle.Top; info.AutoSize = true;
  var total = Ui.Lbl($"Tổng cộng: {Fmt.Vnd(o.Total)}", Ui.Big); total.Dock = DockStyle.Bottom; total.TextAlign = ContentAlignment.MiddleRight; total.AutoSize = false; total.Height = 40;
  f.Controls.Add(g); f.Controls.Add(total); f.Controls.Add(info); f.ShowDialog(this);
 }
}

class StaffPage : Page {
 readonly TextBox q; readonly ComboBox role, status;
 public StaffPage() : base("Staff", "Quản lý nhân sự") {
  q = Add("Tìm nhân sự", new TextBox(), 240); role = Add("Vai trò", Combo("Tất cả", StaffRole.All)); status = Add("Trạng thái", Combo("Tất cả", ["Hoạt động", "Đã khóa"])); ClearButton();
  G.Setup("Nhân sự", "Tên đăng nhập", "Vai trò", "Số điện thoại", "Trạng thái");
  Act("Thêm nhân sự", () => Edit(null), primary: true); Act("Sửa", () => { if (G.Pick<Staff>() is { } s) Edit(s); }); Act("Khóa / mở khóa", Lock);
 }
 public override void Reload() {
  using var db = Db.New(); var r = Sel(role); var st = status.SelectedIndex;
  var list = db.Staffs.AsNoTracking().OrderBy(s => s.Id).ToList().Where(s => (Svc.Has(s.FullName, q.Text) || Svc.Has(s.Username, q.Text) || Svc.Has(s.Phone, q.Text)) && (r is null || s.Role == r) && (st == 0 || s.IsLocked == (st == 2))).ToList();
  G.Load(list, s => [$"{s.FullName}{(s.Id == Session.User.Id ? "  (bạn)" : "")}", s.Username, s.Role, s.Phone ?? "—", s.IsLocked ? "Đã khóa" : "Hoạt động"], (row, s) => Grid.Tint(row, 4, s.IsLocked ? Color.FromArgb(246, 220, 217) : Color.FromArgb(200, 232, 205))); Note($"{list.Count} nhân sự");
 }
 static bool OtherAdmin(ShopDbContext db, int id) => db.Staffs.Any(s => s.Id != id && s.Role == StaffRole.Admin && !s.IsLocked);
 void Lock() {
  if (G.Pick<Staff>() is not { } c) return; using var db = Db.New(); var s = db.Staffs.Find(c.Id)!;
  if (!s.IsLocked && s.Id == Session.User.Id) { Ui.Err("Bạn không thể tự khóa tài khoản đang đăng nhập."); return; }
  if (!s.IsLocked && s.Role == StaffRole.Admin && !OtherAdmin(db, s.Id)) { Ui.Err("Phải còn ít nhất một người quản trị đang hoạt động."); return; }
  if (!Ui.Ask(s.IsLocked ? $"Mở khóa tài khoản “{s.FullName}”?" : $"Khóa tài khoản “{s.FullName}”?\nNgười này sẽ không đăng nhập được nữa.")) return;
  s.IsLocked = !s.IsLocked; db.SaveChanges(); Note(s.IsLocked ? $"Đã khóa tài khoản “{s.FullName}”." : $"Đã mở khóa “{s.FullName}”.");
 }
 void Edit(Staff? cur) {
  using var db = Db.New(); var isNew = cur is null; var s = isNew ? new Staff() : db.Staffs.Find(cur!.Id)!;
  using var f = new FieldForm(isNew ? "Thêm nhân sự" : $"Sửa nhân sự — {s.FullName}", isNew ? "Thêm nhân sự" : "Lưu thay đổi", 300);
  var tName = f.Add("Họ và tên", FieldForm.Txt(s.FullName, 80)); var tUser = f.Add("Tên đăng nhập", FieldForm.Txt(s.Username, 30)); tUser.ReadOnly = !isNew; var tPhone = f.Add("Số điện thoại", FieldForm.Txt(s.Phone, 20));
  var cRole = f.Add("Vai trò", FieldForm.Combo(StaffRole.All, s.Role)); var tPass = f.Add(isNew ? "Mật khẩu" : "Mật khẩu mới", FieldForm.Txt(null, 64, true));
  f.AddWide(Ui.Lbl(isNew ? "Tên đăng nhập 3–30 ký tự (chữ thường, số, . hoặc _); mật khẩu tối thiểu 6 ký tự." : "Không đổi được tên đăng nhập. Để trống mật khẩu nếu không đổi.", Ui.Small, Ui.Muted));
  f.Check = () => {
   var u = tUser.Text.Trim().ToLowerInvariant();
   if (tName.Text.Trim() == "") return "Hãy nhập họ tên."; if (isNew && !Regex.IsMatch(u, "^[a-z0-9._]{3,30}$")) return "Tên đăng nhập 3–30 ký tự: chữ thường, số, dấu . hoặc _"; if (isNew && db.Staffs.Any(x => x.Username == u)) return "Tên đăng nhập đã tồn tại.";
   if (tPhone.Text.Trim() != "" && !Regex.IsMatch(tPhone.Text.Trim(), @"^[0-9+()\-\s.]{8,20}$")) return "Số điện thoại không hợp lệ.";
   if (isNew && tPass.Text == "") return "Hãy nhập mật khẩu."; if (tPass.Text != "" && tPass.Text.Length < 6) return "Mật khẩu dài 6–64 ký tự.";
   if (!isNew && s.Role == StaffRole.Admin && (string)cRole.SelectedItem! != StaffRole.Admin && !OtherAdmin(db, s.Id)) return "Phải còn ít nhất một người quản trị đang hoạt động."; return null;
  };
  if (f.ShowDialog() != DialogResult.OK) return;
  (s.FullName, s.Phone, s.Role) = (tName.Text.Trim(), string.IsNullOrWhiteSpace(tPhone.Text) ? null : tPhone.Text.Trim(), (string)cRole.SelectedItem!); if (isNew) s.Username = tUser.Text.Trim().ToLowerInvariant();
  if (tPass.Text != "") s.PasswordHash = Pw.Hash(tPass.Text); if (isNew) db.Staffs.Add(s); db.SaveChanges(); Note($"Đã lưu nhân sự “{s.FullName}”.");
  if (s.Id == Session.User.Id) Session.User = s;
 }
}
