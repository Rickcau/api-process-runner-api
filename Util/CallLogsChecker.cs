using Azure;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.RegularExpressions;


namespace api_process_runner_api.Util
{
    internal class CallLogChecker
    {
        // This function is a CallLogChecker, it allows you to detect several things. the intent and take action accordingly which 
        // Was Caller Authenticated?: Yes, Was 3rd Party Involved? No, Was this Call Transfered?: No
        // Phone # updated from 8045055319 to 2512271296
        // Address updated from 6608 zW 124TH ST  OKLAHOMA CITY OK 73142 to 205 qfGzfLlf fVE  EVERGREEN AL 36401

        private string _promptVerificationConclusion = @"PersonID: {{$personid}}
        {{$query}}

         Return the Verification Conclusion of the query. The Verification Conclusion must be in the format of JSON that consists of PersonID, ActivityRelatedTo, FormOfAuthentication, Phone Number properties. The phone number will contain 10 digits and may or may not have dashes. If there are multiple numbers listed, identify the most recent, updated phone number. If there is no phone number, return 'no phone number'.
         If ActivityRelatedTo is not 'Inbound Call' VerificationCompleted should be set to 'No'.  ActivityRelatedTo must be set to 'Inbound Call' and FormOfAuthentication must be 'KBA' or 'ID Verification' or 'One Time Passcode' before VerficationsCompleted can be set to 'Yes', otherwise VerficationsCompleted must be set to 'No'. The JSON format should be:
        [JSON]
               {
                  'PersonID': '12345',
                  'ActivityRelatedTo' : '<activity related to>',
                  'FormOfAuthentication' : '<form of authentication>',
                  'PhoneNumber' : '<phone number>',
                  'VerificationsCompleted' : <verifications completed>
               }
        [JSON END]

        [Examples for JSON Output]
             {
                'PersonID': '12345',
                'ActivityRelatedTo' : 'Inbound Call',
                'FormOfAuthentication' : 'KBA',
                'PhoneNumber' : '5555555555',
                'VerificationsCompleted': 'Yes'
             }

             { 
                'PersonID': '12345',
                'ActivityRelatedto' : 'Inbound Call',
                'FormOfAuthentication' : 'ID Verfication',
                'PhoneNumber' : 'no phone number',
                'VerificationsCompleted': 'Yes'
             }

             { 
                'PersonID': '12345',
                'ActivityRelatedto' : 'Inbound Call',
                'FormOfAuthentication' : 'Low Risk',
                'PhoneNumber' : '5555555555',
                'VerificationsCompleted': 'No'
             }
 
        Per user query what is the Verification Conclusion?";



        private string _promptFraudConclusion = @"PersonID: {{$personid}}
        InStep3a: {{$instep3a}}
        PassedStep3a: {{$passedstep3a}}
        {{$query}}

        Return the Fraud Conclusion intent of the query. The Fraud Conclusion must be in the format of JSON that consists of FraudConclusionNotes, FraudConclusionType, Recommendation properties. The FraudConclusionNotes should a short summary based on your review of the query.  The FraudConclusionType should be either 'No Fraud Detected' or 'Possible Account Takeover'.  The Recommendation should be your recommendations for futher action based on your conclusions. 
        If InStep3a is false, then PassedStep3a has no impact on your logic.  If InStep3a is true and PassedStep3a is true, this means the the PersonID has passed all verificaiton steps and the form of authentication was 'One Time Passcode' and it should be noted in the FraudCOnclusionNotes that this record passed Step3a and therefore should not be considered fraud. If based on the settings of InStep3a and PassedStep3a it's concluded this record is NOT fraud
        then this should be reflected in the Recommendation, FraudConclusionNotes and FraudConclusionType.
        The JSON format should be:
        [JSON]
               {
                  'PersonID': '12345',
                  'FraudConclusionNotes': '<conclusion>',
                  'FraudConclusionType' : 'No Fraud Detected',
                  'Recommendation': '<recommendation>'
               }
        [JSON END]

        [Examples for JSON Output]
             { 
             'PersonID':'12345', 
             'FraudConclusionNotes': 'There are multiple red flags suggesting potential fraud, including changes in contact information, inquiries about card information and transaction history, alert updates indicating possible account takeover',
             'FraudConclusionType': 'Account Takeover'
             'Recommendation': 'Further investigation and monitoring of the account are warranted to confirm fraudulent activity.'
             }
 
        Per user query what is the Fraud Conclusion?";

        private string _promptActionConclusion = @"PersonID: {{$personid}}
        {{$query}}

        Return the Action Conclusion intent of the query. The Acton Conclusion must be in the format of JSON that consists of PersonID, CallerAuthenticated, FormOfAuthentication, ThirdPartyInvolved, WasCallTransferred, PhoneUpdateFrom, PhoneUpdatedTo, PhoneChanged, AddressChanged, AddressUpdateFrom, AddressUpdateTo properties. The JSON format should be:

