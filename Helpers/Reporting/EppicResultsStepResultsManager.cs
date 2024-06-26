﻿using api_process_runner_api.Models.Reporting;
using api_process_runner_api.Util;
using System.Collections;
using System.Reflection.Metadata;
using Azure.Storage.Blobs;
using api_process_runner_api.Models;
using FileHelpers;
namespace api_process_runner_api.Helpers.Reporting
{
    public class EppicStepResultsManager
    {
        private List<EppicStepResults> _eppicStepsResults = new List<EppicStepResults>();

        public List<EppicStepResults> EppicStepResults { get { return _eppicStepsResults; } }
        private bool _headercreated = false;

        // Method to add a new item
        public void AddOrUpdateEppicStepResult(EppicStepResults newItem)
        {           
            // So for EPPIC Records there is only one record so when we make updates we need to check for the item and update it
            var existingItem = _eppicStepsResults.FirstOrDefault(item => item.PersonID == newItem.PersonID);

            if (existingItem == null)
            {
                // Add new item
                _eppicStepsResults.Add(newItem);
            }
            else
            {
                // Update existing item
                existingItem.LogDate = newItem.LogDate;
                existingItem.PersonID = newItem.PersonID;
                existingItem.MarkedAsFraud = newItem.MarkedAsFraud;
                existingItem.PhoneNumber = newItem.PhoneNumber;
                existingItem.AddressLine1 = newItem.AddressLine1;
                existingItem.AddressLine2 = newItem.AddressLine2;
                existingItem.City = newItem.City;
                existingItem.State = newItem.State;
                existingItem.Zip = newItem.Zip;
                existingItem.Step1HospitalMatch = newItem.Step1HospitalMatch;
                existingItem.Step2GiactMatch = newItem.Step2GiactMatch;
                existingItem.Step3PassedVerificationCheck = newItem.Step3PassedVerificationCheck;
                existingItem.Step3aPassedOTPPhoneGiact = newItem.Step3aPassedOTPPhoneGiact;
                existingItem.LastStepCompleted = newItem.LastStepCompleted;
                existingItem.Status = newItem.Status;
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
            string csvFilePath = $@"{Constants.LocalFilePath}\CSVResults\EppicStepResults_{DateTime.Now:yyyyMMdd}_{Guid.NewGuid().ToString().Substring(0, 8)}.csv";

            var engineEppic = new FileHelperEngine<EppicStepResults>();
            engineEppic.HeaderText = engineEppic.GetFileHeader();
            engineEppic.WriteFile(csvFilePath, _eppicStepsResults);
        }
        private void WriteToAzureBlob()
        {
            var _blobConnection = Helper.GetEnvironmentVariable("BlobConnection");

            string connectionString = _blobConnection;
            string containerName = "csvresults";

            BlobServiceClient blobServiceClient = new BlobServiceClient(_blobConnection);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            containerClient.CreateIfNotExists();

            string fileName = $"EppicStepsResults_{DateTime.Now:yyyyMMdd}_{Guid.NewGuid().ToString().Substring(0, 8)}.csv";

            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            var engineEppic = new FileHelperEngine<EppicStepResults>();
            engineEppic.HeaderText = engineEppic.GetFileHeader();

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(memoryStream))
                {
                    engineEppic.WriteStream(writer, _eppicStepsResults);

                    writer.Flush();
                    memoryStream.Position = 0;
                    blobClient.Upload(memoryStream);
                }
            }
        }
    }

}
