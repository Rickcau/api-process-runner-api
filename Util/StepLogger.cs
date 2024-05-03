using Azure.Storage.Blobs;
using FileHelpers;


namespace api_process_runner_api.Util;

public class ProcessStep
{
    public string? Title { get; set; }
    public string? Status { get; set; }
    public List<EppicRecord>? EppicRecords { get; set; }
}

public class EppicRecord
{
    public string? PersonID { get; set; }
    public string? Phone_Number { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
}

public class StepLogger
{ 
    public List<ProcessStep> processSteps = new List<ProcessStep>();


    public void AddItem(EppicRecord record, string stepnumber, string steptitle, string status)
    {
        ProcessStep step = new ProcessStep
        {
            Title = $"Step {i} - {steptitle}",
            Status = status,
            EppicRecords = new List<EppicRecord> {  new EppicRecord
                    {
                        PersonID? = record.PersonID,
                        Phone_Number = record.Phone_Number}",
                        AddressLine1 = record.AddressLine1,
                        AddressLine2 = record.AddressLine2,
                        City = record.City,
                        State = record.State,
                        ZipCode = record.ZipCode
                    }
        };
    }


    


    public async Task<bool> WriteToBlobAsync(Stream fileStream,string blobName)
    {
        // TBD: Try Catch 
        var blobServiceClient = new BlobServiceClient(ConnectionString); 
        var containerClient = blobServiceClient.GetBlobContainerClient(Container);  
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(fileStream, true);
        return true;

    }
}