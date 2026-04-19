-- ============================================================================
-- ERP DATABASE — MIGRATION: SKU Generator + Product Attributes
-- Version:  1.6 → 1.7
-- Date:     2026-04-14
-- Mô tả:
--   [+] ProductAttributes    — Định nghĩa thuộc tính linh hoạt (Xuất xứ, Màu, Kích thước...)
--   [+] ProductAttributeValues — Giá trị mỗi thuộc tính (VN, JP, KR... / WH, BK, CL...)
--   [+] ProductAttributeMap  — Gán giá trị thuộc tính cho từng sản phẩm
--   [+] SkuConfigs           — Cấu hình cách gen SKU cho từng danh mục
--   [+] SkuSequences         — Bộ đếm sequence theo nhóm SKU (tránh trùng)
--   [~] ProductCategories    — thêm SkuPrefix (VD: TP, NL, VT, TB)
--   [~] Products             — thêm SKU (mã dài có nghĩa), giữ ProductCode (mã ngắn nội bộ)
-- ============================================================================

USE ERP_ThuongMaiTrungGian;
GO

-- ============================================================================
-- BẢNG 1: PRODUCT ATTRIBUTES (Định nghĩa thuộc tính)
-- ============================================================================
-- Admin tạo các loại thuộc tính dùng cho sản phẩm. Mỗi thuộc tính có 1 mã viết tắt
-- để ghép vào SKU.
--
-- Ví dụ:
-- ┌─────────────────┬──────────┬──────────┬───────┐
-- │ AttributeName   │ AttrCode │ DataType │ Order │
-- ├─────────────────┼──────────┼──────────┼───────┤
-- │ Xuất xứ         │ ORIGIN   │ LIST     │ 1     │
-- │ Trọng lượng     │ WEIGHT   │ LIST     │ 2     │
-- │ Thể tích        │ VOLUME   │ LIST     │ 3     │
-- │ Màu sắc         │ COLOR    │ LIST     │ 4     │
-- │ Kích thước      │ SIZE     │ LIST     │ 5     │
-- │ Chất liệu       │ MATERIAL │ LIST     │ 6     │
-- └─────────────────┴──────────┴──────────┴───────┘

CREATE TABLE ProductAttributes (
    AttributeId     INT IDENTITY(1,1) PRIMARY KEY,
    AttrCode        NVARCHAR(20)    NOT NULL UNIQUE,       -- ORIGIN, WEIGHT, COLOR...
    AttributeName   NVARCHAR(100)   NOT NULL,              -- Xuất xứ, Trọng lượng, Màu sắc...
    Description     NVARCHAR(500)   NULL,
    DataType        NVARCHAR(10)    NOT NULL DEFAULT 'LIST' -- LIST (chọn từ danh sách), TEXT (nhập tự do)
                    CHECK (DataType IN ('LIST','TEXT')),
    IncludeInSku    BIT             NOT NULL DEFAULT 1,    -- Có đưa vào mã SKU không
    SkuPosition     INT             NOT NULL DEFAULT 0,    -- Thứ tự xuất hiện trong SKU (1=đầu tiên sau Category)
    IsRequired      BIT             NOT NULL DEFAULT 0,    -- Bắt buộc khi tạo SP
    DisplayOrder    INT             NOT NULL DEFAULT 0,    -- Thứ tự hiển thị trên form
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);
GO


