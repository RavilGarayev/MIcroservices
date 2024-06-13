﻿using Mango.Services.ShoppingCartAPI.Models.Dto;
using Newtonsoft.Json;

namespace Mango.Services.ShoppingCartAPI.Repository
{
    public class CouponRepository : ICouponRepository
    {
        private readonly HttpClient _client;

        public CouponRepository(HttpClient client)
        {
            _client = client;   
        }

        public async Task<CouponDto> GetCoupon(string couponName)
        {
            var responce = await _client.GetAsync($"/api/coupon/{couponName}");
            var apiContent = await responce.Content.ReadAsStringAsync();
            var resp = JsonConvert.DeserializeObject<ResponceDto>(apiContent);
            if (resp.IsSuccess)
            {
                return JsonConvert.DeserializeObject<CouponDto>(Convert.ToString(resp.Result));
            }
            return new CouponDto();
        }
    }
}
