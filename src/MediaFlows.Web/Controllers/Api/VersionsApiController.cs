using MediaFlows.Data;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/assets/{assetId:int}/versions")]
[Authorize(Policy = "CanCreateContent")]
public class VersionsApiController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IMediaAssetService _assetService;
    private readonly IAuditLogService _auditLog;
    private readonly IS3StorageService _s3Storage;

    public VersionsApiController(
        ApplicationDbContext db,
        IMediaAssetService assetService,
        IAuditLogService auditLog,
        IS3StorageService s3Storage)
    {
        _db = db;
        _assetService = assetService;
        _auditLog = auditLog;
        _s3Storage = s3Storage;
    }

    /// <summary>GET api/v1/assets/{assetId}/versions — list all versions for an asset</summary>
    [HttpGet("")]
    public async Task<IActionResult> GetVersions(int assetId)
    {
        var asset = await _assetService.GetByIdAsync(assetId);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        var versions = await _db.AssetVersions
            .Where(v => v.AssetId == assetId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new
            {
                v.Id,
                v.VersionNumber,
                v.ChangeNotes,
                v.FileSize,
                v.ContentType,
                v.CreatedAt,
                previewUrl = _s3Storage.GetPublicUrl(v.S3Key),
                isCurrent = v.Id == asset.CurrentVersionId
            })
            .ToListAsync();

        return Ok(new { assetId, currentVersionId = asset.CurrentVersionId, versions });
    }

    /// <summary>GET api/v1/assets/{assetId}/versions/compare?a&b — compare two versions</summary>
    [HttpGet("compare")]
    public async Task<IActionResult> Compare(int assetId, [FromQuery] int a, [FromQuery] int b)
    {
        var asset = await _assetService.GetByIdAsync(assetId);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        var versions = await _db.AssetVersions
            .Where(v => v.AssetId == assetId && (v.Id == a || v.Id == b))
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();

        if (versions.Count != 2)
            return ApiError("Two valid version IDs belonging to this asset are required");

        return Ok(new
        {
            assetId,
            currentVersionId = asset.CurrentVersionId,
            versionA = new
            {
                versions[0].Id,
                versions[0].VersionNumber,
                versions[0].ContentType,
                versions[0].FileSize,
                versions[0].ChangeNotes,
                versions[0].CreatedAt,
                mediaUrl = !string.IsNullOrEmpty(versions[0].S3Key) ? _s3Storage.GetPublicUrl(versions[0].S3Key) : (string?)null
            },
            versionB = new
            {
                versions[1].Id,
                versions[1].VersionNumber,
                versions[1].ContentType,
                versions[1].FileSize,
                versions[1].ChangeNotes,
                versions[1].CreatedAt,
                mediaUrl = !string.IsNullOrEmpty(versions[1].S3Key) ? _s3Storage.GetPublicUrl(versions[1].S3Key) : (string?)null
            }
        });
    }

    /// <summary>POST api/v1/assets/{assetId}/versions — upload a new version</summary>
    [HttpPost("")]
    public async Task<IActionResult> UploadNewVersion(int assetId, [FromBody] NewVersionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.S3Key))
            return ApiError("s3Key is required");

        var asset = await _assetService.GetByIdAsync(assetId);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        var maxVersion = asset.Versions.Any()
            ? asset.Versions.Max(v => v.VersionNumber)
            : 0;

        var newVersion = new AssetVersion
        {
            AssetId = assetId,
            VersionNumber = maxVersion + 1,
            S3Key = request.S3Key,
            ContentType = asset.ContentType,
            FileSize = request.FileSize ?? 0,
            UploadedById = CurrentUserId,
            ChangeNotes = string.IsNullOrWhiteSpace(request.ChangeNotes)
                ? $"Version {maxVersion + 1} uploaded"
                : request.ChangeNotes.Trim()
        };

        _db.AssetVersions.Add(newVersion);
        await _db.SaveChangesAsync();

        asset.CurrentVersionId = newVersion.Id;
        asset.S3Key = request.S3Key;
        asset.Status = AssetStatus.Draft;
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync("AssetVersion.Create", "AssetVersion", newVersion.Id.ToString(),
            new { assetId, newVersion.VersionNumber });

        return Ok(new
        {
            id = newVersion.Id,
            assetId,
            versionNumber = newVersion.VersionNumber,
            changeNotes = newVersion.ChangeNotes,
            status = AssetStatus.Draft.ToString()
        });
    }

    /// <summary>POST api/v1/assets/{assetId}/versions/{versionId}/revert — revert to a version</summary>
    [HttpPost("{versionId:int}/revert")]
    public async Task<IActionResult> Revert(int assetId, int versionId)
    {
        var version = await _db.AssetVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.AssetId == assetId);

        if (version == null)
            return ApiNotFound("Version");

        var asset = await _db.MediaAssets.FindAsync(assetId);
        if (asset == null)
            return ApiNotFound("Asset");

        if (asset.CreatorId != CurrentUserId)
            return Forbid();

        asset.CurrentVersionId = versionId;
        asset.S3Key = version.S3Key;
        asset.Status = AssetStatus.Draft;
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync("AssetVersion.Revert", "AssetVersion", versionId.ToString(),
            new { assetId, version.VersionNumber });

        return Ok(new
        {
            assetId,
            revertedToVersionId = versionId,
            versionNumber = version.VersionNumber,
            status = AssetStatus.Draft.ToString()
        });
    }
}

public class NewVersionRequest
{
    public string S3Key { get; set; } = null!;
    public long? FileSize { get; set; }
    public string? ChangeNotes { get; set; }
}
