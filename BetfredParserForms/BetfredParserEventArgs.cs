using System;

namespace BetfredParserForms
{
    public class BetfredParserEventArgs:EventArgs
    {
        public BetfredResult Result { get; private set; }

        public BetfredParserEventArgs(BetfredResult result)
        {
            Result = result;
        }
    }
}