// PerformanceMonitoringService.cs - Comprehensive performance monitoring implementation
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;

namespace Instrument.Scheduler.Monitoring
{
    public class PerformanceMonitoringService : IPerformanceMonitoringService, IHostedService, IDisposable
    {
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private readonly PerformanceMonitoringOptions _options;
        private readonly Timer _metricsCollectionTimer;
        private readonly Timer _reportingTimer;
        
        // Metrics tracking
        private readonly ConcurrentDictionary<string, PerformanceCounter> _performanceCounters;
        private readonly ConcurrentDictionary<string, MetricHistory> _metricHistories;
        private readonly Meter _meter;
        
        // Performance counters
        private readonly Counter<long> _samplesProcessedCounter;
        private readonly Counter<long> _sequencesExecutedCounter;
        private readonly Counter<long> _errorsCounter;
        private readonly Histogram<double> _sampleProcessingDuration;
        private readonly Histogram<double> _sequenceExecutionDuration;
        private readonly Histogram<double> _inventoryCheckDuration;
        private readonly ObservableGauge<int> _activeSamplesGauge;
        private readonly ObservableGauge<int> _queuedSamplesGauge;
        private readonly ObservableGauge<double> _systemCpuUsage;
        private readonly ObservableGauge<double> _systemMemoryUsage;
        private readonly ObservableGauge<double> _applicationMemoryUsage;

        // System monitoring
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly Process _currentProcess;

        // Dependencies for metric collection
        private readonly IAssayManager _assayManager;
        private readonly IInventoryService _inventoryService;
        private readonly ISequenceGroupManager _sequenceGroupManager;

        public event EventHandler<PerformanceAlertEventArgs> OnPerformanceAlert;
        public event EventHandler<MetricThresholdExceededEventArgs> OnMetricThresholdExceeded;

        public PerformanceMonitoringService(
            IAssayManager assayManager,
            IInventoryService inventoryService,
            ISequenceGroupManager sequenceGroupManager,
            IOptions<PerformanceMonitoringOptions> options,
            ILogger<PerformanceMonitoringService> logger)
        {
            _assayManager = assayManager ?? throw new ArgumentNullException(nameof(assayManager));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _sequenceGroupManager = sequenceGroupManager ?? throw new ArgumentNullException(nameof(sequenceGroupManager));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _performanceCounters = new ConcurrentDictionary<string, PerformanceCounter>();
            _metricHistories = new ConcurrentDictionary<string, MetricHistory>();
            _currentProcess = Process.GetCurrentProcess();

            // Initialize .NET metrics
            _meter = new Meter("Scheduler.Performance", "1.0.0");
            
            _samplesProcessedCounter = _meter.CreateCounter<long>(
                "scheduler_samples_processed_total",
                description: "Total number of samples processed");
                
            _sequencesExecutedCounter = _meter.CreateCounter<long>(
                "scheduler_sequences_executed_total", 
                description: "Total number of sequences executed");
                
            _errorsCounter = _meter.CreateCounter<long>(
                "scheduler_errors_total",
                description: "Total number of errors occurred");
                
            _sampleProcessingDuration = _meter.CreateHistogram<double>(
                "scheduler_sample_processing_duration_seconds",
                "s",
                "Duration of sample processing");
                
            _sequenceExecutionDuration = _meter.CreateHistogram<double>(
                "scheduler_sequence_execution_duration_seconds",
                "s", 
                "Duration of sequence execution");
                
            _inventoryCheckDuration = _meter.CreateHistogram<double>(
                "scheduler_inventory_check_duration_seconds",
                "s",
                "Duration of inventory checks");

            // Observable gauges
            _activeSamplesGauge = _meter.CreateObservableGauge<int>(
                "scheduler_active_samples",
                () => GetActiveSamplesCount(),
                description: "Number of currently active samples");
                
            _queuedSamplesGauge = _meter.CreateObservableGauge<int>(
                "scheduler_queued_samples", 
                () => GetQueuedSamplesCount(),
                description: "Number of queued samples");
                
            _systemCpuUsage = _meter.CreateObservableGauge<double>(
                "system_cpu_usage_percent",
                () => GetSystemCpuUsage(),
                "%",
                "System CPU usage percentage");
                
            _systemMemoryUsage = _meter.CreateObservableGauge<double>(
                "system_memory_usage_percent",
                () => GetSystemMemoryUsage(), 
                "%",
                "System memory usage percentage");
                
            _applicationMemoryUsage = _meter.CreateObservableGauge<double>(
                "application_memory_usage_mb",
                () => GetApplicationMemoryUsage(),
                "MB",
                "Application memory usage in megabytes");

            // Initialize system performance counters
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize system performance counters");
            }

