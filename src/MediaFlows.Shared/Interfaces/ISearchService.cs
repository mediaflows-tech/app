using MediaFlows.Shared.DTOs;

namespace MediaFlows.Shared.Interfaces;

public interface ISearchService
{
    Task<PagedResult<SearchResultDto>> SearchAsync(
        string query, string? category, string? fileType, int page, int pageSize);
    Task<List<string>> GetAutocompleteSuggestionsAsync(string prefix, int limit = 10);
}
