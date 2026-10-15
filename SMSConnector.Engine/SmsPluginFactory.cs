using System.Collections.Concurrent;
using System.Reflection;
using log4net;
using Microsoft.Extensions.DependencyInjection;
using SMSConnector.Contract;

namespace SMSConnector.Engine
{
    /// <summary>
    /// Factory chịu trách nhiệm quét thư mục Plugins, nạp động các file DLL
    /// (SMSConnectivity.*.dll) bằng Reflection và cache lại dưới dạng Singleton instance.
    /// Kế thừa đúng mô hình Factory nạp DLL của HISConnector.
    /// </summary>
    public class SmsPluginFactory : ISmsPluginFactory
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(SmsPluginFactory));

        // In-memory Singleton Cache: ánh xạ ProviderCode (VD: "SPEEDSMS") -> Instance thực thi ISmsConnector
        private readonly ConcurrentDictionary<string, ISmsConnector> _connectors 
            = new(StringComparer.OrdinalIgnoreCase);

        private readonly IServiceProvider _serviceProvider;

        public SmsPluginFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Quét thư mục chỉ định và nạp toàn bộ Plugin DLL triển khai ISmsConnector
        /// </summary>
        /// <param name="pluginsFolderPath">Đường dẫn thư mục chứa DLLs</param>
        public void LoadPlugins(string pluginsFolderPath)
        {
            try
            {
                string targetDir = ResolvePluginsDirectory(pluginsFolderPath);

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
                    LoadPluginFromAssembly(dllPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[Plugin Engine] Lỗi trong quá trình quét nạp Plugins: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Giải quyết đường dẫn thư mục Plugins linh hoạt theo nhiều môi trường (Debug, IIS, Windows Service, Docker)
        /// </summary>
        public static string ResolvePluginsDirectory(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                configuredPath = "SMSPlugins";
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return Path.GetFullPath(configuredPath);
            }

            string baseDir = AppContext.BaseDirectory;
            string baseCandidate = Path.GetFullPath(Path.Combine(baseDir, configuredPath));
            if (Directory.Exists(baseCandidate))
            {
                return baseCandidate;
            }

            string currentDir = Directory.GetCurrentDirectory();
            string currentCandidate = Path.GetFullPath(Path.Combine(currentDir, configuredPath));
            if (Directory.Exists(currentCandidate))
            {
                return currentCandidate;
            }

            // Fallback khi chạy từ IDE / bin/Debug/net6.0
            string[] devCandidates = new[]
            {
                Path.GetFullPath(Path.Combine(baseDir, "../../../../SMSPlugins")),
                Path.GetFullPath(Path.Combine(baseDir, "../../../SMSPlugins")),
                Path.GetFullPath(Path.Combine(currentDir, "SMSPlugins")),
                Path.GetFullPath(Path.Combine(currentDir, "../SMSPlugins"))
            };

            foreach (var devDir in devCandidates)
            {
                if (Directory.Exists(devDir))
                {
                    return devDir;
                }
            }

            return baseCandidate;
        }

        private void LoadPluginFromAssembly(string dllPath)
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
                    var instance = (ISmsConnector)ActivatorUtilities.CreateInstance(_serviceProvider, type);
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
        public ISmsConnector GetConnector(string providerCode)
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
        public string[] GetAvailableProviders()
        {
            return _connectors.Keys.ToArray();
        }
    }
}
