using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace BetfredParserForms
{
    public partial class MainForm : Form
    {
        #region Свойства

        private List<WebProxy> _proxies;
        private Thread _proxyEnumThread;

        //строка отправляемая в POST-запросе
        private DateTime _starTime;
        private Timer _timer;

        #endregion

        public MainForm()
        {
            InitializeComponent();
            dataGridView1.AutoGenerateColumns = true;
            proxyEnumStatusLabel.Text = string.Empty;
        }

        public event EventHandler PageLoaded;

        //Таймер отсчёта времени
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            elapsedStatusLabel.Text = (_starTime - e.SignalTime).ToString(@"hh\:mm\:ss");
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }
        }

        //Пробуем загрузить страницу или перебрать адреса прокси
        private void LoadPageOrEnumProxies()
        {
            this.InvokeEx(() => TaskbarProgress.SetState(Handle, TaskbarStates.Indeterminate));
            proxyEnumStatusLabel.Text = string.Format("Попытка соединиться через {0}.", _proxies[0].Address);
            //Если удалось загрузить через первый прокси.
            var result = LoadPage();
            if (!result.IsNullOrEmpty())
            {
                //Завершаем потоки.
                StopTimer();
                this.InvokeEx(() => StartParsing(result));
                return;
            }
            this.InvokeEx(() => TaskbarProgress.SetState(Handle, TaskbarStates.Normal));
            //Если не удалось — начинается перебор адресов.
            var proxyEnum = new ProxyEnumerator(
                _proxies, new Uri("http://www.betfred.com"));
            proxyEnum.ProxyFound += ProxyEnum_ProxyFound;
            proxyEnum.NextProxy += ProxyEnum_NextProxy;
            proxyEnum.ProxyNotFound += ProxyEnum_ProxyNotFound;
            proxyEnum.EnumProxies();
            proxyEnum.ProxyFound -= ProxyEnum_ProxyFound;
            proxyEnum.NextProxy -= ProxyEnum_NextProxy;
            proxyEnum.ProxyNotFound -= ProxyEnum_ProxyNotFound;
        }

        private void ProxyEnum_ProxyNotFound(object sender, EventArgs eventArgs)
        {
            StopTimer();
            this.InvokeEx(
                () =>
                {
                    proxyEnumStatusLabel.Text = "Соединение не удалось.";
                });
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

        //Загрузка страницы
        private string LoadPage()
        {
            var req = (HttpWebRequest)WebRequest.Create("http://webmon9.betfred.com/numbers/results/index.asp");
            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "POST";
            req.Proxy = _proxies[0];
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
            try
            {
                using (var stream = new StreamWriter(req.GetRequestStream()))
                {
                    stream.Write(postData);
                }
                var resp = (HttpWebResponse)req.GetResponse();
                using (var stream = new StreamReader(resp.GetResponseStream()))
                {
                    return stream.ReadToEnd();
                }
            }
            catch (WebException)
            {
                return string.Empty;
            }
        }

        private void loadResultsButton_Click(object sender, EventArgs e)
        {
            loadResultsButton.Enabled = false;
            imageStatusLabel.Visible = false;
            _proxies = GetProxies();

            _proxyEnumThread = new Thread(LoadPageOrEnumProxies);

            _timer = new Timer(200);
            _timer.Elapsed += _timer_Elapsed;
            _starTime = DateTime.Now;

            _timer.Start();
            _proxyEnumThread.Start();
        }

        #region Overrides of Form

        protected override void OnClosed(EventArgs e)
        {
            StopTimer();
            base.OnClosed(e);
        }

        #endregion

        private void Parser_NewResult(object sender, BetfredParserEventArgs e)
        {
            bindingSource1.Add(e.Result);
            countStatusLabel.Text = bindingSource1.Count.ToString();
            Application.DoEvents();
        }

        private void ProxyEnum_NextProxy(object sender, ProxyEnumeratorEventArgs e)
        {
            this.InvokeEx(() => TaskbarProgress.SetValue(Handle, e.Index + 1, e.Count));
            proxyEnumStatusLabel.Text = string.Format("Попытка соединиться через {0}. {1} из {2}", e.Proxy.Address, e.Index, e.Count);
        }

        private void ProxyEnum_ProxyFound(object sender, ProxyEnumeratorEventArgs e)
        {
            var p = _proxies[e.Index];
            _proxies.RemoveAt(e.Index);
            _proxies.Insert(0, p);
            //Завершаем потоки.
            StopTimer();
            var result = LoadPage();
            if (!result.IsNullOrEmpty())
            {
                this.InvokeEx(() => StartParsing(result));
            }
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

        protected virtual void OnPageLoaded()
        {
            var handler = PageLoaded;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}