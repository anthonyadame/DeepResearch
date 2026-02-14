using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DeepResearchAgent.Models
{

    /// <summary>
    /// Summary of webpage content.
    /// Maps to Python's Summary class.
    /// </summary>
    public class WebpageSummary
    {
        [JsonPropertyName("summary")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("key_excerpts")]
        public string KeyExcerpts { get; set; } = string.Empty;
    }

}
