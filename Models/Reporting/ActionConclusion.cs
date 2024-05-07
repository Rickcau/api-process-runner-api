using FileHelpers;

namespace api_process_runner_api.Models.Reporting
{
    [DelimitedRecord(",")]
    public class ActionConclusion
    {
        public string? PersonID { get; set; }
        public string? CallerAuthenticated { get; set; }
        public string? FormOfAuthentication { get; set; }
        public string? ThirdPartyInvolved { get; set; }
        public string? WasCallTransferred { get; set; }
        public string? PhoneUpdateFrom { get; set; }
        public string? PhoneUpdateTo { get; set; }
        public string? PhoneChanged { get; set; }
        public string? AddressChanged { get; set; }
        public string? AddressUpdateFrom { get; set; }
        public string? AddressUpdateTo { get; set; }
    }
}


