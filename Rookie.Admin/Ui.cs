using System.Drawing;
using Rookie.Models;
namespace Rookie.Admin;

// Bảng màu/kiểu chữ lấy theo giao diện web (kem · mực · hồng · bạc hà) và vài hàm dựng control dùng chung. Không dùng icon.
static class Ui {
 public static readonly Color Cream = Color.FromArgb(252, 247, 236), Ink = Color.FromArgb(38, 41, 31), Side = Color.FromArgb(244, 232, 223), Card = Color.FromArgb(255, 253, 246), Line = Color.FromArgb(231, 224, 208),
  Muted = Color.FromArgb(109, 112, 98), Pink = Color.FromArgb(234, 214, 212), Mint = Color.FromArgb(223, 231, 214), Danger = Color.FromArgb(173, 84, 79), Border = Color.FromArgb(207, 207, 192);
 public static readonly Font Base = new("Segoe UI", 9.5f), Bold = new("Segoe UI Semibold", 9.5f), Small = new("Segoe UI Semibold", 7.5f), Title = new("Georgia", 24f, FontStyle.Bold), Big = new("Segoe UI Semibold", 15f), Logo = new("Georgia", 21f, FontStyle.Bold);

 public static Button Btn(string text, Action click, bool primary = false, bool danger = false) {
  var b = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, Font = Bold, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand, MinimumSize = new Size(0, 34), BackColor = primary ? Ink : Card, ForeColor = primary ? Cream : danger ? Danger : Ink, UseVisualStyleBackColor = false };
  b.FlatAppearance.BorderColor = primary ? Ink : danger ? Color.FromArgb(226, 188, 184) : Border;
  b.Click += (_, _) => { try { click(); } catch (Exception e) { Err(e.Message); } };
  return b;
 }
 public static Label Lbl(string t, Font? f = null, Color? c = null) => new() { Text = t, AutoSize = true, Font = f ?? Base, ForeColor = c ?? Ink, Margin = new Padding(0), BackColor = Color.Transparent, UseMnemonic = false };
 public static void Info(string m) => MessageBox.Show(m, "Rookie", MessageBoxButtons.OK, MessageBoxIcon.Information);
 public static void Err(string m) => MessageBox.Show(m, "Rookie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
 public static bool Ask(string m) => MessageBox.Show(m, "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
 // Dùng trong FieldForm.Check: gọi API lưu; lỗi từ server (trùng tên, sai dữ liệu…) hiện ngay trong hộp thoại thay vì đóng hộp thoại.
 public static string? Try(Action save) { try { save(); return null; } catch (ApiException e) { return e.Message; } }

 public static Color OrderColor(string s) => s switch { OrderStatus.New => Color.FromArgb(247, 227, 181), OrderStatus.Confirmed => Color.FromArgb(227, 220, 241), OrderStatus.Preparing => Mint, OrderStatus.Shipping => Color.FromArgb(212, 227, 238), OrderStatus.Done => Color.FromArgb(200, 232, 205), _ => Color.FromArgb(246, 220, 217) };
 public static Color PaymentColor(string s) => s switch { PaymentStatus.Unpaid => Color.FromArgb(247, 227, 181), PaymentStatus.Pending => Color.FromArgb(227, 220, 241), PaymentStatus.Paid => Color.FromArgb(200, 232, 205), _ => Color.FromArgb(246, 220, 217) };
 public static Color DiscardColor(string s) => s switch { DiscardStatus.Pending => Color.FromArgb(247, 227, 181), DiscardStatus.Approved => Color.FromArgb(200, 232, 205), _ => Color.FromArgb(246, 220, 217) };
 public static Color PurchaseColor(string s) => s switch { PurchaseStatus.Pending => Color.FromArgb(247, 227, 181), PurchaseStatus.Received => Color.FromArgb(200, 232, 205), _ => Color.FromArgb(246, 220, 217) };
}

// Hộp thoại CHỈ XEM (không sửa được): ảnh (tùy chọn) + các dòng "nhãn — giá trị" + bảng (tùy chọn) + nút In (tùy chọn).
static class Viewer {
 public static void Show(IWin32Window? owner, string title, IEnumerable<(string Label, string? Value)> rows, string? image = null, Grid? grid = null, Func<System.Drawing.Printing.PrintDocument>? print = null) {
  using var f = new Form { Text = title, Font = Ui.Base, BackColor = Ui.Cream, StartPosition = FormStartPosition.CenterParent, Size = new Size(760, grid is null ? 480 : 640), MinimizeBox = false, Padding = new Padding(20, 16, 20, 12) };
  var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0) }; t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, image is null ? 540 : 300));
  foreach (var (l, v) in rows) { var a = Ui.Lbl(l, Ui.Bold, Ui.Muted); a.Margin = new Padding(0, 3, 8, 3); var b = Ui.Lbl(string.IsNullOrWhiteSpace(v) ? "—" : v); b.MaximumSize = new Size(image is null ? 540 : 300, 0); b.Margin = new Padding(0, 3, 0, 3); t.Controls.Add(a); t.Controls.Add(b); }
  var head = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
  if (image is not null) { var pic = new PictureBox { Width = 260, Height = 195, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Ui.Card, Margin = new Padding(0, 0, 18, 0) }; pic.LoadAsync(image); head.Controls.Add(pic); }
  head.Controls.Add(t);
  var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
  bar.Controls.Add(Ui.Btn("Đóng", f.Close)); if (print is not null) bar.Controls.Add(Ui.Btn("In phiếu", () => PrintDoc.Preview(f, print()), primary: true));
  if (grid is not null) f.Controls.Add(grid); f.Controls.Add(bar); f.Controls.Add(head); f.ShowDialog(owner);
 }
 // Ảnh bánh: mã Unsplash → URL Unsplash; ảnh tải lên (/uploads/...) → lấy từ server web.
 public static string ImageUrl(Product p) { var u = Product.Url(p.Image, 600); return u.StartsWith('/') ? Api.Base + u : u; }
}

