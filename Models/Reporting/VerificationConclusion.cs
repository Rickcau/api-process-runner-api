using FileHelpers;

namespace api_process_runner_api.Models.Reporting
{
    [DelimitedRecord(",")]
    public class VerificationConclusion
    {
        public string? PersonID { get; set; }
        public string? ActivityRelatedTo { get; set; }
        public string? FormOfAuthentication { get; set; }
        public string? PhoneNumber { get; set; }
        public string? VerificationsCompleted { get; set; }
    }
}
