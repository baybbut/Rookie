/* ============================================================
   CSDL QUAN LY TIEM BANH ONLINE - SQL Server
   Gom: tao CSDL, bang + rang buoc, trigger, thu tuc, du lieu mau
   Chay toan bo file trong SSMS (cac lenh GO tu tach batch)
   ============================================================ */

-- 1. TAO CSDL ------------------------------------------------
IF DB_ID(N'QuanLyTiemBanh') IS NULL
    CREATE DATABASE QuanLyTiemBanh COLLATE Vietnamese_CI_AS;
GO
USE QuanLyTiemBanh;
GO

-- 2. XOA DOI TUONG CU (de chay lai nhieu lan) ----------------
IF OBJECT_ID('usp_TruTonKhoFIFO', 'P') IS NOT NULL DROP PROCEDURE usp_TruTonKhoFIFO;
IF OBJECT_ID('CHITIETPHIEUHUY', 'U') IS NOT NULL DROP TABLE CHITIETPHIEUHUY;
IF OBJECT_ID('PHIEUHUY', 'U')        IS NOT NULL DROP TABLE PHIEUHUY;
IF OBJECT_ID('CHITIETLO', 'U')       IS NOT NULL DROP TABLE CHITIETLO;
IF OBJECT_ID('LOBANH', 'U')          IS NOT NULL DROP TABLE LOBANH;
IF OBJECT_ID('PHIEUNHAP', 'U')       IS NOT NULL DROP TABLE PHIEUNHAP;
IF OBJECT_ID('XUONGCUNGCAP', 'U')    IS NOT NULL DROP TABLE XUONGCUNGCAP;
IF OBJECT_ID('CHITIETDONHANG', 'U')  IS NOT NULL DROP TABLE CHITIETDONHANG;
IF OBJECT_ID('DONHANG', 'U')         IS NOT NULL DROP TABLE DONHANG;
IF OBJECT_ID('SANPHAM', 'U')         IS NOT NULL DROP TABLE SANPHAM;
IF OBJECT_ID('LOAIBANH', 'U')        IS NOT NULL DROP TABLE LOAIBANH;
IF OBJECT_ID('DIACHI', 'U')          IS NOT NULL DROP TABLE DIACHI;
IF OBJECT_ID('TAIKHOAN', 'U')        IS NOT NULL DROP TABLE TAIKHOAN;
IF OBJECT_ID('VAITRO', 'U')          IS NOT NULL DROP TABLE VAITRO;
GO

-- 3. NHOM HE THONG -------------------------------------------
CREATE TABLE VAITRO (
    RoleID    INT IDENTITY(1,1) NOT NULL,
    RoleName  NVARCHAR(30)      NOT NULL,
    CONSTRAINT PK_VAITRO PRIMARY KEY (RoleID),
    CONSTRAINT UQ_VAITRO_RoleName UNIQUE (RoleName),
    CONSTRAINT CK_VAITRO_RoleName CHECK (RoleName IN (N'Khách hàng', N'Nhân viên', N'Admin'))
);
GO