// Dàn trang in: mỗi dòng ba cột (trái · giữa căn phải · phải căn phải), tự sang trang khi hết chỗ. Style: 0 thường · 1 đậm · 2 tiêu đề lớn · 3 đầu mục.
record PLine(string A, string B = "", string C = "", int Style = 0);
static class PrintDoc {
 public static System.Drawing.Printing.PrintDocument Build(string name, List<PLine> L) {
  var doc = new System.Drawing.Printing.PrintDocument { DocumentName = name }; var i = 0; doc.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(60, 60, 60, 60);
  var fonts = new[] { new Font("Segoe UI", 10f), new Font("Segoe UI", 10f, FontStyle.Bold), new Font("Georgia", 18f, FontStyle.Bold), new Font("Segoe UI", 12f, FontStyle.Bold) };
  doc.PrintPage += (_, e) => {
   var g = e.Graphics!; var m = e.MarginBounds; float y = m.Top; var right = new StringFormat { Alignment = StringAlignment.Far };
   while (i < L.Count) {
    var ln = L[i]; var f = fonts[ln.Style]; var hgt = f.GetHeight(g) + (ln.Style == 3 ? 8 : 4); if (y + hgt > m.Bottom) { e.HasMorePages = true; return; }
    g.DrawString(ln.A, f, Brushes.Black, m.Left, y + (ln.Style == 3 ? 6 : 0));
    if (ln.B != "") g.DrawString(ln.B, f, Brushes.Black, m.Left + m.Width * 0.62f, y, right); if (ln.C != "") g.DrawString(ln.C, f, Brushes.Black, m.Right, y, right);
    if (ln.Style == 3) g.DrawLine(Pens.Gray, m.Left, y + hgt - 1, m.Right, y + hgt - 1);
    y += hgt; i++;
   }
   e.HasMorePages = false; i = 0;
  };
  return doc;
 }
 // Xem trước rồi bấm nút máy in trên thanh công cụ để in (hoặc chọn "Microsoft Print to PDF").
 public static void Preview(IWin32Window owner, System.Drawing.Printing.PrintDocument doc) { using var pv = new PrintPreviewDialog { Document = doc, WindowState = FormWindowState.Maximized }; pv.ShowDialog(owner); }
}

