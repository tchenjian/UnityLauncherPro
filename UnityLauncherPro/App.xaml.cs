using System.Threading;
using System.Windows;
using UnityLauncherPro.Localization;
using UnityLauncherPro.Properties;

namespace UnityLauncherPro
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 在主窗口创建之前应用用户选择的语言，确保所有资源绑定首次读取到正确的区域性。
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 读取用户保存的语言设置（"" 表示跟随系统）
            var lang = Settings.Default.language;
            if (!string.IsNullOrWhiteSpace(lang))
            {
                try
                {
                    var ci = new System.Globalization.CultureInfo(lang);
                    Thread.CurrentThread.CurrentUICulture = ci;
                    System.Globalization.CultureInfo.CurrentUICulture = ci;
                }
                catch { /* 无效文化名时忽略，使用系统默认 */ }
            }
            LocalizationManager.SetLanguage(Settings.Default.language);

            base.OnStartup(e);
        }
    }
}