            // Set up timers
            _metricsCollectionTimer = new Timer(CollectMetrics, null, 
                TimeSpan.Zero, _options.MetricsCollectionInterval);
            _reportingTimer = new Timer(GenerateReport, null,
                _options.ReportingInterval, _options.ReportingInterval);

            _logger.LogInformation("Performance monitoring service initialized");
        }

        public void RecordSampleProcessed(Guid sampleId, TimeSpan duration, bool successful)
        {
            _samplesProcessedCounter.Add(1, new KeyValuePair<string, object?>("successful", successful));
            _sampleProcessingDuration.Record(duration.TotalSeconds, 
                new KeyValuePair<string, object?>("sample_id", sampleId.ToString()));

            if (!successful)
            {
                _errorsCounter.Add(1, new KeyValuePair<string, object?>("type", "sample_processing"));
            }

            // Update metric history
            UpdateMetricHistory("sample_processing_duration", duration.TotalSeconds);
            UpdateMetricHistory("samples_processed_rate", 1);

            _logger.LogDebug("Recorded sample processing: {SampleId}, Duration: {Duration}, Successful: {Successful}",
                sampleId, duration, successful);
        }

        public void RecordSequenceExecuted(Guid sequenceId, TimeSpan duration, bool successful)
        {
            _sequencesExecutedCounter.Add(1, new KeyValuePair<string, object?>("successful", successful));
            _sequenceExecutionDuration.Record(duration.TotalSeconds,
                new KeyValuePair<string, object?>("sequence_id", sequenceId.ToString()));

            if (!successful)
            {
                _errorsCounter.Add(1, new KeyValuePair<string, object?>("type", "sequence_execution"));
            }

            UpdateMetricHistory("sequence_execution_duration", duration.TotalSeconds);
            UpdateMetricHistory("sequences_executed_rate", 1);

            _logger.LogDebug("Recorded sequence execution: {SequenceId}, Duration: {Duration}, Successful: {Successful}",
                sequenceId, duration, successful);
        }

        public void RecordInventoryCheck(TimeSpan duration, int itemsChecked, bool successful)
        {
            _inventoryCheckDuration.Record(duration.TotalSeconds,
                new KeyValuePair<string, object?>("items_checked", itemsChecked),
                new KeyValuePair<string, object?>("successful", successful));

            if (!successful)
            {
                _errorsCounter.Add(1, new KeyValuePair<string, object?>("type", "inventory_check"));
            }

            UpdateMetricHistory("inventory_check_duration", duration.TotalSeconds);
            UpdateMetricHistory("inventory_checks_rate", 1);
        }

        public void RecordError(string errorType, string errorMessage, Exception exception = null)
        {
            _errorsCounter.Add(1, 
                new KeyValuePair<string, object?>("type", errorType),
                new KeyValuePair<string, object?>("message", errorMessage));

            UpdateMetricHistory($"errors_{errorType}_rate", 1);

            _logger.LogError(exception, "Recorded error: Type={ErrorType}, Message={ErrorMessage}", 
                errorType, errorMessage);

            // Check for error rate thresholds
            CheckErrorRateThresholds(errorType);
        }

        public PerformanceReport GeneratePerformanceReport()
        {
            var report = new PerformanceReport
            {
                GeneratedAt = DateTime.UtcNow,
                ReportingPeriod = _options.ReportingInterval,
                SystemMetrics = CollectSystemMetrics(),
                ApplicationMetrics = CollectApplicationMetrics(),
                BusinessMetrics = CollectBusinessMetrics(),
                Alerts = GetActiveAlerts(),
                Recommendations = GenerateRecommendations()
            };

            _logger.LogInformation("Generated performance report with {MetricCount} metrics and {AlertCount} alerts",
                report.ApplicationMetrics.Count + report.SystemMetrics.Count + report.BusinessMetrics.Count,
                report.Alerts.Count);

            return report;
        }

