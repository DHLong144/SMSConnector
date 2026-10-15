namespace SMSConnector.Contract
{
    /// <summary>
    /// Request gửi sms.
    /// </summary>
    public class SMSMessageRequest
    {
        /// <summary>
        /// Số điện thoại người nhận
        /// </summary>
        public string PhoneNumber { get; set; }
        /// <summary>
        /// Nội dùng tin nhắn
        /// </summary>
        public string MessageContent { get; set; }
        /// <summary>
        /// Tên thương hiệu
        /// </summary>
        public string Brandname { get; set; }
        /// <summary>
        /// Mã nhà cung cấp dịch vụ sms
        /// </summary>
        public string ProviderCode { get; set; }
        /// <summary>
        /// Id người gửi sms
        /// </summary>
        public string UserId { get; set; }
    }
}
