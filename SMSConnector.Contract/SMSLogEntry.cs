using System;

namespace SMSConnector.Contract
{
    /// <summary>
    /// Model đại diện cho một bản ghi log kiểm toán cước SMS.
    /// Ánh xạ tương ứng với bảng tbl_SMSLog và API Logging tập trung.
    /// </summary>
    public class SMSLogEntry
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string MessageContent { get; set; } = string.Empty;
        public string Brandname { get; set; }
        public string ProviderCode { get; set; } = string.Empty;
        public string ProviderMessageId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public int TotalSms { get; set; } = 1;
        public string RawResponse { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
