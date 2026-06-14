using MediaFlows.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/bookmarks")]
[Authorize(Policy = "CanViewContent")]
public class BookmarksApiController : ApiBaseController
{
    private readonly IBookmarkService _bookmarkService;

    public BookmarksApiController(IBookmarkService bookmarkService) => _bookmarkService = bookmarkService;

    /// <summary>GET api/v1/bookmarks — paginated user bookmarks</summary>
    [HttpGet("")]
    public async Task<IActionResult> GetBookmarks(int page = 1)
    {
        var result = await _bookmarkService.GetUserBookmarksAsync(CurrentUserId, page, 20);
        return Ok(result);
    }

    /// <summary>POST api/v1/bookmarks/{assetId}/toggle — toggle bookmark state</summary>
    [HttpPost("{assetId:int}/toggle")]
    public async Task<IActionResult> Toggle(int assetId)
    {
        var isBookmarked = await _bookmarkService.ToggleBookmarkAsync(CurrentUserId, assetId);
        return Ok(new { isBookmarked });
    }
}
