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
        /// <summary>Вспомогательный метод для работы с контролом из другого потока.</summary>
        /// <param name="control">Контрол, к которому нужен доступ из другого потока.</param>
        /// <param name="action">Метод, который будет работать с контролом.</param>
        public static bool InvokeEx(this Control control, Func<bool> action)
        {
            if (control.InvokeRequired)
                return (bool) control.Invoke(action);
            return action.Invoke();
        }


        public static bool IsNullOrEmpty(this string text)
        {
            return string.IsNullOrEmpty(text);
        }
    }
}