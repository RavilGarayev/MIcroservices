using Mango.Services.CouponAPI.Models.Dto;
using Mango.Services.CouponAPI.Repository;
using Microsoft.AspNetCore.Mvc;

namespace Mango.Services.CouponAPI.Controllers
{
    [ApiController]
    [Route("api/coupon")]
    public class CouponController : Controller
    {
        private readonly ICouponRepository _couponRepository;
        protected ResponceDto _responce;

        public CouponController(ICouponRepository couponRepository)
        {
            _couponRepository = couponRepository;
            _responce = new ResponceDto();
        }

        [HttpGet("{code}")]
        public async Task<object> GetDiscountForCode(string code)
        {
            try
            {
                CouponDto couponDto = await _couponRepository.GetCouponByCode(code);
                _responce.Result = couponDto;
            }
            catch (Exception ex)
            {
                _responce.IsSuccess = false;
                _responce.ErrorMessages = new List<string> { ex.ToString() };
            }
            return _responce;
        }
    }
}
