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

// Đăng ký IHttpClientFactory
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("LoggingClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Đăng ký DI cho Plugin Engine và ActionLogService
builder.Services.AddSingleton<ISmsPluginFactory, SmsPluginFactory>();
builder.Services.AddSingleton<SmsActionLogService>();
builder.Services.AddSingleton<ISmsActionLogService>(sp => sp.GetRequiredService<SmsActionLogService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<SmsActionLogService>());

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

// 4. Nạp toàn bộ Plugin DLL từ thư mục cấu hình (hỗ trợ tự động fallback giữa Debug & Production)
string pluginsFolder = builder.Configuration["SMSSetting:PluginsFolder"] ?? "SMSPlugins";
logger.Info($"[Startup] Đang quét và nạp Plugins từ cấu hình: '{pluginsFolder}'...");
var pluginFactory = app.Services.GetRequiredService<ISmsPluginFactory>();
pluginFactory.LoadPlugins(pluginsFolder);

// 5. Cấu hình HTTP Pipeline
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
