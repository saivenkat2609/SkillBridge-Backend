namespace DotnetLearning.Models
{
    public class SkillQueryParams
    {
        public string? SearchTerm { get; set; }

        public int? CategoryId { get; set; }
        public int MinPrice { get; set; } = 0;

        public int MaxPrice { get; set; }=int.MaxValue;

        public string SortBy { get; set; }= "Rating";

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 12;
    }
}