CREATE TABLE TAIKHOAN (
    UserID        INT IDENTITY(1,1) NOT NULL,
    RoleID        INT           NOT NULL,
    UserName      VARCHAR(50)   NOT NULL,
    Email         VARCHAR(100)  NOT NULL,
    PasswordHash  VARCHAR(255)  NOT NULL,
    Status        NVARCHAR(20)  NOT NULL CONSTRAINT DF_TAIKHOAN_Status DEFAULT N'Hoạt động',
    Name          NVARCHAR(100) NOT NULL,
    BirthD        DATE          NULL,
    Avatar        VARCHAR(255)  NULL,
    Sex           NVARCHAR(10)  NULL,
    Phone         VARCHAR(10)   NULL,
    CONSTRAINT PK_TAIKHOAN PRIMARY KEY (UserID),
    CONSTRAINT UQ_TAIKHOAN_UserName UNIQUE (UserName),
    CONSTRAINT UQ_TAIKHOAN_Email UNIQUE (Email),
    CONSTRAINT FK_TAIKHOAN_VAITRO FOREIGN KEY (RoleID) REFERENCES VAITRO(RoleID),
    CONSTRAINT CK_TAIKHOAN_Status CHECK (Status IN (N'Hoạt động', N'Bị khóa')),
    CONSTRAINT CK_TAIKHOAN_Sex CHECK (Sex IS NULL OR Sex IN (N'Nam', N'Nữ', N'Khác')),
    CONSTRAINT CK_TAIKHOAN_Phone CHECK (Phone IS NULL OR Phone LIKE '0[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
    CONSTRAINT CK_TAIKHOAN_Email CHECK (Email LIKE '%_@_%._%'),
    CONSTRAINT CK_TAIKHOAN_BirthD CHECK (BirthD IS NULL OR BirthD < CAST(GETDATE() AS DATE))
);
GO

CREATE TABLE DIACHI (
    AddressID      INT IDENTITY(1,1) NOT NULL,
    UserID         INT           NOT NULL,
    ReceiverName   NVARCHAR(100) NOT NULL,
    ReceiverPhone  VARCHAR(10)   NOT NULL,
    City           NVARCHAR(50)  NOT NULL,
    Ward           NVARCHAR(50)  NOT NULL,
    NumStreet      NVARCHAR(100) NOT NULL,
    IsDefault      BIT           NOT NULL CONSTRAINT DF_DIACHI_IsDefault DEFAULT 0,
    CONSTRAINT PK_DIACHI PRIMARY KEY (AddressID),
    CONSTRAINT FK_DIACHI_TAIKHOAN FOREIGN KEY (UserID) REFERENCES TAIKHOAN(UserID),
    CONSTRAINT CK_DIACHI_Phone CHECK (ReceiverPhone LIKE '0[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]')
);
GO
-- Moi tai khoan toi da mot dia chi mac dinh
CREATE UNIQUE INDEX UX_DIACHI_MacDinh ON DIACHI(UserID) WHERE IsDefault = 1;
GO

-- 4. NHOM BAN HANG - DON DAT BANH ----------------------------
CREATE TABLE LOAIBANH (
    TypeID  INT IDENTITY(1,1) NOT NULL,
    Name    NVARCHAR(50)      NOT NULL,
    CONSTRAINT PK_LOAIBANH PRIMARY KEY (TypeID),
    CONSTRAINT UQ_LOAIBANH_Name UNIQUE (Name)
);
GO

CREATE TABLE SANPHAM (
    ProductID   INT IDENTITY(1,1) NOT NULL,
    TypeID      INT            NOT NULL,
    Name        NVARCHAR(100)  NOT NULL,
    Description NVARCHAR(500)  NULL,
    Image       VARCHAR(255)   NULL,
    Price       DECIMAL(12,0)  NOT NULL,
    SaleStatus  NVARCHAR(20)   NOT NULL CONSTRAINT DF_SANPHAM_SaleStatus DEFAULT N'Đang bán',
    TotalStock  INT            NOT NULL CONSTRAINT DF_SANPHAM_TotalStock DEFAULT 0,
    CONSTRAINT PK_SANPHAM PRIMARY KEY (ProductID),
    CONSTRAINT FK_SANPHAM_LOAIBANH FOREIGN KEY (TypeID) REFERENCES LOAIBANH(TypeID),
    CONSTRAINT CK_SANPHAM_Price CHECK (Price > 0),
    CONSTRAINT CK_SANPHAM_TotalStock CHECK (TotalStock >= 0),
    CONSTRAINT CK_SANPHAM_SaleStatus CHECK (SaleStatus IN (N'Đang bán', N'Ngừng bán'))
);
GO

CREATE TABLE DONHANG (
    OrderID        INT IDENTITY(1,1) NOT NULL,
    UserID         INT           NOT NULL,
    AddressID      INT           NOT NULL,
    Total          DECIMAL(12,0) NOT NULL CONSTRAINT DF_DONHANG_Total DEFAULT 0,
    PaymentMethod  NVARCHAR(20)  NOT NULL,
    PaymentStatus  NVARCHAR(20)  NOT NULL CONSTRAINT DF_DONHANG_PaymentStatus DEFAULT N'Chưa thanh toán',
    Status         NVARCHAR(20)  NOT NULL CONSTRAINT DF_DONHANG_Status DEFAULT N'Chờ xác nhận',
    CreateAt       DATETIME      NOT NULL CONSTRAINT DF_DONHANG_CreateAt DEFAULT GETDATE(),
    DeliveryTime   DATETIME      NULL,
    CONSTRAINT PK_DONHANG PRIMARY KEY (OrderID),
    CONSTRAINT FK_DONHANG_TAIKHOAN FOREIGN KEY (UserID) REFERENCES TAIKHOAN(UserID),
    CONSTRAINT FK_DONHANG_DIACHI FOREIGN KEY (AddressID) REFERENCES DIACHI(AddressID),
    CONSTRAINT CK_DONHANG_Total CHECK (Total >= 0),
    CONSTRAINT CK_DONHANG_PaymentMethod CHECK (PaymentMethod IN (N'COD', N'Chuyển khoản', N'Ví điện tử')),
    CONSTRAINT CK_DONHANG_PaymentStatus CHECK (PaymentStatus IN (N'Chưa thanh toán', N'Đã thanh toán')),
    CONSTRAINT CK_DONHANG_Status CHECK (Status IN (N'Chờ xác nhận', N'Đang chuẩn bị', N'Đang giao', N'Hoàn thành', N'Đã hủy')),
    CONSTRAINT CK_DONHANG_DeliveryTime CHECK (DeliveryTime IS NULL OR DeliveryTime >= CreateAt),
    CONSTRAINT CK_DONHANG_HoanThanh CHECK (Status <> N'Hoàn thành' OR PaymentStatus = N'Đã thanh toán')
);
GO

CREATE TABLE CHITIETDONHANG (
    OrderID    INT           NOT NULL,
    ProductID  INT           NOT NULL,
    Quantity   INT           NOT NULL,
    UnitPrice  DECIMAL(12,0) NOT NULL,
    CONSTRAINT PK_CHITIETDONHANG PRIMARY KEY (OrderID, ProductID),
    CONSTRAINT FK_CTDH_DONHANG FOREIGN KEY (OrderID) REFERENCES DONHANG(OrderID) ON DELETE CASCADE,
    CONSTRAINT FK_CTDH_SANPHAM FOREIGN KEY (ProductID) REFERENCES SANPHAM(ProductID),
    CONSTRAINT CK_CTDH_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_CTDH_UnitPrice CHECK (UnitPrice > 0)
);
GO

-- 5. NHOM NHAP HANG - LO BANH - HUY BANH ---------------------
CREATE TABLE XUONGCUNGCAP (
    FactoryID      INT IDENTITY(1,1) NOT NULL,
    Name           NVARCHAR(100) NOT NULL,
    Phone          VARCHAR(10)   NULL,
    Email          VARCHAR(100)  NULL,
    Address        NVARCHAR(200) NULL,
    PartnerStatus  NVARCHAR(20)  NOT NULL CONSTRAINT DF_XUONG_PartnerStatus DEFAULT N'Đang hợp tác',
    CONSTRAINT PK_XUONGCUNGCAP PRIMARY KEY (FactoryID),
    CONSTRAINT CK_XUONG_PartnerStatus CHECK (PartnerStatus IN (N'Đang hợp tác', N'Ngừng hợp tác')),
    CONSTRAINT CK_XUONG_Phone CHECK (Phone IS NULL OR Phone LIKE '0[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]')
);
GO

CREATE TABLE PHIEUNHAP (
    ImportID    INT IDENTITY(1,1) NOT NULL,
    FactoryID   INT           NOT NULL,
    UserID      INT           NOT NULL,
    Status      NVARCHAR(20)  NOT NULL CONSTRAINT DF_PHIEUNHAP_Status DEFAULT N'Chờ nhập',
    ImportDate  DATE          NULL,
    CreateAt    DATETIME      NOT NULL CONSTRAINT DF_PHIEUNHAP_CreateAt DEFAULT GETDATE(),
    Note        NVARCHAR(200) NULL,
    Total       DECIMAL(12,0) NOT NULL CONSTRAINT DF_PHIEUNHAP_Total DEFAULT 0,
    CONSTRAINT PK_PHIEUNHAP PRIMARY KEY (ImportID),
    CONSTRAINT FK_PHIEUNHAP_XUONG FOREIGN KEY (FactoryID) REFERENCES XUONGCUNGCAP(FactoryID),
    CONSTRAINT FK_PHIEUNHAP_TAIKHOAN FOREIGN KEY (UserID) REFERENCES TAIKHOAN(UserID),
    CONSTRAINT CK_PHIEUNHAP_Status CHECK (Status IN (N'Chờ nhập', N'Đã nhập', N'Đã hủy')),
    CONSTRAINT CK_PHIEUNHAP_Total CHECK (Total >= 0),
    CONSTRAINT CK_PHIEUNHAP_ImportDate CHECK (ImportDate IS NULL OR ImportDate >= CAST(CreateAt AS DATE))
);
GO

CREATE TABLE LOBANH (
    BatchID   INT IDENTITY(1,1) NOT NULL,
    ImportID  INT          NOT NULL,
    CreateAt  DATETIME     NOT NULL CONSTRAINT DF_LOBANH_CreateAt DEFAULT GETDATE(),
    Status    NVARCHAR(20) NOT NULL CONSTRAINT DF_LOBANH_Status DEFAULT N'Chờ xử lý',
    CONSTRAINT PK_LOBANH PRIMARY KEY (BatchID),
    CONSTRAINT FK_LOBANH_PHIEUNHAP FOREIGN KEY (ImportID) REFERENCES PHIEUNHAP(ImportID) ON DELETE CASCADE,
    CONSTRAINT CK_LOBANH_Status CHECK (Status IN (N'Chờ xử lý', N'Hoàn tất'))
);
GO

CREATE TABLE CHITIETLO (
    BatchID      INT           NOT NULL,
    ProductID    INT           NOT NULL,
    Quantity     INT           NOT NULL,
    Remaining    INT           NOT NULL,
    ImportPrice  DECIMAL(12,0) NOT NULL,
    ExpiryDate   DATE          NOT NULL,
    CONSTRAINT PK_CHITIETLO PRIMARY KEY (BatchID, ProductID),
    CONSTRAINT FK_CTLO_LOBANH FOREIGN KEY (BatchID) REFERENCES LOBANH(BatchID) ON DELETE CASCADE,
    CONSTRAINT FK_CTLO_SANPHAM FOREIGN KEY (ProductID) REFERENCES SANPHAM(ProductID),
    CONSTRAINT CK_CTLO_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_CTLO_Remaining CHECK (Remaining >= 0 AND Remaining <= Quantity),
    CONSTRAINT CK_CTLO_ImportPrice CHECK (ImportPrice > 0)
);
GO

CREATE TABLE PHIEUHUY (
    CancelID  INT IDENTITY(1,1) NOT NULL,
    UserID    INT           NOT NULL,
    CreateAt  DATETIME      NOT NULL CONSTRAINT DF_PHIEUHUY_CreateAt DEFAULT GETDATE(),
    Reason    NVARCHAR(200) NOT NULL CONSTRAINT DF_PHIEUHUY_Reason DEFAULT N'Hết hạn sử dụng',
    Status    NVARCHAR(20)  NOT NULL CONSTRAINT DF_PHIEUHUY_Status DEFAULT N'Chờ xử lý',
    CONSTRAINT PK_PHIEUHUY PRIMARY KEY (CancelID),
    CONSTRAINT FK_PHIEUHUY_TAIKHOAN FOREIGN KEY (UserID) REFERENCES TAIKHOAN(UserID),
    CONSTRAINT CK_PHIEUHUY_Status CHECK (Status IN (N'Chờ xử lý', N'Hoàn tất'))
);
GO

CREATE TABLE CHITIETPHIEUHUY (
    CancelID   INT NOT NULL,
    BatchID    INT NOT NULL,
    ProductID  INT NOT NULL,
    Quantity   INT NOT NULL,
    CONSTRAINT PK_CHITIETPHIEUHUY PRIMARY KEY (CancelID, BatchID, ProductID),
    CONSTRAINT FK_CTPH_PHIEUHUY FOREIGN KEY (CancelID) REFERENCES PHIEUHUY(CancelID) ON DELETE CASCADE,
    CONSTRAINT FK_CTPH_CHITIETLO FOREIGN KEY (BatchID, ProductID) REFERENCES CHITIETLO(BatchID, ProductID),
    CONSTRAINT CK_CTPH_Quantity CHECK (Quantity > 0)
);
GO

-- 6. THU TUC TRU TON KHO THEO FIFO ---------------------------
-- Tru Remaining tu lo gan het han nhat truoc. Gia dinh da kiem tra du hang.
CREATE PROCEDURE usp_TruTonKhoFIFO
    @ProductID INT,
    @Quantity  INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @BatchID INT, @Remaining INT, @Take INT;

    DECLARE cur CURSOR LOCAL STATIC FOR
        SELECT BatchID, Remaining
        FROM CHITIETLO
        WHERE ProductID = @ProductID
          AND Remaining > 0
          AND ExpiryDate >= CAST(GETDATE() AS DATE)
        ORDER BY ExpiryDate, BatchID;

    OPEN cur;
    FETCH NEXT FROM cur INTO @BatchID, @Remaining;
    WHILE @@FETCH_STATUS = 0 AND @Quantity > 0
    BEGIN
        SET @Take = CASE WHEN @Remaining < @Quantity THEN @Remaining ELSE @Quantity END;
        UPDATE CHITIETLO
        SET Remaining = Remaining - @Take
        WHERE BatchID = @BatchID AND ProductID = @ProductID;
        SET @Quantity = @Quantity - @Take;
        FETCH NEXT FROM cur INTO @BatchID, @Remaining;
    END
    CLOSE cur;
    DEALLOCATE cur;
END
GO

-- 7. TRIGGER -------------------------------------------------

-- 7.1 DONHANG: dia chi phai thuoc khach dat don; nguoi dat phai la Khach hang
CREATE TRIGGER trg_DONHANG_KiemTra ON DONHANG
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(UserID) OR UPDATE(AddressID)
    BEGIN
        IF EXISTS (SELECT 1 FROM inserted i
                   JOIN DIACHI d ON d.AddressID = i.AddressID
                   WHERE d.UserID <> i.UserID)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50001, N'Địa chỉ giao hàng phải thuộc về chính khách đặt đơn.', 1;
        END

        IF EXISTS (SELECT 1 FROM inserted i
                   JOIN TAIKHOAN t ON t.UserID = i.UserID
                   JOIN VAITRO r ON r.RoleID = t.RoleID
                   WHERE r.RoleName <> N'Khách hàng')
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50002, N'Người đặt đơn phải có vai trò Khách hàng.', 1;
        END
    END
END
GO

-- 7.2 CHITIETDONHANG: kiem tra ban hang + tru ton kho FIFO khi them dong moi
CREATE TRIGGER trg_CTDH_BanHang ON CHITIETDONHANG
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Chi ban san pham dang ban
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN SANPHAM s ON s.ProductID = i.ProductID
               WHERE s.SaleStatus <> N'Đang bán')
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50003, N'Sản phẩm đã ngừng bán, không thể đặt.', 1;
    END

    -- Gia ban phai bang gia hien tai cua san pham
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN SANPHAM s ON s.ProductID = i.ProductID
               WHERE i.UnitPrice <> s.Price)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50004, N'Giá bán (UnitPrice) phải bằng giá hiện tại của sản phẩm.', 1;
    END

    -- So luong dat khong vuot ton kho con han
    IF EXISTS (SELECT 1
               FROM (SELECT ProductID, SUM(Quantity) AS Q FROM inserted GROUP BY ProductID) x
               WHERE x.Q > ISNULL((SELECT SUM(l.Remaining) FROM CHITIETLO l
                                   WHERE l.ProductID = x.ProductID
                                     AND l.ExpiryDate >= CAST(GETDATE() AS DATE)), 0))
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50005, N'Số lượng đặt vượt quá tồn kho còn hạn.', 1;
    END

    -- Tru kho theo FIFO
    DECLARE @ProductID INT, @Qty INT;
    DECLARE cur CURSOR LOCAL STATIC FOR
        SELECT ProductID, SUM(Quantity) FROM inserted GROUP BY ProductID;
    OPEN cur;
    FETCH NEXT FROM cur INTO @ProductID, @Qty;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC usp_TruTonKhoFIFO @ProductID, @Qty;
        FETCH NEXT FROM cur INTO @ProductID, @Qty;
    END
    CLOSE cur;
    DEALLOCATE cur;
