-- =========================================================================
-- Script tạo bảng ghi log kiểm toán cước và lịch sử gửi tin: tbl_SMSLog
-- Chạy trên Database dùng chung của LIS (Labconnect Pro)
-- =========================================================================

IF NOT EXISTS (SELECT * FROM sys.all_objects WHERE object_id = OBJECT_ID(N'[dbo].[tbl_SMSLog]') AND type IN ('U'))
BEGIN
    CREATE TABLE [dbo].[tbl_SMSLog] (
        [LogId] UNIQUEIDENTIFIER DEFAULT NEWID() NOT NULL PRIMARY KEY,
        [PatientId] VARCHAR(50) NULL,               -- Mã bệnh nhân
        [PatientName] NVARCHAR(100) NULL,           -- Tên bệnh nhân
        [SID] VARCHAR(50) NULL,                     -- Barcode xét nghiệm / Sample ID
        [PhoneNumber] VARCHAR(20) NOT NULL,         -- Số điện thoại nhận tin
        [MessageContent] NVARCHAR(1000) NOT NULL,   -- Nội dung tin nhắn
        [Brandname] VARCHAR(50) NULL,               -- Tên Brandname gửi tin
        [ProviderCode] VARCHAR(50) NOT NULL,        -- Mã nhà mạng (SPEEDSMS, VIETTEL, VNPT, ESMS...)
        [ProviderMessageId] VARCHAR(100) NULL,      -- Mã định danh tin do nhà mạng trả về (Transaction ID)
        [IsSuccess] BIT NOT NULL,                   -- 1: Thành công, 0: Thất bại
        [ErrorCode] VARCHAR(50) NULL,               -- Mã lỗi phản hồi
        [ErrorMessage] NVARCHAR(500) NULL,          -- Diễn giải lỗi
        [TotalSmsCount] INT DEFAULT 1 NOT NULL,     -- Số lượng tin bị tính cước
        [RawResponse] NVARCHAR(MAX) NULL,           -- Payload phản hồi thô (JSON/XML) để đối soát
        [UserLogin] VARCHAR(50) NULL,               -- Tài khoản nhân viên bấm gửi trên LIS
        [ReferenceId] VARCHAR(100) NULL,            -- Mã phiếu khám / OrderId tham chiếu
        [CreatedDate] DATETIME DEFAULT GETDATE() NOT NULL -- Thời gian gửi
    );

    -- Tạo các Index tối ưu hóa tìm kiếm tra soát theo SĐT, Mã BN và Thời gian
    CREATE NONCLUSTERED INDEX [IX_tbl_SMSLog_Phone] ON [dbo].[tbl_SMSLog] ([PhoneNumber]);
    CREATE NONCLUSTERED INDEX [IX_tbl_SMSLog_PatientId] ON [dbo].[tbl_SMSLog] ([PatientId]);
    CREATE NONCLUSTERED INDEX [IX_tbl_SMSLog_CreatedDate] ON [dbo].[tbl_SMSLog] ([CreatedDate]);
    
    PRINT 'Đã tạo thành công bảng tbl_SMSLog!';
END
ELSE
BEGIN
    PRINT 'Bảng tbl_SMSLog đã tồn tại.';
END
GO
