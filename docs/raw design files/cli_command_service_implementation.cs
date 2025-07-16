// CmrCommandService.cs - Complete CLI command implementation
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Instrument.Scheduler.CLI
{
    public class CmrCommandService : ICmrCommandService
    {
        private readonly ICmrService _cmrService;
        private readonly IAssayManager _assayManager;
        private readonly ISchedulerStateManager _stateManager;
        private readonly IInventoryService _inventoryService;
        private readonly CmrCommandConfiguration _configuration;
        private readonly ILogger<CmrCommandService> _logger;
        private readonly object _executionLock = new object();
        
        private CmrExecutionContext _currentExecution;

        public CmrCommandService(
            ICmrService cmrService,
            IAssayManager assayManager,
            ISchedulerStateManager stateManager,
            IInventoryService inventoryService,
            IOptions<CmrCommandConfiguration> configuration,
            ILogger<CmrCommandService> logger)
        {
            _cmrService = cmrService ?? throw new ArgumentNullException(nameof(cmrService));
            _assayManager = assayManager ?? throw new ArgumentNullException(nameof(assayManager));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CommandResult> LoadFile(string sourceFilePath, string fileName = null)
        {
            try
            {
                _logger.LogInformation("Loading CMR file: {SourcePath}", sourceFilePath);

                // Validate input parameters
                if (string.IsNullOrWhiteSpace(sourceFilePath))
                {
                    return new CommandResult(false, "Source file path is required");
                }

                if (!File.Exists(sourceFilePath))
                {
                    return new CommandResult(false, $"Source file not found: {sourceFilePath}");
                }

                // Determine destination file name
                var destinationFileName = fileName ?? Path.GetFileName(sourceFilePath);
                if (string.IsNullOrWhiteSpace(destinationFileName))
                {
                    return new CommandResult(false, "Could not determine destination file name");
                }

                // Validate file extension
                if (!IsValidCmrFile(sourceFilePath))
                {
                    return new CommandResult(false, "Invalid CMR file format. Expected .csv or .xlsx file");
                }

                // Ensure CMR library directory exists
                var libraryPath = _configuration.CmrLibraryPath;
                if (!Directory.Exists(libraryPath))
                {
                    Directory.CreateDirectory(libraryPath);
                    _logger.LogInformation("Created CMR library directory: {LibraryPath}", libraryPath);
                }

                // Check if file already exists
                var destinationPath = Path.Combine(libraryPath, destinationFileName);
                if (File.Exists(destinationPath))
                {
                    if (!_configuration.AllowOverwrite)
                    {
                        return new CommandResult(false, $"File already exists: {destinationFileName}. Use --overwrite to replace");
                    }
                    
                    _logger.LogWarning("Overwriting existing file: {FileName}", destinationFileName);
                }

                // Copy file to library
                File.Copy(sourceFilePath, destinationPath, _configuration.AllowOverwrite);

                // Validate the copied file
                var validationResult = await ValidateCmrFile(destinationPath);
                if (!validationResult.IsSuccess)
                {
                    // Clean up invalid file
                    File.Delete(destinationPath);
                    return validationResult;
                }

                _logger.LogInformation("Successfully loaded CMR file: {FileName}", destinationFileName);

                var result = new CommandResult(true, $"CMR file '{destinationFileName}' loaded successfully");
                result.Data["FileName"] = destinationFileName;
                result.Data["FilePath"] = destinationPath;
                result.Data["FileSize"] = new FileInfo(destinationPath).Length;
                result.Data["LoadedAt"] = DateTime.UtcNow;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load CMR file: {SourcePath}", sourceFilePath);
                return new CommandResult(false, $"Failed to load CMR file: {ex.Message}");
            }
        }

        public async Task<CommandResult> PrepareFile(string fileName)
        {
            try
            {
                _logger.LogInformation("Preparing CMR file: {FileName}", fileName);

                // Validate system state
                var stateValidation = ValidateSystemState();
                if (!stateValidation.IsSuccess)
                {
                    return stateValidation;
                }

                // Find and validate file
                var filePath = Path.Combine(_configuration.CmrLibraryPath, fileName);
                if (!File.Exists(filePath))
                {
                    return new CommandResult(false, $"CMR file not found: {fileName}");
                }

                lock (_executionLock)
                {
                    if (_currentExecution != null && _currentExecution.Status != CmrExecutionStatus.Completed)
                    {
                        return new CommandResult(false, $"Another CMR is currently {_currentExecution.Status}. Use 'cmr abort' first");
                    }

                    // Create new execution context
                    _currentExecution = new CmrExecutionContext(fileName, filePath);
                }

                // Parse CMR file
                _logger.LogInformation("Parsing CMR file: {FileName}", fileName);
                var parseResult = await _cmrService.ParseCmrFile(filePath);
                if (!parseResult.IsSuccess)
                {
                    _currentExecution = null;
                    return new CommandResult(false, $"Failed to parse CMR file: {parseResult.ErrorMessage}");
                }

                _currentExecution.CmrFile = parseResult.CmrFile;
                _currentExecution.Status = CmrExecutionStatus.Parsed;

                // Create assay samples
                _logger.LogInformation("Creating assay samples from CMR data");
                var assaySamples = await CreateAssaySamples(parseResult.CmrFile);
                _currentExecution.AssaySamples = assaySamples;
                _currentExecution.Status = CmrExecutionStatus.SamplesCreated;

                // Validate inventory
                _logger.LogInformation("Checking inventory for {SampleCount} assay samples", assaySamples.Count);
                var inventoryResult = await ValidateInventoryForSamples(assaySamples);
                
                if (!inventoryResult.IsSuccess)
                {
                    _currentExecution.Status = CmrExecutionStatus.InventoryCheckFailed;
                    
                    var result = new CommandResult(false, "Insufficient inventory for CMR execution");
                    result.Data["MissingItems"] = inventoryResult.MissingItems;
                    result.Data["InventoryErrors"] = inventoryResult.Errors;
                    result.AddError("Inventory validation failed. Please ensure all required articles are available");
                    
                    foreach (var error in inventoryResult.Errors)
                    {
                        result.AddError($"{error.Key}: {error.Value}");
                    }
                    
                    return result;
                }

                // Add samples to AssayManager
                _logger.LogInformation("Adding assay samples to AssayManager");
                var addResult = await _assayManager.AddAssaySamples(assaySamples);
                if (!addResult)
                {
                    _currentExecution.Status = CmrExecutionStatus.Failed;
                    return new CommandResult(false, "Failed to add assay samples to AssayManager");
                }

                _currentExecution.Status = CmrExecutionStatus.Prepared;
                _currentExecution.PreparedAt = DateTime.UtcNow;

                _logger.LogInformation("CMR preparation completed successfully: {FileName}", fileName);

                var successResult = new CommandResult(true, $"CMR '{fileName}' prepared successfully. Ready for execution");
                successResult.Data["SampleCount"] = assaySamples.Count;
                successResult.Data["TestCount"] = assaySamples.Sum(s => s.Assays.Count);
                successResult.Data["EstimatedDuration"] = assaySamples.Sum(s => s.GetEstimatedDuration().TotalMinutes);
                successResult.Data["PreparedAt"] = _currentExecution.PreparedAt;

                return successResult;
            }
            catch (Exception ex)
            {
                if (_currentExecution != null)
                {
                    _currentExecution.Status = CmrExecutionStatus.Failed;
                    _currentExecution.ErrorMessage = ex.Message;
                }

                _logger.LogError(ex, "Failed to prepare CMR file: {FileName}", fileName);
                return new CommandResult(false, $"Failed to prepare CMR file: {ex.Message}");
            }
        }

        public async Task<CommandResult> ExecuteFile(string fileName = null)
        {
            try
            {
                lock (_executionLock)
                {
                    if (_currentExecution == null)
                    {
                        return new CommandResult(false, "No CMR file prepared. Use 'cmr prepare' first");
                    }

                    if (fileName != null && _currentExecution.FileName != fileName)
                    {
                        return new CommandResult(false, $"Specified file '{fileName}' does not match prepared file '{_currentExecution.FileName}'");
                    }

                    if (_currentExecution.Status != CmrExecutionStatus.Prepared)
                    {
                        return new CommandResult(false, $"CMR is not ready for execution. Current status: {_currentExecution.Status}");
                    }
                }

                _logger.LogInformation("Executing CMR: {FileName}", _currentExecution.FileName);

                // Final system state validation
                var stateValidation = ValidateSystemState();
                if (!stateValidation.IsSuccess)
                {
                    return stateValidation;
                }

                // Final inventory check
                _logger.LogInformation("Performing final inventory check");
                var finalInventoryCheck = await ValidateInventoryForSamples(_currentExecution.AssaySamples);
                if (!finalInventoryCheck.IsSuccess)
                {
                    var result = new CommandResult(false, "Final inventory check failed - cannot execute");
                    result.Data["MissingItems"] = finalInventoryCheck.MissingItems;
                    return result;
                }

                // Update execution context
                _currentExecution.Status = CmrExecutionStatus.InProgress;
                _currentExecution.StartedAt = DateTime.UtcNow;

                // Start execution
                _logger.LogInformation("Starting AssayManager execution");
                var executionResult = await _assayManager.StartExecution();
                if (!executionResult)
                {
                    _currentExecution.Status = CmrExecutionStatus.Failed;
                    _currentExecution.ErrorMessage = "Failed to start AssayManager execution";
                    return new CommandResult(false, "Failed to start execution");
                }

                _logger.LogInformation("CMR execution started successfully: {FileName}", _currentExecution.FileName);

                var successResult = new CommandResult(true, $"CMR '{_currentExecution.FileName}' execution started successfully");
                successResult.Data["ExecutionId"] = _currentExecution.Id;
                successResult.Data["StartedAt"] = _currentExecution.StartedAt;
                successResult.Data["SampleCount"] = _currentExecution.AssaySamples.Count;

                return successResult;
            }
            catch (Exception ex)
            {
                if (_currentExecution != null)
                {
                    _currentExecution.Status = CmrExecutionStatus.Failed;
                    _currentExecution.ErrorMessage = ex.Message;
                }

                _logger.LogError(ex, "Failed to execute CMR");
                return new CommandResult(false, $"Failed to execute CMR: {ex.Message}");
            }
        }

        public async Task<CommandResult> AbortExecution()
        {
            try
            {
                lock (_executionLock)
                {
                    if (_currentExecution == null)
                    {
                        return new CommandResult(false, "No CMR execution in progress");
                    }

                    if (_currentExecution.Status == CmrExecutionStatus.Completed)
                    {
                        return new CommandResult(false, "CMR execution already completed");
                    }
                }

                _logger.LogInformation("Aborting CMR execution: {FileName}", _currentExecution.FileName);

                // Stop AssayManager execution
                if (_currentExecution.Status == CmrExecutionStatus.InProgress)
                {
                    await _assayManager.StopExecution();
                }

                // Remove assay samples from AssayManager
                if (_currentExecution.AssaySamples != null)
                {
                    foreach (var sample in _currentExecution.AssaySamples)
                    {
                        await _assayManager.RemoveAssaySample(sample.Id);
                    }
                }

                // Update execution context
                _currentExecution.Status = CmrExecutionStatus.Aborted;
                _currentExecution.CompletedAt = DateTime.UtcNow;
                _currentExecution.ErrorMessage = "Execution aborted by user";

                _logger.LogInformation("CMR execution aborted: {FileName}", _currentExecution.FileName);

                var result = new CommandResult(true, $"CMR execution '{_currentExecution.FileName}' aborted successfully");
                result.Data["AbortedAt"] = _currentExecution.CompletedAt;

                // Clear execution context
                _currentExecution = null;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to abort CMR execution");
                return new CommandResult(false, $"Failed to abort execution: {ex.Message}");
            }
        }

        public ExecutionStatus GetExecutionStatus()
        {
            lock (_executionLock)
            {
                if (_currentExecution == null)
                {
                    return new ExecutionStatus
                    {
                        IsActive = false,
                        Status = "No active execution",
                        Message = "No CMR file is currently being processed"
                    };
                }

                var status = new ExecutionStatus
                {
                    IsActive = _currentExecution.Status == CmrExecutionStatus.InProgress ||
                              _currentExecution.Status == CmrExecutionStatus.Prepared,
                    Status = _currentExecution.Status.ToString(),
                    Message = GetStatusMessage(_currentExecution.Status),
                    FileName = _currentExecution.FileName,
                    ExecutionId = _currentExecution.Id,
                    StartedAt = _currentExecution.StartedAt,
                    PreparedAt = _currentExecution.PreparedAt,
                    CompletedAt = _currentExecution.CompletedAt,
                    ErrorMessage = _currentExecution.ErrorMessage
                };

                if (_currentExecution.AssaySamples != null)
                {
                    status.SampleCount = _currentExecution.AssaySamples.Count;
                    status.TestCount = _currentExecution.AssaySamples.Sum(s => s.Assays.Count);
                    
                    if (_currentExecution.Status == CmrExecutionStatus.InProgress)
                    {
                        var sampleStatuses = _currentExecution.AssaySamples
                            .GroupBy(s => s.Status)
                            .ToDictionary(g => g.Key.ToString(), g => g.Count());
                        status.SampleStatusDistribution = sampleStatuses;
                    }
                }

                return status;
            }
        }

        public List<CmrFileInfo> ListFiles()
        {
            try
            {
                var files = new List<CmrFileInfo>();
                var libraryPath = _configuration.CmrLibraryPath;

                if (!Directory.Exists(libraryPath))
                {
                    return files;
                }

                var fileExtensions = new[] { "*.csv", "*.xlsx" };
                var allFiles = fileExtensions
                    .SelectMany(ext => Directory.GetFiles(libraryPath, ext))
                    .ToList();

                foreach (var filePath in allFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var cmrFileInfo = new CmrFileInfo
                        {
                            FileName = fileInfo.Name,
                            FilePath = filePath,
                            FileSize = fileInfo.Length,
                            CreatedAt = fileInfo.CreationTime,
                            ModifiedAt = fileInfo.LastWriteTime,
                            IsValid = true // Could add validation here
                        };

                        files.Add(cmrFileInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get info for file: {FilePath}", filePath);
                    }
                }

                return files.OrderByDescending(f => f.ModifiedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list CMR files");
                return new List<CmrFileInfo>();
            }
        }

        private CommandResult ValidateSystemState()
        {
            // Check if processing is allowed
            if (!_stateManager.IsProcessingAllowed())
            {
                var currentState = _stateManager.GetCurrentState();
                return new CommandResult(false, 
                    $"System is not in a state that allows processing. Current state: {currentState.State}. Reason: {currentState.Reason}");
            }

            return new CommandResult(true, "System state validation passed");
        }

        private async Task<CommandResult> ValidateCmrFile(string filePath)
        {
            try
            {
                // Basic file validation
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    return new CommandResult(false, "CMR file is empty");
                }

                if (fileInfo.Length > _configuration.MaxFileSizeBytes)
                {
                    return new CommandResult(false, $"CMR file is too large. Maximum size: {_configuration.MaxFileSizeBytes / (1024 * 1024)} MB");
                }

                // Try to parse the file header
                var parseResult = await _cmrService.ValidateCmrFile(filePath);
                if (!parseResult.IsSuccess)
                {
                    return new CommandResult(false, $"CMR file validation failed: {parseResult.ErrorMessage}");
                }

                return new CommandResult(true, "CMR file validation passed");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"CMR file validation error: {ex.Message}");
            }
        }

        private bool IsValidCmrFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".csv" || extension == ".xlsx";
        }

        private async Task<List<AssaySample>> CreateAssaySamples(CmrFile cmrFile)
        {
            var assaySamples = new List<AssaySample>();
            var sampleId = 1;

            foreach (var testOrder in cmrFile.TestOrders)
            {
                // Create sample
                var sample = new Sample($"CMR_Sample_{sampleId++}", SampleType.Sample)
                {
                    Position = testOrder.Position,
                    Properties = new Dictionary<string, object>
                    {
                        ["SourceFile"] = _currentExecution.FileName,
                        ["TestOrderId"] = testOrder.Id
                    }
                };

                // Create assays for this test order
                var assays = new List<Assay>();
                
                // For each replicate, create an assay
                for (int rep = 1; rep <= testOrder.Replicates; rep++)
                {
                    var assay = new Assay(testOrder.TestId, testOrder.TestName, testOrder.TestMethod)
                    {
                        Technology = testOrder.Technology,
                        Parameters = new Dictionary<string, object>(testOrder.Parameters)
                    };
                    
                    assay.Parameters["Replicate"] = rep;
                    assays.Add(assay);
                }

                // Create assay sample
                var assaySample = new AssaySample(sample, assays, testOrder.Priority);
                assaySamples.Add(assaySample);
            }

            return assaySamples;
        }

        private async Task<InventoryCheckResult> ValidateInventoryForSamples(List<AssaySample> assaySamples)
        {
            var allRequirements = new List<InventoryRequirement>();

            foreach (var sample in assaySamples)
            {
                var requirements = sample.ValidateInventoryRequirements();
                allRequirements.AddRange(requirements);
            }

            return await _inventoryService.CheckAvailability(allRequirements);
        }

        private string GetStatusMessage(CmrExecutionStatus status)
        {
            return status switch
            {
                CmrExecutionStatus.Parsed => "CMR file parsed successfully",
                CmrExecutionStatus.SamplesCreated => "Assay samples created from CMR data",
                CmrExecutionStatus.Prepared => "CMR ready for execution",
                CmrExecutionStatus.InProgress => "CMR execution in progress",
                CmrExecutionStatus.Completed => "CMR execution completed successfully",
                CmrExecutionStatus.Failed => "CMR execution failed",
                CmrExecutionStatus.Aborted => "CMR execution aborted",
                CmrExecutionStatus.InventoryCheckFailed => "Inventory check failed",
                _ => "Unknown status"
            };
        }
    }

    // Supporting classes and interfaces
    public interface ICmrCommandService
    {
        Task<CommandResult> LoadFile(string sourceFilePath, string fileName = null);
        Task<CommandResult> PrepareFile(string fileName);
        Task<CommandResult> ExecuteFile(string fileName = null);
        Task<CommandResult> AbortExecution();
        ExecutionStatus GetExecutionStatus();
        List<CmrFileInfo> ListFiles();
    }

    public class CmrExecutionContext
    {
        public Guid Id { get; }
        public string FileName { get; }
        public string FilePath { get; }
        public CmrExecutionStatus Status { get; set; }
        public CmrFile CmrFile { get; set; }
        public List<AssaySample> AssaySamples { get; set; }
        public DateTime CreatedAt { get; }
        public DateTime? PreparedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ErrorMessage { get; set; }

        public CmrExecutionContext(string fileName, string filePath)
        {
            Id = Guid.NewGuid();
            FileName = fileName;
            FilePath = filePath;
            Status = CmrExecutionStatus.Created;
            CreatedAt = DateTime.UtcNow;
        }
    }

    public class CommandResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        public Dictionary<string, object> Data { get; }
        public List<string> Warnings { get; }
        public List<string> Errors { get; }

        public CommandResult(bool isSuccess, string message = null)
        {
            IsSuccess = isSuccess;
            Message = message ?? string.Empty;
            Data = new Dictionary<string, object>();
            Warnings = new List<string>();
            Errors = new List<string>();
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                Warnings.Add(warning);
            }
        }

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Errors.Add(error);
            }
        }

        public bool HasWarnings() => Warnings.Any();
        public bool HasErrors() => Errors.Any();
    }

    public class ExecutionStatus
    {
        public bool IsActive { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string FileName { get; set; }
        public Guid? ExecutionId { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? PreparedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ErrorMessage { get; set; }
        public int? SampleCount { get; set; }
        public int? TestCount { get; set; }
        public Dictionary<string, int> SampleStatusDistribution { get; set; }
    }

    public class CmrFileInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool IsValid { get; set; }
        public string ValidationMessage { get; set; }
    }

    public class CmrCommandConfiguration
    {
        public string CmrLibraryPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Scheduler", "CMR");
        public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
        public bool AllowOverwrite { get; set; } = false;
        public int MaxConcurrentExecutions { get; set; } = 1;
        public bool EnableFileValidation { get; set; } = true;
        public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromHours(8);
    }

    public enum CmrExecutionStatus
    {
        Created,
        Parsed,
        SamplesCreated,
        InventoryCheckFailed,
        Prepared,
        InProgress,
        Completed,
        Failed,
        Aborted
    }
}