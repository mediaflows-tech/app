using System.ComponentModel.DataAnnotations;

namespace MediaFlows.Shared.DTOs;

public class UploadConfirmRequest
{
    [Required]
    public string S3Key { get; set; } = null!;

    [Required]
    public string FileName { get; set; } = null!;

    [Required]
    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }
}
