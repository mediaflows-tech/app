using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/upload")]
[Authorize(Policy = "CanCreateContent")]
public class UploadApiController : ApiBaseController
{
    private readonly IUploadService _uploadService;
    private readonly IMediaAssetService _assetService;
    private readonly IAuditLogService _auditLog;

    public UploadApiController(
        IUploadService uploadService,
        IMediaAssetService assetService,
        IAuditLogService auditLog)
    {
        _uploadService = uploadService;
        _assetService = assetService;
        _auditLog = auditLog;
    }

    [HttpGet("presign")]
    public IActionResult GetPresignedUrl([FromQuery] string fileName, [FromQuery] string contentType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return ApiError("fileName is required");

        if (string.IsNullOrWhiteSpace(contentType))
            return ApiError("contentType is required");

        try
        {
            var result = _uploadService.GeneratePresignedUrl(CurrentUserId, fileName, contentType);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return ApiError(ex.Message);
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmUpload([FromBody] UploadConfirmRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.S3Key))
            return ApiError("Invalid confirm request");

        try
        {
            var asset = await _uploadService.ConfirmUploadAsync(request, CurrentUserId);
            var created = await _assetService.CreateAsync(asset);

            await _auditLog.LogAsync("Asset.Create", "MediaAsset", created.Id.ToString(),
                new { created.Id, created.Title, created.ContentType });

            return Ok(new
            {
                id = created.Id,
                title = created.Title,
                status = created.Status.ToString(),
                contentType = created.ContentType,
                fileSize = created.FileSize
            });
        }
        catch (InvalidOperationException ex)
        {
            return ApiError(ex.Message);
        }
    }
}
