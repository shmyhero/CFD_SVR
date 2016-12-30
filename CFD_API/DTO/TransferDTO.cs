using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CFD_API.DTO
{
    /// <summary>
    /// 出入金记录
    /// </summary>
    public class TransferDTO
    {
        public string transferType { get; set; }
        public string date { get; set; }
        public decimal amount { get; set; }
    }

    public class DepositSettingDTO
    {
        public decimal minimum { get; set; }
        public decimal fxRate { get; set; }

        public List<BankDTO> banks {get;set;}
    }
}