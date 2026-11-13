        USE QuanLyTiemBanh;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TAIKHOAN', N'U') IS NULL
        THROW 51000, N'Chưa có cấu trúc database. Chạy QuanLyTiemBanh.sql trước.', 1;

    IF EXISTS (SELECT 1 FROM dbo.TAIKHOAN WHERE UserName LIKE 'sample[_]rookie[_]%')
        THROW 51001, N'Đã có bộ dữ liệu sample_rookie_. Script dừng để tránh thêm trùng.', 1;

    INSERT INTO dbo.VAITRO(RoleName)
    SELECT v.RoleName FROM (VALUES (N'Khách hàng'),(N'Nhân viên'),(N'Admin')) v(RoleName)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.VAITRO r WHERE r.RoleName=v.RoleName);

    INSERT INTO dbo.LOAIBANH(Name)
    SELECT v.Name FROM (VALUES (N'Bánh kem'),(N'Bánh mì'),(N'Bánh ngọt'),(N'Bánh quy')) v(Name)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.LOAIBANH t WHERE t.Name=v.Name);

    DECLARE @AdminRole INT=(SELECT RoleID FROM dbo.VAITRO WHERE RoleName=N'Admin');
    DECLARE @StaffRole INT=(SELECT RoleID FROM dbo.VAITRO WHERE RoleName=N'Nhân viên');
    DECLARE @CustomerRole INT=(SELECT RoleID FROM dbo.VAITRO WHERE RoleName=N'Khách hàng');
    DECLARE @Today DATE=CAST(GETDATE() AS DATE);
    DECLARE @CreatedAt DATETIME=DATEADD(HOUR,8,CAST(@Today AS DATETIME));
    DECLARE @AdminID INT, @StaffID INT, @Factory1 INT, @Factory2 INT;

    INSERT INTO dbo.TAIKHOAN(RoleID,UserName,Email,PasswordHash,Name,Phone,Sex,BirthD)
    VALUES(@AdminRole,'sample_rookie_admin','sample_rookie_admin@example.test',
           'SAMPLE_NOT_FOR_LOGIN',N'Quản trị Rookie','0901000098',N'Khác','1990-01-01');
    SET @AdminID=CONVERT(INT,SCOPE_IDENTITY());

    INSERT INTO dbo.TAIKHOAN(RoleID,UserName,Email,PasswordHash,Name,Phone,Sex,BirthD)
    VALUES(@StaffRole,'sample_rookie_staff','sample_rookie_staff@example.test',
           'SAMPLE_NOT_FOR_LOGIN',N'Nhân viên Rookie','0901000099',N'Nữ','1995-06-15');
    SET @StaffID=CONVERT(INT,SCOPE_IDENTITY());

    INSERT INTO dbo.XUONGCUNGCAP(Name,Phone,Email,Address)
    VALUES(N'Xưởng mẫu Rookie Thủ Đức','0911000001','factory1@example.test',N'10 đường Số 1, TP. Hồ Chí Minh');
    SET @Factory1=CONVERT(INT,SCOPE_IDENTITY());
    INSERT INTO dbo.XUONGCUNGCAP(Name,Phone,Email,Address)
    VALUES(N'Xưởng mẫu Rookie Bình Thạnh','0911000002','factory2@example.test',N'20 đường Số 2, TP. Hồ Chí Minh');
    SET @Factory2=CONVERT(INT,SCOPE_IDENTITY());

    DECLARE @Samples TABLE(
        No INT PRIMARY KEY, CustomerName NVARCHAR(100), Sex NVARCHAR(10), BirthD DATE,
        Phone VARCHAR(10), NumStreet NVARCHAR(100), Ward NVARCHAR(50),
        ProductName NVARCHAR(100), TypeName NVARCHAR(50), Price DECIMAL(12,0),
        ImportPrice DECIMAL(12,0), OrderQuantity INT
    );
    INSERT INTO @Samples VALUES
    (1, N'Nguyễn Minh Anh', N'Nữ', '1998-01-15', '0901000001', N'11 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh kem dâu', N'Bánh kem', 285000, 170000, 2),
    (2, N'Trần Hoàng Nam', N'Nam', '1998-02-15', '0901000002', N'12 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh kem chocolate', N'Bánh kem', 320000, 190000, 3),
    (3, N'Lê Thảo Nhi', N'Nữ', '1998-03-15', '0901000003', N'13 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh kem matcha', N'Bánh kem', 300000, 180000, 1),
    (4, N'Phạm Gia Huy', N'Nam', '1998-04-15', '0901000004', N'14 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh mì bơ tỏi', N'Bánh mì', 35000, 18000, 2),
    (5, N'Võ Ngọc Hà', N'Nữ', '1998-05-15', '0901000005', N'15 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh mì phô mai', N'Bánh mì', 45000, 24000, 3),
    (6, N'Đặng Tuấn Kiệt', N'Nam', '1998-06-15', '0901000006', N'16 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh su kem', N'Bánh ngọt', 25000, 12000, 1),
    (7, N'Bùi Khánh Linh', N'Nữ', '1998-07-15', '0901000007', N'17 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh tiramisu', N'Bánh ngọt', 75000, 40000, 2),
    (8, N'Đỗ Đức Minh', N'Nam', '1998-08-15', '0901000008', N'18 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh mousse xoài', N'Bánh ngọt', 65000, 35000, 3),
    (9, N'Hồ Bảo Trân', N'Nữ', '1998-09-15', '0901000009', N'19 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh quy chocolate', N'Bánh quy', 35000, 17000, 1),
    (10, N'Dương Quang Hưng', N'Nam', '1998-10-15', '0901000010', N'20 Nguyễn Huệ', N'Phường Bến Nghé', N'Bánh quy hạnh nhân', N'Bánh quy', 40000, 20000, 2);


    DECLARE @Created TABLE(
        No INT PRIMARY KEY, UserID INT, AddressID INT, ProductID INT,
        ImportID INT, BatchID INT, OrderID INT, OrderedQuantity INT
    );
    DECLARE @No INT=1, @UserID INT, @AddressID INT, @ProductID INT,
            @ImportID INT, @BatchID INT, @OrderID INT, @FactoryID INT;
    DECLARE @PaymentMethod NVARCHAR(20), @PaymentStatus NVARCHAR(20), @OrderStatus NVARCHAR(20);

    WHILE @No<=10
    BEGIN
        INSERT INTO dbo.TAIKHOAN(RoleID,UserName,Email,PasswordHash,Name,BirthD,Sex,Phone)
        SELECT @CustomerRole,CONCAT('sample_rookie_customer',No),
               CONCAT('sample_rookie_customer',No,'@example.test'),'SAMPLE_NOT_FOR_LOGIN',
               CustomerName,BirthD,Sex,Phone
        FROM @Samples WHERE No=@No;
        SET @UserID=CONVERT(INT,SCOPE_IDENTITY());

        INSERT INTO dbo.DIACHI(UserID,ReceiverName,ReceiverPhone,City,Ward,NumStreet,IsDefault)
        SELECT @UserID,CustomerName,Phone,N'TP. Hồ Chí Minh',Ward,NumStreet,1
        FROM @Samples WHERE No=@No;
        SET @AddressID=CONVERT(INT,SCOPE_IDENTITY());

        INSERT INTO dbo.SANPHAM(TypeID,Name,Description,Image,Price)
        SELECT t.TypeID,s.ProductName,N'Sản phẩm mẫu Rookie — làm thủ công, ngọt vừa phải.',
               NULL,s.Price
        FROM @Samples s JOIN dbo.LOAIBANH t ON t.Name=s.TypeName WHERE s.No=@No;
        SET @ProductID=CONVERT(INT,SCOPE_IDENTITY());

        -- Phải nhập lô trước khi bán: trigger kiểm tra tồn còn hạn và trừ FIFO.
        SET @FactoryID=CASE WHEN @No%2=1 THEN @Factory1 ELSE @Factory2 END;
        INSERT INTO dbo.PHIEUNHAP(FactoryID,UserID,Status,ImportDate,CreateAt,Note)
        VALUES(@FactoryID,@StaffID,N'Đã nhập',@Today,@CreatedAt,N'Phiếu nhập dữ liệu mẫu Rookie');
        SET @ImportID=CONVERT(INT,SCOPE_IDENTITY());
        INSERT INTO dbo.LOBANH(ImportID,CreateAt,Status)
        VALUES(@ImportID,@CreatedAt,N'Hoàn tất');
        SET @BatchID=CONVERT(INT,SCOPE_IDENTITY());
        INSERT INTO dbo.CHITIETLO(BatchID,ProductID,Quantity,Remaining,ImportPrice,ExpiryDate)
        SELECT @BatchID,@ProductID,30,30,ImportPrice,DATEADD(DAY,3,@Today)
        FROM @Samples WHERE No=@No;

        SET @PaymentMethod=CASE @No%3 WHEN 1 THEN N'COD' WHEN 2 THEN N'Chuyển khoản' ELSE N'Ví điện tử' END;
        SET @OrderStatus=CASE WHEN @No<=3 THEN N'Chờ xác nhận' WHEN @No<=6 THEN N'Đang chuẩn bị'
                              WHEN @No<=8 THEN N'Đang giao' ELSE N'Hoàn thành' END;
        SET @PaymentStatus=CASE WHEN @PaymentMethod<>N'COD' OR @OrderStatus=N'Hoàn thành'
                                THEN N'Đã thanh toán' ELSE N'Chưa thanh toán' END;
        INSERT INTO dbo.DONHANG(UserID,AddressID,PaymentMethod,PaymentStatus,Status,CreateAt,DeliveryTime)
        VALUES(@UserID,@AddressID,@PaymentMethod,@PaymentStatus,@OrderStatus,
               DATEADD(MINUTE,@No,@CreatedAt),
               CASE WHEN @OrderStatus=N'Hoàn thành' THEN DATEADD(HOUR,2,@CreatedAt) ELSE NULL END);
        SET @OrderID=CONVERT(INT,SCOPE_IDENTITY());

        -- Lấy đúng Price từ SANPHAM; để trigger tự cập nhật Total và TotalStock.
        INSERT INTO dbo.CHITIETDONHANG(OrderID,ProductID,Quantity,UnitPrice)
        SELECT @OrderID,@ProductID,s.OrderQuantity,p.Price
        FROM @Samples s CROSS JOIN dbo.SANPHAM p
        WHERE s.No=@No AND p.ProductID=@ProductID;

        INSERT INTO @Created
        SELECT @No,@UserID,@AddressID,@ProductID,@ImportID,@BatchID,@OrderID,OrderQuantity
        FROM @Samples WHERE No=@No;
        SET @No=@No+1;
    END;

    -- Thêm một lô hết hạn riêng để minh họa phiếu hủy; không dùng lô này để bán.
    DECLARE @ExpiredImportID INT, @ExpiredBatchID INT, @CancelID INT;
    SELECT @ProductID=ProductID FROM @Created WHERE No=1;
    INSERT INTO dbo.PHIEUNHAP(FactoryID,UserID,Status,ImportDate,CreateAt,Note)
    VALUES(@Factory1,@AdminID,N'Đã nhập',DATEADD(DAY,-5,@Today),
           DATEADD(DAY,-5,@CreatedAt),N'Lô mẫu đã hết hạn để kiểm tra hủy bánh');
    SET @ExpiredImportID=CONVERT(INT,SCOPE_IDENTITY());
    INSERT INTO dbo.LOBANH(ImportID,CreateAt,Status)
    VALUES(@ExpiredImportID,DATEADD(DAY,-5,@CreatedAt),N'Hoàn tất');
    SET @ExpiredBatchID=CONVERT(INT,SCOPE_IDENTITY());
    INSERT INTO dbo.CHITIETLO(BatchID,ProductID,Quantity,Remaining,ImportPrice,ExpiryDate)
    VALUES(@ExpiredBatchID,@ProductID,5,5,170000,DATEADD(DAY,-1,@Today));
    INSERT INTO dbo.PHIEUHUY(UserID,CreateAt,Reason,Status)
    VALUES(@StaffID,@CreatedAt,N'Hết hạn sử dụng — dữ liệu mẫu',N'Hoàn tất');
    SET @CancelID=CONVERT(INT,SCOPE_IDENTITY());
    INSERT INTO dbo.CHITIETPHIEUHUY(CancelID,BatchID,ProductID,Quantity)
    VALUES(@CancelID,@ExpiredBatchID,@ProductID,5);

    -- Kiểm tra kết quả trigger trước khi COMMIT.
    IF (SELECT COUNT(*) FROM @Created)<>10
        THROW 51002,N'Không tạo đủ 10 bộ dữ liệu mẫu.',1;
    IF EXISTS(SELECT 1 FROM @Created c JOIN dbo.SANPHAM p ON p.ProductID=c.ProductID
              WHERE p.TotalStock<>30-c.OrderedQuantity)
        THROW 51003,N'Tồn kho sau đặt hàng không đúng.',1;
    IF EXISTS(SELECT 1 FROM @Created c JOIN dbo.DONHANG d ON d.OrderID=c.OrderID
              JOIN dbo.SANPHAM p ON p.ProductID=c.ProductID
              WHERE d.Total<>c.OrderedQuantity*p.Price)
        THROW 51004,N'Tổng tiền đơn hàng không đúng.',1;
    IF EXISTS(SELECT 1 FROM dbo.CHITIETLO WHERE BatchID=@ExpiredBatchID AND Remaining<>0)
        THROW 51005,N'Lô hết hạn chưa được hủy đủ số lượng.',1;

    COMMIT TRANSACTION;

    SELECT c.No,t.UserName,t.Name AS CustomerName,p.Name AS ProductName,
           d.OrderID,d.PaymentMethod,d.PaymentStatus,d.Status,d.Total,p.TotalStock
    FROM @Created c
    JOIN dbo.TAIKHOAN t ON t.UserID=c.UserID
    JOIN dbo.SANPHAM p ON p.ProductID=c.ProductID
    JOIN dbo.DONHANG d ON d.OrderID=c.OrderID
    ORDER BY c.No;
    PRINT N'Đã tạo thành công 10 bộ dữ liệu khách hàng – bánh – đơn hàng.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
