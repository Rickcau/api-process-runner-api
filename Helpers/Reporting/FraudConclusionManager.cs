using api_process_runner_api.Models.Reporting;
using api_process_runner_api.Util;
using Azure.Storage.Blobs;
using FileHelpers;

namespace api_process_runner_api.Helpers.Reporting
{
    public class FraudConclusionManager
    {
        private List<FraudConclusion> _fraudConclusionResults = new List<FraudConclusion>();
        public List<FraudConclusion> FraudConclusoinResults { get { return _fraudConclusionResults; } }
        private bool _headercreated=false;
        // Method to add or update an item
        public void AddOrUpdateFraudConclusion(FraudConclusion newItem)
        {   
            // Important Note: Add new item as there can be multiple items ins SEIBEL call notes so we need the results for each item.
            _fraudConclusionResults.Add(newItem);
            //var existingItem = _fraudConclusionResults.FirstOrDefault(item => item.PersonID == newItem.PersonID);

            //if (existingItem == null)
            //{
            //    // Add new item
            //    _fraudConclusionResults.Add(newItem);
            //}
            //else
            //{
            //    // Update existing item
            //    existingItem.FraudConclusionNote = newItem.FraudConclusionNote;
            //    existingItem.FraudConclusionType = newItem.FraudConclusionType;
            //    existingItem.Recommendation = newItem.Recommendation;
            //}
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
            string csvFilePath = $@"{Constants.LocalFilePath}\CSVResults\FraudConclusionResults_{DateTime.Now:yyyyMMdd}_{Guid.NewGuid().ToString().Substring(0, 8)}.csv";

            var fileEngine = new FileHelperEngine<FraudConclusion>();
            fileEngine.HeaderText = fileEngine.GetFileHeader();
            fileEngine.WriteFile(csvFilePath, _fraudConclusionResults);

            //using (StreamWriter writer = new StreamWriter(csvFilePath))
            //{
            //    if (!_headercreated)
            //    {
            //        writer.WriteLine($"PersonID,FraudConclusionNote,FraudConclusionType,Recommendation");
            //        _headercreated = true;
            //    }
            //    foreach (var item in _fraudConclusionResults)
            //    {
            //        writer.WriteLine($"{item.PersonID},{item.FraudConclusionNote},{item.FraudConclusionType},{item.Recommendation}");
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

            string fileName = $"FraudConclusionResults_{DateTime.Now:yyyyMMdd}_{Guid.NewGuid().ToString().Substring(0, 8)}.csv";

            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            var fileEngine = new FileHelperEngine<FraudConclusion>();
            fileEngine.HeaderText = fileEngine.GetFileHeader();

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(memoryStream))
                {
                    fileEngine.WriteStream(writer, _fraudConclusionResults);
                    //if (!_headercreated)
                    //{
                    //    writer.WriteLine($"PersonID,FraudConclusionNote,FraudConclusionType,Recommendation");
                    //    _headercreated = true;
                    //}
                    //foreach (var item in _fraudConclusionResults)
                    //{
                    //    writer.WriteLine($"{item.PersonID},{item.FraudConclusionNote},{item.FraudConclusionType},{item.Recommendation}");
                    //}

                    writer.Flush();
                    memoryStream.Position = 0;
                    blobClient.Upload(memoryStream);
                }
            }
        }
    }

}
