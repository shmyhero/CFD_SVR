using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickFix.FIX44;

namespace AyondoTrade.Cache
{
    public class FIXUser
    {
        public string Account { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public decimal? Balance { get; set; }
        public IList<PositionReport> OpenPositions { get; set; }
        public IList<PositionReport> ClosedPositions { get; set; }
    }
}