END
GO

-- 7.3 CHITIETDONHANG: dong bo DONHANG.Total = SUM(Quantity * UnitPrice)
CREATE TRIGGER trg_CTDH_TongTien ON CHITIETDONHANG
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE d
    SET Total = ISNULL((SELECT SUM(c.Quantity * c.UnitPrice)
                        FROM CHITIETDONHANG c
                        WHERE c.OrderID = d.OrderID), 0)
    FROM DONHANG d
    WHERE d.OrderID IN (SELECT OrderID FROM inserted
                        UNION
                        SELECT OrderID FROM deleted);
END
GO

-- 7.4 PHIEUNHAP: chi nhap tu xuong dang hop tac; nguoi lap phai la Nhan vien/Admin
CREATE TRIGGER trg_PHIEUNHAP_KiemTra ON PHIEUNHAP
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(FactoryID)
       AND EXISTS (SELECT 1 FROM inserted i
                   JOIN XUONGCUNGCAP x ON x.FactoryID = i.FactoryID
                   WHERE x.PartnerStatus <> N'Đang hợp tác')
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50006, N'Xưởng cung cấp đã ngừng hợp tác.', 1;
    END

    IF UPDATE(UserID)
       AND EXISTS (SELECT 1 FROM inserted i
                   JOIN TAIKHOAN t ON t.UserID = i.UserID
                   JOIN VAITRO r ON r.RoleID = t.RoleID
                   WHERE r.RoleName = N'Khách hàng')
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50007, N'Người lập phiếu nhập phải là Nhân viên hoặc Admin.', 1;
    END
