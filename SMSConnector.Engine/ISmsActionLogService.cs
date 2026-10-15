using SMSConnector.Contract;

namespace SMSConnector.Engine
{
    /// <summary>
    /// Giao diện xử lý ghi nhận lịch sử kiểm toán gửi tin SMS.
    /// Hỗ trợ xếp hàng bất đồng bộ (Queue) an toàn để không làm chậm luồng gửi tin chính.
    /// </summary>
    public interface ISmsActionLogService
    {
        /// <summary>
        /// Đẩy yêu cầu ghi log vào hàng đợi xử lý ngầm (non-blocking).
        /// </summary>
        void QueueLog(SMSMessageRequest message, SMSSendResult result, string providerCode);

        /// <summary>
        /// Xử lý gửi log sang API Logging tập trung.
        /// </summary>
        Task ProcessLogAsync(SMSLogEntry logPayload, CancellationToken cancellationToken = default);
    }
}
