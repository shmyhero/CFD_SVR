﻿using System.Collections.Generic;
using System.Linq;

namespace CFD_COMMON.Localization
{
    public class Translations
    {
        public static Dictionary<TransKey, string> Values = new Dictionary<TransKey, string>
        {
            {TransKey.INVALID_PHONE_NUMBER, "无效的手机号码"},
            {TransKey.INVALID_VERIFY_CODE, "验证码错误"},
            {TransKey.NICKNAME_EXISTS, "昵称已存在"},
            {TransKey.NICKNAME_TOO_LONG, "昵称过长" },
            {TransKey.PHONE_SIGNUP_FORBIDDEN, "手机验证过于频繁，请稍后再试"},

            {TransKey.WECHAT_ALREADY_BOUND, "已绑定过微信号" },
            {TransKey.WECHAT_OPENID_EXISTS, "微信号已被使用"},
            {TransKey.PHONE_ALREADY_BOUND, "已绑定过手机" },
            {TransKey.PHONE_EXISTS, "手机号已被使用"},

            {TransKey.PHONE_NOT_BOUND, "未绑定手机" },

            {TransKey.ORDER_REJECTED, "下单失败"},
            {TransKey.NO_AYONDO_ACCOUNT, "交易功能未开通"},
            {TransKey.EXCEPTION, "服务器繁忙，请稍后再试"},

            { TransKey.OAUTH_LOGIN_REQUIRED, "需要OAuth授权" },

            //{TransKey.USER_NOT_EXIST, "用户不存在"},
            
            { TransKey.USERNAME_UNAVAILABLE, "用户名已存在" },
            { TransKey.USERNAME_INVALID, "用户名不符合要求" },

            { TransKey.LIVE_ACC_EXISTS, "已注册过实盘账号" },
            { TransKey.OCR_NO_TRANSACTION_ID, "请先上传身份证图片" },

            { TransKey.LIVE_ACC_REJ_RejectedMifid, "实盘注册申请信息未达到欧盟金融工具市场法规(MiFID)的要求" },
            { TransKey.LIVE_ACC_REJ_RejectedByDD, "实盘注册申请信息未达到KYC(充分了解你的客户)政策的要求" },
            { TransKey.LIVE_ACC_REJ_RejectedDuplicate, "提交的实盘用户信息已存在" },
            { TransKey.LIVE_ACC_REJ_AbortedByExpiry, "实盘注册申请已过期" },
            { TransKey.LIVE_ACC_REJ_AbortedByPolicy, "实盘注册申请被中止" },

            { TransKey.NO_APPROVED_BANK_CARD, "未绑定银行卡" },

            { TransKey.PRICEDOWN, "当前价格中断,无法交易" },
            {TransKey.Live_Acc_Not_Exist, "实盘账号不存在" },

            {TransKey.PAYMENT_METHOD_DISABLED, "该入金方式暂不可用" },
        };
        
