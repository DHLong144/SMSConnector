using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using log4net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SMSConnector.Contract;

namespace SMSConnector.Engine
{
    /// <summary>
    /// Service ghi log kiểm toán cước và lịch sử gửi tin SMS.
    /// Chạy ngầm dưới dạng BackgroundService thông qua System.Threading.Channels,
    /// đảm bảo không làm gián đoạn luồng API chính và không bị mất log khi ứng dụng shutdown.
    /// </summary>
    public class SmsActionLogService : BackgroundService, ISmsActionLogService
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(SmsActionLogService));
        private readonly Channel<SMSLogEntry> _channel;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _loggingApiUrl;

        public SmsActionLogService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _loggingApiUrl = configuration["LoggingApiUrl"]?.Trim();

            // Khởi tạo Bounded Channel với dung lượng 10.000 log để kiểm soát bộ nhớ
            var options = new BoundedChannelOptions(10000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            };
            _channel = Channel.CreateBounded<SMSLogEntry>(options);
        }

        /// <summary>
        /// Đẩy bản ghi log vào hàng đợi Channel để xử lý ngầm (non-blocking, dưới 1ms).
        /// </summary>
        public void QueueLog(SMSMessageRequest message, SMSSendResult result, string providerCode)
        {
            var logPayload = new SMSLogEntry
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
                UserId = message?.UserId,
                CreatedDate = DateTime.Now
            };

            // 1. Ghi trace log nội bộ bằng log4net ngay lập tức
            _logger.Info($"[Audit Log] Gửi SMS: Người gửi={logPayload.UserId}, SĐT={logPayload.PhoneNumber}, Nội dung={logPayload.MessageContent}, Nhà mạng={providerCode}, Thành công={logPayload.IsSuccess}, Mã lỗi={logPayload.ErrorCode}, Diễn giải={logPayload.ErrorMessage}");

            // 2. Đưa vào Channel ngầm
            if (!_channel.Writer.TryWrite(logPayload))
            {
                // Nếu channel tạm đầy, đẩy vào task ghi bất đồng bộ
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _channel.Writer.WriteAsync(logPayload);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[Audit Log Worker] Lỗi khi enqueue log vào Channel: {ex.Message}", ex);
                    }
                });
            }
        }

        /// <summary>
        /// Vòng lặp đọc log từ Channel và gửi sang API Logging tập trung.
        /// Tự động xử lý dọn dẹp các log còn tồn đọng khi ứng dụng dừng (Graceful Shutdown).
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info("[Audit Log Worker] Background logging worker đã khởi động thành công.");

            try
            {
                while (await _channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    while (_channel.Reader.TryRead(out var logItem))
                    {
                        await ProcessLogAsync(logItem, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.Info("[Audit Log Worker] Đang nhận tín hiệu dừng ứng dụng. Đang xử lý các bản ghi còn lại...");
            }
            catch (Exception ex)
            {
                _logger.Error($"[Audit Log Worker] Lỗi ngoại lệ trong vòng lặp background worker: {ex.Message}", ex);
            }

            // Xử lý nốt các bản ghi còn trong hàng đợi trước khi tắt hoàn toàn
            while (_channel.Reader.TryRead(out var remainingItem))
            {
                try
                {
                    await ProcessLogAsync(remainingItem, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[Audit Log Worker] Lỗi khi ghi bản ghi còn sót: {ex.Message}", ex);
                }
            }

            _logger.Info("[Audit Log Worker] Background logging worker đã hoàn thành và dừng lại an toàn.");
        }

        /// <summary>
        /// Gửi log sang API Logging tập trung qua HttpClientFactory.
        /// </summary>
        public async Task ProcessLogAsync(SMSLogEntry logPayload, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_loggingApiUrl))
            {
                return;
            }

            try
            {
                var httpClient = _httpClientFactory.CreateClient("LoggingClient");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                string jsonBody = JsonSerializer.Serialize(logPayload);
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _loggingApiUrl);
                requestMessage.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(requestMessage, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    string errContent = await response.Content.ReadAsStringAsync(cts.Token);
                    _logger.Warn($"[Audit Log API] Gọi API Logging gặp lỗi HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {errContent}");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // App đang dừng, bỏ qua warning
            }
            catch (Exception ex)
            {
                // Ghi log ra file bằng log4net nếu API logging ngoài bị lỗi
                _logger.Error($"[Audit Log API] Lỗi khi gọi API Logging ({_loggingApiUrl}): {ex.Message}", ex);
            }
        }
    }
}
