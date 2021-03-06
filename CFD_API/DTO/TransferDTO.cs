﻿using System;
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
        public string color { get; set; }
    }

    public class DepositSettingDTO
    {
        public decimal minimum { get; set; }

        public string alipay { get; set; }
        /// <summary>
        /// 阿里最大入金金额
        /// </summary>
        public decimal alipayMax { get; set; }
        public decimal alipayMin { get; set; }

        /// <summary>
        /// Ping++
        /// </summary>
        public string alipayPing { get; set; }
        public decimal alipayMaxPing { get; set; }
        public decimal alipayMinPing { get; set; }

        /// <summary>
        /// 银联最大入金金额
        /// </summary>
        public decimal cupMax { get; set; }
        public decimal cupMin { get; set; }

        public decimal fxRate { get; set; }

        public string notice { get; set; }

        public DepositChargeDTO charge { get; set; }

        public List<BankDTO> banks { get; set; }

        /// <summary>
        /// 可使用的交易金
        /// </summary>
        public decimal availableReward { get; set; }
    }

    public class DepositChargeDTO
    {
        public decimal minimum { get; set; }
        public decimal rate { get; set; }
    }

    public class RefundSettingDTO
    {
        public RefundChargeDTO charge { get; set; }
        public string eta { get; set; }
        public string notice { get; set; }
    }

    public class RefundChargeDTO
    {
        public decimal minimum { get; set; }
        public decimal rate { get; set; }
    }

    public class DataPublishDTO
    {
        public int version;
        public List<string> terms;
    }
}