        public static Dictionary<string, string> ProdCNames = new Dictionary<string, string>
        {
            {"gold", "黄金"},
            {"silver", "白银"},
            
            {"aud/chf", "澳元/瑞郎"},
            {"aud/jpy", "澳元/日元"},
            {"aud/nzd", "澳元/新西兰元"},
            {"aud/usd", "澳元/美元"},
            {"bitcoin cash/usd","比特币现金/美元" },
            {"bitcoin/eur","比特币/欧元" },
            {"bitcoin/gbp","比特币/英镑" },
            {"bitcoin/usd","比特币/美元" },
            {"dash/usd","达世币/美元" },
            {"ethereum/usd","以太币/美元" },
            {"litecoin/usd","莱特币/美元" },
            //{"neo/usd","小蚁币/美元" },
            {"ripple/usd","瑞波币/美元" },
            {"cad/chf", "加元/瑞郎"},
            {"cad/jpy", "加元/日元"},
            {"chf/jpy", "瑞郎/日元"},
            {"eur/aud", "欧元/澳元"},
            {"eur/cad", "欧元/加元"},
            {"eur/chf", "欧元/瑞郎"},
            {"eur/gbp", "欧元/英镑"},
            {"eur/jpy", "欧元/日元"},
            {"eur/usd", "欧元/美元"},
            {"gbp/aud", "英镑/澳元"},
            {"gbp/cad", "英镑/加元"},
            {"gbp/chf", "英镑/瑞郎"},
            {"gbp/jpy", "英镑/日元"},
            {"gbp/usd", "英镑/美元"},
            {"nzd/usd", "新西兰元/美元"},
            {"usd/cad", "美元/加元"},
            {"usd/chf", "美元/瑞郎"},
            {"usd/hkd", "美元/港元"},
            {"usd/jpy", "美元/日元"},

            {"3m co", "3M"},
            {"58.com inc adr", "58同城"},
            {"aareal bank ag", "地金银行"},
            {"abbott laboratories", "雅培制药"},
            {"accenture plc", "埃森哲"},
            {"activision blizzard inc", "动视暴雪"},
            {"activision blizzard", "动视暴雪"},
            {"adecco sa", "德科"},
            {"adidas ag", "阿迪达斯"},
            {"adobe systems inc", "奥多比"},
            {"aes corp", "爱依斯电力"},
            {"aetna inc", "安泰保险"},
            {"agricultural bank of china ltd","农业银行"},
            {"air china ltd","中国国航"},
            {"airbus group nv", "空客"},
            {"akamai technologies inc", "阿卡迈技术"},
            {"alcatel-lucent", "阿尔卡特朗讯"},
            {"alcatel_lucent", "阿尔卡特朗讯"},
            {"alibaba group holding ltd", "阿里巴巴"},
            {"alibaba pictures group ltd","阿里影业"},
            {"allstate corp", "好事达保险"},
            {"alphabet inc a shares", "Alphabet(谷歌)"},
            {"alstom sa", "阿尔斯通"},
            {"amazon.com inc", "亚马逊"},
            {"ambarella inc", "安霸"},
            {"american airlines group inc", "全美航空"},
            {"anhui conch cement co ltd","安徽海螺"},
            {"apple inc", "苹果"},
            {"at&t inc", "美国电话电报"},
            {"autodesk inc", "欧特克"},
            {"baidu inc", "百度"},
            {"bank of america corp", "美国银行"},
            {"bank of china ltd-h", "中国银行"},
            {"bank of china ltd", "中国银行"},
            {"bank of communications co-h", "交通银行"},
            {"bank of communications co ltd", "交通银行"},
            {"bank of east asia", "东亚银行"},
            {"bank of east asia ltd", "东亚银行"},
            {"basf se", "巴斯夫"},
            {"bayer ag", "拜耳"},
            {"best buy co inc", "百思买"},
            {"boeing co", "波音"},
            {"bp plc", "英国石油"},
            {"byd co ltd", "比亚迪"},
            {"bt group plc", "英国电信"},
            {"chevron corp", "雪铁龙"},
            {"china citic bank corp ltd", "中信银行"},
            {"china citic bank corp ltd-h", "中信银行"},
            {"china construction bank-h", "建设银行"},
            {"china eastern airlines corp ltd", "东方航空"},
            {"china eastern airlines co-h", "东方航空"},
            {"china merchants bank co ltd", "招商银行"},
            {"china minsheng banking corp ltd", "民生银行"},
            {"china mobile ltd", "中国移动"},
            {"china national building material co ltd", "中国建材"},
            {"china pacific insurance group co", "太平洋保险"},
            {"china railway construction corp ltd", "中国铁建"},
            {"china state construction international ltd", "中国建筑"},
            {"china state construction int", "中国建筑"},
            {"china telecom corp ltd", "中国电信"},
            {"china travel international investment hk", "中国国旅"},
            {"china travel intl inv hk", "中国国旅"},
            {"china unicom hong kong ltd", "中国联通"},
            {"china vanke co ltd", "万科"},
            {"chow tai fook jewellery group ltd", "周大福"},
            {"cisco systems inc", "思科"},
            {"citigroup inc", "花旗集团"},
            {"coca-cola co", "可口可乐"},
            {"continental ag", "大陆集团"},
            {"costco wholesale corp", "好市多"},
            {"ctrip.com international", "携程"},
            {"cvs caremark corp", "CVS"},
            {"daimler ag", "戴姆勒"},
            {"delta air lines inc", "达美航空"},
            {"delta airlines inc", "达美航空"},
            {"discovery communications", "探索传播"},
            {"dongfeng motor grp co ltd-h", "东风集团"},
            {"ebay inc", "易贝"},
            {"emc corp/ma", "易安信"},
            {"emerson electric co", "​艾默生"},
            {"evergrande real estate group", "​中国恒大"},
            {"expedia inc", "expedia"},
            {"exxon mobil corp", "埃克森美孚"},
            {"facebook inc", "Facebook"},
            {"fedex corp", "联邦快递"},
            {"ford motor co", "福特汽车"},
            {"galaxy entertainment group l", "银河娱乐"},
            {"galaxy entertainment group ltd", "银河娱乐"},
            {"gap inc", "盖璞"},
            {"garmin ltd", "佳明"},
            {"general electric co", "通用电气"},
            {"general motors co", "通用汽车"},
            {"gf securities co ltd-h", "广发证券"},
            {"gome electrical appliances", "国美电器"},
            {"great wall motor company-h", "长城汽车"},
            {"haier electronics group co", "海尔电器"},
            {"haitong securities co ltd-h", "海通证券"},
            {"home depot inc", "家得宝"},
            {"honeywell international inc", "霍尼韦尔"},
            {"hp inc", "惠普"},
            {"hsbc holdings plc", "汇丰银行"},
            {"hugo boss", "胡戈波士"},
            {"intel corp", "英特尔"},
            {"jd.com inc", "京东"},
            {"jd.com inc adr", "京东"},
            {"jiangxi copper co ltd-h", "江西铜业"},
            {"jpmorgan chase & co", "摩根大通"},
            //{"legend holdings corporation", "联想"},
            {"lenovo group ltd", "联想"},
            {"macy's inc", "梅西百货"},
            {"marvell technology group ltd", "迈威科技"},
            {"mastercard inc", "万事达"},
            {"mcdonald's corp", "麦当劳"},
            {"microsoft corp", "微软"},
            {"morgan stanley", "摩根斯坦利"},
            {"motorola solutions inc", "摩托罗拉"},
            {"natra sa", "雀巢"},
            {"netflix inc", "Netflix"},
            {"nike inc", "耐克"},
            {"nokia oyj", "诺基亚"},
            {"oracle corp", "甲骨文"},
            {"people's insurance co grou-h", "中国人民保险"},
            {"petrochina co ltd-h", "中国石化"},
            {"qualcomm inc", "高通"},
            {"qualcom inc", "高通"},
            {"ralph lauren corp", "拉尔夫·劳伦"},
            {"sandisk corp", "闪迪"},
            {"sap ag", "思爱普"},
            {"sap gy", "思爱普"},
            {"siemens ag", "西门子"},
            {"sina corporation", "新浪"},
            {"soufun holdings ltd adr", "搜房网"},
            {"soufun holdings ltd company", "搜房网"},
            {"starbucks corp", "星巴克"},
            {"tencent holdings ltd", "腾讯"},
            {"tesco plc", "乐购"},
            {"texas instruments inc", "德州仪器"},
            {"tiffany & co", "蒂芙尼"},
            {"tsingtao brewery co ltd-h", "青岛啤酒"},
            {"twitter inc", "推特"},
            {"ubs group ag", "瑞士联合银行"},
            {"union pacific corp", "太平洋铁路"},
            {"united technologies corp", "联合科技"},
            {"verizon communications inc", "威瑞森电信"},
            {"verizon communicaitons inc", "威瑞森电信"}, //ayondo has a typo
            {"vipshop holdings ltd adr", "唯品会"},
            {"visa inc", "维萨"},
            {"volvo ab", "沃尔沃"},
            {"wal-mart stores inc", "沃尔玛"},
            {"walt disney co", "迪斯尼"},
            {"yahoo! inc", "雅虎"},
            {"yanzhou coalng co-h", "兖州煤业"},
            {"yy inc", "欢聚时代"},

            {"eu 50 rolling", "欧洲50"},
            {"france 40 rolling", "法国40"},
            {"germany 30 rolling", "德国30"},
            {"uk 100 rolling", "英国100"},
            {"us 500 rolling", "美国标准500"},
            {"us tech 100 rolling", "美国科技股100"},
            {"wall street rolling", "华尔街"},
        };

        //public static IList<KeyValuePair<string, string>> ProdCNamesList = ProdCNames.Select(o => new KeyValuePair<string, string>(o.Key, o.Value)).ToList();
    }
}