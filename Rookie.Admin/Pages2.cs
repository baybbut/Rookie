using System.Text.RegularExpressions;
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
  var st = status.SelectedIndex;
  var byCustomer = Api.Get<List<Order>>("api/admin/orders").GroupBy(o => o.CustomerId).ToDictionary(g => g.Key, g => g.ToList());
  var list = Api.Get<List<Customer>>("api/admin/customers").Where(c => Svc.Has(c.FullName, name.Text) && (Svc.Has(c.Email, contact.Text) || Svc.Has(c.Phone, contact.Text)) && (st == 0 || c.IsLocked == (st == 2))).ToList();
  G.Load(list, c => { var os = byCustomer.GetValueOrDefault(c.Id) ?? []; return [$"{c.FullName}  (tham gia {c.CreatedAt:dd/MM/yyyy})", $"{c.Email} · {c.Phone}{(string.IsNullOrEmpty(c.Address) ? "" : " · " + c.Address)}", os.Count, Fmt.Vnd(os.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.Total)), c.IsLocked ? "Đã khóa" : "Hoạt động"]; },
   (r, c) => Grid.Tint(r, 4, c.IsLocked ? Color.FromArgb(246, 220, 217) : Color.FromArgb(200, 232, 205))); Note($"{list.Count} khách hàng");
 }
 void Orders() { if (G.Pick<Customer>() is { } c) owner.OpenOrdersFor(c.Id); }
 void Lock() {
  if (G.Pick<Customer>() is not { } c) return;
  if (!Ui.Ask(c.IsLocked ? $"Mở khóa tài khoản “{c.FullName}”?" : $"Khóa tài khoản “{c.FullName}”?\nKhách sẽ không thể đăng nhập và đặt đơn mới khi còn bị khóa.")) return;
  var x = Api.Post<Customer>($"api/admin/customers/{c.Id}/lock"); Note(x.IsLocked ? $"Đã khóa tài khoản “{x.FullName}”." : $"Đã mở khóa “{x.FullName}”.");
 }
 void Edit(Customer? cur) {
  var c = cur ?? new Customer(); var isNew = cur is null;
  using var f = new FieldForm(isNew ? "Thêm khách hàng" : $"Sửa khách hàng — {c.FullName}", isNew ? "Thêm khách hàng" : "Lưu thay đổi", 320);
  var tName = f.Add("Họ và tên", FieldForm.Txt(c.FullName, 80)); var tMail = f.Add("Email", FieldForm.Txt(c.Email, 120)); var tPhone = f.Add("Số điện thoại", FieldForm.Txt(c.Phone, 20)); var tAddr = f.Add("Địa chỉ", FieldForm.Txt(c.Address, 200));
  var tPass = f.Add(isNew ? "Mật khẩu" : "Mật khẩu mới", FieldForm.Txt(null, 64, true)); f.AddWide(Ui.Lbl(isNew ? "Mật khẩu tối thiểu 6 ký tự." : "Để trống nếu không đổi mật khẩu. Mật khẩu cũ được lưu mã hóa nên không xem lại được.", Ui.Small, Ui.Muted));
  f.Check = () => {
   if (tName.Text.Trim() == "") return "Hãy nhập họ tên."; if (!Regex.IsMatch(tMail.Text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) return "Email không hợp lệ."; if (!Regex.IsMatch(tPhone.Text.Trim(), @"^[0-9+()\-\s.]{8,20}$")) return "Số điện thoại không hợp lệ.";
   if (isNew && tPass.Text == "") return "Hãy nhập mật khẩu cho khách hàng."; if (tPass.Text != "" && tPass.Text.Length < 6) return "Mật khẩu dài 6–64 ký tự.";
   // Trùng email do server kiểm tra.
   var body = new { fullName = tName.Text.Trim(), email = tMail.Text.Trim(), phone = tPhone.Text.Trim(), address = string.IsNullOrWhiteSpace(tAddr.Text) ? null : tAddr.Text.Trim(), password = tPass.Text == "" ? null : tPass.Text };
   return Ui.Try(() => { if (isNew) Api.Post("api/admin/customers", body); else Api.Put<Customer>($"api/admin/customers/{c.Id}", body); });
  };
  if (f.ShowDialog() == DialogResult.OK) Note($"Đã lưu khách hàng “{tName.Text.Trim()}”.");
 }
}

