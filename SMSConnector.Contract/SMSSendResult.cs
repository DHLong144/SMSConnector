using System;

namespace SMSConnector.Contract
{
    /// <summary>
    /// Kết quả trả về.
    /// </summary>
    public class SMSSendResult
    {
        /// <summary>
        /// True nếu nhà mạng tiếp nhận thành công
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Mã tin nhắn do nhà mạng cấp (Transaction ID / Message ID)
        /// </summary>
        public string ProviderMessageId { get; set; }

        /// <summary>
        /// Mã lỗi từ nhà mạng hoặc lỗi nội bộ nếu thất bại
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Mô tả chi tiết nguyên nhân lỗi
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Số tin SMS
        /// </summary>
        public int TotalSms { get; set; }

        /// <summary>
        /// Payload phản hồi thô (JSON/XML) từ nhà mạng để phục vụ debug
        /// </summary>
        public string RawResponse { get; set; }

        /// <summary>
        /// Thời điểm gửi
        /// </summary>
        public DateTime SentTime { get; set; } = DateTime.Now;

        public static SMSSendResult Success(string messageId, string rawResponse = null, int totalSms = 1)
        {
            return new SMSSendResult
            {
                IsSuccess = true,
                ProviderMessageId = messageId,
                RawResponse = rawResponse,
                TotalSms = totalSms,
                SentTime = DateTime.Now
            };
        }

        public static SMSSendResult Failed(string errorCode, string errorMessage, string rawResponse = null)
        {
            return new SMSSendResult
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                RawResponse = rawResponse,
                SentTime = DateTime.Now
            };
        }
    }
}