-- ============================================================================
-- BẢNG 2: PRODUCT ATTRIBUTE VALUES (Giá trị mỗi thuộc tính)
-- ============================================================================
-- Mỗi thuộc tính có nhiều giá trị. Mỗi giá trị có 1 mã viết tắt (SkuCode)
-- để ghép vào SKU.
--
-- Ví dụ cho ORIGIN:
-- ┌──────────┬──────────────┬─────────┐
-- │ SkuCode  │ ValueName    │ Attr    │
-- ├──────────┼──────────────┼─────────┤
-- │ VN       │ Việt Nam     │ ORIGIN  │
-- │ JP       │ Nhật Bản     │ ORIGIN  │
-- │ KR       │ Hàn Quốc    │ ORIGIN  │
-- │ CN       │ Trung Quốc  │ ORIGIN  │
-- │ TH       │ Thái Lan    │ ORIGIN  │
-- │ US       │ Mỹ          │ ORIGIN  │
-- │ EU       │ Châu Âu     │ ORIGIN  │
-- │ OT       │ Khác        │ ORIGIN  │
-- └──────────┴──────────────┴─────────┘

CREATE TABLE ProductAttributeValues (
    ValueId         INT IDENTITY(1,1) PRIMARY KEY,
    AttributeId     INT             NOT NULL,
    SkuCode         NVARCHAR(10)    NOT NULL,              -- Mã viết tắt ghép vào SKU: VN, 5K, WH...
    ValueName       NVARCHAR(100)   NOT NULL,              -- Tên hiển thị: Việt Nam, 5kg, Trắng...
    Description     NVARCHAR(200)   NULL,
    DisplayOrder    INT             NOT NULL DEFAULT 0,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_AttrVal_Attr FOREIGN KEY (AttributeId) REFERENCES ProductAttributes(AttributeId),
    CONSTRAINT UQ_AttrVal_Code UNIQUE (AttributeId, SkuCode)   -- Không trùng mã trong cùng thuộc tính
);
GO


-- ============================================================================
-- BẢNG 3: PRODUCT ATTRIBUTE MAP (Gán thuộc tính cho sản phẩm)
-- ============================================================================
-- Mỗi sản phẩm có nhiều thuộc tính. Mỗi thuộc tính chọn 1 giá trị.
-- Khi tạo SP: chọn danh mục → hệ thống hiện form thuộc tính (dựa trên SkuConfigs)
-- → user chọn giá trị → hệ thống tự gen SKU.

CREATE TABLE ProductAttributeMap (
    MapId           INT IDENTITY(1,1) PRIMARY KEY,
    ProductId       INT             NOT NULL,
    AttributeId     INT             NOT NULL,
    ValueId         INT             NULL,                  -- FK → ProductAttributeValues (cho DataType=LIST)
    TextValue       NVARCHAR(200)   NULL,                  -- Giá trị tự nhập (cho DataType=TEXT)
    CONSTRAINT FK_AttrMap_Product FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    CONSTRAINT FK_AttrMap_Attr FOREIGN KEY (AttributeId) REFERENCES ProductAttributes(AttributeId),
    CONSTRAINT FK_AttrMap_Value FOREIGN KEY (ValueId) REFERENCES ProductAttributeValues(ValueId),
    CONSTRAINT UQ_AttrMap UNIQUE (ProductId, AttributeId)  -- Mỗi SP chỉ có 1 giá trị/thuộc tính
);
GO


-- ============================================================================
-- BẢNG 4: SKU CONFIGS (Cấu hình cách gen SKU cho từng danh mục)
-- ============================================================================
-- Mỗi danh mục SP có thể yêu cầu các thuộc tính khác nhau để ghép SKU.
-- Ví dụ: Thực phẩm cần Xuất xứ + Trọng lượng, Vật tư cần Xuất xứ + Kích thước + Màu sắc.

