using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Authorize(Policy = "CanViewContent")]
public class CommentsApiController : ApiBaseController
{
    private readonly ICommentService _commentService;

    public CommentsApiController(ICommentService commentService) => _commentService = commentService;

    /// <summary>GET api/v1/assets/{assetId}/comments — list comments for an asset</summary>
    [HttpGet("/api/v1/assets/{assetId:int}/comments")]
    public async Task<IActionResult> GetComments(int assetId)
    {
        var comments = await _commentService.GetCommentsAsync(assetId, CurrentUserId);
        return Ok(comments);
    }

    /// <summary>POST api/v1/assets/{assetId}/comments — add a comment</summary>
    [HttpPost("/api/v1/assets/{assetId:int}/comments")]
    public async Task<IActionResult> CreateComment(int assetId, [FromBody] CreateCommentRequest request)
    {
        if (!ModelState.IsValid)
            return ApiError("Invalid comment data.");

        // Ensure the route assetId takes precedence
        request.AssetId = assetId;

        try
        {
            var comment = await _commentService.AddCommentAsync(
                request.AssetId, CurrentUserId, request.Content, request.ParentCommentId);
            return CreatedAtAction(nameof(GetComments), new { assetId }, comment);
        }
        catch (Exception ex)
        {
            return ApiError(ex.Message);
        }
    }

    /// <summary>PUT api/v1/comments/{id} — edit own comment</summary>
    [HttpPut("/api/v1/comments/{id:int}")]
    public async Task<IActionResult> UpdateComment(int id, [FromBody] UpdateCommentRequest request)
    {
        if (!ModelState.IsValid)
            return ApiError("Invalid comment data.");

        request.CommentId = id;

        try
        {
            var updated = await _commentService.UpdateCommentAsync(id, CurrentUserId, request.Content);
            if (updated == null)
                return ApiNotFound("Comment");
            return Ok(updated);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiError(ex.Message, 403);
        }
    }

    /// <summary>DELETE api/v1/comments/{id} — delete own comment</summary>
    [HttpDelete("/api/v1/comments/{id:int}")]
    public async Task<IActionResult> DeleteComment(int id)
    {
        try
        {
            var deleted = await _commentService.DeleteCommentAsync(id, CurrentUserId);
            if (!deleted)
                return ApiNotFound("Comment");
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiError(ex.Message, 403);
        }
    }
}
