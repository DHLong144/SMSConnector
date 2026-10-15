using System.Collections.Concurrent;
using System.Reflection;
using log4net;
using SMSConnector.Contract;
using Microsoft.Extensions.DependencyInjection;

namespace SMSConnector.Engine
{
    /// <summary>
    /// Factory chịu trách nhiệm quét thư mục Plugins, nạp động các file DLL
    /// (SMSConnectivity.*.dll) bằng Reflection và cache lại dưới dạng Singleton instance.
    /// Kế thừa đúng mô hình Factory nạp DLL của HISConnector.
    /// </summary>
    public static class SmsPluginFactory
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(SmsPluginFactory));

        // In-memory Singleton Cache: ánh xạ ProviderCode (VD: "SPEEDSMS") -> Instance thực thi ISmsConnector
        private static readonly ConcurrentDictionary<string, ISmsConnector> _connectors 
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Quét thư mục chỉ định và nạp toàn bộ Plugin DLL triển khai ISmsConnector
        /// </summary>
        /// <param name="pluginsFolderPath">Đường dẫn thư mục chứa DLLs</param>
        public static void LoadPlugins(string pluginsFolderPath, IServiceProvider serviceProvider)
        {
            try
            {
                string targetDir = Path.GetFullPath(
                    Path.IsPathRooted(pluginsFolderPath)
                        ? pluginsFolderPath
                        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginsFolderPath)
                );

                if (!Directory.Exists(targetDir))
                {
                    _logger.Warn($"[Plugin Engine] Thư mục Plugins không tồn tại, đang tự tạo mới: {targetDir}");
                    Directory.CreateDirectory(targetDir);
                    return;
                }

                var dllFiles = Directory.GetFiles(targetDir, "SMSConnectivity.*.dll", SearchOption.TopDirectoryOnly);
                _logger.Info($"[Plugin Engine] Quét thư mục: {targetDir}. Tìm thấy {dllFiles.Length} file DLL.");

                foreach (var dllPath in dllFiles)
                {
                    LoadPluginFromAssembly(dllPath, serviceProvider);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[Plugin Engine] Lỗi trong quá trình quét nạp Plugins: {ex.Message}", ex);
            }
        }

        private static void LoadPluginFromAssembly(string dllPath, IServiceProvider serviceProvider)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(ISmsConnector).IsAssignableFrom(t) 
                             && !t.IsInterface 
                             && !t.IsAbstract);

                foreach (var type in pluginTypes)
                {
                    var instance = (ISmsConnector)ActivatorUtilities.CreateInstance(serviceProvider, type);
                    if (instance != null && !string.IsNullOrWhiteSpace(instance.ProviderCode))
                    {
                        _connectors[instance.ProviderCode] = instance;
                        _logger.Info($"[Plugin Engine] -> Đã đăng ký thành công Plugin: [{instance.ProviderCode}] (Type: {type.FullName}) từ Assembly [{assembly.GetName().Name}].");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[Plugin Engine] Không thể nạp assembly từ file: {dllPath}. Lỗi: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Lấy đối tượng kết nối (Connector) theo mã nhà cung cấp (ProviderCode)
        /// </summary>
        public static ISmsConnector GetConnector(string providerCode)
        {
            if (string.IsNullOrWhiteSpace(providerCode))
            {
                throw new ArgumentNullException(nameof(providerCode), "Mã nhà mạng (ProviderCode) không được để trống.");
            }

            if (_connectors.TryGetValue(providerCode, out var connector))
            {
                return connector;
            }

            var loadedProviders = string.Join(", ", _connectors.Keys);
            throw new NotSupportedException($"Chưa cấu hình hoặc không tìm thấy DLL cho nhà mạng: '{providerCode}'. Các nhà mạng hiện có sẵn: [{loadedProviders}]");
        }

        /// <summary>
        /// Danh sách tất cả các ProviderCode đã được nạp thành công vào hệ thống
        /// </summary>
        public static string[] GetAvailableProviders()
        {
            return _connectors.Keys.ToArray();
        }
    }
}
