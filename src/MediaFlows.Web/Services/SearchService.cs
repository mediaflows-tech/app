using MediaFlows.Data;
using MediaFlows.Shared.DTOs;
using MediaFlows.Shared.Interfaces;
using MediaFlows.Shared.Models.Entities;
using MediaFlows.Shared.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace MediaFlows.Web.Services;

public class SearchService : ISearchService
{
    private readonly ApplicationDbContext _db;
    private readonly IS3StorageService? _s3Storage;

    public SearchService(ApplicationDbContext db, IS3StorageService s3Storage)
    {
        _db = db;
        _s3Storage = s3Storage;
    }

    public SearchService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<SearchResultDto>> SearchAsync(
        string query, string? category, string? fileType, int page, int pageSize)
    {
        var baseQuery = _db.MediaAssets
            .Where(a => a.Status == AssetStatus.Published);

        if (!string.IsNullOrEmpty(category))
            baseQuery = baseQuery.Where(a => a.Project != null && a.Project.Name == category);

        if (!string.IsNullOrEmpty(fileType))
            baseQuery = baseQuery.Where(a => a.ContentType.StartsWith(fileType));

        return string.IsNullOrWhiteSpace(query)
            ? await ExecuteSearchAsync(baseQuery, page, pageSize)
            : await ExecuteSearchWithFallbackAsync(baseQuery, query, page, pageSize);
    }

    public async Task<List<string>> GetAutocompleteSuggestionsAsync(string prefix, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length < 2)
            return new List<string>();

        return await _db.MediaAssets
            .Where(a => a.Status == AssetStatus.Published)
            .Where(a => EF.Functions.ILike(a.Title, $"{prefix}%"))
            .OrderBy(a => a.Title)
            .Select(a => a.Title)
            .Distinct()
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
    }

    private async Task<PagedResult<SearchResultDto>> ExecuteSearchAsync(
        IQueryable<MediaAsset> query,
        int page,
        int pageSize)
    {
        var totalCount = await query.CountAsync();
        var projected = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                Dto = new SearchResultDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    ThumbnailUrl = a.ThumbnailUrl,
                    PreviewUrl = a.ThumbnailUrl,
                    ContentType = a.ContentType,
                    CreatorName = a.Creator.DisplayName,
                    CreatedAt = a.CreatedAt,
                    Headline = a.Description
                },
                a.S3Key
            })
            .AsNoTracking()
            .ToListAsync();

        var items = projected.Select(p =>
        {
            ResolvePreviewUrl(p.Dto, p.S3Key);
            return p.Dto;
        }).ToList();

        return BuildPagedResult(items, totalCount, page, pageSize);
    }

    private async Task<PagedResult<SearchResultDto>> ExecuteSearchWithFallbackAsync(
        IQueryable<MediaAsset> baseQuery,
        string searchQuery,
        int page,
        int pageSize)
    {
        if (_db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var fullTextQuery = baseQuery.Where(a =>
                a.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("english", searchQuery)));

            var fullTextCount = await fullTextQuery.CountAsync();
            if (fullTextCount > 0)
            {
                var projected = await fullTextQuery
                    .OrderByDescending(a => a.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", searchQuery)))
                    .ThenByDescending(a => a.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new
                    {
                        Dto = new SearchResultDto
                        {
                            Id = a.Id,
                            Title = a.Title,
                            Description = a.Description,
                            ThumbnailUrl = a.ThumbnailUrl,
                            PreviewUrl = a.ThumbnailUrl,
                            ContentType = a.ContentType,
                            CreatorName = a.Creator.DisplayName,
                            CreatedAt = a.CreatedAt,
                            Headline = a.Description
                        },
                        a.S3Key
                    })
                    .AsNoTracking()
                    .ToListAsync();

                var items = projected.Select(p =>
                {
                    ResolvePreviewUrl(p.Dto, p.S3Key);
                    return p.Dto;
                }).ToList();

                return BuildPagedResult(items, fullTextCount, page, pageSize);
            }
        }

        var normalized = NormalizeQuery(searchQuery);
        var fallbackCandidates = await baseQuery
            .Select(a => new
            {
                Dto = new SearchResultDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    ThumbnailUrl = a.ThumbnailUrl,
                    PreviewUrl = a.ThumbnailUrl,
                    ContentType = a.ContentType,
                    CreatorName = a.Creator.DisplayName,
                    CreatedAt = a.CreatedAt,
                    Headline = a.Description
                },
                a.S3Key
            })
            .AsNoTracking()
            .ToListAsync();

        var threshold = Math.Max(2, normalized.Length / 3);
        var fallbackItems = fallbackCandidates
            .Select(item => new
            {
                item.Dto,
                item.S3Key,
                Score = GetBestDistance(normalized, item.Dto.Title, item.Dto.Description)
            })
            .Where(x => x.Score <= threshold)
            .OrderBy(x => x.Score)
            .ThenByDescending(x => x.Dto.Title.StartsWith(searchQuery, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Dto.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x =>
            {
                ResolvePreviewUrl(x.Dto, x.S3Key);
                return x.Dto;
            })
            .ToList();

        var fallbackTotalCount = fallbackCandidates.Count(item => GetBestDistance(normalized, item.Dto.Title, item.Dto.Description) <= threshold);

        return BuildPagedResult(fallbackItems, fallbackTotalCount, page, pageSize);
    }

    private void ResolvePreviewUrl(SearchResultDto dto, string s3Key)
    {
        if (!string.IsNullOrEmpty(dto.PreviewUrl))
            return;

        if (_s3Storage is not null &&
            dto.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var url = _s3Storage.GetPublicUrl(s3Key);
            dto.PreviewUrl = url;
            dto.ThumbnailUrl ??= url;
        }
    }

    private static PagedResult<SearchResultDto> BuildPagedResult(
        List<SearchResultDto> items,
        int totalCount,
        int page,
        int pageSize) =>
        new()
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            HasMore = (page * pageSize) < totalCount
        };

    private static string NormalizeQuery(string query) =>
        Regex.Replace(query.Trim().ToLowerInvariant(), @"\s+", " ");

    private static int GetBestDistance(string normalizedQuery, string title, string? description)
    {
        var haystacks = new[]
        {
            NormalizeQuery(title),
            NormalizeQuery(description ?? string.Empty)
        };

        var best = int.MaxValue;

        foreach (var haystack in haystacks.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (haystack.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                normalizedQuery.Contains(haystack, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            foreach (var token in haystack.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                best = Math.Min(best, LevenshteinDistance(normalizedQuery, token));
            }
        }

        return best == int.MaxValue ? normalizedQuery.Length : best;
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (source == target) return 0;
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        var matrix = new int[source.Length + 1, target.Length + 1];

        for (var i = 0; i <= source.Length; i++) matrix[i, 0] = i;
        for (var j = 0; j <= target.Length; j++) matrix[0, j] = j;

        for (var i = 1; i <= source.Length; i++)
        {
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[source.Length, target.Length];
    }
}