        [JSON]
              {
                  'PersonID': '12345',
                  'CallerAuthenticated': '<authenticated>',
                  'FormOfAuthentication' : '<authform>',
                  'ThirdPartyInvolved': <Thirdpartyinvolved>',
                  'WasCallTransferred':<calltransfered>,
                  'PhoneUpdateFrom':<phoneupdatefrom>,
                  'PhoneUpdatedTo':<phoneupdateto>,
                  'PhoneChanged': 'Yes',
                  'AddressChanged':'No',
                  'AddressUpdateFrom':<addressupdatefrom>,
                  'AddressUpdateTo':<addressupdateto>
               }
        [JSON END]

        [Examples for JSON Output]
        {
                  'PersonID': '12345',
                  'CallerAuthenticated': 'Yes',
                  'FormOfAuthentication' : 'ID Verification',
                  'ThirdPartyInvolved': 'No',
                  'WasCallTransferred':'No',
                  'PhoneUpdateFrom':'8045055319',
                  'PhoneUpdatedTo':'2512271296',
                  'PhoneChanged': 'Yes',
                  'AddressChanged':'Yes',
                  'AddressUpdateFrom':'6608 zW 124TH ST  OKLAHOMA CITY',
                  'AddressUpdateTo':'205 qfGzfLlf fVE  EVERGREEN AL'
        }

        Per use query what is the Action Conclusion?";


        public async Task<string> CheckVerificationIntentAsync(Kernel kernel, string personid, string query)
        {   // This function is used for verifying step 3.  
            // Activities related to: Inbound call (has to happen) && (Form of Auth: Id Verification OR Form of Auth: KBA)
           #pragma warning disable SKEXP0010

            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ResponseFormat = "json_object", // setting JSON output mode
            };

            KernelArguments arguments2 = new(executionSettings) { { "query", query }, { "personid", personid } };
            string result = "";
            try
            {
                // KernelArguments arguments = new(new OpenAIPromptExecutionSettings { ResponseFormat = "json_object" }) { { "query", query } };
                Console.WriteLine("SK ,- CheckVerificationIntent");
                var response = await kernel.InvokePromptAsync(_promptVerificationConclusion, arguments2);
                var metadata = response.Metadata;
                if (metadata != null && metadata.ContainsKey("Usage"))
                {
                        var usage = (CompletionsUsage?)metadata["Usage"];
                        Console.WriteLine($"Token usage. Input tokens: {usage?.PromptTokens}; Output tokens: {usage?.CompletionTokens}");
                }
                result = response.GetValue<string>() ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return result ?? "";
        }

        public async Task<string> CheckFraudIntentAsync(Kernel kernel, string personid, string query,bool instep3a = false,  bool passedstep3a = false)
        {
#pragma warning disable SKEXP0010

            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ResponseFormat = "json_object", // setting JSON output mode
            };

            KernelArguments arguments2 = new(executionSettings) { { "query", query }, { "personid", personid }, { "instep3a", instep3a }, { "passedstep3a", passedstep3a } };
            string result = "";
            try
            {
                // KernelArguments arguments = new(new OpenAIPromptExecutionSettings { ResponseFormat = "json_object" }) { { "query", query } };
                Console.WriteLine("SK ,- CheckFraudIntent");
                var response = await kernel.InvokePromptAsync(_promptFraudConclusion, arguments2);
                var metadata = response.Metadata;
                if (metadata != null && metadata.ContainsKey("Usage"))
                {
                    var usage = (CompletionsUsage?)metadata["Usage"];
                    Console.WriteLine($"Token usage. Input tokens: {usage?.PromptTokens}; Output tokens: {usage?.CompletionTokens}");
                }
                result = response.GetValue<string>() ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return result ?? "";
        }

        public async Task<string> CheckActionConclusionAsync(Kernel kernel, string personid, string query)
        {
#pragma warning disable SKEXP0010

            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ResponseFormat = "json_object",
            };

            KernelArguments arguments2 = new(executionSettings) { { "query", query }, { "personid", personid } };
            string result = "";
            try
            {
                // KernelArguments arguments = new(new OpenAIPromptExecutionSettings { ResponseFormat = "json_object" }) { { "query", query } };
                Console.WriteLine("SK ,- CheckActionConclusionIntent");
                var response = await kernel.InvokePromptAsync(_promptActionConclusion, arguments2);
                var metadata = response.Metadata;
                if (metadata != null && metadata.ContainsKey("Usage"))
                {
                    var usage = (CompletionsUsage?)metadata["Usage"];
                    Console.WriteLine($"Token usage. Input tokens: {usage?.PromptTokens}; Output tokens: {usage?.CompletionTokens}");
                }
                result = response.ToString() ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return result ?? "";
        }
    }
}

