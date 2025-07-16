namespace Instrument.Scheduler.Cmr.Grpc.Service;

using System.Threading;
using System.Threading.Tasks;
using Instrument.Grpc;
using Instrument.Logger;
using Instrument.Scheduler.Cmr.Execution;
using Instrument.Scheduler.Cmr.Grpc.Message;
using Instrument.Scheduler.Cmr.Model;
using Instrument.Scheduler.Cmr.Parser;
using Instrument.Scheduler.Configuration;
using Microsoft.Extensions.Options;

public class CmrService : ICmrService
{
    private readonly IInstrumentLogger _logger;
    private readonly IRequestIdGenerator<Guid> _requestIdGenerator;
    private readonly ICmrFileParser _cmrFileParser;
    private readonly CmrConfiguration _cmrConfiguration;

    public CmrService(
        IRequestIdGenerator<Guid> requestIdGenerator,
        ICmrFileParser cmrFileParser,
        IInstrumentLoggerFactory loggerFactory,
        IOptionsMonitor<SchedulerConfiguration> schedulerConfiguration)
    {
        _logger = loggerFactory.ForContext<CmrService>();
        _requestIdGenerator = requestIdGenerator;
        _cmrConfiguration = schedulerConfiguration.Get(nameof(SchedulerConfiguration)).CmrConfiguration;
        _cmrFileParser = cmrFileParser;
        Directory.CreateDirectory(_cmrConfiguration.StoragePath);
    }

    public async Task<LoadCmrFileResponse> LoadFileAsync(LoadCmrFileRequest request, CancellationToken cancellationToken = default)
    {
        _logger.Trace("Enter");
        Guid requestId = default;

        try
        {
            requestId = _requestIdGenerator.GenerateNewId();

            _logger.Info($"Creating {request.FileName} for request {requestId}...");
            var fullPath = Path.Combine(_cmrConfiguration.StoragePath, request.FileName);

            if (File.Exists(fullPath))
                 File.Delete(fullPath);


            await File.WriteAllTextAsync(fullPath, request.CmrContent);
            _logger.Debug($"{fullPath} successfully created");
            return new LoadCmrFileResponse(requestId, request.FileName, []);
        }
        catch (Exception error)
        {
            _logger.Error("Failed to create script in storage.");
            return new LoadCmrFileResponse(requestId, "Unknown - Check Logs",
            [
                new (ErrorCode: CommonErrorCodes.InternalServerError,
                     Message: "Could not create the script in storage.",
                     Details: new string[] { error.Message })
            ]);
        }
        finally
        {
            _logger.Trace("Exit");
        }
    }

    public async Task<PrepareCmrFileResponse> PrepareCmrFileAsync(PrepareCmrFileRequest request, CancellationToken cancellationToken = default)
    {
        _logger.Trace("Enter");
        Guid requestId = default;

        try
        {
            requestId = _requestIdGenerator.GenerateNewId();

            _logger.Debug($"Preparing {request.FileName} for request {requestId}...");
            var fullPath = Path.Combine(_cmrConfiguration.StoragePath, request.FileName);

            // 1) Validate if file exists.
            // if the file does not exist in storage
            // then return a file not found response
            if (!File.Exists(fullPath))
            {
                return new PrepareCmrFileResponse(
                    requestId,
                    ExecutionPlan: null,
                    [
                           new (ErrorCode: CommonErrorCodes.InternalServerError,
                                Message: $"Could not find file with name {request.FileName} in local storage.",
                                Details: [fullPath])
                    ]);
            }

            // 2) Parse the file into memory
            var cmrFile =  await ParseCmrFileTest(filePath: fullPath);
            _logger.Debug($"Completed Parsing {request.FileName} for request {requestId}...");

            // 3) Get the execution plan
            var executionPlan = await GetExecutionPlan(cmrFile);

            _logger.Debug($"Completed Preparing {request.FileName} for request {requestId}...");

            return new PrepareCmrFileResponse(requestId,
                ExecutionPlan: executionPlan,
                []);
        }
        catch (Exception error)
        {
            _logger.Error($"Failed to prepare an execution plan for for request {requestId}...");
            return new PrepareCmrFileResponse(
                    requestId,
                    ExecutionPlan: null,
                    [
                           new (ErrorCode: CommonErrorCodes.InternalServerError,
                                Message: $"Could not file with name {request.FileName} in local storage.",
                                Details: [error.Message])
                    ]);
        }
        finally
        {
            _logger.Trace("Exit");
        }
    }

    public async Task<ExecuteCmrFileResponse> ExecuteCmrFileAsync(ExecuteCmrFileRequest request, CancellationToken cancellationToken = default)
    {
        _logger.Trace("Enter");
        Guid requestId = default;

        try
        {
            requestId = _requestIdGenerator.GenerateNewId();

            _logger.Debug($"Preparing {request.FileName} for request {requestId}...");
            var fullPath = Path.Combine(_cmrConfiguration.StoragePath, request.FileName);

            // 1) Validate if file exists.
            // if the file does not exist in storage
            // then return a file not found response
            if (!File.Exists(fullPath))
            {
                return new ExecuteCmrFileResponse(
                    requestId,
                    [
                           new (ErrorCode: CommonErrorCodes.InternalServerError,
                                Message: $"Could not find file with name {request.FileName} in local storage.",
                                Details: [fullPath])
                    ]);
            }

            // 2) Parse the file into memory
            var cmrFile = await ParseCmrFileTest(filePath: fullPath);
            _logger.Debug($"Completed Parsing {request.FileName} for request {requestId}...");

            // 3) Get the execution plan
            var executionPlan = await GetExecutionPlan(cmrFile);

            _logger.Debug($"Completed Preparing {request.FileName} for request {requestId}...");

            return new ExecuteCmrFileResponse(requestId,[]);
        }
        catch (Exception error)
        {
            _logger.Error($"Failed to execute cmr for request {requestId}...");
            return new ExecuteCmrFileResponse(
                    requestId,
                    [
                           new (ErrorCode: CommonErrorCodes.InternalServerError,
                                Message: $"Could not file with name {request.FileName} in local storage.",
                                Details: [error.Message])
                    ]);
        }
        finally
        {
            _logger.Trace("Exit");
        }
    }

    #region Private

    private Task<CmrFile> ParseCmrFileTest(string filePath)
    {
        // 1) Get the test orders from the CMR File
        var cmrFileInfo = new FileInfo(filePath);
        var cmrFile = new CmrTestOrderFile(cmrFileInfo);
        var parsedCmrFile = _cmrFileParser.Parse(cmrFile);
        return Task.FromResult(parsedCmrFile);
    }

    private static Task<CmrExecutionPlan> GetExecutionPlan(CmrFile cmrFile)
    {
        var executionPlanner = new CmrExecutionPlanner(cmrFile);
        return Task.FromResult(executionPlanner.GetExecutionPlan());
    }

    #endregion

}
