using System;
using System.Windows.Forms;

namespace BetfredParserForms
{
    //[DebuggerStepThrough]
    public static class Extensions
    {
        /// <summary>Вспомогательный метод для работы с контролом из другого потока.</summary>
        /// <param name="control">Контрол, к которому нужен доступ из другого потока.</param>
        /// <param name="action">Метод, который будет работать с контролом.</param>
        public static void InvokeEx(this Control control, Action action)
        {
            if (control.InvokeRequired)
                control.Invoke(action);
            else
                action.Invoke();
        }

        /// <summary>Проверка, что строка не пустая.</summary>
        /// <param name="value">Строковая переменная</param>
        public static bool IsNullOrEmpty(this string value)
        {
            return string.IsNullOrEmpty(value);
        }


        public static double NextDouble(this Random rnd, double min, double max)
        {
            return rnd.NextDouble() * (max - min) + min;
        }
    }
}