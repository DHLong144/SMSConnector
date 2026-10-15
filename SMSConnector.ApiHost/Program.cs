using log4net;
using log4net.Config;
using Microsoft.OpenApi.Models;
using SMSConnector.Engine;

// 1. Cấu hình thư mục làm việc hiện tại
Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

// 2. Cấu hình log4net từ file log4net.config
XmlConfigurator.Configure(new FileInfo("log4net.config"));
ILog logger = LogManager.GetLogger(typeof(Program));
logger.Info("=================== KHỞI ĐỘNG HỆ THỐNG SMSCONNECTOR ===================");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory()
});

// 3. Đăng ký Services

builder.Services.AddControllers();

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SMSCONNECTOR API GATEWAY - LABCONNECT PRO",
        Version = "v1",
        Description = "Hệ thống tích hợp gửi SMS đa nhà mạng qua cơ chế Plug-in Reflection",
    });
});

var app = builder.Build();

// 4. Khởi tạo ActionLogService với Logging API URL
string loggingApiUrl = builder.Configuration["LoggingApiUrl"];
SmsActionLogService.Configure(loggingApiUrl);
logger.Info($"[Startup] Cấu hình Logging API URL: {(string.IsNullOrWhiteSpace(loggingApiUrl) ? "(Chưa cấu hình, sẽ chỉ ghi log file bằng log4net)" : loggingApiUrl)}");


// 5. Nạp toàn bộ Plugin DLL từ thư mục Plugins
string pluginsFolder = builder.Configuration["SMSSetting:PluginsFolder"] ?? "SMSPlugins";
logger.Info($"[Startup] Đang quét và nạp Plugins từ thư mục: '{pluginsFolder}'...");
SmsPluginFactory.LoadPlugins(pluginsFolder, app.Services);

// 6. Cấu hình HTTP Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SMSConnector API v1");
    });
}

app.UseCors("AllowAll");
app.UseRouting();
app.MapControllers();

logger.Info("=================== SMSCONNECTOR SẴN SÀNG NHẬN REQUEST ===================");
app.Run();
