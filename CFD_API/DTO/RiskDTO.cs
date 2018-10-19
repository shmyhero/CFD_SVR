using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    public class UserRiskDTO
    {
        public UserRiskDTO()
        {

        }

        public int UserId { get; set; }

        public string NickName { get; set; }

        /// <summary>
        /// average leverage.
        /// </summary>
        public decimal Leverage { get; set; }

        /// <summary>
        /// average order frequency per hour.
        /// </summary>
        public decimal Frequency { get; set; }

        /// <summary>
        /// average hold time(second).
        /// </summary>
        public decimal HoldTime { get; set; }

        /// <summary>
        /// cv_invest std_invest/ave_invest.
        /// </summary>
        public decimal Invest { get; set; }

        public int PosCount { get; set; }

        public decimal TotalInvest { get; set; }

        public decimal TotalPL { get; set; }

        public decimal AveragePL { get; set; }

        private decimal correctIndex(decimal index)
        {
            if (index < 0)
            {
                return 0;
            }
            else if (index > 100)
            {
                return 100;
            }
            else
            {
                return index;
            }
        }

        public decimal LeverageIndex
        {
            get
            {
                decimal index = (this.Leverage - 10m) * 0.28m*4;
                return this.correctIndex(index);
            }
        }

        public decimal HoldTimeIndex
        {
            get
            {
                decimal index = (this.HoldTime - 9000m) * 0.000045m*4;
                return this.correctIndex(index);
            }
        }

        public decimal FrequencyIndex
        {
            get
            {
                decimal index = this.Frequency * 119m*4;
                return this.correctIndex(index);
            }
        }

        public decimal InvestIndex
        {
            get
            {
                decimal index = (this.Invest - 0.15m) * 27.5m*4;
                return this.correctIndex(index);
            }
        }

        public decimal PLIndex
        {
            get
            {
                decimal index = 100 + this.TotalPL * 1000 / this.TotalInvest;
                return this.correctIndex(index);
            }
        }

        public decimal Index
        {
            get
            {                
                return this.LeverageIndex * 0.125m + 
                       this.HoldTimeIndex * 0.125m + 
                       this.FrequencyIndex * 0.125m +
                       this.InvestIndex *0.125m +
                       this.PLIndex * 0.5m;
            }
        }
    }
}