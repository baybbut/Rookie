# Rookie Bakery
Website bán bánh bằng ASP.NET Core MVC, Razor và EF Core SQLite (.NET 8).

## Chạy
```sh
dotnet restore
dotnet run --urls http://0.0.0.0:5080
```
SQLite `rookie.db` tự tạo với 6 sản phẩm mẫu. Trang chủ truy vấn sản phẩm qua EF Core; lọc bánh bán chạy/bánh mới, tìm kiếm, giỏ hàng và cập nhật số lượng chạy trong trình duyệt. Giỏ hàng lưu localStorage. Chưa có thanh toán hoặc quản trị; đánh giá, thông tin liên hệ là nội dung minh họa. Ảnh từ Unsplash cần Internet.

WinForms chỉ chạy trên Windows và không dùng để render website. Có thể xây dựng ứng dụng quản trị WinForms riêng kết nối API trong giai đoạn sau.
