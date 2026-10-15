using log4net;
using SMSConnector.Contract;
using System.Text;
using System.Text.Json;

namespace SMSConnector.Engine
{
    /// <summary>
    /// Service ghi log kiểm toán cước và lịch sử gửi tin SMS.
    /// Gọi API Logging tập trung (URL_API_LOGGING). Nếu API lỗi hoặc chưa cấu hình thì chỉ ghi ra file qua log4net.
    /// </summary>
    public static class SmsActionLogService
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(SmsActionLogService));
        private static readonly HttpClient _httpClient = new HttpClient();
        private static string _loggingApiUrl;

        public static void Configure(string loggingApiUrl)
        {
            _loggingApiUrl = loggingApiUrl?.Trim();
        }

        /// <summary>
        /// Ghi nhận 1 giao dịch gửi SMS bằng cách gọi sang API Logging.
        /// Bọc try-catch toàn bộ: nếu có lỗi thì chỉ ghi log qua log4net, không làm gián đoạn luồng chính.
        /// </summary>
        public static async Task InsertLogAsync(SMSMessageRequest message, SMSSendResult result, string providerCode)
        {
            // 1. Chuẩn bị Payload log
            var logPayload = new
            {
                PhoneNumber = message?.PhoneNumber ?? string.Empty,
                MessageContent = message?.MessageContent ?? string.Empty,
                Brandname = message?.Brandname,
                ProviderCode = providerCode,
                ProviderMessageId = result?.ProviderMessageId,
                IsSuccess = result?.IsSuccess ?? false,
                ErrorCode = result?.ErrorCode,
                ErrorMessage = result?.ErrorMessage,
                TotalSms = result?.TotalSms ?? 1,
                RawResponse = result?.RawResponse,
                UserId = message?.UserId ?? message?.UserId,
                CreatedDate = DateTime.Now
            };

            // 2. Ghi trace log nội bộ bằng log4net trước
            _logger.Info($"[Audit Log] Gửi SMS: Người gửi = {logPayload.UserId}, SĐT={logPayload.PhoneNumber}, Nội dung = {logPayload.MessageContent}, Nhà cung cấp dịch vụ SMS={providerCode}, Raw Response= {logPayload.RawResponse}, Thành công={logPayload.IsSuccess}, Mã lỗi={logPayload.ErrorCode}, Diễn giải={logPayload.ErrorMessage}");

            // 3. Nếu chưa cấu hình LoggingApiUrl, chỉ log debug và kết thúc
            if (string.IsNullOrWhiteSpace(_loggingApiUrl))
            {
                _logger.Debug("[Audit Log] Bỏ qua gọi API Logging vì LoggingApiUrl chưa được cấu hình.");
                return;
            }

            // 4. Bắn log sang API Logging tập trung
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string jsonBody = JsonSerializer.Serialize(logPayload);
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _loggingApiUrl);
                requestMessage.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(requestMessage, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    string errContent = await response.Content.ReadAsStringAsync();
                    _logger.Warn($"[Audit Log API] Gọi API Logging gặp lỗi HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {errContent}");
                }
            }
            catch (Exception ex)
            {
                // Gọi API lỗi thì thôi, chỉ ghi log ra file bằng log4net trong dự án
                _logger.Error($"[Audit Log API] Lỗi khi gọi API Logging ({_loggingApiUrl}): {ex.Message}", ex);
            }
        }
    }
}
