using System;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows;

namespace UnityLauncherPro.Localization
{
    /// <summary>
    /// XAML 标记扩展：{loc:Loc Key} —— 运行时从资源文件取值并支持动态切换语言。
    /// 用法示例：&lt;Label Content="{loc:Loc Btn_NewProject}" /&gt;
    /// </summary>
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return Key;

            // 通过绑定到 LocalizationManager 的索引器，实现语言切换时自动刷新
            var binding = new Binding
            {
                Source = LocalizationManager.Instance,
                Path = new PropertyPath("[" + Key + "]"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            };

            // 当在设计时（VS/Xaml 设计器）也能显示，否则返回 key
            var value = binding.ProvideValue(serviceProvider);
            if (value == DependencyProperty.UnsetValue)
            {
                return Key;
            }
            return value;
        }
    }
}
