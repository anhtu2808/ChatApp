using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ChatServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        [HttpPost]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file");

            var path = Path.Combine("wwwroot/files", file.FileName);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"http://{Request.Host}/files/{file.FileName}";
            return Ok(new { url = fileUrl });
        }
    }
}
