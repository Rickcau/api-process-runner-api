namespace api_process_runner_api.Models
{
    public class VerificationCompleted
    {
        public string? PersonID { get; set; }
        public string? ActivityRelatedTo { get; set; }
        public string? FormOfAuthentication { get; set; }
        public string? PhoneNumber { get; set; }
        public string? RefIdFoundNineOrMore { get; set; }
        public string? PassFound { get; set; }
        public string? VerificationsCompleted { get; set; }
    }
}
