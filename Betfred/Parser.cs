using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Betfred
{
    public class Parser
    {
        private readonly Dictionary<string, int> _proxys;
        public int ProxiesCount { get; set; }

        public Parser()
        {
            _proxys = new Dictionary<string, int>();
            using (var reader = new StreamReader("proxy.txt"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine().Split(':');
                    _proxys.Add(line[0], int.Parse(line[1]));
                }
            }
            ProxiesCount = _proxys.Count;
        }

        public event EventHandler BadProxyFound;
        public event EventHandler GoodProxyFound;
        public event EventHandler GotResponse;
        public event EventHandler Finished;

        public void TryConnect()
        {
            foreach (var proxy in _proxys)
            {
                var req = (HttpWebRequest)WebRequest.Create("http://webmon9.betfred.com/numbers/results/index.asp");
                req.Proxy = new WebProxy(proxy.Key, proxy.Value);
                //req.ContentType = "application/x-www-form-urlencoded";
                req.Method = "GET";
                try
                {
                    var resp = (HttpWebResponse)req.GetResponse();
                    OnGoodProxyFound(req.Proxy);
                    string text;
                    using (var stream = resp.GetResponseStream())
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            text = reader.ReadToEnd();
                            OnGotResponse(text);
                        }
                    }
                }
                catch (Exception e)
                {
                    OnBadProxyFound(req.Proxy);
                }

            }
            OnFinished();
        }

        public string Connect(IWebProxy proxy)
        {
            var req = (HttpWebRequest)WebRequest.Create("http://webmon9.betfred.com/numbers/results/index.asp");
            req.Proxy = proxy;
            req.ContentType = "application/x-www-form-urlencoded";
            req.Method = "POST";
            var resp = (HttpWebResponse)req.GetResponse();
            string text;
            using (var stream = resp.GetResponseStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }
            }
            return text;
        }

        protected virtual void OnGoodProxyFound(IWebProxy proxy)
        {
            GoodProxyFound?.Invoke(proxy, EventArgs.Empty);
        }

        protected virtual void OnBadProxyFound(IWebProxy proxy)
        {
            BadProxyFound?.Invoke(proxy, EventArgs.Empty);
        }

        protected virtual void OnGotResponse(string text)
        {
            GotResponse?.Invoke(text, EventArgs.Empty);
        }

        protected virtual void OnFinished()
        {
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }
}
