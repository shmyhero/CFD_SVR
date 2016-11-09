using AutoMapper;
using CFD_COMMON.Models.Entities;

namespace CFD_COMMON
{
    public class MapperConfig
    {
        public static MapperConfiguration GetAutoMapperConfiguration()
        {
            return new MapperConfiguration(cfg =>
            {
                CreateCommonMap(cfg);
            });
        }

        public static void CreateCommonMap(IMapperConfiguration cfg)
        {
            cfg.CreateMap<AyondoTradeHistoryBase, AyondoTradeHistory>();
            cfg.CreateMap<AyondoTradeHistoryBase, AyondoTradeHistory_Live>();

            cfg.CreateMap<MessageBase, Message>();
            cfg.CreateMap<MessageBase, Message_Live>();

            cfg.CreateMap<NewPositionHistoryBase, NewPositionHistory>();
            cfg.CreateMap<NewPositionHistoryBase, NewPositionHistory_live>();
        }
    }
}