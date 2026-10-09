using System;
using System.Globalization;
using System.Windows.Data;

namespace GameVault
{
    /// <summary>
    /// 多值转换器：只要有一个输入是 true 就返回 true。
    ///
    /// 用途：概览卡的悬停小窗（Popup）。Popup 的内容渲染在**独立窗口层**，
    /// 不在卡片的视觉树里 —— 所以鼠标一移到小窗上，卡片的 IsMouseOver 就会变 false。
    /// 若 IsOpen 只绑卡片的 IsMouseOver，鼠标刚碰到小窗它就消失了。
    /// 这里把「卡片自身悬停」与「小窗内容悬停」一起 OR 起来再驱动 IsOpen，
    /// 纯声明式、无需 code-behind 的 Enter/Leave 状态机。
    /// </summary>
    public class AnyTrueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null) return false;
            foreach (var v in values)
            {
                if (v is bool && (bool)v) return true;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
