using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace BetfredParserForms
{
    public partial class MainForm : Form
    {
        #region Свойства

        private List<WebProxy> _proxies;
        private Thread _proxyEnum;

        #endregion

        public MainForm()
        {
            InitializeComponent();
            dataGridView1.AutoGenerateColumns = true;
            proxyEnumStatusLabel.Text = string.Empty;
        }

        //Соединение через определённый прокси.
        private bool ConnectViaProxy(WebProxy proxy)
        {
            //строка отправляемая в POST-запросе
            var postData = string.Format(
                "nDayFrom={0}&" +
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
            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "POST";
            req.Proxy = proxy;
            try
            {
                using (var stream = new StreamWriter(req.GetRequestStream()))
                {
                    stream.Write(postData);
                }
                var resp = (HttpWebResponse)req.GetResponse();
                proxyEnumStatusLabel.Text = string.Format("Соединение удалось через {0}.", proxy.Address);
                using (var stream = new StreamReader(resp.GetResponseStream()))
                {
                    var html = stream.ReadToEnd();
                    this.InvokeEx(() => ParsePage(html));
                }
                return true;
            }
            catch (WebException)
            {
                Debug.WriteLine("Wrong: {0}", proxy.Address);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return false;
        }

        //Формат столбцов таблицы
        private void dataGridView1_ColumnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            if (e.Column.DataPropertyName == "Booster" || e.Column.DataPropertyName == "Draw")
                e.Column.DefaultCellStyle.Format = "D2";
            if (e.Column.DataPropertyName == "Booster")
                e.Column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (e.Column.DataPropertyName == "Date")
                e.Column.DefaultCellStyle.Format = "dd.MM.yyyy";
        }

        //Перебор прокси-серверов.
        private void EnumProxies()
        {
            //Пытаемся соединиться через успешный прокси.
            //Поначалу предполагаем, что это первый в списке
            proxyEnumStatusLabel.Text = string.Format("Попытка соединиться через {0}.", _proxies[0].Address);
            if (ConnectViaProxy(_proxies[0]))
                return;
            //Если не получилось, то перебираем весь список
            var n = 0;
            this.InvokeEx(() => TaskbarProgress.SetState(Handle, TaskbarStates.Normal));

            for (var i = 1; i < _proxies.Count; i++)
            {
                var proxy = _proxies[i];
                this.InvokeEx(() => TaskbarProgress.SetValue(this.Handle, i + 1, _proxies.Count));
                proxyEnumStatusLabel.Text = string.Format(
                    "Попытка соединиться через {0}. {1} из {2}", proxy.Address, i + 1, _proxies.Count);
                if (ConnectViaProxy(proxy))
                {
                    var p = proxy;
                    _proxies.Remove(proxy);
                    _proxies.Insert(0, p);
                    return;
                }
            }
        }

        //Загрузка списка прокси-серверов из файла "proxy.txt"
        private static List<WebProxy> GetProxies()
        {
            var fullpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "proxy.txt");
            if (!File.Exists(fullpath))
                throw new FileNotFoundException(fullpath);
            List<WebProxy> webProxies = new List<WebProxy>();
            using (var reader = new StreamReader(fullpath))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;
                    if (line.IndexOf(':') != -1)
                    {
                        string[] parts = line.Split(':');
                        webProxies.Add(new WebProxy(parts[0], int.Parse(parts[1])));
                    }
                    else
                    {
                        webProxies.Add(new WebProxy(line));
                    }
                }
            }
            return webProxies;
        }

        private void loadResultsButton_Click(object sender, EventArgs e)
        {
            _proxies = GetProxies();
            _proxyEnum = new Thread(EnumProxies);
            _proxyEnum.Start();
        }

        #region Overrides of Form

        protected override void OnClosed(EventArgs e)
        {
            if (_proxyEnum != null && _proxyEnum.IsAlive)
                _proxyEnum.Abort();
            SaveProxies();

            base.OnClosed(e);
        }

        #endregion

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

        private void ParsePage(string pageHtml)
        {
            TaskbarProgress.SetState(this.Handle, TaskbarStates.Indeterminate);
            var doc = new HtmlDocument();
            doc.LoadHtml(pageHtml);
            bindingSource1.Clear();
            //Выбор всех элементов страницы, содержащих даты и результаты
            var nodes = doc.DocumentNode.SelectNodes("//tr[starts-with(@class,'row') or @class = 'tableheading']");
            var drawDate = toDateTimePicker.Value;
            var i = 0;
            do
            {
                var node = nodes[i];
                //Пропуск узлов с классом, отличным от tableheading
                if (!node.GetAttributeValue("class", "").Equals("tableheading")) continue;
                //Выбираем узел с датой из tableheading по атрибуту colspan
                ParseDate(
                    node.SelectSingleNode("./th[@colspan]").InnerText,
                    drawDate.Year,
                    fromDateTimePicker.Value.Year,
                    out drawDate);
                //Следующий узел. Это следующая строка таблицы
                node = nodes[++i];
                //Проверяем первую строку с первым результатом. У неё должен быть класс row0
                if (node.GetAttributeValue("class", "").StartsWith("row0"))
                    bindingSource1.Add(new BetfredResult(PrepareLineForParsing(node.InnerText), drawDate));
                //Следующий узел. Это следующая строка таблицы
                node = nodes[++i];
                //Проверяем первую строку со вторым результатом. У неё должен быть класс row1
                if (node.GetAttributeValue("class", "").StartsWith("row1"))
                    bindingSource1.Add(new BetfredResult(PrepareLineForParsing(node.InnerText), drawDate));
                Application.DoEvents();
            } while (++i < nodes.Count);
            TaskbarProgress.SetState(this.Handle, TaskbarStates.NoProgress);
        }

        /// <summary>
        ///     Подготовка строки из html к парсингу
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

        //Переписываем список прокси. Последний успешный прокси записывается первым.
        private void SaveProxies()
        {
            if (_proxies == null)
            {
                return;
            }
            var fullpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "proxy.txt");
            using (var stream = new StreamWriter(fullpath))
            {
                foreach (var proxy in _proxies)
                {
                    var address = proxy.Address.ToString();
                    stream.WriteLine(address.Substring(address.LastIndexOf("//") + 1).Replace("/",""));
                }
            }
        }
    }
}