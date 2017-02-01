using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace BetfredParserForms
{
    public partial class MainForm : Form
    {
        #region Свойства

        //строка отправляемая в POST-запросе
        private string _postData;
        private DateTime _starTime;
        private List<WebProxy> _proxies;
        private Thread _proxyEnum;
        private Timer _timer;
        #endregion

        public MainForm()
        {
            InitializeComponent();
            dataGridView1.AutoGenerateColumns = true;
            proxyEnumStatusLabel.Text = string.Empty;
        }

        private void AbortThread()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            if (_proxyEnum != null && _proxyEnum.IsAlive)
                _proxyEnum.Abort();
        }

        //Соединение через определённый прокси.
        private bool ConnectViaProxy(WebProxy proxy)
        {
            var req = (HttpWebRequest)WebRequest.Create("http://webmon9.betfred.com/numbers/results/index.asp");
            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "POST";
            req.Proxy = proxy;
            try
            {
                using (var stream = new StreamWriter(req.GetRequestStream()))
                {
                    stream.Write(_postData);
                }
                var resp = (HttpWebResponse)req.GetResponse();
                using (var stream = new StreamReader(resp.GetResponseStream()))
                {
                    var html = stream.ReadToEnd();
                    this.InvokeEx(() => StartParsing(html));
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
            this.InvokeEx(() => TaskbarProgress.SetState(Handle, TaskbarStates.Indeterminate));
            //Пытаемся соединиться через успешный прокси.
            //Поначалу предполагаем, что это первый в списке
            proxyEnumStatusLabel.Text = string.Format("Попытка соединиться через {0}.", _proxies[0].Address);
            if (ConnectViaProxy(_proxies[0]))
                return;
            //Если не получилось, то перебираем весь список
            this.InvokeEx(() => TaskbarProgress.SetState(Handle, TaskbarStates.Normal));

            for (var i = 1; i < _proxies.Count; i++)
            {
                var proxy = _proxies[i];
                this.InvokeEx(() => TaskbarProgress.SetValue(Handle, i + 1, _proxies.Count));
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
            loadResultsButton.Enabled = false;
            imageStatusLabel.Visible = false;
            _postData = string.Format(
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
            _proxies = GetProxies();
            _proxyEnum = new Thread(EnumProxies);
            _timer = new Timer(200);
            _timer.Elapsed += _timer_Elapsed;
            _starTime = DateTime.Now;
            _timer.Start();
            _proxyEnum.Start();
        }

        private void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            elapsedStatusLabel.Text = (_starTime - e.SignalTime).ToString(@"hh\:mm\:ss");
        }

        #region Overrides of Form

        protected override void OnClosed(EventArgs e)
        {
            AbortThread();
            base.OnClosed(e);
        }

        #endregion

        private void Parser_NewResult(object sender, BetfredParserEventArgs e)
        {
            bindingSource1.Add(e.Result);
            countStatusLabel.Text = bindingSource1.Count.ToString();
            Application.DoEvents();
        }

        //Переписываем список прокси. Последний успешный прокси записывается первым.
        private void SaveProxies()
        {
            if (_proxies == null)
                return;
            var fullpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "proxy.txt");
            using (var stream = new StreamWriter(fullpath))
            {
                foreach (var proxy in _proxies)
                {
                    var address = proxy.Address.ToString();
                    stream.WriteLine(address.Substring(address.LastIndexOf("//") + 1).Replace("/", ""));
                }
            }
        }

        //Прокси найден. Страница получена. Начинаем парсить.
        private void StartParsing(string html)
        {
            //Завершаем поток поиска прокси.
            AbortThread();
            imageStatusLabel.Visible = true;
            proxyEnumStatusLabel.Text = string.Format("Соединение удалось через {0}.", _proxies[0].Address);
            TaskbarProgress.SetState(Handle, TaskbarStates.Indeterminate);
            bindingSource1.Clear();
            //Инициализируем парсер
            var parser = new Parser(fromDateTimePicker.Value, toDateTimePicker.Value);
            //Подписываемся на событие
            parser.NewResult += Parser_NewResult;
            //Парсим страницу
            parser.ParsePage(html);
            //Отписываемся от событий
            parser.NewResult -= Parser_NewResult;
            loadResultsButton.Enabled = true;
            TaskbarProgress.SetState(Handle, TaskbarStates.NoProgress);
            SaveProxies();
        }
    }
}