// Bảng dữ liệu chỉ-đọc: mỗi dòng giữ đối tượng gốc trong Tag; tiêu đề bắt đầu bằng '>' thì căn phải.
class Grid : DataGridView {
 public Grid() {
  ReadOnly = true; AllowUserToAddRows = false; AllowUserToDeleteRows = false; AllowUserToResizeRows = false; RowHeadersVisible = false; MultiSelect = false;
  SelectionMode = DataGridViewSelectionMode.FullRowSelect; AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; BackgroundColor = Ui.Card; BorderStyle = BorderStyle.None;
  EnableHeadersVisualStyles = false; GridColor = Ui.Line; CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; Dock = DockStyle.Fill; Font = Ui.Base; RowTemplate.Height = 34;
  ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; ColumnHeadersHeight = 38; ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
  ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Ui.Card, ForeColor = Ui.Muted, Font = Ui.Small, Padding = new Padding(6, 0, 6, 0), SelectionBackColor = Ui.Card, SelectionForeColor = Ui.Muted };
  DefaultCellStyle = new DataGridViewCellStyle { BackColor = Ui.Card, ForeColor = Ui.Ink, SelectionBackColor = Ui.Pink, SelectionForeColor = Ui.Ink, Padding = new Padding(6, 0, 6, 0) };
 }
 public Grid Setup(params string[] heads) {
  Columns.Clear();
  foreach (var h0 in heads) { var parts = h0.Split('|'); var h = parts[0]; var c = new DataGridViewTextBoxColumn { HeaderText = h.TrimStart('>').ToUpperInvariant(), SortMode = DataGridViewColumnSortMode.NotSortable, FillWeight = parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) * 100 : 100 }; if (h.StartsWith('>')) { c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight; } Columns.Add(c); }
  return this;
 }
 public void Load<T>(IEnumerable<T> items, Func<T, object?[]> cells, Action<DataGridViewRow, T>? style = null) where T : class {
  var sel = CurrentRow?.Index ?? -1; SuspendLayout(); Rows.Clear();
  foreach (var t in items) { var i = Rows.Add(cells(t)); Rows[i].Tag = t; style?.Invoke(Rows[i], t); }
  if (Rows.Count > 0) { var k = Math.Clamp(sel, 0, Rows.Count - 1); Rows[k].Selected = true; CurrentCell = Rows[k].Cells[0]; }
  ResumeLayout();
 }
 public T? Pick<T>() where T : class => CurrentRow?.Tag as T;
 public static void Tint(DataGridViewRow r, int col, Color c) { r.Cells[col].Style.BackColor = c; r.Cells[col].Style.SelectionBackColor = c; }
}

// Biểu đồ cột đơn giản (doanh thu theo ngày/tháng); di chuột lên cột để xem chi tiết.
class BarChart : Control {
 List<(string Label, decimal Value, string Tip)> bars = [];
 readonly ToolTip tip = new(); int lastHover = -1;
 public BarChart() { DoubleBuffered = true; BackColor = Ui.Card; Font = new Font("Segoe UI", 7.5f); }
 public void Set(IEnumerable<(string, decimal, string)> data) { bars = data.ToList(); Invalidate(); }
 (int Left, int Top, int W, int H, float Step) Area() { var step = bars.Count == 0 ? 1f : (Width - 24f) / bars.Count; return (12, 8, Width - 24, Height - 34, step); }
 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; if (bars.Count == 0) return;
  var (l, t, w, h, step) = Area(); var max = Math.Max(1m, bars.Max(b => b.Value)); var skip = bars.Count > 31 ? 10 : bars.Count > 12 ? 5 : bars.Count > 7 ? 2 : 1;
  using var fill = new SolidBrush(Ui.Pink); using var txt = new SolidBrush(Ui.Muted); using var axis = new Pen(Ui.Line);
  g.DrawLine(axis, l, t + h, l + w, t + h);
  for (var i = 0; i < bars.Count; i++) {
   var bh = (int)(h * (double)(bars[i].Value / max)); var bw = Math.Max(2f, step - 4);
   g.FillRectangle(fill, l + i * step + 2, t + h - Math.Max(bh, 2), bw, Math.Max(bh, 2));
   if (i % skip == 0) { var s = bars[i].Label.StartsWith("Tháng ") ? bars[i].Label[6..] : bars[i].Label; var sz = g.MeasureString(s, Font); g.DrawString(s, Font, txt, Math.Clamp(l + i * step + step / 2 - sz.Width / 2, 0, Width - sz.Width), t + h + 5); }
  }
 }
 protected override void OnMouseMove(MouseEventArgs e) {
  base.OnMouseMove(e); if (bars.Count == 0) return; var (l, _, _, _, step) = Area(); var i = (int)((e.X - l) / step);
  if (i != lastHover) { lastHover = i; if (i >= 0 && i < bars.Count) tip.SetToolTip(this, bars[i].Tip); else tip.SetToolTip(this, null); }
 }
}

