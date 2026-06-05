using DZI_api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DZI_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HelloController(
        IOptions<CloudflareR2Options> _options) : ControllerBase
    {
        [HttpGet]
        public async Task<string> Get()
        {
            return await Task.FromResult(_options.Value.BucketName);
        }
    }
}