CREATE TABLE SkuConfigs (
    SkuConfigId     INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId      INT             NOT NULL,              -- Danh mục nào
    AttributeId     INT             NOT NULL,              -- Thuộc tính nào
    SkuPosition     INT             NOT NULL,              -- Thứ tự trong SKU (1, 2, 3...)
    IsRequired      BIT             NOT NULL DEFAULT 1,    -- Bắt buộc chọn khi tạo SP trong danh mục này
    DefaultValueId  INT             NULL,                  -- Giá trị mặc định (VD: ORIGIN=VN)
    IsActive        BIT             NOT NULL DEFAULT 1,
    CONSTRAINT FK_SkuCfg_Cat FOREIGN KEY (CategoryId) REFERENCES ProductCategories(CategoryId),
    CONSTRAINT FK_SkuCfg_Attr FOREIGN KEY (AttributeId) REFERENCES ProductAttributes(AttributeId),
    CONSTRAINT FK_SkuCfg_DefVal FOREIGN KEY (DefaultValueId) REFERENCES ProductAttributeValues(ValueId),
    CONSTRAINT UQ_SkuCfg UNIQUE (CategoryId, AttributeId)
);
GO


-- ============================================================================
-- BẢNG 5: SKU SEQUENCES (Bộ đếm sequence tránh trùng)
-- ============================================================================
-- Mỗi tổ hợp (Category prefix + attribute codes) có 1 bộ đếm riêng.
-- Ví dụ: "TP-VN-5K-WH" có sequence 0001, 0002, 0003...
--         "TP-VN-5K-BK" có sequence riêng: 0001, 0002...
-- Dùng UPDLOCK khi gen SKU để thread-safe.

CREATE TABLE SkuSequences (
    SequenceId      INT IDENTITY(1,1) PRIMARY KEY,
    SkuPrefix       NVARCHAR(100)   NOT NULL UNIQUE,       -- "TP-VN-5K-WH" (phần trước sequence number)
    LastNumber      INT             NOT NULL DEFAULT 0,    -- Số cuối cùng đã gen
    UpdatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);
GO


-- ============================================================================
-- SỬA BẢNG ProductCategories — Thêm SkuPrefix
-- ============================================================================

ALTER TABLE ProductCategories ADD
    SkuPrefix       NVARCHAR(10)    NULL;                  -- VD: TP, NL, VT, TB, HH
GO

-- ============================================================================
-- SỬA BẢNG Products — Thêm SKU (mã dài có nghĩa)
-- ============================================================================
-- ProductCode (SP-00001) = mã ngắn nội bộ, vẫn giữ
-- SKU (TP-VN-5K-WH-0001) = mã dài có nghĩa, dùng trong quản lý và in ấn

ALTER TABLE Products ADD
    SKU             NVARCHAR(50)    NULL UNIQUE,            -- Mã SKU tự gen: TP-VN-5K-WH-0001
	SourceUrl       NVARCHAR(500)    NULL;                  -- VD: Url to buy product (shopee,...)
GO


-- ============================================================================
-- INDEXES
-- ============================================================================

CREATE INDEX IX_ProdAttr_Code ON ProductAttributes(AttrCode, IsActive);
CREATE INDEX IX_ProdAttrVal_AttrId ON ProductAttributeValues(AttributeId, IsActive);
CREATE INDEX IX_ProdAttrMap_ProductId ON ProductAttributeMap(ProductId);
CREATE INDEX IX_ProdAttrMap_AttrVal ON ProductAttributeMap(AttributeId, ValueId);
CREATE INDEX IX_SkuCfg_CategoryId ON SkuConfigs(CategoryId, IsActive);
CREATE INDEX IX_Products_SKU ON Products(SKU);
CREATE INDEX IX_Products_Brand ON Products(Brand);
GO


-- ============================================================================
-- SEED DATA
-- ============================================================================

-- ── Cập nhật SkuPrefix cho danh mục (nếu đã có data) ──
-- (Chạy thủ công hoặc thêm vào DbSeeder)
-- UPDATE ProductCategories SET SkuPrefix = 'TP' WHERE CategoryCode = 'THUC_PHAM';
-- UPDATE ProductCategories SET SkuPrefix = 'NL' WHERE CategoryCode = 'NGUYEN_LIEU';

