using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SMSConnector.Contract;

namespace SMSConnectivity.SpeedSMS
{
    /// <summary>
    /// Adapter kết nối gửi tin nhắn qua SpeedSMS Gateway (https://speedsms.vn).
    /// Kế thừa hợp đồng ISmsConnector, đọc trực tiếp cấu hình qua IConfiguration.
    /// </summary>
    public class SpeedSMSConnectService : ISmsConnector
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _accessToken;
        private readonly string _brandName;
        private readonly int _smsType;
        private readonly int _timeout;

        /// <summary>
        /// Mã định danh nhà mạng để Factory nạp động
        /// </summary>
        public string ProviderCode => "SPEEDSMS";

        public SpeedSMSConnectService(IConfiguration config, HttpClient httpClient)
        {
            var cfg = config.GetSection("SMSSetting:Providers:SPEEDSMS");
            _httpClient = httpClient;
            _baseUrl = cfg["BaseUrl"] ?? null;
            _accessToken = cfg["AccessToken"] ?? null;
            _brandName = cfg["Brandname"] ?? null;
            
            if (int.TryParse(cfg["Timeout"], out int parsedTimeout) && parsedTimeout > 0)
            {
                _timeout = parsedTimeout;
            }
            else
            {
                _timeout = 15;
            }

            if (int.TryParse(cfg["SmsType"], out int parsedSmsType) && parsedSmsType > 1 && parsedSmsType < 6)
            {
                _smsType = parsedSmsType;
            }
            else
            {
                _smsType = 2;
            }
        }

        /// <summary>
        /// Thực thi gửi 1 tin SMS qua SpeedSMS API
        /// </summary>
        public async Task<SMSSendResult> SendSMSAsync(SMSMessageRequest message)
        {
            if (string.IsNullOrWhiteSpace(_baseUrl) || string.IsNullOrWhiteSpace(_accessToken))
            {
                return SMSSendResult.Failed("CONFIGURATION_ERROR", "Chưa cấu hình BaseUrl hoặc AccessToken cho dịch vụ SMS.");
            }
                
            if (message == null || string.IsNullOrWhiteSpace(message.PhoneNumber) 
                || string.IsNullOrWhiteSpace(message.MessageContent) 
                || string.IsNullOrWhiteSpace(message.UserId)
                || string.IsNullOrWhiteSpace(message.ProviderCode))
            {
                return SMSSendResult.Failed("INVALID_PARAM", "Dữ liệu tin nhắn rỗng hoặc không hợp lệ.");
            }

            // Chuẩn hóa số điện thoại
            string phone = NormalizePhoneNumber(message.PhoneNumber);
            if (string.IsNullOrWhiteSpace(phone))
            {
                return SMSSendResult.Failed("INVALID_PHONE", $"Số điện thoại không hợp lệ: {message.PhoneNumber}");
            }

            var payload = new
            {
                to = new string[] { phone },
                content = message.MessageContent,
                type = _smsType,
                sender = message.Brandname ?? _brandName
            };

            var endpoint = _baseUrl + "/sms/send";

            string jsonBody = JsonSerializer.Serialize(payload);
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            requestMessage.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Xác thực (Authorization): SpeedSMS hỗ trợ Basic Auth với Token làm username
            if (!string.IsNullOrWhiteSpace(_accessToken))
            {
                var authBytes = Encoding.ASCII.GetBytes($"{_accessToken}:x");
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            // Gửi HTTP Request kèm CancellationToken timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeout));

            string rawResponse = string.Empty;
            try
            {
                var response = await _httpClient.SendAsync(requestMessage, cts.Token);
                rawResponse = await response.Content.ReadAsStringAsync();

                // Kiểm tra mã HTTP trả về từ SpeedSMS Gateway
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = $"HTTP {response.StatusCode}";
                    if (!string.IsNullOrWhiteSpace(rawResponse))
                    {
                        try
                        {
                            using var errDoc = JsonDocument.Parse(rawResponse);
                            var root = errDoc.RootElement;
                            if (root.TryGetProperty("message", out var msgElement))
                            {
                                errorMsg = msgElement.GetString() ?? errorMsg;
                            }
                            else if (root.TryGetProperty("name", out var nameElement))
                            {
                                errorMsg = nameElement.GetString() ?? errorMsg;
                            }
                        }
                        catch { }
                    }

                    return SMSSendResult.Failed(
                        errorCode: $"SPEEDSMS_HTTP_{(int)response.StatusCode}",
                        errorMessage: $"SpeedSMS báo lỗi: {errorMsg}",
                        rawResponse: rawResponse
                    );
                }

                // 6. Parse kết quả trả về từ SpeedSMS khi HTTP 200 OK
                using var doc = JsonDocument.Parse(rawResponse);
                var rootElement = doc.RootElement;

                string status = rootElement.TryGetProperty("status", out var statusProp)
                    ? statusProp.GetString()?.ToLower() ?? string.Empty
                    : string.Empty;

                string code = rootElement.TryGetProperty("code", out var codeProp)
                    ? codeProp.GetString()?.ToLower() ?? string.Empty
                    : string.Empty;

                bool hasData = rootElement.TryGetProperty("data", out var dataProp);

                if (status == "success")
                {
                    string tranId = null;
                    int totalSms = 1;

                    if (hasData)
                    {
                        if (dataProp.TryGetProperty("tranId", out var tranProp))
                        {
                            tranId = tranProp.GetString() ?? tranProp.GetRawText();
                        }

                        if (dataProp.TryGetProperty("totalSMS", out var totalProp) && totalProp.TryGetInt32(out int count))
                        {
                            totalSms = count;
                        }
                    }

                    return SMSSendResult.Success(
                        messageId: tranId ?? Guid.NewGuid().ToString(),
                        rawResponse: rawResponse,
                        totalSms: totalSms
                    );
                }
                else
                {
                    string errorMsg = rootElement.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString() ?? ""
                        : "";

                    return SMSSendResult.Failed(
                        errorCode: $"SPEEDSMS_{code}",
                        errorMessage: errorMsg,
                        rawResponse: rawResponse
                    );
                }
            }
            catch (TaskCanceledException)
            {
                return SMSSendResult.Failed("TIMEOUT", $"Timeout sau {_timeout}s khi gọi sang cổng SpeedSMS.", rawResponse);
            }
            catch (Exception ex)
            {
                return SMSSendResult.Failed("EXCEPTION", ex.Message, rawResponse);
            }
        }

        /// <summary>
        /// Chuẩn hóa số điện thoại: loại bỏ dấu cách, chuyển đầu số 84 hoặc 0
        /// </summary>
        private static string NormalizePhoneNumber(string rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone)) return string.Empty;

            string clean = rawPhone.Trim()
                .Replace(" ", "")
                .Replace(".", "")
                .Replace("-", "")
                .Replace("+", "");

            if (clean.StartsWith("84") && clean.Length >= 11)
            {
                clean = "0" + clean.Substring(2);
            }

            return clean;
        }
    }
}
