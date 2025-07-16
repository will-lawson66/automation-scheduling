using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using System.IO;
using FluidHandling.Core.Models;
using FluidHandling.Core.Interfaces;

namespace FluidHandling.Integration
{
    /// <summary>
    /// OPC UA Integration for IVD Instruments
    /// Based on research showing OPC UA as the dominant communication standard
    /// for industrial automation with platform-independent, service-oriented communication
    /// </summary>
    public class OPCUAIntegration
    {
        private readonly Dictionary<int, OPCUAClient> _instrumentClients;
        private readonly OPCUAConfig _config;
        private readonly Timer _monitoringTimer;
        private bool _isConnected;

        public event EventHandler<InstrumentStatusChangedEventArgs> InstrumentStatusChanged;
        public event EventHandler<OperationCompletedEventArgs> OperationCompleted;
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        public OPCUAIntegration(OPCUAConfig config = null)
        {
            _config = config ?? new OPCUAConfig();
            _instrumentClients = new Dictionary<int, OPCUAClient>();
            _monitoringTimer = new Timer(MonitorInstruments, null, Timeout.Infinite, Timeout.Infinite);
            _isConnected = false;
        }

        public async Task<bool> ConnectAsync()
        {
            Console.WriteLine("[OPC UA] Connecting to instruments...");
            
            try
            {
                // Connect to each configured instrument
                foreach (var instrumentConfig in _config.InstrumentConfigurations)
                {
                    var client = new OPCUAClient(instrumentConfig);
                    await client.ConnectAsync();
                    
                    _instrumentClients[instrumentConfig.InstrumentId] = client;
                    
                    // Subscribe to instrument events
                    client.StatusChanged += OnInstrumentStatusChanged;
                    client.OperationCompleted += OnOperationCompleted;
                    client.ErrorOccurred += OnErrorOccurred;
                    
                    Console.WriteLine($"[OPC UA] Connected to instrument {instrumentConfig.InstrumentId} at {instrumentConfig.EndpointUrl}");
                }
                
                _isConnected = true;
                
                // Start monitoring
                _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(_config.MonitoringIntervalSeconds));
                
                Console.WriteLine($"[OPC UA] Successfully connected to {_instrumentClients.Count} instruments");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OPC UA] Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            Console.WriteLine("[OPC UA] Disconnecting from instruments...");
            
            try
            {
                _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
                foreach (var client in _instrumentClients.Values)
                {
                    await client.DisconnectAsync();
                }
                
                _instrumentClients.Clear();
                _isConnected = false;
                
                Console.WriteLine("[OPC UA] Successfully disconnected from all instruments");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OPC UA] Disconnection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExecuteOperationAsync(int instrumentId, FluidOperation operation)
        {
            if (!_isConnected || !_instrumentClients.ContainsKey(instrumentId))
            {
                Console.WriteLine($"[OPC UA] Instrument {instrumentId} not connected");
                return false;
            }

            try
            {
                var client = _instrumentClients[instrumentId];
                var opcOperation = ConvertToOPCOperation(operation);
                
                Console.WriteLine($"[OPC UA] Executing operation {operation.Id} on instrument {instrumentId}");
                
                var result = await client.ExecuteOperationAsync(opcOperation);
                
                if (result.Success)
                {
                    Console.WriteLine($"[OPC UA] Operation {operation.Id} completed successfully");
                    operation.IsCompleted = true;
                    operation.CompletionTime = DateTime.Now;
                    operation.Status = OperationStatus.Completed;
                }
                else
                {
                    Console.WriteLine($"[OPC UA] Operation {operation.Id} failed: {result.ErrorMessage}");
                    operation.Status = OperationStatus.Failed;
                }
                
                return result.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OPC UA] Operation execution failed: {ex.Message}");
                operation.Status = OperationStatus.Failed;
                return false;
            }
        }

