using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityLauncherPro.Properties;

namespace UnityLauncherPro.Localization
{
    /// <summary>
    /// 本地化管理器：维护当前 UI 区域性，并向 XAML 提供索引器绑定，以支持运行时动态切换语言。
    /// </summary>
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        /// <summary>支持的语言列表（区域性名称 -> 显示名称）。</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> SupportedLanguages = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("", "System"),           // 跟随系统
            new KeyValuePair<string, string>("en", "English"),        // 英文
            new KeyValuePair<string, string>("zh-CN", "中文(简体)"),    // 中文简体
        };

        static LocalizationManager _instance;
        public static LocalizationManager Instance => _instance ?? (_instance = new LocalizationManager());

        CultureInfo _culture;

        LocalizationManager()
        {
            _culture = CultureInfo.CurrentUICulture;
        }

        /// <summary>当前 UI 区域性。设置时会通知所有索引器绑定刷新。</summary>
        public CultureInfo Culture
        {
            get => _culture;
            set
            {
                if (value == null) value = CultureInfo.CurrentUICulture;
                if (!Equals(_culture, value))
                {
                    _culture = value;
                    try
                    {
                        Thread.CurrentThread.CurrentUICulture = value;
                        CultureInfo.CurrentUICulture = value;
                    }
                    catch { /* 部分场景 CurrentUICulture 不可写，忽略 */ }
                    // 用空字符串通知索引器项已更改，使所有绑定刷新
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
                }
            }
        }

        /// <summary>索引器：供 XAML 通过 Binding [Key] 访问。找不到时回退返回 key 本身。</summary>
        public string this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key)) return key;
                try
                {
                    var value = Resources.ResourceManager.GetString(key, _culture);
                    return string.IsNullOrEmpty(value) ? key : value;
                }
                catch
                {
                    return key;
                }
            }
        }

        /// <summary>按名称设置语言（"en"、"zh-CN"、"" 表示跟随系统）。</summary>
        public static void SetLanguage(string name)
        {
            CultureInfo ci;
            if (string.IsNullOrWhiteSpace(name))
            {
                ci = CultureInfo.InstalledUICulture ?? CultureInfo.CurrentUICulture;
            }
            else
            {
                try { ci = new CultureInfo(name); }
                catch { ci = CultureInfo.CurrentUICulture; }
            }
            Instance.Culture = ci;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        internal void RaiseChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
