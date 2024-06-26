﻿using Azure;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;
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
         Return the Verification Conclusion of the query, (ignore case making decisions). Use the following steps when evaluating the query.
         1. Check to see if 'Activities Related To:' is set to 'Inbound Call' if not VerificationCompleted should be set to 'No'  
         2. Check the 'Form of Authentication' is 'ID Verification' skip to step 7
         3. Check the 'Form of Authentication' for 'KBA' or 'One time passcode'
         4. If step 3 is true, check to see if 'Reference ID:' exists and if so, is there a 9 digit value following it, if true set 'RefIdFoundNineOrMore' to 'Yes' otherwise set to 'No'
         5. Check to see of the word 'Pass' or 'Passed' exists if so set 'PassFound' to 'Yes', otherwise set to 'No'
         6. If step 4 and 5 are true conditions, set VertificatonCompleted to 'Yes'.
         7. If 'Form of Authtication' is 'ID Verification' set VerificationCompleted to 'Yes' 
 The JSON format should be:
        [JSON]
              {
                 'PersonID': '12345',
                 'ActivityRelatedTo' : '<activity related to>',
                 'FormOfAuthentication' : '<form of authentication>',
                 'PhoneNumber' : '<phone number>',
                 'RefIdFoundNineOrMore' : '<Yes>',
                 'PassFound' : '<No>',
                 'VerificationsCompleted' : '<verification completed>'
              }
        [JSON END]

        [Examples for JSON Output]
             {
                'PersonID': '12345',
                'ActivityRelatedTo' : 'Inbound Call',
                'FormOfAuthentication' : 'KBA',
                'PhoneNumber' : '5555555555',
                'RefIdFoundNineOrMore' : 'Yes',
                'PassFound' : 'Yes',
                'VerificationsCompleted': 'Yes'
             }

             { 
                'PersonID': '12345',
                'ActivityRelatedto' : 'Inbound Call',
                'FormOfAuthentication' : 'ID Verfication',
                'PhoneNumber' : 'no phone number',
                'RefIdFoundNineOrMore' : 'No',
                'PassFound' : 'No',
                'VerificationsCompleted': 'Yes'
             }

             { 
                'PersonID': '12345',
                'ActivityRelatedto' : 'Inbound Call',
                'FormOfAuthentication' : 'Low Risk',
                'PhoneNumber' : '5555555555',
                'RefIdFoundNineOrMore' : 'No',
                'PassFound' : 'No',
                'VerificationsCompleted': 'No'
             }
 
        Per user query what is the Verification Conclusion?";



        private string _promptFraudConclusion = @"InStep3a is {{$instep3a}}
                PassedStep3a is {{$passedstep3a}}
                PersonID is {{$personid}}
                Return the Fraud Conclusion intent of the user query. The Fraud Conclusion must be in the format of JSON that consists of FraudConclusionNotes, FraudConclusionType, Recommendation properties.
                The FraudConclusionNotes should a short summary based on your review of the query.
                        The FraudConclusionType should be either 'No Fraud Detected' or 'Possible Account Takeover'.
                        The Recommendation should be your recommendations for futher action based on your conclusions. 
                        If InStep3a is false, then PassedStep3a has no impact on your logic.
                        If InStep3a is true AND PassedStep3a is true, you must conclude that this is NOT fraud.
                            This means that the PersonID has passed all verification steps and the form of authentication was 'One Time Passcode'
                            and it should be noted in the FraudConclusionNotes that this record passed Step3a.
                        Based on the instructions above on how to interpret InStep3a and PassedStep3a values, if you conclude that this record is NOT fraud
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
                  'ThirdPartyInvolved': '<Thirdpartyinvolved>',
                  'WasCallTransferred':'<calltransfered>',
                  'PhoneUpdateFrom':'<phoneupdatefrom>',
                  'PhoneUpdatedTo':'<phoneupdateto>',
                  'PhoneChanged': 'Yes',
                  'AddressChanged':'No',
                  'AddressUpdateFrom':'<addressupdatefrom>',
                  'AddressUpdateTo':'<addressupdateto>'
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
        {
                  'PersonID': '12345',
                  'CallerAuthenticated': 'Yes',
                  'FormOfAuthentication' : 'ID Verification',
                  'ThirdPartyInvolved': 'Yes',
                  'WasCallTransferred':'Yes',
                  'PhoneUpdateFrom':'8045055319',
                  'PhoneUpdatedTo':'2512271296',
                  'PhoneChanged': 'Yes',
                  'AddressChanged':'Yes',
                  'AddressUpdateFrom':'6608 zW 124TH ST  OKLAHOMA CITY',
                  'AddressUpdateTo':'205 qfGzfLlf fVE  EVERGREEN AL'
        }
        All JSON properties of the JSON output are of string type and you should never return boolean for any of the properties.

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
                Console.WriteLine($@"Verificaiton Conclusion:{personid}");
                Console.WriteLine(response);
                Console.WriteLine("----------------------");
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

        public async Task<string> CheckFraudIntentAsync(Kernel kernel, IChatCompletionService chat, string personid, string query, bool instep3a = false, bool passedstep3a = false)
        {
#pragma warning disable SKEXP0010
            string promptFraudConclusion = _promptFraudConclusion;
            promptFraudConclusion = promptFraudConclusion.Replace("{{$personid}}", personid);
            promptFraudConclusion = promptFraudConclusion.Replace("{{$instep3a}}", instep3a != null ? instep3a.ToString() : "");
            promptFraudConclusion = promptFraudConclusion.Replace("{{$passedstep3a}}", passedstep3a != null ? passedstep3a.ToString() : "");

            ChatHistory chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(promptFraudConclusion);
            chatHistory.AddSystemMessage(query);
            // The quorom patten should avoid needing to use this
            //chatHistory.AddUserMessage("If PassedStep3a is True and InStep3a is True, the Fraud Conclusion Type Must be 'No Fraud Detected'");


            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ResponseFormat = "json_object", // setting JSON output mode
                Temperature = .5,
                // Instruct the model to give us 3 results for the prompt in one call
                ResultsPerPrompt = 3,
            };

            string result = "";

            try
            {
                var chatResponse = await chat.GetChatMessageContentsAsync(
                    chatHistory,
                    executionSettings);

                string strChatResponse = string.Join(", ", chatResponse.Select(o => o.ToString()));

                // TBD:  Need to extract MetaData to so we can print the token usage.  

                var fraudConclusions = Regex.Matches(strChatResponse, @"(?<=""FraudConclusionType"":\s*"")[^""]+");

                // Count fraud conclusion occurrences
                var fraudConclusionCounts = new Dictionary<string, int>();
                foreach (Match fraudConclusion in fraudConclusions)
                {
                    var conclusion = fraudConclusion.Value;
                    if (fraudConclusionCounts.ContainsKey(conclusion))
                        fraudConclusionCounts[conclusion]++;
                    else
                        fraudConclusionCounts[conclusion] = 1;
                }

                // get the fraud conclusion with the highest occurence
                var finalFraudConclusionType = fraudConclusionCounts.MaxBy(kvp => kvp.Value).Key;

                // select the result to be the first result which in the chat response which has this fraud conclusion
                foreach (var response in chatResponse)
                {
                    var jsonDocument = JsonDocument.Parse(response.Content).RootElement;

                    string responseFraudConclusionType = jsonDocument.GetProperty("FraudConclusionType").GetString();

                    if (responseFraudConclusionType.Equals(finalFraudConclusionType))
                    {
                        result = response.Content;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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

