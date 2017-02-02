using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace BetfredParserForms
{
    class ProxyEnumerator
    {
        private readonly List<WebProxy> _proxies;
        public event EventHandler<ProxyEnumeratorEventArgs> ProxyFound;
        public event EventHandler<ProxyEnumeratorEventArgs> NextProxy;
        private readonly Uri _uri;
        public WebProxy GoodProxy { get; private set; }

        public ProxyEnumerator(IEnumerable<WebProxy> proxies, Uri uri)
        {
            _uri = uri;
            _proxies = proxies.ToList();
        }

        //Перебор прокси-серверов.
        public void EnumProxies(bool skipFirst = true)
        {
            for (var i = skipFirst ? 1 : 0; i < _proxies.Count; i++)
            {
                var proxy = _proxies[i];
                OnNextProxy(new ProxyEnumeratorEventArgs(proxy, _proxies.Count, i));
                if (ConnectViaProxy(proxy))
                    break;
            }
        }

        //Соединение через определённый прокси.
        private bool ConnectViaProxy(WebProxy proxy)
        {
            var req = (HttpWebRequest)WebRequest.Create(_uri);
            req.Proxy = proxy;
            req.Method = "POST";
            try
            {
                //пробуем подключиться.
                using (var stream = new StreamWriter(req.GetRequestStream()))
                {
                }
                var resp = (HttpWebResponse)req.GetResponse();
                using (var stream = new StreamReader(resp.GetResponseStream()))
                {
                    var html = stream.ReadToEnd();
                }
                OnProxyFound(new ProxyEnumeratorEventArgs(proxy, _proxies.Count, _proxies.IndexOf(proxy)));
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


        protected virtual void OnProxyFound(ProxyEnumeratorEventArgs e)
        {
            EventHandler<ProxyEnumeratorEventArgs> handler = ProxyFound;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnNextProxy(ProxyEnumeratorEventArgs e)
        {
            EventHandler<ProxyEnumeratorEventArgs> handler = NextProxy;
            if (handler != null) handler(this, e);
        }
    }
}
