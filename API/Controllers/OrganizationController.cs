using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using platform_ai_backend_netcore.Application.Organization.Interfaces;
using platform_ai_backend_netcore.Application.Partners.DTOs;

namespace platform_ai_backend_netcore.API.Controllers
{
    [ApiController]
    [Route("v1/api/admin/partners/organizations")] 
    public class OrganizationController(IOrganizationService organizationService) : ControllerBase
    {
        /// <summary>Lấy thông tin tổ chức (organization profile) của partner</summary>
        [HttpGet("{partnerId:guid}")]
        public async Task<IActionResult> Get(Guid partnerId)
        {
            var result = await organizationService.GetByPartnerIdAsync(partnerId);
            return result.Success
                ? Ok(result.Data)
                : NotFound(new { message = result.Message });
        }

        /// <summary>Cập nhật thông tin tổ chức — chỉ update field được gửi lên</summary>
        [HttpPatch("{partnerId:guid}")]
        public async Task<IActionResult> Update(Guid partnerId, [FromBody] UpdatePartnerDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await organizationService.UpdateAsync(partnerId, dto);
            return result.Success
                ? Ok(result.Data)
                : NotFound(new { message = result.Message });
        }
    }
}