using MediaFlows.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFlows.Web.Controllers.Api;

[Route("api/v1/search")]
[Authorize(Policy = "CanViewContent")]
public class SearchApiController : ApiBaseController
{
    private readonly ISearchService _searchService;

    public SearchApiController(ISearchService searchService) => _searchService = searchService;

    [HttpGet("")]
    public async Task<IActionResult> Search(
        string q = "",
        string? category = null,
        string? fileType = null,
        int page = 1)
    {
        var results = await _searchService.SearchAsync(q, category, fileType, page, 20);
        return Ok(results);
    }

    [HttpGet("autocomplete")]
    public async Task<IActionResult> Autocomplete(string prefix = "")
    {
        var suggestions = await _searchService.GetAutocompleteSuggestionsAsync(prefix, 10);
        return Ok(suggestions);
    }
}
