using System.Threading.Tasks;

namespace SMSConnector.Contract
{
    /// <summary>
    /// Giao diện chuẩn (Hợp đồng) cho tất cả các Plugin nhà mạng SMS.
    /// Mỗi nhà cung cấp (SpeedSMS, Viettel, VNPT, eSMS...) bắt buộc phải triển khai interface này.
    /// </summary>
    public interface ISmsConnector
    {
        /// <summary>
        /// Mã định danh nhà mạng (Ví dụ: "SPEEDSMS", "VIETTEL", "VNPT", "ESMS").
        /// Dùng để Factory nhận diện và map với ProviderCode trong cấu hình.
        /// </summary>
        string ProviderCode { get; }

        /// <summary>
        /// Gửi 1 tin nhắn SMS duy nhất và trả kết quả ngay lập tức cho LIS.
        /// Cấu hình được đọc trực tiếp qua IConfiguration từ appsettings.json mà không cần tạo class riêng.
        /// </summary>
        /// <param name="message">Dữ liệu tin nhắn từ LIS</param>
        /// <param name="config">Cấu hình nhà mạng đọc trực tiếp từ appsettings qua IConfiguration</param>
        Task<SMSSendResult> SendSMSAsync(SMSMessageRequest message);
    }
}
