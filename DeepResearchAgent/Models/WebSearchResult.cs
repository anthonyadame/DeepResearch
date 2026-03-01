using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DeepResearchAgent.Models
{

    /// <summary>
    /// Result from web search.
    /// Maps search API response structure.
    /// </summary>
    public class WebSearchResult
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("engine")]
        public string Engine { get; set; } = string.Empty;

        [JsonPropertyName("raw_content")]
        public string? RawContent { get; set; }

        [JsonPropertyName("score")]
        public double? Score { get; set; }
    }
}