// Hộp thoại nhập liệu: mỗi dòng "nhãn + control"; Validate trả về thông báo lỗi (hộp thoại giữ nguyên) hoặc null để đóng.
class FieldForm : Form {
 readonly TableLayoutPanel t = new() { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 4) };
 readonly Label err = new() { AutoSize = true, ForeColor = Ui.Danger, Font = Ui.Base, MaximumSize = new Size(440, 0), Margin = new Padding(0, 4, 0, 4) };
 public Func<string?>? Check;
 public FieldForm(string title, string okText = "Lưu", int fieldWidth = 300) {
  Text = title; Font = Ui.Base; BackColor = Ui.Cream; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Padding = new Padding(20, 16, 20, 14);
  t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, fieldWidth));
  var ok = Ui.Btn(okText, Submit, primary: true); var cancel = Ui.Btn("Hủy", () => { DialogResult = DialogResult.Cancel; Close(); });
  var bar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0), Padding = new Padding(130, 6, 0, 0) }; bar.Controls.AddRange([ok, cancel]);
  var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill }; root.Controls.AddRange([t, err, bar]);
  Controls.Add(root); AcceptButton = null; CancelButton = null;
 }
 void Submit() { var e = Check?.Invoke(); if (e is not null) { err.Text = e; return; } DialogResult = DialogResult.OK; Close(); }
 public T Add<T>(string label, T c) where T : Control {
  var r = t.RowCount++; t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
  t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 9, 8, 0), ForeColor = Ui.Muted }, 0, r);
  c.Dock = DockStyle.Fill; c.Margin = new Padding(0, 3, 0, 3); t.Controls.Add(c, 1, r); return c;
 }
 public void AddWide(Control c) { var r = t.RowCount++; t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.Controls.Add(c, 0, r); t.SetColumnSpan(c, 2); }
 public static TextBox Txt(string? v = null, int max = 200, bool pwd = false) => new() { Text = v ?? "", MaxLength = max, UseSystemPasswordChar = pwd, BorderStyle = BorderStyle.FixedSingle };
 public static ComboBox Combo(IEnumerable<object> items, object? sel = null) { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat }; c.Items.AddRange(items.ToArray()); c.SelectedItem = sel; if (c.SelectedIndex < 0 && c.Items.Count > 0) c.SelectedIndex = 0; return c; }
 public static NumericUpDown Num(decimal v, decimal min, decimal max, decimal step = 1) => new() { Minimum = min, Maximum = max, Increment = step, Value = Math.Clamp(v, min, max), ThousandsSeparator = true, TextAlign = HorizontalAlignment.Right };
}

