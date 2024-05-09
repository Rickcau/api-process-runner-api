using FileHelpers;

namespace api_process_runner_api.Models.Reporting
{
    [DelimitedRecord(",")]
    public class FraudConclusion
    {
        public string? PersonID { get; set; }

        public string? FraudConclusionNotes { get; set; }
        public string? FraudConclusionType { get; set; }
        [FieldQuoted]
        public string? Recommendation { get; set; }
    }

}