        public List<PerformanceAlert> GetActiveAlerts()
        {
            var alerts = new List<PerformanceAlert>();

            // Check CPU usage
            var cpuUsage = GetSystemCpuUsage();
            if (cpuUsage > _options.CpuUsageThreshold)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighCpuUsage,
                    Severity = cpuUsage > _options.CpuCriticalThreshold ? AlertSeverity.Critical : AlertSeverity.Warning,
                    Message = $"CPU usage is {cpuUsage:F1}%",
                    Value = cpuUsage,
                    Threshold = _options.CpuUsageThreshold,
                    DetectedAt = DateTime.UtcNow
                });
            }

            // Check memory usage
            var memoryUsage = GetApplicationMemoryUsage();
            if (memoryUsage > _options.MemoryUsageThresholdMB)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighMemoryUsage,
                    Severity = memoryUsage > _options.MemoryCriticalThresholdMB ? AlertSeverity.Critical : AlertSeverity.Warning,
                    Message = $"Memory usage is {memoryUsage:F1} MB",
                    Value = memoryUsage,
                    Threshold = _options.MemoryUsageThresholdMB,
                    DetectedAt = DateTime.UtcNow
                });
            }

            // Check processing time
            if (_metricHistories.TryGetValue("sample_processing_duration", out var processingHistory))
            {
                var avgProcessingTime = processingHistory.GetAverageOverPeriod(TimeSpan.FromMinutes(10));
                if (avgProcessingTime > _options.MaxAverageProcessingTimeSeconds)
                {
                    alerts.Add(new PerformanceAlert
                    {
                        Type = AlertType.SlowProcessing,
                        Severity = AlertSeverity.Warning,
                        Message = $"Average processing time is {avgProcessingTime:F1} seconds",
                        Value = avgProcessingTime,
                        Threshold = _options.MaxAverageProcessingTimeSeconds,
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            // Check error rates
            CheckErrorRates(alerts);

            return alerts;
        }

        public Dictionary<string, object> GetCurrentMetrics()
        {
            var metrics = new Dictionary<string, object>
            {
                ["system_cpu_usage"] = GetSystemCpuUsage(),
                ["system_memory_usage"] = GetSystemMemoryUsage(),
                ["application_memory_usage"] = GetApplicationMemoryUsage(),
                ["active_samples"] = GetActiveSamplesCount(),
                ["queued_samples"] = GetQueuedSamplesCount(),
                ["total_samples_processed"] = GetTotalSamplesProcessed(),
                ["total_sequences_executed"] = GetTotalSequencesExecuted(),
                ["error_rate"] = GetCurrentErrorRate(),
                ["uptime"] = GetApplicationUptime()
            };

            // Add metric histories
            foreach (var history in _metricHistories)
            {
                metrics[$"{history.Key}_avg_1min"] = history.Value.GetAverageOverPeriod(TimeSpan.FromMinutes(1));
                metrics[$"{history.Key}_avg_5min"] = history.Value.GetAverageOverPeriod(TimeSpan.FromMinutes(5));
                metrics[$"{history.Key}_avg_15min"] = history.Value.GetAverageOverPeriod(TimeSpan.FromMinutes(15));
            }

            return metrics;
        }

        private void CollectMetrics(object state)
        {
            try
            {
                // Update metric histories with current values
                UpdateMetricHistory("system_cpu_usage", GetSystemCpuUsage());
                UpdateMetricHistory("system_memory_usage", GetSystemMemoryUsage());
                UpdateMetricHistory("application_memory_usage", GetApplicationMemoryUsage());
                UpdateMetricHistory("active_samples", GetActiveSamplesCount());
                UpdateMetricHistory("queued_samples", GetQueuedSamplesCount());

                // Check thresholds and generate alerts
                var alerts = GetActiveAlerts();
                foreach (var alert in alerts.Where(a => a.Severity >= AlertSeverity.Warning))
                {
                    OnPerformanceAlert?.Invoke(this, new PerformanceAlertEventArgs(alert));
                }

                _logger.LogTrace("Metrics collection completed. {AlertCount} alerts generated", alerts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics collection");
            }
        }

        private void GenerateReport(object state)
        {
            try
            {
                var report = GeneratePerformanceReport();
                
                // Log key metrics
                _logger.LogInformation("Performance Report - CPU: {CpuUsage:F1}%, Memory: {MemoryUsage:F1}MB, " +
                    "Active Samples: {ActiveSamples}, Queued: {QueuedSamples}, Alerts: {AlertCount}",
                    report.SystemMetrics.GetValueOrDefault("cpu_usage", 0),
                    report.ApplicationMetrics.GetValueOrDefault("memory_usage", 0),
                    report.BusinessMetrics.GetValueOrDefault("active_samples", 0),
                    report.BusinessMetrics.GetValueOrDefault("queued_samples", 0),
                    report.Alerts.Count);

                // Save report if configured
                if (_options.SaveReportsToFile)
                {
                    SaveReportToFile(report);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during report generation");
            }
        }

        private void UpdateMetricHistory(string metricName, double value)
        {
            var history = _metricHistories.GetOrAdd(metricName, _ => new MetricHistory(_options.MetricHistorySize));
            history.AddValue(value, DateTime.UtcNow);
        }

        private int GetActiveSamplesCount()
        {
            try
            {
                return _assayManager.GetAssaySamplesByStatus(AssayStatus.InProgress).Count;
            }
            catch
            {
                return 0;
            }
        }

        private int GetQueuedSamplesCount()
        {
            try
            {
                return _assayManager.GetAssaySamplesByStatus(AssayStatus.Queued).Count +
                       _assayManager.GetAssaySamplesByStatus(AssayStatus.InventoryReserved).Count;
            }
            catch
            {
                return 0;
            }
        }

        private double GetSystemCpuUsage()
        {
            try
            {
                return _cpuCounter?.NextValue() ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private double GetSystemMemoryUsage()
        {
            try
            {
                var availableMemoryMB = _memoryCounter?.NextValue() ?? 0;
                var totalMemoryMB = GetTotalSystemMemoryMB();
                return totalMemoryMB > 0 ? ((totalMemoryMB - availableMemoryMB) / totalMemoryMB) * 100 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private double GetApplicationMemoryUsage()
        {
            try
            {
                return _currentProcess.WorkingSet64 / (1024.0 * 1024.0); // Convert to MB
            }
            catch
            {
                return 0;
            }
        }

        private double GetTotalSystemMemoryMB()
        {
            try
            {
                var computerInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
                return computerInfo.TotalPhysicalMemory / (1024.0 * 1024.0);
            }
            catch
            {
                return 0;
            }
        }

        private long GetTotalSamplesProcessed()
        {
            // This would typically come from a persistent counter
            return _metricHistories.TryGetValue("samples_processed_rate", out var history) 
                ? (long)history.GetTotalSum() : 0;
        }

        private long GetTotalSequencesExecuted()
        {
            return _metricHistories.TryGetValue("sequences_executed_rate", out var history)
                ? (long)history.GetTotalSum() : 0;
        }

        private double GetCurrentErrorRate()
        {
            var errorHistories = _metricHistories.Where(kvp => kvp.Key.Contains("errors_")).ToList();
            if (!errorHistories.Any()) return 0;

            var totalErrors = errorHistories.Sum(h => h.Value.GetSumOverPeriod(TimeSpan.FromMinutes(5)));
            var totalOperations = GetTotalSamplesProcessed() + GetTotalSequencesExecuted();
            
            return totalOperations > 0 ? (totalErrors / totalOperations) * 100 : 0;
        }

        private TimeSpan GetApplicationUptime()
        {
            return DateTime.UtcNow - _currentProcess.StartTime.ToUniversalTime();
        }

        private Dictionary<string, double> CollectSystemMetrics()
        {
            return new Dictionary<string, double>
            {
                ["cpu_usage"] = GetSystemCpuUsage(),
                ["memory_usage"] = GetSystemMemoryUsage(),
                ["available_memory_mb"] = _memoryCounter?.NextValue() ?? 0,
                ["disk_usage"] = GetDiskUsage(),
                ["network_throughput"] = GetNetworkThroughput()
            };
        }

        private Dictionary<string, double> CollectApplicationMetrics()
        {
            return new Dictionary<string, double>
            {
                ["memory_usage"] = GetApplicationMemoryUsage(),
                ["gc_collections_gen0"] = GC.CollectionCount(0),
                ["gc_collections_gen1"] = GC.CollectionCount(1),
                ["gc_collections_gen2"] = GC.CollectionCount(2),
                ["thread_count"] = _currentProcess.Threads.Count,
                ["handle_count"] = _currentProcess.HandleCount
            };
        }

        private Dictionary<string, double> CollectBusinessMetrics()
        {
            return new Dictionary<string, double>
            {
                ["active_samples"] = GetActiveSamplesCount(),
                ["queued_samples"] = GetQueuedSamplesCount(),
                ["completed_samples"] = _assayManager.GetAssaySamplesByStatus(AssayStatus.Completed).Count,
                ["failed_samples"] = _assayManager.GetAssaySamplesByStatus(AssayStatus.Failed).Count,
                ["total_samples_processed"] = GetTotalSamplesProcessed(),
                ["total_sequences_executed"] = GetTotalSequencesExecuted(),
                ["error_rate"] = GetCurrentErrorRate(),
                ["throughput_samples_per_hour"] = CalculateThroughput()
            };
        }

        private double GetDiskUsage()
        {
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
                var totalSize = drives.Sum(d => d.TotalSize);
                var freeSpace = drives.Sum(d => d.AvailableFreeSpace);
                return totalSize > 0 ? ((totalSize - freeSpace) / (double)totalSize) * 100 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private double GetNetworkThroughput()
        {
            // Implementation would depend on specific network monitoring requirements
            return 0;
        }

        private double CalculateThroughput()
        {
            if (_metricHistories.TryGetValue("samples_processed_rate", out var history))
            {
                var samplesLastHour = history.GetSumOverPeriod(TimeSpan.FromHours(1));
                return samplesLastHour;
            }
            return 0;
        }

        private void CheckErrorRateThresholds(string errorType)
        {
            var historyKey = $"errors_{errorType}_rate";
            if (_metricHistories.TryGetValue(historyKey, out var history))
            {
                var errorRate = history.GetSumOverPeriod(TimeSpan.FromMinutes(5));
                if (errorRate > _options.MaxErrorRatePerMinute)
                {
                    OnMetricThresholdExceeded?.Invoke(this, new MetricThresholdExceededEventArgs
                    {
                        MetricName = historyKey,
                        CurrentValue = errorRate,
                        Threshold = _options.MaxErrorRatePerMinute,
                        ExceededAt = DateTime.UtcNow
                    });
                }
            }
        }

        private void CheckErrorRates(List<PerformanceAlert> alerts)
        {
            var errorRate = GetCurrentErrorRate();
            if (errorRate > _options.MaxErrorRatePercent)
            {
                alerts.Add(new PerformanceAlert
                {
                    Type = AlertType.HighErrorRate,
                    Severity = errorRate > _options.CriticalErrorRatePercent ? AlertSeverity.Critical : AlertSeverity.Warning,
                    Message = $"Error rate is {errorRate:F2}%",
                    Value = errorRate,
                    Threshold = _options.MaxErrorRatePercent,
                    DetectedAt = DateTime.UtcNow
                });
            }
        }

        private List<string> GenerateRecommendations()
        {
            var recommendations = new List<string>();

            // CPU recommendations
            var cpuUsage = GetSystemCpuUsage();
            if (cpuUsage > 80)
            {
                recommendations.Add("Consider scaling up CPU resources or optimizing CPU-intensive operations");
            }

            // Memory recommendations
            var memoryUsage = GetApplicationMemoryUsage();
            if (memoryUsage > _options.MemoryUsageThresholdMB)
            {
                recommendations.Add("Monitor memory usage patterns and consider implementing memory optimization");
            }

            // Throughput recommendations
            var activeSamples = GetActiveSamplesCount();
            var queuedSamples = GetQueuedSamplesCount();
            if (queuedSamples > activeSamples * 2)
            {
                recommendations.Add("Consider increasing parallel processing capacity to reduce queue buildup");
            }

            return recommendations;
        }

        private void SaveReportToFile(PerformanceReport report)
        {
            try
            {
                var fileName = $"performance_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(_options.ReportOutputPath, fileName);
                
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                
                var json = System.Text.Json.JsonSerializer.Serialize(report, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(filePath, json);
                
                _logger.LogInformation("Performance report saved to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save performance report to file");
            }
        }

        // IHostedService implementation
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performance monitoring service starting");
            
            // Subscribe to component events
            _assayManager.OnAssaySampleStatusChanged += HandleAssaySampleStatusChanged;
            _sequenceGroupManager.OnSequenceGroupCompleted += HandleSequenceGroupCompleted;
            _inventoryService.OnInventoryChanged += HandleInventoryChanged;
            
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performance monitoring service stopping");
            
            // Unsubscribe from events
            _assayManager.OnAssaySampleStatusChanged -= HandleAssaySampleStatusChanged;
            _sequenceGroupManager.OnSequenceGroupCompleted -= HandleSequenceGroupCompleted;
            _inventoryService.OnInventoryChanged -= HandleInventoryChanged;
            
            await Task.CompletedTask;
        }

        private void HandleAssaySampleStatusChanged(object sender, AssayStatusChangedEventArgs e)
        {
            // Track status change metrics
            UpdateMetricHistory($"status_changes_{e.NewStatus}", 1);
        }

        private void HandleSequenceGroupCompleted(object sender, SequenceGroupCompletedEventArgs e)
        {
            // This would be called when sequence groups complete
            var duration = e.Result.TotalDuration;
            RecordSequenceExecuted(e.SequenceGroupId, duration, e.Result.IsSuccess);
        }

        private void HandleInventoryChanged(object sender, InventoryChangedEventArgs e)
        {
            // Track inventory changes
            UpdateMetricHistory("inventory_changes", 1);
        }

        public void Dispose()
        {
            _metricsCollectionTimer?.Dispose();
            _reportingTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            _currentProcess?.Dispose();
            _meter?.Dispose();
        }
    }

    // Supporting classes and interfaces
    public interface IPerformanceMonitoringService
    {
        void RecordSampleProcessed(Guid sampleId, TimeSpan duration, bool successful);
        void RecordSequenceExecuted(Guid sequenceId, TimeSpan duration, bool successful);
        void RecordInventoryCheck(TimeSpan duration, int itemsChecked, bool successful);
        void RecordError(string errorType, string errorMessage, Exception exception = null);
        PerformanceReport GeneratePerformanceReport();
        List<PerformanceAlert> GetActiveAlerts();
        Dictionary<string, object> GetCurrentMetrics();
        
        event EventHandler<PerformanceAlertEventArgs> OnPerformanceAlert;
        event EventHandler<MetricThresholdExceededEventArgs> OnMetricThresholdExceeded;
    }

    public class PerformanceMonitoringOptions
    {
        public TimeSpan MetricsCollectionInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan ReportingInterval { get; set; } = TimeSpan.FromMinutes(15);
        public int MetricHistorySize { get; set; } = 1000;
        public double CpuUsageThreshold { get; set; } = 80.0;
        public double CpuCriticalThreshold { get; set; } = 95.0;
        public double MemoryUsageThresholdMB { get; set; } = 1024.0;
        public double MemoryCriticalThresholdMB { get; set; } = 2048.0;
        public double MaxAverageProcessingTimeSeconds { get; set; } = 300.0;
        public double MaxErrorRatePercent { get; set; } = 5.0;
        public double CriticalErrorRatePercent { get; set; } = 10.0;
        public double MaxErrorRatePerMinute { get; set; } = 10.0;
        public bool SaveReportsToFile { get; set; } = true;
        public string ReportOutputPath { get; set; } = "./reports";
    }

    public class MetricHistory
    {
        private readonly Queue<(double Value, DateTime Timestamp)> _values;
        private readonly int _maxSize;
        private readonly object _lock = new object();

        public MetricHistory(int maxSize)
        {
            _maxSize = maxSize;
            _values = new Queue<(double, DateTime)>();
        }

        public void AddValue(double value, DateTime timestamp)
        {
            lock (_lock)
            {
                _values.Enqueue((value, timestamp));
                
                while (_values.Count > _maxSize)
                {
                    _values.Dequeue();
                }
            }
        }

        public double GetAverageOverPeriod(TimeSpan period)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow - period;
                var relevantValues = _values.Where(v => v.Timestamp >= cutoff).ToList();
                
                return relevantValues.Any() ? relevantValues.Average(v => v.Value) : 0;
            }
        }

        public double GetSumOverPeriod(TimeSpan period)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow - period;
                return _values.Where(v => v.Timestamp >= cutoff).Sum(v => v.Value);
            }
        }

        public double GetTotalSum()
        {
            lock (_lock)
            {
                return _values.Sum(v => v.Value);
            }
        }
    }

    public class PerformanceReport
    {
        public DateTime GeneratedAt { get; set; }
        public TimeSpan ReportingPeriod { get; set; }
        public Dictionary<string, double> SystemMetrics { get; set; } = new();
        public Dictionary<string, double> ApplicationMetrics { get; set; } = new();
        public Dictionary<string, double> BusinessMetrics { get; set; } = new();
        public List<PerformanceAlert> Alerts { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class PerformanceAlert
    {
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    // Event argument classes
    public class PerformanceAlertEventArgs : EventArgs
    {
        public PerformanceAlert Alert { get; }

        public PerformanceAlertEventArgs(PerformanceAlert alert)
        {
            Alert = alert;
        }
    }

    public class MetricThresholdExceededEventArgs : EventArgs
    {
        public string MetricName { get; set; }
        public double CurrentValue { get; set; }
        public double Threshold { get; set; }
        public DateTime ExceededAt { get; set; }
    }

    // Enums
    public enum AlertType
    {
        HighCpuUsage,
        HighMemoryUsage,
        SlowProcessing,
        HighErrorRate,
        QueueBuildup,
        ResourceExhaustion
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }
}