using api_process_runner_api.Models.Reporting;
using api_process_runner_api.Util;
using Azure.Storage.Blobs;
using FileHelpers;

namespace api_process_runner_api.Helpers.Reporting
{
    public class ActionConclusionManager
    {
        private List<ActionConclusion> _actionConclusionResults = new List<ActionConclusion>();

        public List<ActionConclusion> ActionConclusionResults { get { return _actionConclusionResults; } }
        private bool _headercreated=false;

        // Method to add or update an item
        public void AddOrUpdateActionConclusion(ActionConclusion newItem)
        {
            //Important Note: Add new item as there can be multiple items ins SEIBEL call notes so we need the results for each item.


           _actionConclusionResults.Add(newItem);
           var existingItem = _actionConclusionResults.FirstOrDefault(item => item.PersonID == newItem.PersonID);

            if (existingItem == null)
            {
                // Add new item
                _actionConclusionResults.Add(newItem);
            }
            else
            {
                // Update existing item
                existingItem.CallerAuthenticated = newItem.CallerAuthenticated;
                existingItem.FormOfAuthentication = newItem.FormOfAuthentication;
                existingItem.ThirdPartyInvolved = newItem.ThirdPartyInvolved;
                existingItem.WasCallTransferred = newItem.WasCallTransferred;
                existingItem.PhoneUpdateFrom = newItem.PhoneUpdateFrom;
                existingItem.PhoneUpdateTo = newItem.PhoneUpdateTo;
                existingItem.PhoneChanged = newItem.PhoneChanged;
                existingItem.AddressChanged = newItem.AddressChanged;
                existingItem.AddressUpdateFrom = newItem.AddressUpdateFrom;
                existingItem.AddressUpdateTo = newItem.AddressUpdateTo;
            }
        }

        public void WriteToCsv(bool useLocalFiles)
        {
            if (useLocalFiles)
            {
                WriteToLocalCsv();
            }
            else
            {
                WriteToAzureBlob();
            }
        }

        private void WriteToLocalCsv()
        {
            string csvFilePath = $@"{Constants.LocalFilePath}\CSVResults\ActionConclusionResults_{DateTime.Now:yyyyMMdd}_{Guid.NewGuid().ToString().Substring(0, 8)}.csv";

            var fileEngine = new FileHelperEngine<ActionConclusion>();
            fileEngine.HeaderText = fileEngine.GetFileHeader();
            fileEngine.WriteFile(csvFilePath, _actionConclusionResults);


            //using (StreamWriter writer = new StreamWriter(csvFilePath))
            //{
            //    if (!_headercreated)
            //    {
            //        writer.WriteLine($"PersonID,CallerAuthenticated,FormOfAuthentication,ThirdPartyInvolved,WasCallTransferred,PhoneUpdateFrom,PhoneUpdateTo,PhoneChanged,AddressChanged,AddressUpdateFrom,AddressUpdateTo");
            //        _headercreated = true;
            //    }
            //    foreach (var item in _actionConclusionResults)
            //    {
            //        writer.WriteLine($"{item.PersonID},{item.CallerAuthenticated},{item.FormOfAuthentication},{item.ThirdPartyInvolved},{item.WasCallTransferred},{item.PhoneUpdateFrom},{item.PhoneUpdateTo},{item.PhoneChanged},{item.AddressChanged},{item.AddressUpdateFrom},{item.AddressUpdateTo}");
            //    }
            //}
        }

        private void WriteToAzureBlob()
        {
            var _blobConnection = Helper.GetEnvironmentVariable("BlobConnection");
            var _blobHelper = new BlobHelper()
            {
                Container = "CSVResults",
                ConnectionString = _blobConnection
            };

            string connectionString = _blobConnection;
            string containerName = "CVSResults";

            BlobServiceClient blobServiceClient = new BlobServiceClient(_blobConnection);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            containerClient.CreateIfNotExists();

            string fileName = $"ActionConclusionResults_{DateTime.Now:yyyyMMdd}_{Guid.NewGuid().ToString().Substring(0, 8)}.csv";

            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            var fileEngine = new FileHelperEngine<ActionConclusion>();
            fileEngine.HeaderText = fileEngine.GetFileHeader();

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(memoryStream))
                {
                    fileEngine.WriteStream(writer, _actionConclusionResults);
                    //if (!_headercreated)
                    //{
                    //    writer.WriteLine($"PersonID,CallerAuthenticated,FormOfAuthentication,ThirdPartyInvolved,WasCallTransferred,PhoneUpdateFrom,PhoneUpdateTo,PhoneChanged,AddressChanged,AddressUpdateFrom,AddressUpdateTo");
                    //    _headercreated = true;
                    //}
                    //foreach (var item in _actionConclusionResults)
                    //{
                    //    writer.WriteLine($"{item.PersonID},{item.CallerAuthenticated},{item.FormOfAuthentication},{item.ThirdPartyInvolved},{item.WasCallTransferred},{item.PhoneUpdateFrom},{item.PhoneUpdateTo},{item.PhoneChanged},{item.AddressChanged},{item.AddressUpdateFrom},{item.AddressUpdateTo}");
                    //}

                    writer.Flush();
                    memoryStream.Position = 0;
                    blobClient.Upload(memoryStream);
                }
            }
        }
    }

}
