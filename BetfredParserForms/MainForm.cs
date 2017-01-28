using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Windows.Forms;
using HtmlAgilityPack;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace BetfredParserForms
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            Load += Form1_Load;
            dataGridView1.AutoGenerateColumns = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _browser = new WebBrowser();
            _browser.DocumentCompleted += Browser_DocumentCompleted;
        }

        private WebBrowser _browser;

        private void loadResultsButton_Click(object sender, EventArgs e)
        {
            //строка отправляемая в POST-запросе
            var postData = string.Format("nDayFrom={0}&" +
                                        "nMonthfrom={1}&" +
                                        "nYearfrom={2}&" +
                                        "nDayto={3}&" +
                                        "nMonthto={4}&" +
                                        "nYearto={5}&" +
                                        "xSubmit=Find+draws&slng=SIS49",
                                        fromDateTimePicker.Value.Day,
                                        fromDateTimePicker.Value.Month,
                                        fromDateTimePicker.Value.Year,
                                        toDateTimePicker.Value.Day,
                                        toDateTimePicker.Value.Month,
                                        toDateTimePicker.Value.Year);
            var req = (HttpWebRequest)WebRequest.Create("http://webmon9.betfred.com/numbers/results/index.asp");
            req.Proxy= new WebProxy("46.19.93.212", 8080);
            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "GET";
            var resp = (HttpWebResponse)req.GetResponse();
            //_browser.Navigate(new Uri("http://webmon9.betfred.com/numbers/results/index.asp"),
            //    string.Empty,
            //    Encoding.UTF8.GetBytes(postData),
            //    "Content-Type: application/x-www-form-urlencoded");
        }

        private void Browser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            ParsePage(_browser.DocumentText);
        }

        private void ParsePage(string pageHtml)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(pageHtml);
            bindingSource1.Clear();
            //Выбор всех элементов страницы, содержащих даты и результаты
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//tr[starts-with(@class,'row') or @class = 'tableheading']");
            DateTime drawDate = toDateTimePicker.Value;
            var i = 0;
            do
            {
                HtmlNode node = nodes[i];
                //Пропуск узлов с классом, отличным от tableheading
                if (!node.GetAttributeValue("class", "").Equals("tableheading")) continue;
                //Выбираем узел с датой из tableheading по атрибуту colspan
                ParseDate(node.SelectSingleNode("./th[@colspan]").InnerText,
                    drawDate.Year,
                    fromDateTimePicker.Value.Year,
                    out drawDate);
                //Следующий узел. Это следующая строка таблицы
                node = nodes[++i];
                //Проверяем первую строку с первым результатом. У неё должен быть класс row0
                if (node.GetAttributeValue("class", "").StartsWith("row0"))
                {
                    bindingSource1.Add(new BetfredResult(PrepareLineForParsing(node.InnerText), drawDate));
                }
                //Следующий узел. Это следующая строка таблицы
                node = nodes[++i];
                //Проверяем первую строку со вторым результатом. У неё должен быть класс row1
                if (node.GetAttributeValue("class", "").StartsWith("row1"))
                {
                    bindingSource1.Add(new BetfredResult(PrepareLineForParsing(node.InnerText), drawDate));
                }
            } while (++i < nodes.Count);
        }
        /// <summary>
        /// Подготовка строки из html к парсингу
        /// </summary>
        /// <param name="htmlLine">Строка из html</param>
        /// <returns>Возвращает строку вида 00 — 00 00 00 00 00 00 00</returns>
        private string PrepareLineForParsing(string htmlLine)
        {
            //Удаление переносов строк
            var line = htmlLine.Replace("\r\n", " ");
            //Удаление лишнего текста и пробелов
            line = line.Replace(" Draw ", string.Empty).Replace("  ", " ");
            //Вставка дефиса после первых двух символов в строке
            return line.Insert(2, " —");
        }
        /// <summary>
        /// Парсинг даты, полученной с сайта
        /// </summary>
        /// <param name="innerText">Строка с датой вида dddd d MMMM</param>
        /// <param name="year">Год для данной даты</param>
        /// <param name="minYear">Самый ранний год, для которого ищется дата</param>
        /// <param name="date">Переменная в которую записывается результат</param>
        /// <remarks>Дата вида "Wednesday 5th October" не содержит года, поэтому используется <paramref name="year"/> для указания года искомой даты.
        /// Если указанный год не подходит, то он уменьшается и подбор осуществляется снова, пока год не подойдёт или не будет достигнут <paramref name="minYear"/></remarks>
        private void ParseDate(string innerText, int year, int minYear, out DateTime date)
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
        //Формат столбцов таблицы
        private void dataGridView1_ColumnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            if (e.Column.DataPropertyName == "Booster" || e.Column.DataPropertyName == "Draw")
            {
                e.Column.DefaultCellStyle.Format = "D2";
            }
            if (e.Column.DataPropertyName == "Booster")
            {
                e.Column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (e.Column.DataPropertyName == "Date")
            {
                e.Column.DefaultCellStyle.Format = "dd.MM.yyyy";
            }
        }
    }
}