// Trang nội dung: tiêu đề + nút thao tác (phải) · thanh lọc · bảng · dòng thông báo. Bộ lọc tự tải lại khi thay đổi.
abstract class Page : UserControl {
 protected readonly Grid G = new(); protected readonly FlowLayoutPanel Filters = new() { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0, 0, 0, 8) };
 readonly FlowLayoutPanel actions = new() { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Margin = new Padding(0, 0, 0, 6) };
 readonly TableLayoutPanel root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Ui.Cream };
 readonly Label msg = new() { AutoSize = true, ForeColor = Color.FromArgb(60, 110, 70), Font = Ui.Base, Margin = new Padding(0, 6, 0, 0) };
 bool quiet = true;
 protected Page(string? eyebrow, string? title) {
  Dock = DockStyle.Fill; BackColor = Ui.Cream; root.Padding = new Padding(title is null ? 0 : 24, title is null ? 8 : 18, title is null ? 0 : 24, 8);
  root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
  var head = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 10) }; head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
  var left = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
  if (title is not null) { left.Controls.Add(Ui.Lbl((eyebrow ?? "").ToUpperInvariant(), Ui.Small, Ui.Muted)); left.Controls.Add(Ui.Lbl(title, Ui.Title)); }
  head.Controls.Add(left, 0, 0); head.Controls.Add(actions, 1, 0);
  root.Controls.Add(head, 0, 0); root.Controls.Add(Filters, 0, 1); root.Controls.Add(G, 0, 2); root.Controls.Add(msg, 0, 3); Controls.Add(root);
 }
 protected void Body(Control c) { root.Controls.Remove(G); c.Dock = DockStyle.Fill; root.Controls.Add(c, 0, 2); }
 public virtual void OnShow() { quiet = false; Reload(); }
 public abstract void Reload();
 protected void Note(string m) => msg.Text = m;
 protected Button Act(string text, Action a, bool primary = false, bool danger = false, bool reload = true) { var b = Ui.Btn(text, () => { a(); if (reload) Reload(); }, primary, danger); actions.Controls.Add(b); return b; }
 protected T Add<T>(string label, T c, int width = 150) where T : Control {
  var p = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 14, 4) };
  p.Controls.Add(Ui.Lbl(label.ToUpperInvariant(), Ui.Small, Ui.Muted)); c.Width = width; c.Margin = new Padding(0, 4, 0, 0); p.Controls.Add(c); Filters.Controls.Add(p);
  switch (c) { case TextBox x: x.BorderStyle = BorderStyle.FixedSingle; x.TextChanged += (_, _) => Fire(); break; case ComboBox x: x.DropDownStyle = ComboBoxStyle.DropDownList; x.FlatStyle = FlatStyle.Flat; x.SelectedIndexChanged += (_, _) => Fire(); break; case DateTimePicker x: x.Format = DateTimePickerFormat.Custom; x.CustomFormat = "dd/MM/yyyy"; x.ShowCheckBox = true; x.Checked = false; x.ValueChanged += (_, _) => Fire(); break; case NumericUpDown x: x.ValueChanged += (_, _) => Fire(); break; }
  return c;
 }
 void Fire() { if (!quiet) Reload(); }
 protected void ClearButton(Action? extra = null) {
  // "Làm mới" tải lại dữ liệu mà giữ nguyên bộ lọc (dữ liệu có thể vừa đổi từ web, ví dụ khách vừa đặt đơn).
  var p = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 19, 0, 0) }; p.Controls.Add(Ui.Btn("Xóa lọc", () => ClearFilters(extra))); p.Controls.Add(Ui.Btn("Làm mới", Reload)); Filters.Controls.Add(p);
 }
 protected void ClearFilters(Action? extra = null) {
  quiet = true;
  foreach (FlowLayoutPanel p in Filters.Controls.OfType<FlowLayoutPanel>()) foreach (var c in p.Controls) switch (c) { case TextBox x: x.Clear(); break; case ComboBox x: if (x.Items.Count > 0) x.SelectedIndex = 0; break; case DateTimePicker x: x.Checked = false; break; }
  extra?.Invoke(); quiet = false; Reload();
 }
 protected static ComboBox Combo(string all, IEnumerable<string> items) { var c = new ComboBox(); c.Items.Add(all); c.Items.AddRange(items.ToArray()); c.SelectedIndex = 0; return c; }
 protected static string? Sel(ComboBox c) => c.SelectedIndex <= 0 ? null : c.SelectedItem?.ToString();
 protected static DateTime? Pick(DateTimePicker d) => d.Checked ? d.Value.Date : null;
 protected static int? Num(string s) => int.TryParse(s.Trim(), out var n) ? n : null;
}
