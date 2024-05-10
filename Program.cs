using api_process_runner_api.Helpers;
using api_process_runner_api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
string _apiDeploymentName = Helper.GetEnvironmentVariable("ApiDeploymentName");
string _apiEndpoint = Helper.GetEnvironmentVariable("ApiEndpoint");
string _apiKey = Helper.GetEnvironmentVariable("ApiKey");

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add the Semantic Kernel as a transient service
builder.Services.AddTransient<Kernel>(s =>
{
    var builder = Kernel.CreateBuilder();
    builder.AddAzureOpenAIChatCompletion(
        _apiDeploymentName,
        _apiEndpoint,
        _apiKey
    );
    return builder.Build();
});

builder.Services.AddSingleton<IChatCompletionService>(sp =>
    sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

builder.Services.AddSingleton<UploadedFilesRequest>(provider =>
{
    return new UploadedFilesRequest
    {
        AddressFilename = "Hospital-Shelters.20231107.csv",
        EppicFilename = "EPPIC.20231107.CSV",
        GiactFilename = "GIACT202131107.CSV",
        SiebelFilename = "Siebel.20231107.CSV"
    };
});


builder.Services.AddSingleton<StepsLogFile>(provider =>
{
    return new StepsLogFile
    {
      FileName = LogFileGenerator.GenerateLogFileName()
    };
});

builder.Services.AddSingleton<JobStatus>(provider =>
{
    return new JobStatus
    {
        Status = "Not Started"
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
