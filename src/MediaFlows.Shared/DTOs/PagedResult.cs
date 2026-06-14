namespace MediaFlows.Shared.DTOs;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
    public int NextPage => Page + 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
