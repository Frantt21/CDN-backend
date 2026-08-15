using System.Security.Claims;
using CDNBackend.API.Models.Dtos;
using CDNBackend.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDNBackend.API.Controllers;

[ApiController]
[Route("api/saved")]
[Authorize]
public class SavedImagesController : ControllerBase
{
    private readonly SavedImageService _saved;

    public SavedImagesController(SavedImageService saved) => _saved = saved;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ImageDto>>> GetMySaved()
        => Ok((await _saved.ListByUserAsync(GetUserId())).Select(ImageDto.From));

    [HttpPost("{imageId:int}")]
    public async Task<IActionResult> Save(int imageId)
        => Ok(await _saved.SaveAsync(GetUserId(), imageId));

    [HttpDelete("{imageId:int}")]
    public async Task<IActionResult> Unsave(int imageId)
    {
        await _saved.UnsaveAsync(GetUserId(), imageId);
        return NoContent();
    }

    private int GetUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
