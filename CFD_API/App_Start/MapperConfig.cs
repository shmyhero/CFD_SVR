using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON.Localization;
using CFD_COMMON.Models.Cached;
using CFD_COMMON.Models.Entities;
using CFD_COMMON.Utils;

namespace CFD_API
{
    public class MapperConfig
    {
        public static MapperConfiguration GetAutoMapperConfiguration()
        {
            return new MapperConfiguration(cfg =>
            {
                CFD_COMMON.MapperConfig.CreateCommonMap(cfg);

                //var r = new Random();

                cfg.CreateMap<Version, VersionDTO>();
                cfg.CreateMap<Version, VersionIOSDTO>();
                cfg.CreateMap<Version, VersionAndroidDTO>();

                cfg.CreateMap<User, MeDTO>();
                cfg.CreateMap<UserInfo, MyInfoDTO>();

                cfg.CreateMap<AyondoSecurity, SecurityLiteDTO>()
                    //.ForMember(dest => dest.last, opt => opt.MapFrom(src => Quotes.GetLastPrice(src)))
                    ////tag
                    //.ForMember(dest => dest.tag, opt => opt.Condition(o => o.AssetClass == "Single Stocks"))
                    .ForMember(dest => dest.tag, opt => opt.MapFrom(src => src.Financing == "US Stocks" ? "US" : null))
                    ////name
                    ////                    .ForMember(dest => dest.name, opt => opt.MapFrom(src => src.CName ?? (r.Next(0, 2) == 0 ? "阿里巴巴" : "苹果")))
                    //.ForMember(dest => dest.name, opt => opt.MapFrom(src => src.CName ?? src.Name.TruncateMax(10)))
                    .ForMember(dest => dest.name, opt => opt.MapFrom(src => src.CName))
                    ////open
                    ////.ForMember(dest => dest.open, opt => opt.MapFrom(src => src.Ask*((decimal) r.Next(80, 121))/100))
                    ;
                cfg.CreateMap<AyondoSecurity, SecurityDetailDTO>()
                    .ForMember(dest => dest.tag, opt => opt.MapFrom(src => src.Financing == "US Stocks" ? "US" : null))
                    .ForMember(dest => dest.name, opt => opt.MapFrom(src => src.CName))
                    ;

                cfg.CreateMap<ProdDef, SecurityLiteDTO>()
                    .ForMember(dest => dest.name, opt => opt.MapFrom(src => Translator.GetCName(src.Name)))
                    .ForMember(dest => dest.open, opt => opt.MapFrom(src => Quotes.GetOpenPrice(src)))
                    .ForMember(dest => dest.isOpen, opt => opt.MapFrom(src => src.QuoteType == enmQuoteType.Open))
                    .ForMember(dest => dest.status, opt => opt.MapFrom(src => src.QuoteType))
                    .ForMember(dest => dest.tag, opt => opt.MapFrom(src => Products.GetStockTag(src.Symbol)));

                cfg.CreateMap<ProdDef, SecurityDetailDTO>()
                    .ForMember(dest => dest.last, opt => opt.MapFrom(src => Quotes.GetLastPrice(src)))
                    .ForMember(dest => dest.ask, opt => opt.MapFrom(src => src.Offer))
                    .ForMember(dest => dest.name, opt => opt.MapFrom(src => Translator.GetCName(src.Name)))
                    .ForMember(dest => dest.open, opt => opt.MapFrom(src => Quotes.GetOpenPrice(src)))
                    //.ForMember(dest => dest.preClose, opt => opt.MapFrom(src => src.CloseAsk))
                    .ForMember(dest => dest.isOpen, opt => opt.MapFrom(src => src.QuoteType == enmQuoteType.Open))
                    .ForMember(dest => dest.status, opt => opt.MapFrom(src => src.QuoteType))
                    .ForMember(dest => dest.tag, opt => opt.MapFrom(src => Products.GetStockTag(src.Symbol)))
                    .ForMember(dest => dest.dcmCount, opt => opt.MapFrom(src => src.Prec))
                    .ForMember(dest => dest.ccy, opt => opt.MapFrom(src => src.Ccy2));

                cfg.CreateMap<ProdDef, ProdDefDTO>();

                cfg.CreateMap<Tick, TickDTO>();


                cfg.CreateMap<Banner, BannerDTO>();
                cfg.CreateMap<Banner, SimpleBannerDTO>();
                cfg.CreateMap<Banner2, BannerDTO>();
                cfg.CreateMap<Banner2, SimpleBannerDTO>();
                cfg.CreateMap<Headline, HeadlineDTO>();


                cfg.CreateMap<CompetitionResult, CompetitionResultDTO>();
                cfg.CreateMap<CompetitionUserPosition, CompetitionUserPositionDTO>();

                cfg.CreateMap<UserAlertBase, StockAlertDTO>();

                cfg.CreateMap<Partner, PartnerDTO>();
                cfg.CreateMap<PartnerSignUpDTO, Partner>();
            });
        }
    }
}