END
GO

-- 7.5 CHITIETLO: dong bo SANPHAM.TotalStock va PHIEUNHAP.Total
CREATE TRIGGER trg_CTLO_DongBo ON CHITIETLO
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Ton kho san pham = tong Remaining cac lo con han
    UPDATE s
    SET TotalStock = ISNULL((SELECT SUM(l.Remaining)
                             FROM CHITIETLO l
                             WHERE l.ProductID = s.ProductID
                               AND l.ExpiryDate >= CAST(GETDATE() AS DATE)), 0)
    FROM SANPHAM s
    WHERE s.ProductID IN (SELECT ProductID FROM inserted
                          UNION
                          SELECT ProductID FROM deleted);

    -- Tong tien phieu nhap = tong Quantity * ImportPrice
    UPDATE p
    SET Total = ISNULL((SELECT SUM(l.Quantity * l.ImportPrice)
                        FROM CHITIETLO l
                        JOIN LOBANH b ON b.BatchID = l.BatchID
                        WHERE b.ImportID = p.ImportID), 0)
    FROM PHIEUNHAP p
    WHERE p.ImportID IN (SELECT b.ImportID FROM LOBANH b
                         WHERE b.BatchID IN (SELECT BatchID FROM inserted
                                             UNION
                                             SELECT BatchID FROM deleted));
