# Rookie Bakery
Website bán bánh bằng ASP.NET Core MVC, Razor và EF Core SQLite (.NET 8).

## Chạy
```sh
dotnet restore
dotnet run --urls http://0.0.0.0:5080
```
SQLite `rookie.db` tự tạo với 6 sản phẩm mẫu. Trang chủ truy vấn sản phẩm qua EF Core; lọc bánh bán chạy/bánh mới, tìm kiếm, giỏ hàng và cập nhật số lượng chạy trong trình duyệt. Giỏ hàng lưu localStorage. Chưa có thanh toán hoặc quản trị; đánh giá, thông tin liên hệ là nội dung minh họa. Ảnh từ Unsplash cần Internet.

WinForms chỉ chạy trên Windows và không dùng để render website. Có thể xây dựng ứng dụng quản trị WinForms riêng kết nối API trong giai đoạn sau.

## Ứng dụng quản trị WinForms
`Rookie.Admin/` là app quản trị WinForms (Windows, .NET 8) dùng chung model và file `rookie.db` với web, nên dữ liệu hai bên luôn khớp. Web admin (`/admin`) vẫn còn nguyên.
```sh
dotnet run --project Rookie.Admin
```
Đăng nhập `admin` / `admin` (đặt biến môi trường `Admin__Password` để đổi mật khẩu cho lần tạo DB đầu tiên). App tự tìm `rookie.db` ở thư mục dự án web; đặt `ROOKIE_DB` để chỉ file khác. Nếu chưa có DB, app tạo mới kèm dữ liệu mẫu. Xuất Excel ghi file .xlsx; xuất PDF dùng máy in "Microsoft Print to PDF" của Windows.
