using System.Collections.Generic;
using System.Web.Http;
using AutoMapper;
using CFD_API.Controllers.Attributes;
using CFD_API.DTO;
using CFD_COMMON.Models.Context;

namespace CFD_API.Controllers
{
    //[BasicAuth]
    [RoutePrefix("api/security")]
    public class SecurityController : CFDController
    {
        public SecurityController(CFDEntities db, IMapper mapper)
            : base(db, mapper)
        {
        }

        [HttpGet]
        [Route("bookmark")]
        public List<SecurityDTO> GetBookmarkList()
        {
            return new List<SecurityDTO>()
            {
                new SecurityDTO() {name = "上证指数", symbol = "000001", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "测试1", symbol = "111", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试2", symbol = "222", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试3", symbol = "333", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试4", symbol = "444", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试5", symbol = "555", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试6", symbol = "666", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试7", symbol = "777", last = 2823.45m, open = 2723.45m}
            };
        }

        [HttpGet]
        [Route("stock/topGainer")]
        public List<SecurityDTO> GetTopGainerList()
        {
            return new List<SecurityDTO>()
            {
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
            };
        }

        [HttpGet]
        [Route("stock/topLoser")]
        public List<SecurityDTO> GetTopLoserList()
        {
            return new List<SecurityDTO>()
            {
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
            };
        }

        [HttpGet]
        [Route("stock/trend")]
        public List<SecurityDTO> GetTrendList()
        {
            return new List<SecurityDTO>()
            {
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "惠普", symbol = "HPQ", last = 9.45m, open = 9.2m, tag = "US"},
                new SecurityDTO() {name = "苹果", symbol = "AAPL", last = 98.12m, open = 101.1m, tag = "US"},
                new SecurityDTO() {name = "Facebook", symbol = "FB", last = 105.18m, open = 115.11m, tag = "US"},
                new SecurityDTO() {name = "微软", symbol = "MSFT", last = 53.01m, open = 49.98m, tag = "US"},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "百度", symbol = "BIDU", last = 2823.45m, open = 2723.45m, tag = "US"},
                new SecurityDTO() {name = "阿里巴巴", symbol = "BABA", last = 2823.45m, open = 2723.45m, tag = "US"},
            };
        }

        [HttpGet]
        [Route("index")]
        public List<SecurityDTO> GetIndexList()
        {
            return new List<SecurityDTO>()
            {
                new SecurityDTO() {name = "上证指数", symbol = "000001", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "测试1", symbol = "111", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试2", symbol = "222", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试3", symbol = "333", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试4", symbol = "444", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试5", symbol = "555", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试6", symbol = "666", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "测试7", symbol = "777", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "上证指数", symbol = "000001", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "上证指数", symbol = "000001", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "上证指数", symbol = "000001", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m},
                new SecurityDTO() {name = "上证指数", symbol = "000001", last = 2823.45m, open = 2723.45m},
                new SecurityDTO() {name = "盛大游戏", symbol = "HPQ", last = 111.11m, open = 111.01m}
            };
        }

        [HttpGet]
        [Route("fx")]
        public List<SecurityDTO> GetFxList()
        {
            return new List<SecurityDTO>()
            {
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m},
                new SecurityDTO() {name = "美元/人民币", symbol = "USD/CNY", last = 6.45m, open = 6.54m}
            };
        }

        [HttpGet]
        [Route("futures")]
        public List<SecurityDTO> GetFuturesList()
        {
            return new List<SecurityDTO>()
            {
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m},
                new SecurityDTO() {name = "COMEX黄金", symbol = "GOLD", last = 1116.45m, open = 1106.54m}
            };
        }
    }
}