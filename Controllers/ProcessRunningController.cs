using Microsoft.AspNetCore.Mvc;
using api_process_runner_api.Models;
using api_process_runner_api.Helpers;
using Microsoft.SemanticKernel;
using api_process_runner_api.Util;
using Microsoft.Extensions.Configuration.UserSecrets;
using Azure;
using api_process_runner_api.Tests;

namespace api_process_runner_api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProcessRunnerController : ControllerBase
    {
        private readonly ILogger<ProcessRunnerController> _logger;
        private readonly Kernel _kernel;
        private readonly JobStatus _jobstatus;
        private readonly UploadedFilesRequest? _filesrequest;
        private readonly StepsLogFile _stepslogfile;

        private readonly bool _debugging = true;
        public ProcessRunnerController(ILogger<ProcessRunnerController> logger, Kernel kernel, UploadedFilesRequest uploadedfilesrequest, StepsLogFile stepslogfile, JobStatus jobstatus)
        {
            _logger = logger;
            _kernel = kernel;
            _jobstatus = jobstatus;
            _filesrequest = uploadedfilesrequest;
            _stepslogfile = stepslogfile;
        }


        [HttpGet("StartProcessing")] // Pass the 4 Files that were uploaded
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> ProcessRunner()
        {
            // we need to clear out all the collections after each completion otherwise the collection will get larger as long as the process lives.
            // TBD
            try
            {
                var _blobConnection = Helper.GetEnvironmentVariable("BlobConnection");
                // Writing to CSV Tested 5/5 RDC
                //Tester.TestCSVManagers(_stepslogfile.FileName ?? "");

                DataHelper dataHelper;  // TBD I think there is an issue with Step2 building of the IEnumerable it's not finished Step 3
                
                if (_filesrequest != null) {
                    dataHelper = new DataHelper(_filesrequest , _blobConnection, Constants.UseLocalFiles, _kernel);  // Change Constants.UseLocalFiles to false to use Azure Blob Storage
                    dataHelper?.ClearCollections(); // Clear All collections just to make sure data does not linger across runs.
                }
                else
                {
                    return BadRequest("Issue with File Detals!");
                }
                if (dataHelper != null)
                {
                    var result = await dataHelper.Intialize();
                    if (result == "Failed to load stream")
                    {
                        return StatusCode(500, "An internal server error occurred trying to intialize the data!");
                    }
                    // Comment out if you don't to test data and logic.  Update the RunTestOnData if you want to add additional logic.
                    // Technically, all this should be done from xUnit/Mock but I don't have time for that.
                    //response = await dataHelper.RunTestsOnData(dataHelper);
                    //Console.WriteLine(response);
                    
                    ProcessRunnerSteps processRunner = new ProcessRunnerSteps(dataHelper, _kernel, _jobstatus, _stepslogfile);
                    _ = processRunner.RunSteps();

                    // Comment out the two lines about to not run the tests.
                    // This is where we beed ti cakk ProcessRunnerSteps
                }
                return Ok("Long-running process started in the background.");
                // return new OkObjectResult(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return BadRequest($"An error occured: {ex.Message}");
            }
        }

        [HttpPost("SendFileDetails")] // Pass the 4 Files that were uploaded
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public ActionResult SendFileDetails([FromBody] UploadedFilesRequest filesrequest)
        {
            if (filesrequest != null && _filesrequest != null && filesrequest.SiebelFilename !="string")
            { // This only gets executed if the request body has the file details, otherwise it uses the defaults from the DI in the program.cs
 
                _filesrequest.EppicFilename = filesrequest.EppicFilename;
                _filesrequest.SiebelFilename = filesrequest.SiebelFilename;
                _filesrequest.GiactFilename = filesrequest.GiactFilename;
                _filesrequest.AddressFilename = filesrequest.AddressFilename;
            } // Otherwise we assume the local files in the program.cs should be used.
            
            try
            {
                if (filesrequest == null)
                {
                    return BadRequest("Please pass a valid FileRequest in the Request Body!");
                }
                _stepslogfile.FileName = LogFileGenerator.GenerateLogFileName();  // return generated filename to client.  Shared across async methods
                return new OkObjectResult(_stepslogfile.FileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return BadRequest($"An error occured: {ex.Message}");
            }
        }

        [HttpGet("CheckJobStatus")] // Pass the 4 Files that were uploaded
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public ActionResult CheckJobStatus()
        {
          return Ok(_jobstatus);
        }
    }
}
