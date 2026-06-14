using MediaFlows.Shared.DTOs;

namespace MediaFlows.Shared.Interfaces;

public interface ICommentService
{
    Task<CommentDto> AddCommentAsync(int assetId, string authorId, string content, int? parentCommentId = null);
    Task<CommentDto?> UpdateCommentAsync(int commentId, string authorId, string newContent);
    Task<bool> DeleteCommentAsync(int commentId, string authorId);
    Task<List<CommentDto>> GetCommentsAsync(int assetId, string? currentUserId = null);
    Task<int> GetCommentCountAsync(int assetId);
}
