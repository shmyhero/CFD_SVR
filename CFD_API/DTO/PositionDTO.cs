﻿using System;
using System.Collections.Generic;

namespace CFD_API.DTO
{
    public class PositionDTO
    {
        public string id { get; set; }

        //public int secId { get; set; }
        //public string symbol { get; set; }
        //public string name { get; set; }
        public SecurityDetailDTO security { get; set; }

        public decimal invest { get; set; }
        public bool isLong { get; set; }
        public decimal leverage { get; set; }
        public decimal settlePrice { get; set; }
        public decimal quantity { get; set; }

        public decimal? pl { get; set; }
        public decimal? upl { get; set; }
        public DateTime createAt { get; set; }

        public decimal? stopPx { get; set; }
        public string stopOID { get; set; }
        public decimal? takePx { get; set; }
        public string takeOID { get; set; }

        public decimal? financingSum { get; set; }
        public decimal? dividendSum { get; set; }

        public SecurityDTO fxOutright { get; set; }

        public CardDTO card { get; set; }
    }

    /// <summary>
    /// 简化的仓位信息，给达人榜的个人主页用。允许别人看到的自己的仓位信息。
    /// </summary>
    public class SimplePositionDTO
    {
        public int id { get; set; }
        public string symbol { get; set; }
        public string name { get; set; }
        public decimal pl { get; set; }
        public decimal rate { get; set; }
    }

    public class PositionHistoryDTO
    {
        public string id { get; set; }

        public SecurityDetailDTO security { get; set; }

        public decimal? invest { get; set; }
        public bool isLong { get; set; }
        public decimal? leverage { get; set; }
        public decimal openPrice { get; set; }
        public decimal closePrice { get; set; }

        public decimal pl { get; set; }

        public decimal? financingSum { get; set; }
        public decimal? dividendSum { get; set; }

        public bool hasCard { get; set; }

        public DateTime openAt { get; set; }
        public DateTime closeAt { get; set; }
    }

    public class PositionReportDTO
    {
        public string id { get; set; }

        public SecurityDetailDTO security { get; set; }

        public decimal? invest { get; set; }
        public bool isLong { get; set; }
        public decimal? leverage { get; set; }

        public decimal openPrice { get; set; }
        public decimal closePrice { get; set; }

        public decimal pl { get; set; }

        public DateTime openAt { get; set; }
        public DateTime closeAt { get; set; }

        public bool isAutoClosed { get; set; }
    }

    public class ReplaceStopTakeFormDTO
    {
        public string posId { get; set; }
        public int securityId { get; set; }
        public string orderId { get; set; }
        public decimal price { get; set; }
    }

    public class CancelTakeFormDTO
    {
        public string posId { get; set; }
        public string orderId { get; set; }
        public int securityId { get; set; }
    }

    public class NewTakeFormDTO
    {
        public string posId { get; set; }
        public int securityId { get; set; }
        public decimal price { get; set; }
    }

    public class CardDTOCollection
    {
        public List<CardDTO> cards { get; set; }
        public bool hasNew { get; set; }
    }

    public class PosChartDTO
    {
        public DateTime date { get; set; }
        public decimal pl { get; set; }
    }
}