END
GO

-- 7.6 PHIEUHUY: nguoi lap phai la Nhan vien/Admin
CREATE TRIGGER trg_PHIEUHUY_KiemTra ON PHIEUHUY
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(UserID)
       AND EXISTS (SELECT 1 FROM inserted i
                   JOIN TAIKHOAN t ON t.UserID = i.UserID
                   JOIN VAITRO r ON r.RoleID = t.RoleID
                   WHERE r.RoleName = N'Khách hàng')
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50008, N'Người lập phiếu hủy phải là Nhân viên hoặc Admin.', 1;
    END
END
GO

-- 7.7 CHITIETPHIEUHUY: chi huy dong da het han, khong huy qua so con lai, tru Remaining
CREATE TRIGGER trg_CTPH_Huy ON CHITIETPHIEUHUY
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Chi huy dong da het han tai thoi diem lap phieu
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN PHIEUHUY p ON p.CancelID = i.CancelID
               JOIN CHITIETLO l ON l.BatchID = i.BatchID AND l.ProductID = i.ProductID
               WHERE l.ExpiryDate >= CAST(p.CreateAt AS DATE))
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50009, N'Chỉ được hủy sản phẩm đã hết hạn sử dụng.', 1;
    END

    -- So luong huy khong vuot so con lai
    IF EXISTS (SELECT 1
               FROM (SELECT BatchID, ProductID, SUM(Quantity) AS Q
                     FROM inserted GROUP BY BatchID, ProductID) x
               JOIN CHITIETLO l ON l.BatchID = x.BatchID AND l.ProductID = x.ProductID
               WHERE x.Q > l.Remaining)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50010, N'Số lượng hủy vượt quá số lượng còn lại của lô.', 1;
    END

    -- Tru Remaining
    UPDATE l
    SET Remaining = l.Remaining - x.Q
    FROM CHITIETLO l
    JOIN (SELECT BatchID, ProductID, SUM(Quantity) AS Q
          FROM inserted GROUP BY BatchID, ProductID) x
      ON l.BatchID = x.BatchID AND l.ProductID = x.ProductID;