class OrdersPage : Page {
 readonly TextBox q; readonly DateTimePicker from, to; readonly ComboBox status; readonly Panel chip = new() { AutoSize = true, Visible = false, Margin = new Padding(0, 0, 0, 6) }; readonly Label chipText = Ui.Lbl("");
 readonly Button bConfirm, bNext, bCancel; int? customer;
 // Đơn khách đặt trên web: cứ 15 giây hỏi server, có đơn mới (mã lớn hơn) thì tải lại danh sách.
 readonly System.Windows.Forms.Timer poll = new() { Interval = 15000 }; int lastId;
 public OrdersPage() : base("Orders", "Quản lý đơn hàng") {
  q = Add("Tìm đơn hàng", new TextBox(), 230); from = Add("Từ ngày", new DateTimePicker(), 130); to = Add("Đến ngày", new DateTimePicker(), 130); status = Add("Trạng thái", Combo("Tất cả", OrderStatus.All)); ClearButton(() => customer = null);
  var cp = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 19, 0, 0) }; cp.Controls.Add(chipText); cp.Controls.Add(Ui.Btn("Bỏ lọc khách", () => { customer = null; Reload(); })); chip.Controls.Add(cp); Filters.Controls.Add(chip);
  G.Setup("Mã|0.45", "Khách hàng|1.7", "Thời gian|1.25", "Sản phẩm|2.4", ">Tổng|1", "Trạng thái|1", "Xác nhận bởi|1.3");
  bConfirm = Act("Xác nhận đơn", () => Advance(OrderStatus.Confirmed), primary: true); bNext = Act("Chuyển bước tiếp", () => { if (G.Pick<Order>() is { } o && OrderStatus.Next(o.Status) is { } n) Advance(n); });
  bCancel = Act("Hủy đơn", () => Advance(OrderStatus.Cancelled), danger: true); Act("Chi tiết", Detail, reload: false); G.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Detail(); }; G.SelectionChanged += (_, _) => Buttons();
  // Lỗi mạng khi tự kiểm tra thì bỏ qua (lần sau thử lại), không bật hộp thoại mỗi 15 giây.
  poll.Tick += (_, _) => { if (!Visible || Api.Token is null) return; try { if (Api.Get<List<Order>>($"api/admin/orders?status={Api.Q(OrderStatus.New)}").Select(o => o.Id).DefaultIfEmpty().Max() > lastId) Reload(); } catch (ApiException) { } }; poll.Start();
 }
 public void FilterCustomer(int id) { customer = id; Reload(); }
 void Buttons() { var s = G.Pick<Order>()?.Status; bConfirm.Enabled = s == OrderStatus.New; bNext.Enabled = s is not null && s != OrderStatus.New && OrderStatus.Next(s) is not null; bCancel.Enabled = s is not null && OrderStatus.CanCancel(s); }
 public override void Reload() {
  // Đơn mới nhất (mã lớn nhất) lên đầu.
  IEnumerable<Order> r = Api.Get<List<Order>>("api/admin/orders").OrderByDescending(o => o.Id).ToList();
  lastId = r.Select(o => o.Id).DefaultIfEmpty().Max();
  if (!string.IsNullOrWhiteSpace(q.Text)) { var t = q.Text.Trim().TrimStart('#'); r = r.Where(o => o.Id.ToString() == t || Svc.Has(o.Customer?.FullName, t) || Svc.Has(o.Customer?.Phone, t)); }
  if (customer is int cid) { r = r.Where(o => o.CustomerId == cid); chipText.Text = $"Đang lọc theo khách hàng:  {Api.Get<Customer>($"api/admin/customers/{cid}").FullName}   "; chip.Visible = true; } else chip.Visible = false;
  if (Pick(from) is { } a) r = r.Where(o => o.CreatedAt >= a); if (Pick(to) is { } b) r = r.Where(o => o.CreatedAt < b.AddDays(1)); if (Sel(status) is { } s) r = r.Where(o => o.Status == s);
  var list = r.ToList();
  G.Load(list, o => [$"#{o.Id}", $"{o.Customer?.FullName}  ({o.Customer?.Phone})", Fmt.Date(o.CreatedAt), $"{o.Items.Sum(i => i.Quantity)} món: {string.Join(", ", o.Items.Select(i => i.ProductName))}", Fmt.Vnd(o.Total), o.Status, o.ConfirmedBy ?? "—"], (row, o) => Grid.Tint(row, 5, Ui.OrderColor(o.Status)));
  Note($"{list.Count} đơn · tổng {Fmt.Vnd(list.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.Total))} (không tính đơn hủy)"); Buttons();
 }
 // Server kiểm tra đúng quy trình (Mới → Đã xác nhận → … ; hủy được tới trước khi hoàn thành) và ghi tên người xác nhận theo token.
 void Advance(string to) {
  if (G.Pick<Order>() is not { } o) return;
  if (!Ui.Ask(to == OrderStatus.Cancelled ? $"Hủy đơn #{o.Id}? Không thể mở lại sau khi hủy." : to == OrderStatus.Confirmed ? $"Xác nhận đơn #{o.Id}?\nBạn xác nhận đã kiểm tra thông tin khách và sản phẩm." : $"Chuyển đơn #{o.Id} sang “{to}”?")) return;
  var x = Api.Put<Order>($"api/admin/orders/{o.Id}/status", new { status = to });
  Note(to == OrderStatus.Confirmed ? $"{x.ConfirmedBy} đã xác nhận đơn #{x.Id}." : $"Đơn #{x.Id} → {x.Status}.");
 }
 void Detail() {
  if (G.Pick<Order>() is not { } o) return;
  using var f = new Form { Text = $"Đơn #{o.Id}", Font = Ui.Base, BackColor = Ui.Cream, StartPosition = FormStartPosition.CenterParent, Size = new Size(620, 520), MinimizeBox = false, Padding = new Padding(20) };
  var g = new Grid().Setup("Sản phẩm", ">Đơn giá", ">SL", ">Thành tiền"); g.Load(o.Items, i => [i.ProductName, Fmt.Vnd(i.UnitPrice), i.Quantity, Fmt.Vnd(i.Subtotal)]);
  var c = o.Customer; var info = Ui.Lbl($"{c?.FullName}\n{c?.Email} · {c?.Phone}\n{c?.Address}{(o.ShipAddress is null ? "" : $"\nGiao tới: {o.ShipName} · {o.ShipPhone} · {o.ShipAddress}")}\n\nĐặt lúc {Fmt.Date(o.CreatedAt)}{(o.CreatedBy is null ? "" : " · tạo bởi " + o.CreatedBy)}\nTrạng thái: {o.Status}{(o.ConfirmedBy is null ? "  (chưa nhân viên nào xác nhận)" : $"  ·  xác nhận bởi {o.ConfirmedBy}{(o.ConfirmedAt is { } t ? " lúc " + Fmt.Date(t) : "")}")}{(string.IsNullOrEmpty(o.Note) ? "" : "\nGhi chú: " + o.Note)}"); info.Dock = DockStyle.Top; info.AutoSize = true;
  var total = Ui.Lbl($"Tổng cộng: {Fmt.Vnd(o.Total)}", Ui.Big); total.Dock = DockStyle.Bottom; total.TextAlign = ContentAlignment.MiddleRight; total.AutoSize = false; total.Height = 40;
  f.Controls.Add(g); f.Controls.Add(total); f.Controls.Add(info); f.ShowDialog(this);
 }
}

