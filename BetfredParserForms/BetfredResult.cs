using System;
using System.ComponentModel;
using System.Linq;

namespace BetfredParserForms
{
    public class BetfredResult
    {
        public int Draw { get; set; }
        public int[] Results { get; set; }
        public DateTime Date { get; set; }
        [DisplayName("Result")]
        public string ResultsString
        {
            get
            {
                return Results == null ? string.Empty : string.Join(" ", Results.Select(v => v.ToString("d2")));
            }
        }

        public int Booster { get; set; }

        public BetfredResult(string drawLine, DateTime date)
        {
            string[] numbers = drawLine.Split(new[] { '—', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Results = new int[6];
            int[] values = numbers.Select((s, i) => int.Parse(s)).ToArray();
            Draw = values.First();
            Booster = values.Last();
            Array.Copy(values, 1, Results, 0, Results.Length);
            Date = date;
        }

        #region Overrides of Object

        public override string ToString()
        {
            return string.Format("{0:dd.MM.yyyy}\t{1:d2} {2} {3:d2}", Date, Draw, ResultsString, Booster);
        }

        #endregion
    }
}