-- ── Thuộc tính mẫu ──
INSERT INTO ProductAttributes (AttrCode, AttributeName, DataType, IncludeInSku, SkuPosition, IsRequired, DisplayOrder) VALUES
('ORIGIN',   N'Xuất xứ',      'LIST', 1, 1, 0, 1),
('WEIGHT',   N'Trọng lượng',  'LIST', 1, 2, 0, 2),
('VOLUME',   N'Thể tích',     'LIST', 1, 2, 0, 3),
('COLOR',    N'Màu sắc',      'LIST', 1, 3, 0, 4),
('SIZE',     N'Kích thước',   'LIST', 1, 3, 0, 5),
('MATERIAL', N'Chất liệu',    'LIST', 0, 0, 0, 6),
('SPEC',     N'Quy cách',     'TEXT', 0, 0, 0, 7);

-- ── Giá trị thuộc tính: Xuất xứ ──
DECLARE @OriginId INT = (SELECT AttributeId FROM ProductAttributes WHERE AttrCode = 'ORIGIN');
INSERT INTO ProductAttributeValues (AttributeId, SkuCode, ValueName, DisplayOrder) VALUES
(@OriginId, 'VN', N'Việt Nam',     1),
(@OriginId, 'JP', N'Nhật Bản',     2),
(@OriginId, 'KR', N'Hàn Quốc',    3),
(@OriginId, 'CN', N'Trung Quốc',  4),
(@OriginId, 'TH', N'Thái Lan',    5),
(@OriginId, 'US', N'Mỹ',          6),
(@OriginId, 'EU', N'Châu Âu',     7),
(@OriginId, 'AU', N'Úc',          8),
(@OriginId, 'OT', N'Khác',        99);

-- ── Giá trị thuộc tính: Trọng lượng ──
DECLARE @WeightId INT = (SELECT AttributeId FROM ProductAttributes WHERE AttrCode = 'WEIGHT');
INSERT INTO ProductAttributeValues (AttributeId, SkuCode, ValueName, DisplayOrder) VALUES
(@WeightId, '100G', N'100 gram',     1),
(@WeightId, '250G', N'250 gram',     2),
(@WeightId, '500G', N'500 gram',     3),
(@WeightId, '1K',   N'1 kg',         4),
(@WeightId, '2K',   N'2 kg',         5),
(@WeightId, '5K',   N'5 kg',         6),
(@WeightId, '10K',  N'10 kg',        7),
(@WeightId, '25K',  N'25 kg',        8),
(@WeightId, 'NA',   N'Không áp dụng', 99);

-- ── Giá trị thuộc tính: Thể tích ──
DECLARE @VolumeId INT = (SELECT AttributeId FROM ProductAttributes WHERE AttrCode = 'VOLUME');
INSERT INTO ProductAttributeValues (AttributeId, SkuCode, ValueName, DisplayOrder) VALUES
(@VolumeId, '250M', N'250 ml',       1),
(@VolumeId, '500M', N'500 ml',       2),
(@VolumeId, '1L',   N'1 lít',        3),
(@VolumeId, '5L',   N'5 lít',        4),
(@VolumeId, '20L',  N'20 lít',       5),
(@VolumeId, 'NA',   N'Không áp dụng', 99);

-- ── Giá trị thuộc tính: Màu sắc ──
DECLARE @ColorId INT = (SELECT AttributeId FROM ProductAttributes WHERE AttrCode = 'COLOR');
INSERT INTO ProductAttributeValues (AttributeId, SkuCode, ValueName, DisplayOrder) VALUES
(@ColorId, 'WH', N'Trắng',       1),
(@ColorId, 'BK', N'Đen',         2),
(@ColorId, 'RD', N'Đỏ',          3),
(@ColorId, 'BL', N'Xanh dương',  4),
(@ColorId, 'GR', N'Xanh lá',     5),
(@ColorId, 'YL', N'Vàng',        6),
(@ColorId, 'CL', N'Trong suốt',  7),
(@ColorId, 'NA', N'Không áp dụng', 99);