// Chỉ hiện với vai trò "Quản trị"; dù vậy quyền thật do server kiểm tra (API nhân sự trả 403 cho "Nhân viên").
// Quy tắc "không tự khóa mình", "phải còn ít nhất một quản trị" cũng do server giữ.
class StaffPage : Page {
 readonly TextBox q; readonly ComboBox role, status;
 public StaffPage() : base("Staff", "Quản lý nhân sự") {
  q = Add("Tìm nhân sự", new TextBox(), 240); role = Add("Vai trò", Combo("Tất cả", StaffRole.All)); status = Add("Trạng thái", Combo("Tất cả", ["Hoạt động", "Đã khóa"])); ClearButton();
  G.Setup("Nhân sự", "Tên đăng nhập", "Vai trò", "Số điện thoại", "Trạng thái");
  Act("Thêm nhân sự", () => Edit(null), primary: true); Act("Sửa", () => { if (G.Pick<Staff>() is { } s) Edit(s); }); Act("Khóa / mở khóa", Lock);
 }
 public override void Reload() {
  var r = Sel(role); var st = status.SelectedIndex;
  var list = Api.Get<List<Staff>>("api/admin/staff").Where(s => (Svc.Has(s.FullName, q.Text) || Svc.Has(s.Username, q.Text) || Svc.Has(s.Phone, q.Text)) && (r is null || s.Role == r) && (st == 0 || s.IsLocked == (st == 2))).ToList();
  G.Load(list, s => [$"{s.FullName}{(s.Id == Session.User.Id ? "  (bạn)" : "")}", s.Username, s.Role, s.Phone ?? "—", s.IsLocked ? "Đã khóa" : "Hoạt động"], (row, s) => Grid.Tint(row, 4, s.IsLocked ? Color.FromArgb(246, 220, 217) : Color.FromArgb(200, 232, 205))); Note($"{list.Count} nhân sự");
 }
 void Lock() {
  if (G.Pick<Staff>() is not { } s) return;
  if (!Ui.Ask(s.IsLocked ? $"Mở khóa tài khoản “{s.FullName}”?" : $"Khóa tài khoản “{s.FullName}”?\nNgười này sẽ bị đăng xuất và không đăng nhập được nữa.")) return;
  var x = Api.Post<Staff>($"api/admin/staff/{s.Id}/lock"); Note(x.IsLocked ? $"Đã khóa tài khoản “{x.FullName}”." : $"Đã mở khóa “{x.FullName}”.");
 }
 void Edit(Staff? cur) {
  var s = cur ?? new Staff(); var isNew = cur is null;
  using var f = new FieldForm(isNew ? "Thêm nhân sự" : $"Sửa nhân sự — {s.FullName}", isNew ? "Thêm nhân sự" : "Lưu thay đổi", 300);
  var tName = f.Add("Họ và tên", FieldForm.Txt(s.FullName, 80)); var tUser = f.Add("Tên đăng nhập", FieldForm.Txt(s.Username, 30)); tUser.ReadOnly = !isNew; var tPhone = f.Add("Số điện thoại", FieldForm.Txt(s.Phone, 20));
  var cRole = f.Add("Vai trò", FieldForm.Combo(StaffRole.All, s.Role)); var tPass = f.Add(isNew ? "Mật khẩu" : "Mật khẩu mới", FieldForm.Txt(null, 64, true));
  f.AddWide(Ui.Lbl(isNew ? "Tên đăng nhập 3–30 ký tự (chữ thường, số, . hoặc _); mật khẩu tối thiểu 6 ký tự." : "Không đổi được tên đăng nhập. Để trống mật khẩu nếu không đổi.", Ui.Small, Ui.Muted));
  f.Check = () => {
   var u = tUser.Text.Trim().ToLowerInvariant();
   if (tName.Text.Trim() == "") return "Hãy nhập họ tên."; if (isNew && !Regex.IsMatch(u, "^[a-z0-9._]{3,30}$")) return "Tên đăng nhập 3–30 ký tự: chữ thường, số, dấu . hoặc _";
   if (tPhone.Text.Trim() != "" && !Regex.IsMatch(tPhone.Text.Trim(), @"^[0-9+()\-\s.]{8,20}$")) return "Số điện thoại không hợp lệ.";
   if (isNew && tPass.Text == "") return "Hãy nhập mật khẩu."; if (tPass.Text != "" && tPass.Text.Length < 6) return "Mật khẩu dài 6–64 ký tự.";
   var body = new { fullName = tName.Text.Trim(), username = isNew ? u : null, phone = string.IsNullOrWhiteSpace(tPhone.Text) ? null : tPhone.Text.Trim(), role = (string)cRole.SelectedItem!, password = tPass.Text == "" ? null : tPass.Text };
   return Ui.Try(() => { if (isNew) Api.Post("api/admin/staff", body); else Api.Put<Staff>($"api/admin/staff/{s.Id}", body); });
  };
  if (f.ShowDialog() != DialogResult.OK) return;
  Note($"Đã lưu nhân sự “{tName.Text.Trim()}”.");
  if (s.Id == Session.User.Id) Session.User.FullName = tName.Text.Trim(); // tự hạ quyền mình thì token cũ bị server từ chối → app tự đăng xuất
 }
}
