using api_process_runner_api.Helpers.Parsers;
using api_process_runner_api.Models;
using api_process_runner_api.Util;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace api_process_runner_api.Helpers
{
    static class Globals
    {
        public static List<HospitalShelterRecords>? hospitalRecords;
        public static List<SiebelRecords>? siebelRecords;
        public static List<EppicRecords>? eppicRecords;
        public static List<GiactRecords>? giactRecords;
        public static IEnumerable<EppicRecords>? inputEppicRecordsInHospitalDB;
        public static IEnumerable<EppicRecords>? inputEppicRecordsNotInHospitalDB;
        public static IEnumerable<EppicRecords>? inputEppicRecordsInGiactDBNotInHospitalsDB;
        public static IEnumerable<EppicRecords>? inputEppicRecordsNotInGiactDBNorHospitalsDB;
    }

    internal class DataHelper
    {
        private BlobHelper _blobHelper;
        private Kernel _kernel;
        private IChatCompletionService _chat;
        private bool _usingLocalFiles;
        private UploadedFilesRequest _uploadedFilesRequest;
        private SiebelDataParser _siebelDataParser;
        private EppicDataParser _eppicDataParser;
        private GiactDataParser _giactDataParser;
        private HospitalShelterDataParser _hospitalShelterDataParser;
        private List<HospitalShelterRecords>? _hospitaldataRecords;
        private List<EppicRecords>? _eppicdataRecords;
        private List<SiebelRecords>? _siebeldataRecords;
        private List<GiactRecords>? _giactdataRecords;

        public SiebelDataParser SiebelDataParser        {
            get { return _siebelDataParser; }
        }
        public EppicDataParser EppicDataParser
        {
            get { return _eppicDataParser; }
        }
        public GiactDataParser GiactDataParser
        {
            get { return _giactDataParser; }
        }
        public HospitalShelterDataParser HospitalShelterDataParser
        {
            get { return _hospitalShelterDataParser; }
        }

        public List<HospitalShelterRecords>? HospitalDataRecords
        {
            get { return _hospitaldataRecords; }
        }
        public List<EppicRecords>? EppicDataRecords
        {
            get { return _eppicdataRecords; }
        }
        public List<SiebelRecords>? SiebelDataRecords
        {
            get { return _siebeldataRecords; }
        }
        public List<GiactRecords>? GiactDataRecords
        {
            get { return _giactdataRecords; }
        }

        public DataHelper(UploadedFilesRequest uploadedFilesRequest,string blobconnectionstring, bool usingLocalFiles, Kernel kernel, IChatCompletionService chat) 
        {
            _usingLocalFiles = usingLocalFiles; 
            _kernel = kernel;
            _chat = chat;
            _uploadedFilesRequest = uploadedFilesRequest;
            _blobHelper = new BlobHelper()
            {
                Container = "eppic",
                ConnectionString = blobconnectionstring
            };
            _siebelDataParser = new SiebelDataParser();
            _eppicDataParser = new EppicDataParser();
            _giactDataParser = new GiactDataParser();
            _hospitalShelterDataParser = new HospitalShelterDataParser();
        }


        public async Task<string> Intialize()
        {
            var result = "Blank";
            if (_usingLocalFiles)
            {
                try
                {
                    using (StreamReader stream = File.OpenText($@"{Constants.LocalFilePath}\Siebel\{_uploadedFilesRequest.SiebelFilename}"))
                    {
                        _siebelDataParser.LoadData(stream);
                    }
                    using (StreamReader stream = File.OpenText($@"{Constants.LocalFilePath}\Eppic\{_uploadedFilesRequest.EppicFilename}"))
                    {
                        _eppicDataParser.LoadData(stream);
                    }
                    using (StreamReader stream = File.OpenText($@"{Constants.LocalFilePath}\Giact\{_uploadedFilesRequest.GiactFilename}"))
                    {
                        _giactDataParser.LoadData(stream);
                    }
                    using (StreamReader stream = File.OpenText($@"{Constants.LocalFilePath}\Hospitals\{_uploadedFilesRequest.AddressFilename}"))
                    {
                        _hospitalShelterDataParser.LoadData(stream);
                    }
                }
                catch (Exception e) 
                { 
                    Console.WriteLine(e.ToString());
                    return result = "Failed to load stream";
                }

            }
            else // Using Azure Storage
            {
                try
                {
                    // Use below to load from Azure Blob Storage note how this uses the Blob Helper
                    _blobHelper.Container = "hospitals";
                    using (StreamReader stream = await _blobHelper.GetStreamReaderFromBlob(_uploadedFilesRequest.AddressFilename ?? String.Empty))
                    {
                        _hospitalShelterDataParser.LoadData(stream);
                    }
                    _blobHelper.Container = "siebel";
                    using (StreamReader stream = await _blobHelper.GetStreamReaderFromBlob(_uploadedFilesRequest.SiebelFilename ?? String.Empty))
                    {
                        _siebelDataParser.LoadData(stream);
                    }
                    _blobHelper.Container = "eppic";
                    using (StreamReader stream = await _blobHelper.GetStreamReaderFromBlob(_uploadedFilesRequest.EppicFilename ?? String.Empty))
                    {
                        _eppicDataParser.LoadData(stream);
                    }
                    _blobHelper.Container = "giact";
                    using (StreamReader stream = await _blobHelper.GetStreamReaderFromBlob(_uploadedFilesRequest.GiactFilename ?? String.Empty))
                    {
                        _giactDataParser.LoadData(stream);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    return result = "Failed to load stream";
                }
            }

            _hospitaldataRecords = _hospitalShelterDataParser.ParseCsv();
            _eppicdataRecords = _eppicDataParser.ParseCsv();
            _siebeldataRecords = _siebelDataParser.ParseCsv();
            _giactdataRecords = _giactDataParser.ParseCsv();
            Globals.hospitalRecords = _hospitaldataRecords;
            Globals.eppicRecords = _eppicdataRecords;
            Globals.siebelRecords = _siebeldataRecords;
            Globals.giactRecords = _giactdataRecords;
            Step1_BuildEppicListInHospitalsDB();
            Step1_BuildEppicListNotInHospitalsDB();
            Step2_BuildEppicListWithMatchesInGiactNotInHospitalsDB();
            Step2_BuildEppicListWithMatchesNotInGiactNorHospitals();
            result = "Success";

            return result;
        }

        public void Step1_BuildEppicListInHospitalsDB()
        {
            // Builds a List of Eppic records that have a match in Hospital DB
            Console.WriteLine($"Total eppicRecords: {Globals.eppicRecords?.Count}");
            if (Globals.hospitalRecords != null)
            {
                Globals.inputEppicRecordsInHospitalDB =
                    from e in Globals.eppicRecords
                    from a in Globals.hospitalRecords
                    where string.Compare(e?.AddressLine1?.Trim(), a?.AddressLine1?.Trim(), ignoreCase: true) == 0
                    where string.Compare(e?.AddressLine2?.Trim(), a?.AddressLine2?.Trim(), ignoreCase: true) == 0
                    where string.Compare(e?.City?.Trim(), a?.City?.Trim(), ignoreCase: true) == 0
                    where string.Compare(e?.State?.Trim(), a?.State?.Trim(), ignoreCase: true) == 0
                    select e;
                Console.WriteLine($"Total inputEppicRecordsInHospitalDB: {Globals.inputEppicRecordsInHospitalDB.Count()}");
            }
            else
            {
                throw new NullReferenceException();
            }
        }

        public void Step1_BuildEppicListNotInHospitalsDB()
        {
            Console.WriteLine($"Total eppicRecords: {Globals.eppicRecords?.Count}");

            // Check if Globals.hospitalRecords is not null
            if (Globals.inputEppicRecordsInHospitalDB != null && Globals.eppicRecords != null)
            {
                // Find Eppic records that do not match in the hospitalRecords
                Globals.inputEppicRecordsNotInHospitalDB =
                    Globals.eppicRecords.Except(Globals.inputEppicRecordsInHospitalDB);

                Console.WriteLine($"Total inputEppicRecordsNotInHospitalDB: {Globals.inputEppicRecordsNotInHospitalDB.Count()}");
            }
            else
            {
                throw new NullReferenceException();
            }
        }

        public void Step2_BuildEppicListWithMatchesInGiactNotInHospitalsDB()
        {
            Console.WriteLine($"Total inputEppicRecordsNotInHospitalDB: {Globals.inputEppicRecordsNotInHospitalDB?.Count()}");
            if (Globals.giactRecords != null && Globals.inputEppicRecordsNotInHospitalDB != null)
            {

                Globals.inputEppicRecordsInGiactDBNotInHospitalsDB =
                    (from e in Globals.inputEppicRecordsNotInHospitalDB
                     join a in Globals.giactRecords on e.PersonID equals a.UniqueID
                     where string.Compare(e.AddressLine1?.Trim(), a.AddressCurrentPast?.Trim(), ignoreCase: true) == 0
                     where string.Compare(e.City?.Trim(), a.CityCurrentPast?.Trim(), ignoreCase: true) == 0
                     where string.Compare(e.State?.Trim(), a.StateCurrentPast?.Trim(), ignoreCase: true) == 0
                     where string.Compare(e.ZipCode?.Trim(), a.ZipCodeCurrentPast?.Trim(), ignoreCase: true) == 0
                     select e).Distinct();

                Console.WriteLine($"Total inputEppicRecordsInGiactDBNotInHospitalsDB: {Globals.inputEppicRecordsInGiactDBNotInHospitalsDB.Count()}");
            }
            else
            {
                throw new NullReferenceException();
            }
        }

        public void Step2_BuildEppicListWithMatchesNotInGiactNorHospitals()
        {
            Console.WriteLine($"Total inputEppicRecordsNotInHospitalDB: {Globals.inputEppicRecordsNotInHospitalDB?.Count()}");
            if (Globals.inputEppicRecordsInGiactDBNotInHospitalsDB != null && Globals.inputEppicRecordsNotInHospitalDB != null)
            {
                Globals.inputEppicRecordsNotInGiactDBNorHospitalsDB =
                   Globals.inputEppicRecordsNotInHospitalDB.Except(Globals.inputEppicRecordsInGiactDBNotInHospitalsDB);
                
                Console.WriteLine($"Total inputEppicRecordsNotInGiactDBNorHospitalsDB: {Globals.inputEppicRecordsNotInGiactDBNorHospitalsDB.Count()}");
            }
            else
            {
                throw new NullReferenceException();
            }
        }

        /// <summary>
        /// Processes step 3a which is only run when an OTP pass is identified in the SIEBEL call notes. If so, in
        /// order for this step to pass, the request phone number in the call notes must match the phone number in
        /// the EPPIC system OR the GIACT system.
        /// </summary>
        /// <param name="callNotesPhoneNumber">The phone number from the SIEBEL call notes</param>
        /// <param name="eppicRecord">The EPPIC record corresponding to this person ID</param>
        /// <returns>True if this check passes</returns>
        public bool Step3a_Check(string callNotesPhoneNumber, EppicRecords eppicRecord)
        {
            bool pass = false;

            // we then need to verify that the phone number matches the EPICC record or GIACT record phone number; check EPICC first
            // Depending on resolution to comment on user story 801 and task 923, this if statement may be removed and only check GIACT
            if (callNotesPhoneNumber.Equals(eppicRecord.Phone_Number))
            {
                Console.WriteLine("Phone number in call notes matches phone number in EPICC! Check passed");
                pass = true;
            }
            // pull all GIACT records for this person to check if the phone number matches any of the phone numbers in the GIACT records matches
            else
            {
                
                List<GiactRecords>? giactRecordsForId = _giactDataParser.FindGiactRecordsByUniqueID(eppicRecord.PersonID ?? "");
                if (giactRecordsForId != null)
                {
                    GiactRecords matchingGiactRecord = giactRecordsForId.FirstOrDefault(record => record.PhoneNumber == callNotesPhoneNumber.ToString()) ?? new GiactRecords();

                    if (matchingGiactRecord != null)
                    {
                        Console.WriteLine("Phone number in call notes matches phone number in GIACT! Check passed");
                        pass = true;
                    }
                }
            }

            return pass;
        }

        public void ClearCollections()
        {
            Globals.eppicRecords?.Clear();
            Globals.hospitalRecords?.Clear();
            Globals.siebelRecords?.Clear();
            Globals.giactRecords?.Clear();
            if (Globals.inputEppicRecordsNotInHospitalDB != null && Globals.inputEppicRecordsInHospitalDB != null)
                {
                    List<EppicRecords> eppicListNotIn = Globals.inputEppicRecordsNotInHospitalDB.ToList();
                    eppicListNotIn.Clear();
                    List<EppicRecords> eppicListIn = Globals.inputEppicRecordsInHospitalDB.ToList();
                    eppicListIn.Clear();
                }
            HospitalDataRecords?.Clear();
            EppicDataRecords?.Clear();
            SiebelDataRecords?.Clear();
            GiactDataRecords?.Clear();
        }

        public async Task<string> RunTestsOnData(DataHelper dataHelper, bool v)
        {
            try
            {
                var hospitaldataRecords = dataHelper.HospitalDataRecords;
                Console.WriteLine("Ready to Go, let's search for a HospitalByFullAddress. Press Enter!");
                var recordswithFullAddress = dataHelper.HospitalShelterDataParser.FindHospitalByFullAddress("799 47dH bd", "", "SAN DIEGO", "CA", hospitaldataRecords ?? new List<HospitalShelterRecords>());
                //var recordswithFullAddress = dataHelper.HospitalShelterDataParser.FindHospitalByFullAddress("3409 pLlzKEzBEfqD Dq", "", "KILLEEN", "TX", hospitaldataRecords ?? new List<HospitalShelterRecords>());
                Console.WriteLine($@"Hospital Found: {recordswithFullAddress?.AddressLine1}");
                Console.WriteLine("Let's print out all the Hospital Records, press  Enter!");
                dataHelper.HospitalShelterDataParser.PrintHospitalRecords(hospitaldataRecords ?? new List<HospitalShelterRecords>());
                Console.WriteLine();

                var eppicdataRecords = dataHelper.EppicDataRecords;
                Console.WriteLine("Ready to Go, let's search for a Eppic PersonID. Press Enter!");
                //var recordMatchedPersonID = dataHelper.EppicDataParser.FindEppicPersonID("5094334", eppicdataRecords ?? new List<EppicRecords>());
                var recordMatchedPersonID = dataHelper.EppicDataParser.FindEppicPersonID("6488958", eppicdataRecords ?? new List<EppicRecords>());
                Console.WriteLine($@"PersonID Found: {recordMatchedPersonID?.PersonID}");
                Console.WriteLine("Let's print out all the Eppic Records, press  Enter!");
                dataHelper.EppicDataParser.PrintEppicRecords(eppicdataRecords ?? new List<EppicRecords>());
                Console.WriteLine();

                // Let's test the Step 3 verification
                CallLogChecker callLogChecker = new CallLogChecker();
                // get a ref to the sibeldataRecords first
                var siebeldataRecords = dataHelper.SiebelDataRecords;
                // get a record with callnotes
                //var recordswithCallNotes = dataHelper.SiebelDataParser.FindAllSiebelCallNotesByPersonID("5094334");
                var recordswithCallNotes = dataHelper.SiebelDataParser.FindAllSiebelCallNotesByPersonIDLastFirst("6488958");
                var verificationsCompletedResult1 = await callLogChecker.CheckFraudIntentAsync(_kernel, _chat, recordswithCallNotes?.FirstOrDefault()?.PersonID ?? "", recordswithCallNotes?.FirstOrDefault()?.CallNotes ?? "");

                var verificationsCompletedResult2 = await callLogChecker.CheckVerificationIntentAsync(_kernel, recordswithCallNotes?.FirstOrDefault()?.PersonID ?? "", recordswithCallNotes?.FirstOrDefault()?.CallNotes ?? "");
                Console.WriteLine(verificationsCompletedResult2);

                VerificationCompleted? verificationcompleted = JsonSerializer.Deserialize<VerificationCompleted>(verificationsCompletedResult2);

                // Step 3a - we need to verify, but it seems that this check is ONLY done if the SIEBEL call notes list the form of authentication as "One Time Passcode".
                //           If so, then we need to ensure that the phone # in the call notes matches the EPICC record phone # OR the phone number in GIACT
                bool step3aPass = false;
                // if we have a one time passcode, we then need to verify that the phone number matches the EPICC record or GIACT record phone number
                // otherwise, we skip step 3
                if (verificationcompleted?.FormOfAuthentication != null)
                {
                    if (verificationcompleted.FormOfAuthentication.Equals("one time passcode"))
                    {
                        step3aPass = Step3a_Check(verificationcompleted.PhoneNumber ?? "", recordMatchedPersonID ?? new EppicRecords());

                        // if the check doesn't pass, the request is determeind to be fraud. We can extrapolate this checkPassed
                        // and add it to the steplogger below
                        if (!step3aPass)
                        {
                            Console.WriteLine("Fraud detected!");
                        }
                    }
                }
                // Test the Step Logger  Let's Add the Eppic Items that Failed Step 1
                // There are not Eppic Items that have a Match in Hospital DB so no need to test
                // So there are no records that match Hospital Address so nothing will be added!
                StepLogger stepLogger = new StepLogger();
                if (Globals.inputEppicRecordsNotInHospitalDB != null)
                {
                    var eppicRecordsNotInHospitalDB = Globals.inputEppicRecordsNotInHospitalDB.ToList();
                    
                    foreach (var record in eppicRecordsNotInHospitalDB)
                    {
                        // TBD Needs to be debugged it's printing out like 20 items when there are only 5

                        stepLogger.AddItem(record, "Step 1 - Eppic Records Not in Hospital List", "FAIL Go to next Step");
                    }
                }
                // if step3a failed, we can log the issue here
                if (!step3aPass)
                {
                    stepLogger.AddItem(recordMatchedPersonID ?? new EppicRecords(), "Step 3a - OTP Pass identified but phone number does not match the phone number in EPPIC or GIACT", "FAIL - fraudelant request");
                }

                // stepLogger.TestAddItems();  // This will add 10 test items to the Logger Collection
                stepLogger.PrintItems();
                return "All Tests have ran";
            }
            catch (Exception ex)
            {
                var errormsg = $"Issue running the tests. Exception: {ex.ToString()}";
                Console.WriteLine(errormsg);
                return errormsg;
            }
        }
    }
}
