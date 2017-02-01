using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HtmlAgilityPack;

namespace BetfredParserForms
{
    internal class Parser
    {
        /// <summary>
        ///     Парсинг даты, полученной с сайта
        /// </summary>
        /// <param name="innerText">Строка с датой вида dddd d MMMM</param>
        /// <param name="year">Год для данной даты</param>
        /// <param name="minYear">Самый ранний год, для которого ищется дата</param>
        /// <param name="date">Переменная в которую записывается результат</param>
        /// <remarks>
        ///     Дата вида "Wednesday 5th October" не содержит года, поэтому используется <paramref name="year" /> для указания года
        ///     искомой даты.
        ///     Если указанный год не подходит, то он уменьшается и подбор осуществляется снова, пока год не подойдёт или не будет
        ///     достигнут <paramref name="minYear" />
        /// </remarks>
        private static void ParseDate(string innerText, int year, int minYear, out DateTime date)
        {
            var ci = new CultureInfo("en-US");
            //Получение даты из текста. Число может быть 4-х видов: 15th, 22nd, 3rd, 21st
            do
            {
                var dateString = string.Format("{0} {1}", innerText, year);
                //Парсинг даты вида Wednesday 5th October
                if (DateTime.TryParseExact(dateString, "dddd d\\t\\h MMMM yyyy", ci, DateTimeStyles.None, out date))
                    return;
                //Парсинг даты вида Monday 3rd October
                if (DateTime.TryParseExact(dateString, "dddd d\\r\\d MMMM yyyy", ci, DateTimeStyles.None, out date))
                    return;
                //Парсинг даты вида Thursday 22nd September
                if (DateTime.TryParseExact(dateString, "dddd d\\n\\d MMMM yyyy", ci, DateTimeStyles.None, out date))
                    return;
                //Парсинг даты вида Wednesday 21st September
                if (DateTime.TryParseExact(dateString, "dddd d\\s\\t MMMM yyyy", ci, DateTimeStyles.None, out date))
                    return;
                //Дату не удалось спарсить → уменьшаем год
                year--;
                if (year < minYear) return;
            } while (true);
        }

        /// <summary>
        ///     Подготовка строки из html к парсингу
        /// </summary>
        /// <param name="htmlLine">Строка из html</param>
        /// <returns>Возвращает строку вида 00 — 00 00 00 00 00 00 00</returns>
        private static string PrepareLineForParsing(string htmlLine)
        {
            //Удаление переносов строк
            var line = htmlLine.Replace("\r\n", " ");
            //Удаление лишнего текста и пробелов
            line = line.Replace(" Draw ", string.Empty).Replace("  ", " ");
            //Вставка дефиса после первых двух символов в строке
            return line.Insert(2, " —");
        }

        /// <summary>
        /// Конструктор парсера
        /// </summary>
        /// <param name="startDate">Начальная дата для дат на странице</param>
        /// <param name="endDate">Предполагаемая конечная дата</param>
        public Parser(DateTime startDate, DateTime endDate)
        {
            _startDate = startDate;
            _endDate = endDate;
        }

        public event EventHandler<BetfredParserEventArgs> NewResult;

        private readonly DateTime _startDate;
        private DateTime _endDate;

        public DateTime EndDate
        {
            get
            {
                return _endDate;
            }
            set
            {
                _endDate = value;
            }
        }


        public void ParsePage(string pageHtml)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(pageHtml);
            //Выбор всех элементов страницы, содержащих даты и результаты
            var nodes = doc.DocumentNode.SelectNodes("//tr[starts-with(@class,'row') or @class = 'tableheading']");
            var i = 0;
            do
            {
                var node = nodes[i];
                //Пропуск узлов с классом, отличным от tableheading
                if (!node.GetAttributeValue("class", "").Equals("tableheading")) continue;
                //Выбираем узел с датой из tableheading по атрибуту colspan
                ParseDate(
                    node.SelectSingleNode("./th[@colspan]").InnerText,
                    _endDate.Year,
                    _startDate.Year,
                    out _endDate);
                //Следующий узел. Это следующая строка таблицы
                node = nodes[++i];
                //Проверяем первую строку с первым результатом. У неё должен быть класс row0
                if (node.GetAttributeValue("class", "").StartsWith("row0"))
                    OnNewResult(new BetfredParserEventArgs(new BetfredResult(PrepareLineForParsing(node.InnerText), _endDate)));
                //Следующий узел. Это следующая строка таблицы
                node = nodes[++i];
                //Проверяем первую строку со вторым результатом. У неё должен быть класс row1
                if (node.GetAttributeValue("class", "").StartsWith("row1"))
                    OnNewResult(new BetfredParserEventArgs(new BetfredResult(PrepareLineForParsing(node.InnerText), _endDate)));
            } while (++i < nodes.Count());
        }

        protected virtual void OnNewResult(BetfredParserEventArgs e)
        {
            EventHandler<BetfredParserEventArgs> handler = NewResult;
            if (handler != null) handler(this, e);
        }
    }
}
