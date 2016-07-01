﻿using System.Collections.Generic;

namespace CFD_COMMON.Localization
{
    public class Translations
    {
        public static Dictionary<TransKey, string> Values = new Dictionary<TransKey, string>
        {
            {TransKey.INVALID_PHONE_NUMBER, "无效的手机号码"},
            {TransKey.INVALID_VERIFY_CODE, "验证码错误"},
            {TransKey.NICKNAME_EXISTS, "昵称已存在"},
            {TransKey.ORDER_REJECTED, "下单失败"},
            {TransKey.NO_AYONDO_ACCOUNT, "交易功能未开通"},
            {TransKey.EXCEPTION, "服务器繁忙，请稍后再试"},
            {TransKey.PHONE_SIGNUP_FORBIDDEN, "手机验证过于频繁，请稍后再试"},
        };

        public static Dictionary<string, string> ProdCNames = new Dictionary<string, string>
        {
            {"Gold Spot", "黄金"},
            {"Silver Spot", "白银"},
            {"CAD/JPY Spot", "加元/日元"},
            {"EUR/CAD Spot", "欧元/加元"},
            {"EUR/GBP Spot", "欧元/英镑"},
            {"EUR/JPY Spot", "欧元/日元"},
            {"EUR/USD Spot", "欧元/美元"},
            {"GBP/CAD Spot", "英镑/加元"},
            {"GBP/JPY Spot", "英镑/日元"},
            {"GBP/USD Spot", "英镑/美元"},
            {"USD/CAD Spot", "美元/加元"},
            {"USD/JPY Spot", "美元/日元"},
            {"3M Co", "3M"},
            {"Aareal Bank AG", "地金银行"},
            {"Abbott Laboratories", "雅培制药"},
            {"Accenture Plc", "埃森哲"},
            {"Activision Blizzard Inc", "动视暴雪"},
            {"Adecco SA", "德科"},
            {"Adidas AG", "阿迪达斯"},
            {"Adobe Systems Inc", "奥多比"},
            {"AES Corp", "爱依斯电力"},
            {"Aetna Inc", "安泰保险"},
            {"Airbus Group NV", "空客"},
            {"Akamai Technologies Inc", "阿卡迈技术"},
            {"Alcatel-Lucent", "阿尔卡特朗讯"},
            {"Allstate Corp", "好事达保险"},
            {"Alphabet Inc A Shares", "Alphabet(谷歌)"},
            {"Alstom SA", "阿尔斯通"},
            {"Amazon.Com Inc", "亚马逊"},
            {"Ambarella Inc", "安霸"},
            {"American Airlines Group Inc", "全美航空"},
            {"Apple Inc", "苹果"},
            {"AT&T Inc", "美国电话电报"},
            {"Autodesk Inc", "欧特克"},
            {"Baidu Inc", "百度"},
            {"Bank of America Corp", "美国银行"},
            {"Bank of China Ltd-H", "中国银行"},
            {"Bank of Communications Co-H", "交通银行"},
            {"Bank of East Asia", "东亚银行"},
            {"BASF SE", "巴斯夫"},
            {"Bayer AG", "拜耳"},
            {"Best Buy Co Inc", "百思买"},
            {"Boeing Co", "波音"},
            {"BP Plc", "英国石油"},
            {"Chevron Corp", "雪铁龙"},
            {"China CITIC Bank Corp Ltd", "中信银行"},
            {"China Eastern Airlines Corp Ltd", "东方航空"},
            {"China State Construction International Ltd", "中国建筑"},
            {"China Travel International Investment HK", "中国国旅"},
            {"China Unicom Hong Kong Ltd", "中国联通"},
            {"Cisco Systems Inc", "思科"},
            {"Citigroup Inc", "花旗集团"},
            {"Coca-Cola Co", "可口可乐"},
            {"Continental AG", "大陆集团"},
            {"Costco Wholesale Corp", "好市多"},
            {"CVS Caremark Corp", "CVS"},
            {"Daimler AG", "戴姆勒"},
            {"Delta Air Lines Inc", "达美航空"},
            {"Discovery Communications", "探索传播"},
            {"Ebay Inc", "易贝"},
            {"EMC Corp/MA", "易安信"},
            {"Emerson Electric Co", "​艾默生"},
            {"Expedia Inc", "Expedia"},
            {"Exxon Mobil Corp", "埃克森美孚"},
            {"Facebook Inc", "Facebook"},
            {"Fedex Corp", "联邦快递"},
            {"Ford Motor Co", "福特汽车"},
            {"Galaxy Entertainment Group L", "银河娱乐"},
            {"Gap Inc", "盖璞"},
            {"Garmin Ltd", "佳明"},
            {"General Electric Co", "通用电气"},
            {"General Motors Co", "通用汽车"},
            {"Home Depot Inc", "家得宝"},
            {"Honeywell International Inc", "霍尼韦尔"},
            {"HP Inc", "惠普"},
            {"HSBC Holdings Plc", "汇丰银行"},
            {"Hugo Boss", "胡戈波士"},
            {"Intel Corp", "英特尔"},
            {"JPMorgan Chase & Co", "摩根大通"},
            {"Lenovo Group Ltd", "联想"},
            {"Macy's Inc", "梅西百货"},
            {"Marvell Technology Group Ltd", "迈威科技"},
            {"Mastercard Inc", "万事达"},
            {"McDonald's Corp", "麦当劳"},
            {"Microsoft Corp", "微软"},
            {"Morgan Stanley", "摩根斯坦利"},
            {"Motorola Solutions Inc", "摩托罗拉"},
            {"Natra SA", "雀巢"},
            {"Netflix Inc", "Netflix"},
            {"Nike Inc", "耐克"},
            {"Nokia OYJ", "诺基亚"},
            {"Oracle Corp", "甲骨文"},
            {"Qualcomm Inc", "高通"},
            {"Ralph Lauren Corp", "拉尔夫·劳伦"},
            {"SanDisk Corp", "闪迪"},
            {"SAP AG", "思爱普"},
            {"Siemens AG", "西门子"},
            {"Starbucks Corp", "星巴克"},
            {"Tesco Plc", "乐购"},
            {"Texas Instruments Inc", "德州仪器"},
            {"Tiffany & Co", "蒂芙尼"},
            {"Twitter Inc", "推特"},
            {"UBS Group AG", "瑞士联合银行"},
            {"Union Pacific Corp", "太平洋铁路"},
            {"United Technologies Corp", "联合科技"},
            {"Verizon Communications Inc", "威瑞森电信"},
            {"Verizon Communicaitons Inc", "威瑞森电信"}, //Ayondo has a typo
            {"Visa Inc", "维萨"},
            {"Volvo AB", "沃尔沃"},
            {"Wal-Mart Stores Inc", "沃尔玛"},
            {"Walt Disney Co", "迪斯尼"},
            {"Yahoo! Inc", "雅虎"},
            {"EU 50 Rolling", "欧洲斯托克50"},
            {"France 40 Rolling", "法国CAC40"},
            {"Germany 30 Rolling (1 EUR Contract)", "德国DAX30"},
            {"UK 100 Rolling", "英国富时100"},
            {"US 500 Rolling", "标准普尔500"},
            {"US Tech 100 Rolling", "纳斯达克100"},
            {"Wall Street Rolling", "华尔街指数"},
            //{"Wall Street Rolling", "道琼斯工业指数"},
        };
    }
}