using AutoMapper;
using CFD_API.DTO;
using CFD_COMMON.Models.Entities;

namespace CFD_API
{
    public class MapperConfig
    {
        public static MapperConfiguration GetAutoMapperConfiguration()
        {
            return new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<User, UserDTO>();
                cfg.CreateMap<AyondoSecurity, SecurityDTO>().ForMember(dest=>dest.last,opt=>opt.MapFrom(src=>src.Ask));
            });
        }
    }
}