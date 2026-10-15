using SMSConnector.Contract;

namespace SMSConnector.Engine
{
    /// <summary>
    /// Giao diện quản lý quét, nạp động và cung cấp Connector nhà mạng SMS.
    /// </summary>
    public interface ISmsPluginFactory
    {
        /// <summary>
        /// Quét thư mục chỉ định và nạp toàn bộ Plugin DLL triển khai ISmsConnector.
        /// </summary>
        /// <param name="pluginsFolderPath">Đường dẫn thư mục chứa DLLs</param>
        void LoadPlugins(string pluginsFolderPath);

        /// <summary>
        /// Lấy đối tượng kết nối (Connector) theo mã nhà cung cấp (ProviderCode).
        /// </summary>
        /// <param name="providerCode">Mã nhà cung cấp (Ví dụ: SPEEDSMS, VIETTEL...)</param>
        ISmsConnector GetConnector(string providerCode);

        /// <summary>
        /// Danh sách tất cả các ProviderCode đã được nạp thành công vào hệ thống.
        /// </summary>
        string[] GetAvailableProviders();
    }
}