-- ── Giá trị thuộc tính: Kích thước ──
DECLARE @SizeId INT = (SELECT AttributeId FROM ProductAttributes WHERE AttrCode = 'SIZE');
INSERT INTO ProductAttributeValues (AttributeId, SkuCode, ValueName, DisplayOrder) VALUES
(@SizeId, 'XS', N'Rất nhỏ',     1),
(@SizeId, 'SM', N'Nhỏ',         2),
(@SizeId, 'MD', N'Trung bình',  3),
(@SizeId, 'LG', N'Lớn',         4),
(@SizeId, 'XL', N'Rất lớn',     5),
(@SizeId, 'NA', N'Không áp dụng', 99);
GO


-- ============================================================================
-- STORED PROCEDURE: Gen SKU tự động (thread-safe)
-- ============================================================================

CREATE OR ALTER PROCEDURE sp_GenerateSKU
    @ProductId      INT,
    @GeneratedSKU   NVARCHAR(50)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CatPrefix NVARCHAR(10);
    DECLARE @SkuBody NVARCHAR(100) = '';
    DECLARE @SkuPrefix NVARCHAR(100);
    DECLARE @NextNum INT;

    -- 1. Lấy prefix danh mục
    SELECT @CatPrefix = ISNULL(pc.SkuPrefix, 'XX')
    FROM Products p
    INNER JOIN ProductCategories pc ON p.CategoryId = pc.CategoryId
    WHERE p.ProductId = @ProductId;

    IF @CatPrefix IS NULL SET @CatPrefix = 'XX';

    -- 2. Ghép attribute codes theo thứ tự SkuPosition
    SELECT @SkuBody = @SkuBody +
        CASE WHEN @SkuBody = '' THEN '' ELSE '-' END +
        ISNULL(pav.SkuCode, 'NA')
    FROM ProductAttributeMap pam
    INNER JOIN ProductAttributes pa ON pam.AttributeId = pa.AttributeId
    LEFT JOIN ProductAttributeValues pav ON pam.ValueId = pav.ValueId
    INNER JOIN SkuConfigs sc ON sc.CategoryId = (
        SELECT CategoryId FROM Products WHERE ProductId = @ProductId
    ) AND sc.AttributeId = pa.AttributeId
    WHERE pam.ProductId = @ProductId
      AND pa.IncludeInSku = 1
      AND sc.IsActive = 1
    ORDER BY sc.SkuPosition;

    -- 3. Build prefix (phần trước sequence)
    SET @SkuPrefix = @CatPrefix +
        CASE WHEN @SkuBody = '' THEN '' ELSE '-' + @SkuBody END;

    -- 4. Gen sequence number (thread-safe với UPDLOCK)
    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM SkuSequences WITH (UPDLOCK) WHERE SkuPrefix = @SkuPrefix)
    BEGIN
        UPDATE SkuSequences
        SET LastNumber = LastNumber + 1, UpdatedAt = SYSDATETIME()
        WHERE SkuPrefix = @SkuPrefix;

        SELECT @NextNum = LastNumber FROM SkuSequences WHERE SkuPrefix = @SkuPrefix;
    END
    ELSE
    BEGIN
        INSERT INTO SkuSequences (SkuPrefix, LastNumber)
        VALUES (@SkuPrefix, 1);

        SET @NextNum = 1;
    END

    -- 5. Build SKU hoàn chỉnh
    SET @GeneratedSKU = @SkuPrefix + '-' + RIGHT('0000' + CAST(@NextNum AS NVARCHAR), 4);

    -- 6. Cập nhật Product
    UPDATE Products SET SKU = @GeneratedSKU, UpdatedAt = SYSDATETIME()
    WHERE ProductId = @ProductId;

    COMMIT;
END;
GO


