using log4net;
using Microsoft.AspNetCore.Mvc;
using SMSConnector.Contract;
using SMSConnector.Engine;

namespace SMSConnector.ApiHost.Controllers
{
    /// <summary>
    /// API Gateway tiếp nhận yêu cầu gửi SMS từ LIS.
    /// Thiết kế theo nguyên tắc K.I.S.S: Chỉ làm nhiệm vụ điều phối và chuyển tiếp sang Plug-in DLL.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SmsController : ControllerBase
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(SmsController));
        private readonly IConfiguration _configuration;
        private readonly ISmsPluginFactory _pluginFactory;
        private readonly ISmsActionLogService _actionLogService;

        public SmsController(
            IConfiguration configuration,
            ISmsPluginFactory pluginFactory,
            ISmsActionLogService actionLogService)
        {
            _configuration = configuration;
            _pluginFactory = pluginFactory;
            _actionLogService = actionLogService;
        }

        /// <summary>
        /// API gửi 1 tin nhắn SMS duy nhất cho bệnh nhân.
        /// </summary>
        /// <param name="request">Thông tin tin nhắn từ LIS</param>
        /// <returns>SMSSendResult chứa trạng thái thành công/thất bại</returns>
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SMSMessageRequest request)
        {
            // 1. Kiểm tra tính hợp lệ của đầu vào (Input Validation)
            if (request == null)
            {
                return BadRequest(SMSSendResult.Failed("INVALID_REQUEST", "Dữ liệu yêu cầu rỗng."));
            }

            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(SMSSendResult.Failed("EMPTY_PHONE", "Số điện thoại nhận tin không được để trống."));
            }

            if (string.IsNullOrWhiteSpace(request.MessageContent))
            {
                return BadRequest(SMSSendResult.Failed("EMPTY_CONTENT", "Nội dung tin nhắn không được để trống."));
            }

            // 2. Xác định nhà cung cấp SMS (Lấy theo request chỉ định, hoặc lấy nhà mạng mặc định)
            string providerCode = !string.IsNullOrWhiteSpace(request.ProviderCode)
                ? request.ProviderCode.Trim().ToUpper()
                : _configuration["SMSSetting:DefaultProvider"]?.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(providerCode))
            {
                return BadRequest(SMSSendResult.Failed("NO_PROVIDER", "Chưa cấu hình mã nhà mạng (ProviderCode)."));
            }

            request.ProviderCode = providerCode;

            // 3. Đọc cấu hình kết nối trực tiếp từ appsettings qua IConfiguration
            var providerConfig = _configuration.GetSection($"SMSSetting:Providers:{providerCode}");
            if (!providerConfig.Exists())
            {
                _logger.Warn($"[SmsController] Không tìm thấy cấu hình cho nhà mạng [{providerCode}] trong appsettings.json.");
                return BadRequest(SMSSendResult.Failed("CONFIG_NOT_FOUND", $"Không tìm thấy cấu hình kết nối cho nhà mạng [{providerCode}]. Vui lòng cấu hình trong appsettings.json"));
            }

            SMSSendResult sendResult;
            try
            {
                // 4. Lấy Plugin Adapter từ Factory và gọi gửi tin
                var connector = _pluginFactory.GetConnector(providerCode);
                sendResult = await connector.SendSMSAsync(request);
            }
            catch (NotSupportedException ex)
            {
                _logger.Error($"[SmsController] Không tìm thấy Plugin: {ex.Message}");
                sendResult = SMSSendResult.Failed("PLUGIN_NOT_FOUND", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error($"[SmsController] Ngoại lệ khi gọi Plugin [{providerCode}]: {ex.Message}", ex);
                sendResult = SMSSendResult.Failed("PLUGIN_EXCEPTION", ex.Message);
            }

            // 5. Ghi log kiểm toán cước: Đẩy vào Queue Channels xử lý ngầm (non-blocking, an toàn)
            _actionLogService.QueueLog(request, sendResult, providerCode);

            // 6. Ghi trace log
            _logger.Info($"[SmsController] Gửi SMS SĐT={request.PhoneNumber}, Provider={providerCode}, Success={sendResult.IsSuccess}, Code={sendResult.ErrorCode}");

            return Ok(sendResult);
        }

        /// <summary>
        /// API kiểm tra danh sách các nhà mạng (Plug-in DLL) đã được nạp thành công vào hệ thống.
        /// </summary>
        [HttpGet("providers")]
        public IActionResult GetLoadedProviders()
        {
            var providers = _pluginFactory.GetAvailableProviders();
            return Ok(new
            {
                DefaultProvider = _configuration["SMSSetting:DefaultProvider"],
                TotalLoaded = providers.Length,
                Providers = providers
            });
        }
    }
}
