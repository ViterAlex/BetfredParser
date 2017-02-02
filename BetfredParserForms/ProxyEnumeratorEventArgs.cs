using System;
using System.Net;

namespace BetfredParserForms
{
    internal class ProxyEnumeratorEventArgs : EventArgs
    {
        public WebProxy Proxy { get; private set; }
        public int Count { get; private set; }
        public int Index { get; private set; }

        public ProxyEnumeratorEventArgs(WebProxy proxy, int count, int index)
        {
            Proxy = proxy;
            Count = count;
            Index = index;
        }
    }
}