-- ============================================================================
-- GHI CHÚ IMPLEMENTATION
-- ============================================================================
/*
    ┌─────────────────────────────────────────────────────────────┐
    │            LUỒNG TẠO SẢN PHẨM MỚI (CÓ SKU)                │
    ├─────────────────────────────────────────────────────────────┤
    │                                                             │
    │  1. User chọn Danh mục (VD: Thực phẩm → SkuPrefix = "TP") │
    │                                                             │
    │  2. Frontend gọi GET /api/skuconfigs?categoryId=5           │
    │     → Trả về danh sách thuộc tính cần chọn cho danh mục    │
    │       [{ attrCode: "ORIGIN", values: [...] },               │
    │        { attrCode: "WEIGHT", values: [...] },               │
    │        { attrCode: "COLOR",  values: [...] }]               │
    │                                                             │
    │  3. User chọn giá trị cho từng thuộc tính                   │
    │     → Frontend preview SKU realtime: "TP-VN-5K-WH-????"    │
    │                                                             │
    │  4. User nhập tên SP, giá, ảnh... → bấm Lưu                │
    │                                                             │
    │  5. Backend: ProductService.CreateAsync()                   │
    │     a) Insert Products (chưa có SKU)                        │
    │     b) Insert ProductAttributeMap cho mỗi thuộc tính        │
    │     c) EXEC sp_GenerateSKU @ProductId, @SKU OUTPUT          │
    │        → SKU = "TP-VN-5K-WH-0001"                          │
    │     d) Return product với SKU đã gen                        │
    │                                                             │
    │  6. SKU dùng để:                                            │
    │     - In trên barcode label                                 │
    │     - Hiện trên báo giá / SO                                │
    │     - Tìm kiếm nhanh (quét barcode hoặc gõ SKU)            │
    │     - Phân biệt 2 SP cùng tên nhưng khác thuộc tính        │
    │                                                             │
    ├─────────────────────────────────────────────────────────────┤
    │                     CẤU TRÚC SKU                             │
    ├─────────────────────────────────────────────────────────────┤
    │                                                             │
    │  ┌────────┬────────┬────────┬────────┬────────┐             │
    │  │ Cat.   │ Attr 1 │ Attr 2 │ Attr 3 │ Seq.   │             │
    │  │ Prefix │ (pos 1)│ (pos 2)│ (pos 3)│ Number │             │
    │  ├────────┼────────┼────────┼────────┼────────┤             │
    │  │ TP     │ VN     │ 5K     │ WH     │ 0001   │             │
    │  └────────┴────────┴────────┴────────┴────────┘             │
    │                                                             │
    │  - Cat. Prefix: từ ProductCategories.SkuPrefix              │
    │  - Attributes:  từ SkuConfigs → ProductAttributeValues      │
    │  - Sequence:    từ SkuSequences (thread-safe, per-prefix)   │
    │  - Thuộc tính không áp dụng: dùng "NA"                      │
    │  - Tối đa ~50 ký tự                                         │
    │                                                             │
    ├─────────────────────────────────────────────────────────────┤
    │               TÍNH LINH HOẠT                                 │
    ├─────────────────────────────────────────────────────────────┤
    │                                                             │
    │  - Thêm thuộc tính mới: INSERT ProductAttributes            │
    │  - Thêm giá trị mới: INSERT ProductAttributeValues          │
    │  - Đổi cấu hình SKU cho danh mục: UPDATE SkuConfigs         │
    │  - Không cần sửa code khi thêm thuộc tính mới              │
    │  - SkuConfigs quyết định mỗi danh mục dùng thuộc tính nào  │
    │                                                             │
    └─────────────────────────────────────────────────────────────┘
*/


PRINT '═══ SKU Generator Migration completed successfully ═══';
PRINT 'New tables: ProductAttributes, ProductAttributeValues, ProductAttributeMap, SkuConfigs, SkuSequences';
PRINT 'Modified: ProductCategories (+SkuPrefix), Products (+SKU, +Brand, +Origin)';
PRINT 'New procedure: sp_GenerateSKU';
GO
