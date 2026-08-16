using System.Security.Claims;
using CDNBackend.API.Models.Dtos;
using CDNBackend.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDNBackend.API.Controllers;

[ApiController]
[Route("api/images")]
public class ImagesController : ControllerBase
{
    private readonly ImageService _images;

    public ImagesController(ImageService images) => _images = images;

    [Authorize]
    [HttpPost]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<ImageDto>> Upload(
        [FromForm] string? name,
        [FromForm] string? description,
        [FromForm] string? category,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var image = await _images.UploadAsync(GetUserId(), file, name, description, category, cancellationToken);
        return Created($"/api/images/{image.Id}", ImageDto.From(image));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ImageDto>>> GetAll([FromQuery] int? userId)
        => Ok((await _images.ListAsync(userId)).Select(ImageDto.From));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ImageDto>> GetById(int id)
        => Ok(ImageDto.From(await _images.GetByIdAsync(id)));

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, [FromQuery] bool download = false, CancellationToken cancellationToken = default)
    {
        var image = await _images.GetByIdAsync(id);
        var stream = await _images.OpenReadAsync(image.Url, cancellationToken);

        // Las URLs son content-addressed (guid único por subida): el archivo nunca
        // cambia en esa URL. Cache largo + immutable para que el navegador baje
        // cada imagen UNA sola vez (evita el lag/recarga al navegar o cambiar tabs).
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";

        return download
            ? File(stream, image.ContentType, image.Name, enableRangeProcessing: true)
            : File(stream, image.ContentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Versión reducida de la imagen para grids (mucho más liviana de decodificar
    /// y texturizar). Si la imagen no tiene miniatura, se genera bajo demanda.
    /// </summary>
    [HttpGet("{id:int}/thumbnail")]
    public async Task<IActionResult> Thumbnail(int id, CancellationToken cancellationToken = default)
    {
        var image = await _images.GetByIdAsync(id);
        var (stream, contentType) = await _images.OpenThumbnailAsync(image, cancellationToken);
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(stream, contentType, enableRangeProcessing: true);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _images.DeleteAsync(id, GetUserId(), User.IsInRole("admin"));
        return NoContent();
    }

    private int GetUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
