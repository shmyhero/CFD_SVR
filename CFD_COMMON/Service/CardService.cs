using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFD_COMMON.Models.Context;
using CFD_COMMON.Models.Entities;

namespace CFD_COMMON.Service
{
    public class CardService
    {
        public CFDEntities db { get; set; }

        public CardService(CFDEntities db)
        {
            this.db = db;
        }

        public Card GetCard(decimal pl, decimal plRate, decimal openPrice)
        {
            var cards = db.Cards.Where(o => o.LowProfit < pl && (!o.HighProfit.HasValue || o.HighProfit > pl)
            && o.LowProfitRate < plRate && (!o.HighProfitRate.HasValue || o.HighProfitRate > plRate)).ToList();

            if (cards.Count==0)
            {
                return null;
            }

            Random ran = new Random(DateTime.UtcNow.Millisecond);
            int index = ran.Next(0, cards.Count - 1);
            return cards[index];
        }
    }
}