END
GO

-- 8. DU LIEU MAU CO BAN --------------------------------------
INSERT INTO VAITRO (RoleName) VALUES (N'Khách hàng'), (N'Nhân viên'), (N'Admin');

INSERT INTO LOAIBANH (Name) VALUES (N'Bánh kem'), (N'Bánh mì'), (N'Bánh ngọt'), (N'Bánh quy');
GO

/* ---- VI DU THU NGHIEM (bo comment de chay thu) ----
-- Tai khoan (mat khau o day chi la chuoi bam gia lap)
INSERT INTO TAIKHOAN (RoleID, UserName, Email, PasswordHash, Name, Phone)
VALUES (3, 'admin',  'admin@tiembanh.vn',  'hash_admin',  N'Quản trị', '0900000001'),
       (1, 'khach1', 'khach1@gmail.com',   'hash_khach1', N'Nguyễn Văn A', '0900000002');

INSERT INTO DIACHI (UserID, ReceiverName, ReceiverPhone, City, Ward, NumStreet, IsDefault)
VALUES (2, N'Nguyễn Văn A', '0900000002', N'TP. Hồ Chí Minh', N'Bến Nghé', N'12 Lê Lợi', 1);

INSERT INTO SANPHAM (TypeID, Name, Price) VALUES (1, N'Bánh kem dâu', 250000);

INSERT INTO XUONGCUNGCAP (Name, Phone) VALUES (N'Xưởng Bánh Ngon', '0911111111');
INSERT INTO PHIEUNHAP (FactoryID, UserID) VALUES (1, 1);
INSERT INTO LOBANH (ImportID) VALUES (1);
INSERT INTO CHITIETLO (BatchID, ProductID, Quantity, Remaining, ImportPrice, ExpiryDate)
VALUES (1, 1, 20, 20, 150000, DATEADD(DAY, 2, CAST(GETDATE() AS DATE)));
-- Kiem tra: SELECT ProductID, TotalStock FROM SANPHAM;  (ky vong 20)

INSERT INTO DONHANG (UserID, AddressID, PaymentMethod) VALUES (2, 1, N'COD');
INSERT INTO CHITIETDONHANG (OrderID, ProductID, Quantity, UnitPrice) VALUES (1, 1, 2, 250000);
-- Kiem tra: SELECT Total FROM DONHANG;  (500000)   SELECT TotalStock FROM SANPHAM; (18)
---------------------------------------------------- */