        public async Task<InstrumentStatus> GetInstrumentStatusAsync(int instrumentId)
        {
            if (!_isConnected || !_instrumentClients.ContainsKey(instrumentId))
            {
                return new InstrumentStatus
                {
                    InstrumentId = instrumentId,
                    Status = InstrumentState.Disconnected,
                    ErrorMessage = "Instrument not connected"
                };
            }

            try
            {
                var client = _instrumentClients[instrumentId];
                return await client.GetStatusAsync();
            }
            catch (Exception ex)
            {
                return new InstrumentStatus
                {
                    InstrumentId = instrumentId,
                    Status = InstrumentState.Error,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> CalibrateInstrumentAsync(int instrumentId, CalibrationParameters parameters)
        {
            if (!_isConnected || !_instrumentClients.ContainsKey(instrumentId))
            {
                Console.WriteLine($"[OPC UA] Instrument {instrumentId} not connected");
                return false;
            }

            try
            {
                var client = _instrumentClients[instrumentId];
                Console.WriteLine($"[OPC UA] Starting calibration for instrument {instrumentId}");
                
                var result = await client.CalibrateAsync(parameters);
                
                if (result.Success)
                {
                    Console.WriteLine($"[OPC UA] Calibration completed for instrument {instrumentId}");
                }
                else
                {
                    Console.WriteLine($"[OPC UA] Calibration failed for instrument {instrumentId}: {result.ErrorMessage}");
                }
                
                return result.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OPC UA] Calibration failed: {ex.Message}");
                return false;
            }
        }

        private OPCOperation ConvertToOPCOperation(FluidOperation operation)
        {
            return new OPCOperation
            {
                Id = operation.Id.ToString(),
                OperationType = operation.OperationType,
                SourceLocation = operation.SourceLocation,
                DestinationLocation = operation.DestinationLocation,
                Volume = operation.VolumeInMicroliters,
                Parameters = new Dictionary<string, object>
                {
                    ["Priority"] = operation.Priority,
                    ["SampleId"] = operation.SampleId,
                    ["EstimatedDuration"] = operation.EstimatedDurationMs
                }
            };
        }

        private void MonitorInstruments(object state)
        {
            if (!_isConnected) return;

            _ = Task.Run(async () =>
            {
                foreach (var kvp in _instrumentClients)
                {
                    try
                    {
                        var status = await kvp.Value.GetStatusAsync();
                        
                        // Check for status changes or errors
                        if (status.Status == InstrumentState.Error)
                        {
                            ErrorOccurred?.Invoke(this, new ErrorEventArgs
                            {
                                InstrumentId = kvp.Key,
                                ErrorMessage = status.ErrorMessage,
                                Timestamp = DateTime.Now
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OPC UA] Monitoring error for instrument {kvp.Key}: {ex.Message}");
                    }
                }
            });
        }

        private void OnInstrumentStatusChanged(object sender, InstrumentStatusChangedEventArgs e)
        {
            InstrumentStatusChanged?.Invoke(this, e);
        }

        private void OnOperationCompleted(object sender, OperationCompletedEventArgs e)
        {
            OperationCompleted?.Invoke(this, e);
        }

        private void OnErrorOccurred(object sender, ErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Simplified OPC UA Client for demonstration
    /// In real implementation, would use libraries like OPCFoundation.NetStandard.Opc.Ua
    /// </summary>
    public class OPCUAClient
    {
        private readonly OPCUAInstrumentConfig _config;
        private bool _isConnected;
        private readonly Random _random;

        public event EventHandler<InstrumentStatusChangedEventArgs> StatusChanged;
        public event EventHandler<OperationCompletedEventArgs> OperationCompleted;
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        public OPCUAClient(OPCUAInstrumentConfig config)
        {
            _config = config;
            _isConnected = false;
            _random = new Random();
        }

        public async Task ConnectAsync()
        {
            // Simulate connection
            await Task.Delay(1000);
            _isConnected = true;
        }

        public async Task DisconnectAsync()
        {
            // Simulate disconnection
            await Task.Delay(500);
            _isConnected = false;
        }

        public async Task<OperationResult> ExecuteOperationAsync(OPCOperation operation)
        {
            if (!_isConnected)
            {
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = "Client not connected"
                };
            }

            // Simulate operation execution
            await Task.Delay(Convert.ToInt32(operation.Parameters["EstimatedDuration"]));
            
            // Simulate 95% success rate
            var success = _random.NextDouble() > 0.05;
            
            if (success)
            {
                OperationCompleted?.Invoke(this, new OperationCompletedEventArgs
                {
                    InstrumentId = _config.InstrumentId,
                    OperationId = operation.Id,
                    Timestamp = DateTime.Now
                });
            }
            
            return new OperationResult
            {
                Success = success,
                ErrorMessage = success ? null : "Simulated operation failure"
            };
        }

        public async Task<InstrumentStatus> GetStatusAsync()
        {
            if (!_isConnected)
            {
                return new InstrumentStatus
                {
                    InstrumentId = _config.InstrumentId,
                    Status = InstrumentState.Disconnected
                };
            }

            // Simulate status check
            await Task.Delay(100);
            
            return new InstrumentStatus
            {
                InstrumentId = _config.InstrumentId,
                Status = InstrumentState.Ready,
                Temperature = 25.0 + _random.NextDouble() * 5.0,
                Pressure = 1013.25 + _random.NextDouble() * 10.0,
                LastCalibration = DateTime.Now.AddDays(-_random.Next(1, 30)),
                OperationCount = _random.Next(100, 1000)
            };
        }

        public async Task<OperationResult> CalibrateAsync(CalibrationParameters parameters)
        {
            if (!_isConnected)
            {
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = "Client not connected"
                };
            }

            // Simulate calibration
            await Task.Delay(parameters.CalibrationDurationMs);
            
            var success = _random.NextDouble() > 0.1; // 90% success rate
            
            return new OperationResult
            {
                Success = success,
                ErrorMessage = success ? null : "Calibration failed"
            };
        }
    }

    /// <summary>
    /// MES (Manufacturing Execution System) Integration
    /// Provides bidirectional communication with MES systems for job management
    /// </summary>
    public class MESIntegration
    {
        private readonly MESConfig _config;
        private readonly Dictionary<string, object> _jobCache;
        private readonly Timer _syncTimer;

        public event EventHandler<JobReceivedEventArgs> JobReceived;
        public event EventHandler<JobStatusChangedEventArgs> JobStatusChanged;

        public MESIntegration(MESConfig config = null)
        {
            _config = config ?? new MESConfig();
            _jobCache = new Dictionary<string, object>();
            _syncTimer = new Timer(SyncWithMES, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task<bool> ConnectAsync()
        {
            Console.WriteLine("[MES] Connecting to MES system...");
            
            try
            {
                // Simulate MES connection
                await Task.Delay(2000);
                
                // Start synchronization
                _syncTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(_config.SyncIntervalSeconds));
                
                Console.WriteLine($"[MES] Connected to MES at {_config.MESEndpoint}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MES] Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<List<MESJob>> GetPendingJobsAsync()
        {
            try
            {
                // Simulate API call to MES
                await Task.Delay(500);
                
                var jobs = new List<MESJob>();
                var random = new Random();
                
                // Generate some sample jobs
                for (int i = 1; i <= random.Next(3, 8); i++)
                {
                    var job = new MESJob
                    {
                        JobId = $"MES-JOB-{DateTime.Now:yyyyMMdd}-{i:D3}",
                        JobType = GetRandomJobType(),
                        Priority = random.Next(1, 10),
                        SampleCount = random.Next(1, 20),
                        RequestedCompletionTime = DateTime.Now.AddHours(random.Next(2, 24)),
                        Operations = GenerateOperationsForJob(i, random.Next(2, 6))
                    };
                    
                    jobs.Add(job);
                }
                
                Console.WriteLine($"[MES] Retrieved {jobs.Count} pending jobs");
                return jobs;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MES] Failed to retrieve jobs: {ex.Message}");
                return new List<MESJob>();
            }
        }

        public async Task<bool> UpdateJobStatusAsync(string jobId, JobStatus status, string details = null)
        {
            try
            {
                Console.WriteLine($"[MES] Updating job {jobId} status to {status}");
                
                // Simulate API call to MES
                await Task.Delay(200);
                
                var statusUpdate = new JobStatusUpdate
                {
                    JobId = jobId,
                    Status = status,
                    UpdateTime = DateTime.Now,
                    Details = details
                };
                
                // Cache the update
                _jobCache[$"{jobId}_status"] = statusUpdate;
                
                // Notify listeners
                JobStatusChanged?.Invoke(this, new JobStatusChangedEventArgs
                {
                    JobId = jobId,
                    NewStatus = status,
                    Details = details,
                    Timestamp = DateTime.Now
                });
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MES] Failed to update job status: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReportOperationResultAsync(string jobId, string operationId, OperationResult result)
        {
            try
            {
                Console.WriteLine($"[MES] Reporting operation {operationId} result for job {jobId}");
                
                // Simulate API call to MES
                await Task.Delay(150);
                
                var report = new OperationReport
                {
                    JobId = jobId,
                    OperationId = operationId,
                    Success = result.Success,
                    CompletionTime = DateTime.Now,
                    ErrorMessage = result.ErrorMessage,
                    QualityData = result.QualityData
                };
                
                // Cache the report
                _jobCache[$"{jobId}_{operationId}_result"] = report;
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MES] Failed to report operation result: {ex.Message}");
                return false;
            }
        }

        private void SyncWithMES(object state)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Check for new jobs
                    var pendingJobs = await GetPendingJobsAsync();
                    
                    foreach (var job in pendingJobs)
                    {
                        if (!_jobCache.ContainsKey(job.JobId))
                        {
                            _jobCache[job.JobId] = job;
                            
                            JobReceived?.Invoke(this, new JobReceivedEventArgs
                            {
                                Job = job,
                                Timestamp = DateTime.Now
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MES] Sync error: {ex.Message}");
                }
            });
        }

        private string GetRandomJobType()
        {
            var jobTypes = new[] { "PCR", "ELISA", "Immunoassay", "Sequencing", "Protein Analysis" };
            return jobTypes[new Random().Next(jobTypes.Length)];
        }

        private List<FluidOperation> GenerateOperationsForJob(int jobId, int operationCount)
        {
            var operations = new List<FluidOperation>();
            var random = new Random();
            
            for (int i = 1; i <= operationCount; i++)
            {
                var operation = new FluidOperation
                {
                    Id = jobId * 100 + i,
                    SampleId = $"SAMPLE-{jobId}-{i:D2}",
                    OperationType = GetRandomOperationType(),
                    VolumeInMicroliters = random.Next(10, 500),
                    EstimatedDurationMs = random.Next(30000, 300000),
                    Priority = random.Next(1, 5),
                    SubmissionTime = DateTime.Now,
                    Deadline = DateTime.Now.AddHours(random.Next(1, 8)),
                    SourceLocation = $"R{random.Next(1, 5)}",
                    DestinationLocation = $"W{random.Next(1, 3)}"
                };
                
                operations.Add(operation);
            }
            
            return operations;
        }

        private string GetRandomOperationType()
        {
            var operationTypes = new[] { "Sample Transfer", "Reagent Addition", "Mixing", "Incubation", "Washing", "Detection" };
            return operationTypes[new Random().Next(operationTypes.Length)];
        }
    }

    /// <summary>
    /// LIMS (Laboratory Information Management System) Integration
    /// Handles sample tracking and results reporting
    /// </summary>
    public class LIMSIntegration
    {
        private readonly LIMSConfig _config;
        private readonly Dictionary<string, SampleInfo> _sampleCache;

        public event EventHandler<SampleResultReportedEventArgs> SampleResultReported;

        public LIMSIntegration(LIMSConfig config = null)
        {
            _config = config ?? new LIMSConfig();
            _sampleCache = new Dictionary<string, SampleInfo>();
        }

        public async Task<bool> ConnectAsync()
        {
            Console.WriteLine("[LIMS] Connecting to LIMS system...");
            
            try
            {
                // Simulate LIMS connection
                await Task.Delay(1500);
                
                Console.WriteLine($"[LIMS] Connected to LIMS at {_config.LIMSEndpoint}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LIMS] Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task<SampleInfo> GetSampleInfoAsync(string sampleId)
        {
            try
            {
                // Check cache first
                if (_sampleCache.ContainsKey(sampleId))
                {
                    return _sampleCache[sampleId];
                }
                
                // Simulate API call to LIMS
                await Task.Delay(300);
                
                var random = new Random();
                var sampleInfo = new SampleInfo
                {
                    SampleId = sampleId,
                    PatientId = $"PT-{random.Next(10000, 99999)}",
                    SampleType = GetRandomSampleType(),
                    CollectionDate = DateTime.Now.AddDays(-random.Next(1, 7)),
                    Priority = random.Next(1, 5),
                    TestsOrdered = GenerateTestsOrdered(random.Next(1, 5)),
                    SpecialInstructions = GetRandomSpecialInstructions(),
                    Status = SampleStatus.Received
                };
                
                _sampleCache[sampleId] = sampleInfo;
                
                Console.WriteLine($"[LIMS] Retrieved sample info for {sampleId}");
                return sampleInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LIMS] Failed to retrieve sample info: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ReportSampleResultAsync(string sampleId, SampleResult result)
        {
            try
            {
                Console.WriteLine($"[LIMS] Reporting result for sample {sampleId}");
                
                // Simulate API call to LIMS
                await Task.Delay(400);
                
                var report = new SampleResultReport
                {
                    SampleId = sampleId,
                    Result = result,
                    ReportTime = DateTime.Now,
                    AnalystId = _config.AnalystId,
                    InstrumentId = result.InstrumentId,
                    QualityFlags = result.QualityFlags
                };
                
                // Update sample status
                if (_sampleCache.ContainsKey(sampleId))
                {
                    _sampleCache[sampleId].Status = SampleStatus.Completed;
                }
                
                // Notify listeners
                SampleResultReported?.Invoke(this, new SampleResultReportedEventArgs
                {
                    SampleId = sampleId,
                    Result = result,
                    Timestamp = DateTime.Now
                });
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LIMS] Failed to report sample result: {ex.Message}");
                return false;
            }
        }

        public async Task<List<SampleInfo>> GetPendingSamplesAsync()
        {
            try
            {
                // Simulate API call to LIMS
                await Task.Delay(600);
                
                var samples = new List<SampleInfo>();
                var random = new Random();
                
                // Generate some sample pending samples
                for (int i = 1; i <= random.Next(5, 15); i++)
                {
                    var sample = new SampleInfo
                    {
                        SampleId = $"SAMPLE-{DateTime.Now:yyyyMMdd}-{i:D4}",
                        PatientId = $"PT-{random.Next(10000, 99999)}",
                        SampleType = GetRandomSampleType(),
                        CollectionDate = DateTime.Now.AddDays(-random.Next(1, 3)),
                        Priority = random.Next(1, 5),
                        TestsOrdered = GenerateTestsOrdered(random.Next(1, 3)),
                        SpecialInstructions = GetRandomSpecialInstructions(),
                        Status = SampleStatus.Pending
                    };
                    
                    samples.Add(sample);
                    _sampleCache[sample.SampleId] = sample;
                }
                
                Console.WriteLine($"[LIMS] Retrieved {samples.Count} pending samples");
                return samples;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LIMS] Failed to retrieve pending samples: {ex.Message}");
                return new List<SampleInfo>();
            }
        }

        private string GetRandomSampleType()
        {
            var sampleTypes = new[] { "Blood", "Urine", "Saliva", "Tissue", "CSF" };
            return sampleTypes[new Random().Next(sampleTypes.Length)];
        }

        private List<string> GenerateTestsOrdered(int count)
        {
            var allTests = new[] { "CBC", "CMP", "Lipid Panel", "HbA1c", "TSH", "PSA", "Troponin", "CRP" };
            var random = new Random();
            var tests = new List<string>();
            
            for (int i = 0; i < count; i++)
            {
                var test = allTests[random.Next(allTests.Length)];
                if (!tests.Contains(test))
                {
                    tests.Add(test);
                }
            }
            
            return tests;
        }

        private string GetRandomSpecialInstructions()
        {
            var instructions = new[] { "Stat", "Fasting", "Rush", "Handle with care", "Keep at room temperature" };
            return new Random().NextDouble() > 0.5 ? instructions[new Random().Next(instructions.Length)] : null;
        }
    }

    /// <summary>
    /// Real-time monitoring and adaptive scheduling system
    /// Continuously monitors system performance and adapts scheduling accordingly
    /// </summary>
    public class RealTimeMonitoringSystem
    {
        private readonly List<IScheduler> _schedulers;
        private readonly OPCUAIntegration _opcuaIntegration;
        private readonly MESIntegration _mesIntegration;
        private readonly LIMSIntegration _limsIntegration;
        private readonly MonitoringConfig _config;
        private readonly Timer _monitoringTimer;
        private readonly Timer _adaptationTimer;
        private readonly Dictionary<string, PerformanceMetrics> _realtimeMetrics;

        public event EventHandler<PerformanceAlertEventArgs> PerformanceAlert;
        public event EventHandler<SchedulerRecommendationEventArgs> SchedulerRecommendation;

        public RealTimeMonitoringSystem(
            List<IScheduler> schedulers,
            OPCUAIntegration opcuaIntegration,
            MESIntegration mesIntegration,
            LIMSIntegration limsIntegration,
            MonitoringConfig config = null)
        {
            _schedulers = schedulers;
            _opcuaIntegration = opcuaIntegration;
            _mesIntegration = mesIntegration;
            _limsIntegration = limsIntegration;
            _config = config ?? new MonitoringConfig();
            _realtimeMetrics = new Dictionary<string, PerformanceMetrics>();
            
            _monitoringTimer = new Timer(MonitorPerformance, null, Timeout.Infinite, Timeout.Infinite);
            _adaptationTimer = new Timer(AdaptScheduling, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task StartMonitoringAsync()
        {
            Console.WriteLine("[Monitor] Starting real-time monitoring system...");
            
            // Subscribe to system events
            _opcuaIntegration.ErrorOccurred += OnInstrumentError;
            _opcuaIntegration.OperationCompleted += OnOperationCompleted;
            _mesIntegration.JobStatusChanged += OnJobStatusChanged;
            _limsIntegration.SampleResultReported += OnSampleResultReported;
            
            // Start monitoring timers
            _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(_config.MonitoringIntervalSeconds));
            _adaptationTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(_config.AdaptationIntervalMinutes));
            
            Console.WriteLine("[Monitor] Real-time monitoring started");
        }

        public async Task StopMonitoringAsync()
        {
            Console.WriteLine("[Monitor] Stopping real-time monitoring system...");
            
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _adaptationTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            Console.WriteLine("[Monitor] Real-time monitoring stopped");
        }

        public PerformanceMetrics GetCurrentMetrics()
        {
            var aggregatedMetrics = new PerformanceMetrics
            {
                MeasurementStart = DateTime.Now.AddMinutes(-_config.MetricsWindowMinutes),
                MeasurementEnd = DateTime.Now
            };
            
            if (_realtimeMetrics.Count > 0)
            {
                var metrics = _realtimeMetrics.Values.ToList();
                aggregatedMetrics.Throughput = metrics.Average(m => m.Throughput);
                aggregatedMetrics.Utilization = metrics.Average(m => m.Utilization);
                aggregatedMetrics.DeadlineMissRate = metrics.Average(m => m.DeadlineMissRate);
                aggregatedMetrics.ErrorRate = metrics.Average(m => m.ErrorRate);
                aggregatedMetrics.AverageResponseTime = metrics.Average(m => m.AverageResponseTime);
            }
            
            return aggregatedMetrics;
        }

        private void MonitorPerformance(object state)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var currentMetrics = await CollectCurrentMetrics();
                    _realtimeMetrics[DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")] = currentMetrics;
                    
                    // Clean up old metrics
                    var cutoffTime = DateTime.Now.AddMinutes(-_config.MetricsWindowMinutes);
                    var keysToRemove = _realtimeMetrics.Keys
                        .Where(k => DateTime.Parse(k) < cutoffTime)
                        .ToList();
                    
                    foreach (var key in keysToRemove)
                    {
                        _realtimeMetrics.Remove(key);
                    }
                    
                    // Check for performance alerts
                    CheckPerformanceAlerts(currentMetrics);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Monitor] Performance monitoring error: {ex.Message}");
                }
            });
        }

        private async Task<PerformanceMetrics> CollectCurrentMetrics()
        {
            var metrics = new PerformanceMetrics
            {
                MeasurementStart = DateTime.Now.AddMinutes(-1),
                MeasurementEnd = DateTime.Now
            };
            
            // Collect metrics from schedulers
            foreach (var scheduler in _schedulers)
            {
                var schedulerMetrics = scheduler.GetPerformanceMetrics();
                // Aggregate metrics (simplified)
                metrics.Throughput += schedulerMetrics.Throughput;
                metrics.Utilization += schedulerMetrics.Utilization;
                metrics.DeadlineMissRate += schedulerMetrics.DeadlineMissRate;
                metrics.ErrorRate += schedulerMetrics.ErrorRate;
            }
            
            if (_schedulers.Count > 0)
            {
                metrics.Throughput /= _schedulers.Count;
                metrics.Utilization /= _schedulers.Count;
                metrics.DeadlineMissRate /= _schedulers.Count;
                metrics.ErrorRate /= _schedulers.Count;
            }
            
            return metrics;
        }

        private void CheckPerformanceAlerts(PerformanceMetrics metrics)
        {
            var alerts = new List<PerformanceAlert>();
            
            // Check for high error rate
            if (metrics.ErrorRate > _config.ErrorRateThreshold)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighErrorRate,
                    Severity = AlertSeverity.High,
                    Message = $"Error rate {metrics.ErrorRate:P2} exceeds threshold {_config.ErrorRateThreshold:P2}",
                    Value = metrics.ErrorRate,
                    Threshold = _config.ErrorRateThreshold
                });
            }
            
            // Check for low utilization
            if (metrics.Utilization < _config.UtilizationThreshold)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.LowUtilization,
                    Severity = AlertSeverity.Medium,
                    Message = $"Utilization {metrics.Utilization:P2} below threshold {_config.UtilizationThreshold:P2}",
                    Value = metrics.Utilization,
                    Threshold = _config.UtilizationThreshold
                });
            }
            
            // Check for high deadline miss rate
            if (metrics.DeadlineMissRate > _config.DeadlineMissThreshold)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighDeadlineMissRate,
                    Severity = AlertSeverity.High,
                    Message = $"Deadline miss rate {metrics.DeadlineMissRate:P2} exceeds threshold {_config.DeadlineMissThreshold:P2}",
                    Value = metrics.DeadlineMissRate,
                    Threshold = _config.DeadlineMissThreshold
                });
            }
            
            // Raise alerts
            foreach (var alert in alerts)
            {
                PerformanceAlert?.Invoke(this, new PerformanceAlertEventArgs
                {
                    Alert = alert,
                    Timestamp = DateTime.Now
                });
            }
        }

        private void AdaptScheduling(object state)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var currentMetrics = GetCurrentMetrics();
                    var recommendation = AnalyzeAndRecommendScheduler(currentMetrics);
                    
                    if (recommendation != null)
                    {
                        SchedulerRecommendation?.Invoke(this, new SchedulerRecommendationEventArgs
                        {
                            Recommendation = recommendation,
                            Timestamp = DateTime.Now
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Monitor] Scheduling adaptation error: {ex.Message}");
                }
            });
        }

        private SchedulerRecommendation AnalyzeAndRecommendScheduler(PerformanceMetrics metrics)
        {
            // Analyze current performance and recommend scheduler changes
            var recommendation = new SchedulerRecommendation();
            
            if (metrics.ErrorRate > 0.1) // High error rate
            {
                recommendation.RecommendedScheduler = "Quality Control Scheduler";
                recommendation.Reason = "High error rate detected - switch to quality-focused scheduling";
                recommendation.Confidence = 0.8;
            }
            else if (metrics.DeadlineMissRate > 0.15) // High deadline miss rate
            {
                recommendation.RecommendedScheduler = "Earliest Deadline First Scheduler";
                recommendation.Reason = "High deadline miss rate - switch to deadline-focused scheduling";
                recommendation.Confidence = 0.9;
            }
            else if (metrics.Utilization < 0.6) // Low utilization
            {
                recommendation.RecommendedScheduler = "Greedy Scheduler";
                recommendation.Reason = "Low utilization - switch to efficiency-focused scheduling";
                recommendation.Confidence = 0.7;
            }
            else
            {
                return null; // No recommendation needed
            }
            
            return recommendation;
        }

        private void OnInstrumentError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine($"[Monitor] Instrument error detected: {e.ErrorMessage}");
            
            // Update error metrics
            var currentMetrics = GetCurrentMetrics();
            currentMetrics.ErrorRate += 0.01; // Increment error rate
            
            // Could trigger scheduler adaptation here
        }

        private void OnOperationCompleted(object sender, OperationCompletedEventArgs e)
        {
            Console.WriteLine($"[Monitor] Operation {e.OperationId} completed on instrument {e.InstrumentId}");
            
            // Update completion metrics
            var currentMetrics = GetCurrentMetrics();
            currentMetrics.Throughput += 0.1; // Increment throughput
        }

        private void OnJobStatusChanged(object sender, JobStatusChangedEventArgs e)
        {
            Console.WriteLine($"[Monitor] Job {e.JobId} status changed to {e.NewStatus}");
        }

        private void OnSampleResultReported(object sender, SampleResultReportedEventArgs e)
        {
            Console.WriteLine($"[Monitor] Sample {e.SampleId} result reported");
        }
    }

    #region Configuration Classes
    
    public class OPCUAConfig
    {
        public List<OPCUAInstrumentConfig> InstrumentConfigurations { get; set; }
        public int MonitoringIntervalSeconds { get; set; } = 30;
        public int ConnectionTimeoutSeconds { get; set; } = 10;
        public bool EnableSecurity { get; set; } = true;
        public string SecurityPolicy { get; set; } = "Basic256Sha256";

        public OPCUAConfig()
        {
            InstrumentConfigurations = new List<OPCUAInstrumentConfig>();
        }
    }

    public class OPCUAInstrumentConfig
    {
        public int InstrumentId { get; set; }
        public string InstrumentName { get; set; }
        public string EndpointUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public Dictionary<string, string> NodeIds { get; set; }

        public OPCUAInstrumentConfig()
        {
            NodeIds = new Dictionary<string, string>();
        }
    }

    public class MESConfig
    {
        public string MESEndpoint { get; set; } = "http://localhost:8080/mes";
        public string ApiKey { get; set; }
        public int SyncIntervalSeconds { get; set; } = 60;
        public int TimeoutSeconds { get; set; } = 30;
        public bool EnableRealTimeSync { get; set; } = true;
    }

    public class LIMSConfig
    {
        public string LIMSEndpoint { get; set; } = "http://localhost:8081/lims";
        public string ApiKey { get; set; }
        public string AnalystId { get; set; } = "AUTO_SCHEDULER";
        public int TimeoutSeconds { get; set; } = 30;
        public bool EnableAutoReporting { get; set; } = true;
    }

    public class MonitoringConfig
    {
        public int MonitoringIntervalSeconds { get; set; } = 30;
        public int AdaptationIntervalMinutes { get; set; } = 5;
        public int MetricsWindowMinutes { get; set; } = 30;
        public double ErrorRateThreshold { get; set; } = 0.05;
        public double UtilizationThreshold { get; set; } = 0.7;
        public double DeadlineMissThreshold { get; set; } = 0.1;
    }

    #endregion

    #region Data Classes

    public class OPCOperation
    {
        public string Id { get; set; }
        public string OperationType { get; set; }
        public string SourceLocation { get; set; }
        public string DestinationLocation { get; set; }
        public double Volume { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        public OPCOperation()
        {
            Parameters = new Dictionary<string, object>();
        }
    }

    public class OperationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> QualityData { get; set; }

        public OperationResult()
        {
            QualityData = new Dictionary<string, object>();
        }
    }

    public class InstrumentStatus
    {
        public int InstrumentId { get; set; }
        public InstrumentState Status { get; set; }
        public double Temperature { get; set; }
        public double Pressure { get; set; }
        public DateTime LastCalibration { get; set; }
        public int OperationCount { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class CalibrationParameters
    {
        public string CalibrationType { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public int CalibrationDurationMs { get; set; }

        public CalibrationParameters()
        {
            Parameters = new Dictionary<string, object>();
        }
    }

    public class MESJob
    {
        public string JobId { get; set; }
        public string JobType { get; set; }
        public int Priority { get; set; }
        public int SampleCount { get; set; }
        public DateTime RequestedCompletionTime { get; set; }
        public List<FluidOperation> Operations { get; set; }

        public MESJob()
        {
            Operations = new List<FluidOperation>();
        }
    }

    public class JobStatusUpdate
    {
        public string JobId { get; set; }
        public JobStatus Status { get; set; }
        public DateTime UpdateTime { get; set; }
        public string Details { get; set; }
    }

    public class OperationReport
    {
        public string JobId { get; set; }
        public string OperationId { get; set; }
        public bool Success { get; set; }
        public DateTime CompletionTime { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> QualityData { get; set; }

        public OperationReport()
        {
            QualityData = new Dictionary<string, object>();
        }
    }

    public class SampleInfo
    {
        public string SampleId { get; set; }
        public string PatientId { get; set; }
        public string SampleType { get; set; }
        public DateTime CollectionDate { get; set; }
        public int Priority { get; set; }
        public List<string> TestsOrdered { get; set; }
        public string SpecialInstructions { get; set; }
        public SampleStatus Status { get; set; }

        public SampleInfo()
        {
            TestsOrdered = new List<string>();
        }
    }

    public class SampleResult
    {
        public string SampleId { get; set; }
        public string TestName { get; set; }
        public object Value { get; set; }
        public string Units { get; set; }
        public string ReferenceRange { get; set; }
        public string InstrumentId { get; set; }
        public List<string> QualityFlags { get; set; }
        public DateTime ResultTime { get; set; }

        public SampleResult()
        {
            QualityFlags = new List<string>();
        }
    }

    public class SampleResultReport
    {
        public string SampleId { get; set; }
        public SampleResult Result { get; set; }
        public DateTime ReportTime { get; set; }
        public string AnalystId { get; set; }
        public string InstrumentId { get; set; }
        public List<string> QualityFlags { get; set; }

        public SampleResultReport()
        {
            QualityFlags = new List<string>();
        }
    }

    public class PerformanceAlert
    {
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
    }

    public class SchedulerRecommendation
    {
        public string RecommendedScheduler { get; set; }
        public string Reason { get; set; }
        public double Confidence { get; set; }
    }

    #endregion

    #region Enums

    public enum InstrumentState
    {
        Disconnected,
        Connecting,
        Ready,
        Busy,
        Error,
        Maintenance
    }

    public enum JobStatus
    {
        Received,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    public enum SampleStatus
    {
        Received,
        Pending,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    public enum AlertType
    {
        HighErrorRate,
        LowUtilization,
        HighDeadlineMissRate,
        InstrumentFailure,
        SystemOverload
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion

    #region Event Args

    public class InstrumentStatusChangedEventArgs : EventArgs
    {
        public int InstrumentId { get; set; }
        public InstrumentState OldStatus { get; set; }
        public InstrumentState NewStatus { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class OperationCompletedEventArgs : EventArgs
    {
        public int InstrumentId { get; set; }
        public string OperationId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ErrorEventArgs : EventArgs
    {
        public int InstrumentId { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class JobReceivedEventArgs : EventArgs
    {
        public MESJob Job { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class JobStatusChangedEventArgs : EventArgs
    {
        public string JobId { get; set; }
        public JobStatus NewStatus { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SampleResultReportedEventArgs : EventArgs
    {
        public string SampleId { get; set; }
        public SampleResult Result { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class PerformanceAlertEventArgs : EventArgs
    {
        public PerformanceAlert Alert { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SchedulerRecommendationEventArgs : EventArgs
    {
        public SchedulerRecommendation Recommendation { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
}