using api_process_runner_api.Helpers;
using api_process_runner_api.Helpers.Reporting;
using api_process_runner_api.Models;
using api_process_runner_api.Models.Reporting;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace api_process_runner_api.Util
{
   
    public class StepResult
    {
        public string? StepName { get; set; }
        public string? StatusCode { get; set; }
        public string? Message { get; set; }
    }

    public class ProcessResult
    {
        public StepResult? EppicAddressInHospitalDBResult { get; set; }  // Step 1
        public StepResult? EppicAddressInGiactResult { get; set; }       // Step 2 
        public StepResult? EppicSeibelCallNotesResult { get; set; }      // Step 3
        public StepResult? EppicSeibelOPTCallNotesResult { get; set; }   // Step 3a
        public string? MasterStatusCode { get; set; }                    // Pass or Fail
    }
    //  public class ProcessRunnerSteps(EppicRecords eppicrecord, SiebelDataParser siebeldataparser, GiactDataParser giactdataparser, EppicDataParser eppicdataparser)
    internal class ProcessRunnerSteps(DataHelper datahelper, Kernel kernel, IChatCompletionService chat, JobStatus jobstatus, StepsLogFile stepslogfile)
    {
        private DataHelper _datahelper = datahelper;
        private Kernel _kernel = kernel;
        private IChatCompletionService _chat = chat;
        private JobStatus _jobstatus = jobstatus;
        private StepsLogFile _stepslogfile = stepslogfile;
        // Adding Managers for CSV reporting
        private EppicStepResultsManager _eppicstepresultsmanager = new EppicStepResultsManager();
        private ActionConclusionManager _actionconclusionmanager = new ActionConclusionManager();
        private FraudConclusionManager _fraudconclusionmanager = new FraudConclusionManager();
        private VerificationConclusionManager _verificationconclusionmanager = new VerificationConclusionManager();

        //  public ProcessResult RunSteps()
        public async Task RunSteps()
        {
            // var processResult = new ProcessResult();
            #region Step 1: Eppic Check Against Hospital DB - no need to run step 1
            StepLogger stepLogger = new StepLogger();
            // var eppicStepResultsRecord = new EppicStepResults();


            if (Globals.inputEppicRecordsInHospitalDB != null && (Globals.inputEppicRecordsInHospitalDB.Count() > 0))
            {
                // Let's add the items that have a match in Hospital DB to the Log Collection
                var eppicRecordsInHospitalDB = Globals.inputEppicRecordsInHospitalDB.ToList() ;
                foreach (var record in eppicRecordsInHospitalDB)
                {
                    // TBD Needs to be debugged it's printing out like 20 items when there are only 5
                    stepLogger.AddItem(record, "Step 1 - Eppic Records in Hospital List", "PASS/Stop do not go to next step");
                    var eppicStepResultRecord = new EppicStepResults
                    {
                        LogID = _stepslogfile.FileName,
                        LogDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    };
                    eppicStepResultRecord.LogID = _stepslogfile.FileName;
                    eppicStepResultRecord.PersonID = record.PersonID;
                    eppicStepResultRecord.MarkedAsFraud = false;
                    eppicStepResultRecord.PhoneNumber = record.Phone_Number;
                    eppicStepResultRecord.AddressLine1 = record.AddressLine1;
                    eppicStepResultRecord.AddressLine2 = record.AddressLine2;
                    eppicStepResultRecord.City = record.City;
                    eppicStepResultRecord.State = record.State;
                    eppicStepResultRecord.Zip = record.ZipCode;
                    eppicStepResultRecord.Step1HospitalMatch = true;
                    eppicStepResultRecord.Step2GiactMatch = false;
                    eppicStepResultRecord.Step3PassedVerificationCheck = false;
                    eppicStepResultRecord.Step3aPassedOTPPhoneGiact = false;
                    eppicStepResultRecord.LastStepCompleted = "Step1";
                    eppicStepResultRecord.Status = "Eppic Record found in Hospital List - Pass/Stop no need to go to Step 2";
                    _eppicstepresultsmanager.AddOrUpdateEppicStepResult(eppicStepResultRecord); // Add the result to the collection
                }
            }
            if (Globals.inputEppicRecordsNotInHospitalDB != null && (Globals.inputEppicRecordsNotInHospitalDB.Count() > 0))
            {
                // Let's add the items that do not have match Hospital DB to the Log Collection
                var eppicRecordsNotInHospitalDB = Globals.inputEppicRecordsNotInHospitalDB.ToList();
                foreach (var record in eppicRecordsNotInHospitalDB)
                {
                    // TBD Needs to be debugged it's printing out like 20 items when there are only 5
                    stepLogger.AddItem(record, "Step 1 - Eppic Records Not in Hospital List", "FAIL Go to next Step");
                    var eppicStepResultRecord = new EppicStepResults
                    {
                        LogID = _stepslogfile.FileName,
                        LogDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    };
                    eppicStepResultRecord.PersonID = record.PersonID;
                    eppicStepResultRecord.MarkedAsFraud = false;
                    eppicStepResultRecord.PhoneNumber = record.Phone_Number;
                    eppicStepResultRecord.AddressLine1 = record.AddressLine1;
                    eppicStepResultRecord.AddressLine2 = record.AddressLine2;
                    eppicStepResultRecord.City = record.City;
                    eppicStepResultRecord.State = record.State;
                    eppicStepResultRecord.Zip = record.ZipCode;
                    eppicStepResultRecord.Step1HospitalMatch = false;
                    eppicStepResultRecord.Step2GiactMatch = false;
                    eppicStepResultRecord.Step3PassedVerificationCheck = false;
                    eppicStepResultRecord.Step3aPassedOTPPhoneGiact = false;
                    eppicStepResultRecord.LastStepCompleted = "Step1";
                    eppicStepResultRecord.Status = "Eppic Record not found in Hospital List - Fail go to Step 2";
                    _eppicstepresultsmanager.AddOrUpdateEppicStepResult(eppicStepResultRecord); // Add the result to the collection
                }
            }
            _jobstatus.Status = "Step 1 completed";
            Console.WriteLine(_jobstatus.Status);
            #endregion

            #region Step 2: Check Eppic Address against GIACT only if last step is set to Pass
            // Now let's process the items from Step 1 that did not have a match in the Hospital DB
            // Check the Address Information in GIACT 
            // In the DataHelper.Initilize() when we load the data we build a list of Epic items that have an exact match in Giact and list that does not have a match
            // Now, all we have to do is log the result to the Log collection!

            if (Globals.inputEppicRecordsInGiactDBNotInHospitalsDB != null && (Globals.inputEppicRecordsInGiactDBNotInHospitalsDB.Count() > 0))
            {
                // Let's add the items that have a match in Hospital DB to the Log Collection
                var eppicRecordsInGiatDB = Globals.inputEppicRecordsInGiactDBNotInHospitalsDB.ToList();
                foreach (var record in eppicRecordsInGiatDB)
                {
                    stepLogger.AddItem(record, "Step 2 - Eppic Records in Giact DB", "Match/Contine to step 3");
                    var eppicStepResultRecord = new EppicStepResults
                    {
                        LogID = _stepslogfile.FileName,
                        LogDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    };
                    eppicStepResultRecord.PersonID = record.PersonID;
                    eppicStepResultRecord.MarkedAsFraud = false;
                    eppicStepResultRecord.PhoneNumber = record.Phone_Number;
                    eppicStepResultRecord.AddressLine1 = record.AddressLine1;
                    eppicStepResultRecord.AddressLine2 = record.AddressLine2;
                    eppicStepResultRecord.City = record.City;
                    eppicStepResultRecord.State = record.State;
                    eppicStepResultRecord.Zip = record.ZipCode;
                    eppicStepResultRecord.Step1HospitalMatch = false;
                    eppicStepResultRecord.Step2GiactMatch = true;
                    eppicStepResultRecord.Step3PassedVerificationCheck = false;
                    eppicStepResultRecord.Step3aPassedOTPPhoneGiact = false;
                    eppicStepResultRecord.LastStepCompleted = "Step2";
                    eppicStepResultRecord.Status = "Eppic Record found match in Giact - Continue to Step 3";
                    _eppicstepresultsmanager.AddOrUpdateEppicStepResult(eppicStepResultRecord); // Add the result to the collection

                }
            }
            if (Globals.inputEppicRecordsNotInGiactDBNorHospitalsDB != null && (Globals.inputEppicRecordsNotInGiactDBNorHospitalsDB.Count() > 0))
            {
                // Let's add the items that do not have match Hospital DB to the Log Collection
                var eppicRecordsNotInGiactDB = Globals.inputEppicRecordsNotInGiactDBNorHospitalsDB.ToList();
                foreach (var record in eppicRecordsNotInGiactDB)
                {
                    stepLogger.AddItem(record, "Step 2 - Eppic Records Not in Giact DB", "Denied, marked as Fraud no need to process");
                    var eppicStepResultRecord = new EppicStepResults
                    {
                        LogID = _stepslogfile.FileName,
                        LogDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    };
                    eppicStepResultRecord.PersonID = record.PersonID;
                    eppicStepResultRecord.MarkedAsFraud = true;
                    eppicStepResultRecord.PhoneNumber = record.Phone_Number;
                    eppicStepResultRecord.AddressLine1 = record.AddressLine1;
                    eppicStepResultRecord.AddressLine2 = record.AddressLine2;
                    eppicStepResultRecord.City = record.City;
                    eppicStepResultRecord.State = record.State;
                    eppicStepResultRecord.Zip = record.ZipCode;
                    eppicStepResultRecord.Step1HospitalMatch = false;
                    eppicStepResultRecord.Step2GiactMatch = false;
                    eppicStepResultRecord.Step3PassedVerificationCheck = false;
                    eppicStepResultRecord.Step3aPassedOTPPhoneGiact = false;
                    eppicStepResultRecord.LastStepCompleted = "Step2";
                    eppicStepResultRecord.Status = "Eppic Record no match in Giact - Denied - Marked as Fraud no need to proceed";
                    _eppicstepresultsmanager.AddOrUpdateEppicStepResult(eppicStepResultRecord); // Add the result to the collection
                }
            }
            _jobstatus.Status = "Step 2 completed";
            Console.WriteLine(_jobstatus.Status);

            #endregion

            #region Step 3: Check Eppic Record Against Seibel CallNotesResult only if last step is set to pass
            // TBD.  Here I would like to add the additional JSON data from the SK logic to the data we are logging to the Collection
            // Need to look into how to add optional JSON data to the structure
            // Let's run the step 3 verification

            CallLogChecker callLogChecker = new CallLogChecker();
            

            // We need to loop through all the items that have a match in Giact DB for step 3 now
            // This logic could be moved into a function/method to simplify this section of code.
            if (Globals.inputEppicRecordsInGiactDBNotInHospitalsDB != null)
            {
                // Let's add the items that have a match in Hospital DB to the Log Collection
                foreach (var record in Globals.inputEppicRecordsInGiactDBNotInHospitalsDB)
                {
                    Console.WriteLine($"Doing Step 3 for PersonID: { record.PersonID }");

                    // get a ref to the sibeldataRecords first
                    //var siebeldataRecords = _datahelper.SiebelDataRecords;
                    var recordswithCallNotes = datahelper.SiebelDataParser.FindAllSiebelCallNotesByPersonIDLastFirst(record.PersonID ?? "");

                    var verificationsCompletedJson = await callLogChecker.CheckVerificationIntentAsync(_kernel, recordswithCallNotes?.FirstOrDefault()?.PersonID ?? "", recordswithCallNotes?.FirstOrDefault()?.CallNotes ?? "");
                    // JsonSerrializer can thow an exception so really need a try/catch
                    VerificationCompleted? verificationcompleted = JsonSerializer.Deserialize<VerificationCompleted>(verificationsCompletedJson);
                    
                    // Lets get the Fraud Concluson from AI we need to use a special POCO call to collect those details so we can log it to file.
                    var fraudConclusionJson = await callLogChecker.CheckFraudIntentAsync(_kernel, _chat, recordswithCallNotes?.FirstOrDefault()?.PersonID ?? "", recordswithCallNotes?.FirstOrDefault()?.CallNotes ?? "");
                    // JsonSerrializer can thow an exception so really need a try/catch
                    FraudConclusion? fraudConclusion = JsonSerializer.Deserialize<FraudConclusion>(fraudConclusionJson);

                    var verificationConclusionJson = await callLogChecker.CheckVerificationIntentAsync(_kernel, recordswithCallNotes?.FirstOrDefault()?.PersonID ?? "", recordswithCallNotes?.FirstOrDefault()?.CallNotes ?? "");
                    // JsonSerrializer can thow an exception so really need a try/catch
                    VerificationConclusion? verificationConclusion = JsonSerializer.Deserialize<VerificationConclusion>(verificationConclusionJson);

                    var actionConclusionJson = await callLogChecker.CheckActionConclusionAsync(_kernel, recordswithCallNotes?.FirstOrDefault()?.PersonID ?? "", recordswithCallNotes?.FirstOrDefault()?.CallNotes ?? "");
                    // JsonSerrializer can thow an exception so really need a try/catch
                    ActionConclusion? actionConclusion = JsonSerializer.Deserialize<ActionConclusion>(actionConclusionJson);

                    if (verificationcompleted?.VerificationsCompleted == "Yes")
                    {
                        // No need to move to step 3.a if verification has been completed
                        // TBD I would like to actually log the JSON of the verificationcompleted to the collection 
                        //if (record.PersonID == "6488958")
                        //{
                        //    Console.WriteLine("This is the record with the OTP in Siebel");
                        //}
                        // TBD needs to be looked at on Monday
                              
                        stepLogger.AddItem(record, "Step 3 - Eppic Record Passed Verification Check", "PASS Verification");
                        var eppicStepResultRecord = new EppicStepResults
                        {
                            LogID = _stepslogfile.FileName,
                            LogDate = DateTime.Today.ToString("yyyy-MM-dd"),
                        };
                        eppicStepResultRecord.PersonID = record.PersonID;
                        eppicStepResultRecord.MarkedAsFraud = false;
                        eppicStepResultRecord.PhoneNumber = record.Phone_Number;
                        eppicStepResultRecord.AddressLine1 = record.AddressLine1;
                        eppicStepResultRecord.AddressLine2 = record.AddressLine2;
                        eppicStepResultRecord.City = record.City;
                        eppicStepResultRecord.State = record.State;
                        eppicStepResultRecord.Zip = record.ZipCode;
                        eppicStepResultRecord.Step1HospitalMatch = false;
                        eppicStepResultRecord.Step2GiactMatch = true;
                        eppicStepResultRecord.Step3PassedVerificationCheck = true;
                        eppicStepResultRecord.Step3aPassedOTPPhoneGiact = false;
                        eppicStepResultRecord.LastStepCompleted = "Step3";
                        eppicStepResultRecord.Status = "Eppic Record Passed Verification Check ";
                        var fraudConclusionResultRecord = new FraudConclusion();
                        var actionConclusionResultRecord = new ActionConclusion();
                        var verificationConclusionResultRecord = new VerificationConclusion();
                        // Call to CSV Manager to log the data to the collection
                        fraudConclusionResultRecord.PersonID = record.PersonID;
                        fraudConclusionResultRecord.FraudConclusionNotes = fraudConclusion?.FraudConclusionNotes?.Replace(",",";");  
                        fraudConclusionResultRecord.FraudConclusionType = fraudConclusion?.FraudConclusionType?.Replace(",", ";");
                        fraudConclusionResultRecord.Recommendation = fraudConclusion?.Recommendation?.Replace(",", ";");
                        
                        verificationConclusionResultRecord.PersonID = record?.PersonID;
                        verificationConclusionResultRecord.ActivityRelatedTo = verificationConclusion?.ActivityRelatedTo;
                        verificationConclusionResultRecord.FormOfAuthentication = verificationConclusion?.FormOfAuthentication;
                        verificationConclusionResultRecord.PhoneNumber = verificationConclusion?.PhoneNumber;
                        verificationConclusionResultRecord.VerificationsCompleted = verificationConclusion?.VerificationsCompleted;

                        actionConclusionResultRecord.PersonID = record?.PersonID;
                        actionConclusionResultRecord.CallerAuthenticated = actionConclusion?.CallerAuthenticated;
                        actionConclusionResultRecord.FormOfAuthentication = actionConclusion?.FormOfAuthentication;
                        actionConclusionResultRecord.ThirdPartyInvolved = actionConclusion?.ThirdPartyInvolved;
                        actionConclusionResultRecord.WasCallTransferred = actionConclusion?.WasCallTransferred; 
                        actionConclusionResultRecord.PhoneUpdateFrom = actionConclusion?.PhoneUpdateFrom;
                        actionConclusionResultRecord.PhoneUpdateTo = actionConclusion?.PhoneUpdateTo;
                        actionConclusionResultRecord.PhoneChanged = actionConclusion?.PhoneChanged;
                        actionConclusionResultRecord.AddressUpdateFrom = actionConclusion?.AddressUpdateFrom;
                        actionConclusionResultRecord.AddressUpdateTo = actionConclusion?.AddressUpdateTo;
                        actionConclusionResultRecord.AddressChanged = actionConclusion?.AddressChanged;
                        // Add record to the collections
                        _eppicstepresultsmanager.AddOrUpdateEppicStepResult(eppicStepResultRecord); // Add the result to the collection
                        _fraudconclusionmanager.AddOrUpdateFraudConclusion(fraudConclusionResultRecord);
                        _verificationconclusionmanager.AddOrUpdateVerificationConclusion(verificationConclusionResultRecord);
                        _actionconclusionmanager.AddOrUpdateActionConclusion(actionConclusionResultRecord);

                        #region step 3a  we need to look closely at this 
                        // the above needs to include OTP as well; in the SIEBEL call notes list the form of authentication as "One Time Passcode",
                        // then we need to ensure that the phone # in the call notes matches the EPICC record phone # OR the phone number in GIACT
                        if (verificationcompleted?.FormOfAuthentication == "One Time Passcode")
                        {
                            bool step3aPass = false;
                            step3aPass = _datahelper.Step3a_Check(verificationcompleted.PhoneNumber ?? "", record);

                            // if the check doesn't pass, the request is determined to be fraud
                            if (!step3aPass)
                            {
                                // We need to call the AI for FraudConclusion one more time because in previous calls the AI does not know if the record passed the 3a check since
                                // we haven't got here yet.  We need to let the AI that this record has passed or failed Step3a.
                                var fraudConclusionJson3a = await callLogChecker.CheckFraudIntentAsync(_kernel, _chat, recordswithCallNotes?.FirstOrDefault()?.PersonID ?? "", recordswithCallNotes?.FirstOrDefault()?.CallNotes ?? "",true,false);
                                // JsonSerrializer can thow an exception so really need a try/catch
                                FraudConclusion? fraudConclusion3a = JsonSerializer.Deserialize<FraudConclusion>(fraudConclusionJson3a);


                                var fraudConclusionResultRecord3a = new FraudConclusion();
                                fraudConclusionResultRecord3a.PersonID = record.PersonID;
                                fraudConclusionResultRecord3a.FraudConclusionNotes = fraudConclusion3a?.FraudConclusionNotes?.Replace(",", ";");
                                fraudConclusionResultRecord3a.FraudConclusionType = fraudConclusion3a?.FraudConclusionType?.Replace(",", ";");
                                fraudConclusionResultRecord3a.Recommendation = fraudConclusion3a?.Recommendation?.Replace(",", ";");

                                stepLogger.AddItem(record, "Step 3a - OTP Pass ID Verification but phone number does not match the phone number in EPPIC or GIACT", "FAIL - fraudelant request");
                                eppicStepResultRecord.PersonID = record.PersonID;
                                eppicStepResultRecord.PhoneNumber = record.Phone_Number;
                                eppicStepResultRecord.AddressLine1 = record.AddressLine1;
                                eppicStepResultRecord.AddressLine2 = record.AddressLine2;
                                eppicStepResultRecord.City = record.City;
                                eppicStepResultRecord.State = record.State;
                                eppicStepResultRecord.Zip = record.ZipCode;
                                eppicStepResultRecord.Step1HospitalMatch = false;
                                eppicStepResultRecord.Step2GiactMatch = true;
                                eppicStepResultRecord.Step3PassedVerificationCheck = true;
                                eppicStepResultRecord.Step3aPassedOTPPhoneGiact = false;
                                eppicStepResultRecord.LastStepCompleted = "Step3a";
                                eppicStepResultRecord.Status = "Eppic Record Auth=OTP Check phone number for match in EPPIC or GIACT not found - Fail - Fraduelant Request ";
                               
                                // Add record to the collections
                                _eppicstepresultsmanager.AddOrUpdateEppicStepResult(eppicStepResultRecord);
                                _fraudconclusionmanager.AddOrUpdateFraudConclusion(fraudConclusionResultRecord3a);
                            }
                            else
                            {
                                stepLogger.AddItem(record, "Step 3a - OTP Pass ID Verification, phone number matches the phone number in EPPIC or GIACT", "PASS");
                                var fraudConclusionJson3a = await callLogChecker.CheckFraudIntentAsync(_kernel, _chat, recordswithCallNotes?.FirstOrDefault()?.PersonID ?? "", recordswithCallNotes?.FirstOrDefault()?.CallNotes ?? "", true, true);
                                FraudConclusion? fraudConclusion3a = JsonSerializer.Deserialize<FraudConclusion>(fraudConclusionJson3a);


                                var fraudConclusionResultRecord3a = new FraudConclusion();
                                fraudConclusionResultRecord3a.PersonID = record.PersonID;
                                fraudConclusionResultRecord3a.FraudConclusionNotes = fraudConclusion3a?.FraudConclusionNotes?.Replace(",", ";");
                                fraudConclusionResultRecord3a.FraudConclusionType = fraudConclusion3a?.FraudConclusionType?.Replace(",", ";");
                                fraudConclusionResultRecord3a.Recommendation = fraudConclusion3a?.Recommendation?.Replace(",", ";");
                                // Call to CSV Manager to log the data to the collection
                                eppicStepResultRecord.PersonID = record.PersonID;
                                eppicStepResultRecord.PhoneNumber = record.Phone_Number;
                                eppicStepResultRecord.AddressLine1 = record.AddressLine1;
                                eppicStepResultRecord.AddressLine2 = record.AddressLine2;
                                eppicStepResultRecord.City = record.City;
                                eppicStepResultRecord.State = record.State;
                                eppicStepResultRecord.Zip = record.ZipCode;
                                eppicStepResultRecord.Step1HospitalMatch = false;
                                eppicStepResultRecord.Step2GiactMatch = true;
                                eppicStepResultRecord.Step3PassedVerificationCheck = true;
                                eppicStepResultRecord.Step3aPassedOTPPhoneGiact = true;
                                eppicStepResultRecord.LastStepCompleted = "Step3a";
                                eppicStepResultRecord.Status = "Eppic Record Auth=OTP Check found phone number match in EPPIC or GIACT - PASS ";

                                // Add record to the collections
                                _eppicstepresultsmanager.AddOrUpdateEppicStepResult(eppicStepResultRecord);
                                _fraudconclusionmanager.AddOrUpdateFraudConclusion(fraudConclusionResultRecord3a);
                            }
                        }
                        #endregion
                    }
                    else  // Verifications have not been completed
                    {
                        stepLogger.AddItem(record, "Step 3 - Verifications were not complete based on SIEBEL call notes.", "FAIL - fraudelant request");
                        // Call to CSV Manager to log the data to the collection
                        var eppicStepResultRecord = new EppicStepResults
                        {
                            LogID = _stepslogfile.FileName,
                            LogDate = DateTime.Today.ToString("yyyy-MM-dd"),
                        };
                        eppicStepResultRecord.PersonID = record.PersonID;
                        eppicStepResultRecord.MarkedAsFraud = true;
                        eppicStepResultRecord.PhoneNumber = record.Phone_Number;
                        eppicStepResultRecord.AddressLine1 = record.AddressLine1;
                        eppicStepResultRecord.AddressLine2 = record.AddressLine2;
                        eppicStepResultRecord.City = record.City;
                        eppicStepResultRecord.State = record.State;
                        eppicStepResultRecord.Zip = record.ZipCode;
                        eppicStepResultRecord.Step1HospitalMatch = false;
                        eppicStepResultRecord.Step2GiactMatch = true;
                        eppicStepResultRecord.Step3PassedVerificationCheck = false;
                        eppicStepResultRecord.Step3aPassedOTPPhoneGiact = false;
                        eppicStepResultRecord.LastStepCompleted = "Step3";
                        eppicStepResultRecord.Status = "Eppic Record Verifications were not complete based on SIEBEL call notes. FAIL - fraudelant request";

                        // Add record to the collections
                        _eppicstepresultsmanager.AddOrUpdateEppicStepResult(eppicStepResultRecord);
                    }
                }
            }
            _jobstatus.Status = "Step 3 - 3.a completed";
            Console.WriteLine(_jobstatus.Status);
            #endregion

            #region Step 3a: Check Eppic Record Against Seibel CallNotes OTP Check only if last step is set to pass
            // TBD.  Here I would like to add the additional JSON data from the SK logic to the data we are logging to the Collection
            // Need to look into how to add optional JSON data to the structure
            // In this step you can leverage basically that same type of logic that is in Step 3.
            _jobstatus.Status = "Processing Completed";
            Console.WriteLine(_jobstatus.Status);
            #endregion
            // return "Steps have ran";
            // write the file to disk
            await stepLogger.WriteLogCollectionToDisk(_stepslogfile.FileName ?? "", Constants.UseLocalFiles);
            // Let's create the CSV files.
            _eppicstepresultsmanager.WriteToCsv(Constants.UseLocalFiles);
            _fraudconclusionmanager.WriteToCsv(Constants.UseLocalFiles);
            _verificationconclusionmanager.WriteToCsv(Constants.UseLocalFiles);
            _actionconclusionmanager.WriteToCsv(Constants.UseLocalFiles);
        }

       

        #region GoogleSearch Not bing used
        //private bool PerformGoogleSearch(string address)
        //{
        //    // Implement Google Search API call to verify address
        //    // Replace with actual implementation
        //    using (var client = new HttpClient())
        //    {
        //        // var address = "1600 Amphitheatre Parkway, Mountain View, CA"; // Replace with actual address
        //        var apiKey = "YOUR_GOOGLE_API_KEY"; // Replace with actual API key
        //        var url = $"https://maps.googleapis.com/maps/api/place/textsearch/json?query={address}&key={apiKey}";
        //        var response = client.GetAsync(url).Result;
        //        var content = response.Content.ReadAsStringAsync().Result;
        //        var jsonData = JsonDocument.Parse(content);
        //        var results = jsonData.RootElement.GetProperty("results");
        //        foreach (var result in results.EnumerateObject())
        //        {
        //            var types = result.Value.GetProperty("types");
        //            if (types.EnumerateArray().Any(t => t.GetString() == "hospital" || t.GetString() == "health"))
        //            {
        //                return true;
        //            }
        //        }
        //    }
        //    return false;
        //}
        #endregion
    }
}

