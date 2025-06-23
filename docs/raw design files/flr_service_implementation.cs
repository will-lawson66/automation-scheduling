// FlrService.cs - Fluorescence Light Reader integration service
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Instrument.Scheduler.Services
{
    public class FlrService : IFlrService
    {
        private readonly IFlrContextFactory _contextFactory;
        private readonly IFlrDataRepository _dataRepository;
        private readonly FlrConfiguration _configuration;
        private readonly ILogger<FlrService> _logger;
        private readonly object _contextLock = new object();
        
        private readonly Dictionary<Guid, IFlrAssayRunContext> _activeContexts;

        public event EventHandler<FlrDataReceivedEventArgs> OnDataReceived;
        public event EventHandler<FlrContextCreatedEventArgs> OnContextCreated;
        public event EventHandler<FlrContextClosedEventArgs> OnContextClosed;

        public FlrService(
            IFlrContextFactory contextFactory,
            IFlrDataRepository dataRepository,
            IOptions<FlrConfiguration> configuration,
            ILogger<FlrService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeContexts = new Dictionary<Guid, IFlrAssayRunContext>();
        }

        public async Task<IFlrAssayRunContext> CreateAssayRunContext()
        {
            try
            {
                _logger.LogInformation("Creating new FLR assay run context");

                var context = await _contextFactory.CreateAssayRunContext();
                
                lock (_contextLock)
                {
                    _activeContexts[context.Id] = context;
                }

                // Subscribe to context events
                context.OnDataReceived += HandleContextDataReceived;
                context.OnContextClosed += HandleContextClosed;

                OnContextCreated?.Invoke(this, new FlrContextCreatedEventArgs(context.Id));

                _logger.LogInformation("Created FLR assay run context: {ContextId}", context.Id);
                return context;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create FLR assay run context");
                throw;
            }
        }

        public async Task<bool> ReportAssayData(Guid assayId, FlrData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            try
            {
                _logger.LogDebug("Reporting FLR data for assay {AssayId}: {DataType}", assayId, data.DataType);

                // Validate data
                var validationResult = ValidateData(data);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("FLR data validation failed for assay {AssayId}: {Errors}", 
                        assayId, string.Join(", ", validationResult.Errors));
                    return false;
                }

                // Store data
                await _dataRepository.SaveFlrData(data);

                // Process data if needed
                await ProcessFlrData(data);

                OnDataReceived?.Invoke(this, new FlrDataReceivedEventArgs(data));

                _logger.LogDebug("Successfully reported FLR data for assay {AssayId}", assayId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to report FLR data for assay {AssayId}", assayId);
                return false;
            }
        }

        public async Task<List<FlrResult>> GetAssayResults(Guid assayId)
        {
            try
            {
                _logger.LogDebug("Retrieving FLR results for assay {AssayId}", assayId);

                var flrData = await _dataRepository.GetFlrDataByAssayId(assayId);
                var results = new List<FlrResult>();

                foreach (var data in flrData.OrderBy(d => d.Timestamp))
                {
                    var result = ConvertFlrDataToResult(data);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }

                _logger.LogDebug("Retrieved {ResultCount} FLR results for assay {AssayId}", results.Count, assayId);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve FLR results for assay {AssayId}", assayId);
                return new List<FlrResult>();
            }
        }

        public ValidationResult ValidateData(FlrData data)
        {
            var result = new ValidationResult(true);

            if (data == null)
            {
                result.AddError("FlrData cannot be null");
                return result;
            }

            // Validate required fields
            if (data.AssayId == Guid.Empty)
            {
                result.AddError("AssayId is required");
            }

            if (data.SequenceId == Guid.Empty)
            {
                result.AddError("SequenceId is required");
            }

            if (data.Timestamp == default)
            {
                result.AddError("Timestamp is required");
            }

            // Validate data type specific requirements
            switch (data.DataType)
            {
                case FlrDataType.RawSignal:
                    ValidateRawSignalData(data, result);
                    break;
                case FlrDataType.ProcessedSignal:
                    ValidateProcessedSignalData(data, result);
                    break;
                case FlrDataType.Calibration:
                    ValidateCalibrationData(data, result);
                    break;
                case FlrDataType.Quality:
                    ValidateQualityData(data, result);
                    break;
                case FlrDataType.Diagnostic:
                    ValidateDiagnosticData(data, result);
                    break;
            }

            // Validate measurement values
            foreach (var measurement in data.Measurements)
            {
                if (measurement.Value == null)
                {
                    result.AddWarning($"Measurement '{measurement.Key}' has null value");
                }
                else if (!IsValidMeasurementValue(measurement.Value))
                {
                    result.AddError($"Measurement '{measurement.Key}' has invalid value: {measurement.Value}");
                }
            }

            // Check data quality
            if (data.Quality == DataQuality.Poor)
            {
                result.AddWarning("Data quality is marked as poor");
            }
            else if (data.Quality == DataQuality.Invalid)
            {
                result.AddError("Data quality is marked as invalid");
            }

            return result;
        }

        public async Task<Stream> ExportData(FlrDataFilter filter)
        {
            try
            {
                _logger.LogInformation("Exporting FLR data with filter: {Filter}", filter?.ToString() ?? "All data");

                var data = await _dataRepository.GetFlrData(filter);
                var exportStream = new MemoryStream();

                using (var writer = new StreamWriter(exportStream, leaveOpen: true))
                {
                    // Write CSV header
                    await writer.WriteLineAsync("Id,AssayId,SequenceId,Timestamp,DataType,Quality,Measurements,Metadata");

                    // Write data rows
                    foreach (var flrData in data.OrderBy(d => d.Timestamp))
                    {
                        var measurementsJson = System.Text.Json.JsonSerializer.Serialize(flrData.Measurements);
                        var metadataJson = System.Text.Json.JsonSerializer.Serialize(flrData.Metadata);

                        await writer.WriteLineAsync($"{flrData.Id},{flrData.AssayId},{flrData.SequenceId}," +
                            $"{flrData.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{flrData.DataType},{flrData.Quality}," +
                            $"\"{measurementsJson}\",\"{metadataJson}\"");
                    }
                }

                exportStream.Position = 0;
                _logger.LogInformation("Exported {RecordCount} FLR data records", data.Count);
                return exportStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export FLR data");
                throw;
            }
        }

        public async Task<FlrDataSummary> GetDataSummary(Guid? assayId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var filter = new FlrDataFilter
                {
                    AssayId = assayId,
                    FromDate = fromDate,
                    ToDate = toDate
                };

                var data = await _dataRepository.GetFlrData(filter);

                var summary = new FlrDataSummary
                {
                    TotalRecords = data.Count,
                    DateRange = data.Any() ? new DateRange
                    {
                        Start = data.Min(d => d.Timestamp),
                        End = data.Max(d => d.Timestamp)
                    } : null,
                    DataTypeDistribution = data.GroupBy(d => d.DataType).ToDictionary(g => g.Key, g => g.Count()),
                    QualityDistribution = data.GroupBy(d => d.Quality).ToDictionary(g => g.Key, g => g.Count()),
                    UniqueAssays = data.Select(d => d.AssayId).Distinct().Count(),
                    UniqueSequences = data.Select(d => d.SequenceId).Distinct().Count()
                };

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate FLR data summary");
                throw;
            }
        }

        private async Task ProcessFlrData(FlrData data)
        {
            try
            {
                // Apply data processing based on type
                switch (data.DataType)
                {
                    case FlrDataType.RawSignal:
                        await ProcessRawSignalData(data);
                        break;
                    case FlrDataType.ProcessedSignal:
                        await ProcessProcessedSignalData(data);
                        break;
                    case FlrDataType.Calibration:
                        await ProcessCalibrationData(data);
                        break;
                    case FlrDataType.Quality:
                        await ProcessQualityData(data);
                        break;
                    case FlrDataType.Diagnostic:
                        await ProcessDiagnosticData(data);
                        break;
                }

                // Apply quality assessment
                AssessDataQuality(data);

                // Update metadata
                data.Metadata["ProcessedAt"] = DateTime.UtcNow;
                data.Metadata["ProcessedBy"] = "FlrService";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process FLR data: {DataId}", data.Id);
                data.Quality = DataQuality.Invalid;
                data.Metadata["ProcessingError"] = ex.Message;
            }
        }

        private async Task ProcessRawSignalData(FlrData data)
        {
            // Raw signal processing logic
            if (data.Measurements.ContainsKey("RawSignal"))
            {
                var rawValue = Convert.ToDouble(data.Measurements["RawSignal"]);
                
                // Apply baseline correction if configured
                if (_configuration.ApplyBaselineCorrection)
                {
                    var baseline = await GetBaseline(data.AssayId);
                    var correctedValue = rawValue - baseline;
                    data.Measurements["BaselineCorrectedSignal"] = correctedValue;
                }

                // Calculate signal-to-noise ratio
                if (data.Measurements.ContainsKey("NoiseLevel"))
                {
                    var noiseLevel = Convert.ToDouble(data.Measurements["NoiseLevel"]);
                    var snr = noiseLevel > 0 ? rawValue / noiseLevel : double.MaxValue;
                    data.Measurements["SignalToNoiseRatio"] = snr;
                }
            }
        }

        private async Task ProcessProcessedSignalData(FlrData data)
        {
            // Processed signal analysis
            if (data.Measurements.ContainsKey("ProcessedSignal"))
            {
                var processedValue = Convert.ToDouble(data.Measurements["ProcessedSignal"]);
                
                // Apply calibration curve if available
                var calibrationCurve = await GetCalibrationCurve(data.AssayId);
                if (calibrationCurve != null)
                {
                    var concentration = calibrationCurve.Apply(processedValue);
                    data.Measurements["Concentration"] = concentration;
                    data.Measurements["Units"] = calibrationCurve.Units;
                }
            }
        }

        private async Task ProcessCalibrationData(FlrData data)
        {
            // Calibration curve processing
            if (data.Measurements.ContainsKey("CalibrationPoints"))
            {
                // Store calibration data for future use
                await _dataRepository.SaveCalibrationData(data.AssayId, data);
            }
        }

        private async Task ProcessQualityData(FlrData data)
        {
            // Quality control processing
            var qualityScore = CalculateQualityScore(data);
            data.Measurements["QualityScore"] = qualityScore;
            
            if (qualityScore < _configuration.MinimumQualityScore)
            {
                data.Quality = DataQuality.Poor;
            }
        }

        private async Task ProcessDiagnosticData(FlrData data)
        {
            // Diagnostic data processing
            foreach (var measurement in data.Measurements)
            {
                if (IsDiagnosticParameter(measurement.Key))
                {
                    var value = Convert.ToDouble(measurement.Value);
                    var status = EvaluateDiagnosticParameter(measurement.Key, value);
                    data.Metadata[$"{measurement.Key}_Status"] = status;
                }
            }
        }

        private void AssessDataQuality(FlrData data)
        {
            var qualityFactors = new List<double>();

            // Check signal strength
            if (data.Measurements.ContainsKey("SignalStrength"))
            {
                var signalStrength = Convert.ToDouble(data.Measurements["SignalStrength"]);
                var signalQuality = signalStrength / _configuration.MaxSignalStrength;
                qualityFactors.Add(Math.Min(signalQuality, 1.0));
            }

            // Check signal-to-noise ratio
            if (data.Measurements.ContainsKey("SignalToNoiseRatio"))
            {
                var snr = Convert.ToDouble(data.Measurements["SignalToNoiseRatio"]);
                var snrQuality = Math.Min(snr / _configuration.MinimumSnr, 1.0);
                qualityFactors.Add(snrQuality);
            }

            // Check measurement stability
            if (data.Measurements.ContainsKey("Stability"))
            {
                var stability = Convert.ToDouble(data.Measurements["Stability"]);
                qualityFactors.Add(stability);
            }

            if (qualityFactors.Any())
            {
                var overallQuality = qualityFactors.Average();
                data.Measurements["OverallQuality"] = overallQuality;

                data.Quality = overallQuality switch
                {
                    >= 0.8 => DataQuality.Excellent,
                    >= 0.6 => DataQuality.Good,
                    >= 0.4 => DataQuality.Fair,
                    >= 0.2 => DataQuality.Poor,
                    _ => DataQuality.Invalid
                };
            }
        }

        private FlrResult ConvertFlrDataToResult(FlrData data)
        {
            var result = new FlrResult
            {
                Id = Guid.NewGuid(),
                AssayId = data.AssayId,
                SequenceId = data.SequenceId,
                Timestamp = data.Timestamp,
                DataType = data.DataType,
                Quality = data.Quality,
                Measurements = new Dictionary<string, object>(data.Measurements),
                Metadata = new Dictionary<string, object>(data.Metadata)
            };

            // Add calculated values
            if (data.Measurements.ContainsKey("Concentration"))
            {
                result.PrimaryValue = Convert.ToDouble(data.Measurements["Concentration"]);
                result.Units = data.Measurements.ContainsKey("Units") ? data.Measurements["Units"].ToString() : "";
            }
            else if (data.Measurements.ContainsKey("ProcessedSignal"))
            {
                result.PrimaryValue = Convert.ToDouble(data.Measurements["ProcessedSignal"]);
                result.Units = "RFU"; // Relative Fluorescence Units
            }

            result.QualityScore = data.GetQualityScore();
            result.IsValid = data.Quality != DataQuality.Invalid;

            return result;
        }

        private void HandleContextDataReceived(object sender, FlrContextDataEventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    await ReportAssayData(e.AssayId, e.Data);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle context data received event");
                }
            });
        }

        private void HandleContextClosed(object sender, FlrContextClosedEventArgs e)
        {
            lock (_contextLock)
            {
                if (_activeContexts.TryGetValue(e.ContextId, out var context))
                {
                    context.OnDataReceived -= HandleContextDataReceived;
                    context.OnContextClosed -= HandleContextClosed;
                    _activeContexts.Remove(e.ContextId);
                }
            }

            OnContextClosed?.Invoke(this, e);
            _logger.LogInformation("FLR context closed: {ContextId}", e.ContextId);
        }

        // Validation helper methods
        private void ValidateRawSignalData(FlrData data, ValidationResult result)
        {
            if (!data.Measurements.ContainsKey("RawSignal"))
            {
                result.AddError("Raw signal data must contain 'RawSignal' measurement");
            }
        }

        private void ValidateProcessedSignalData(FlrData data, ValidationResult result)
        {
            if (!data.Measurements.ContainsKey("ProcessedSignal"))
            {
                result.AddError("Processed signal data must contain 'ProcessedSignal' measurement");
            }
        }

        private void ValidateCalibrationData(FlrData data, ValidationResult result)
        {
            if (!data.Measurements.ContainsKey("CalibrationPoints"))
            {
                result.AddError("Calibration data must contain 'CalibrationPoints' measurement");
            }
        }

        private void ValidateQualityData(FlrData data, ValidationResult result)
        {
            if (!data.Measurements.ContainsKey("QualityMetric"))
            {
                result.AddWarning("Quality data should contain 'QualityMetric' measurement");
            }
        }

        private void ValidateDiagnosticData(FlrData data, ValidationResult result)
        {
            if (data.Measurements.Count == 0)
            {
                result.AddError("Diagnostic data must contain at least one measurement");
            }
        }

        private bool IsValidMeasurementValue(object value)
        {
            if (value == null) return false;

            return value switch
            {
                double d => !double.IsNaN(d) && !double.IsInfinity(d),
                float f => !float.IsNaN(f) && !float.IsInfinity(f),
                int _ => true,
                long _ => true,
                string s => !string.IsNullOrEmpty(s),
                bool _ => true,
                _ => true
            };
        }

        private bool IsDiagnosticParameter(string parameterName)
        {
            var diagnosticParameters = new[] 
            { 
                "Temperature", "Pressure", "FlowRate", "Voltage", "Current", 
                "LaserPower", "DetectorGain", "FilterPosition" 
            };
            
            return diagnosticParameters.Contains(parameterName, StringComparer.OrdinalIgnoreCase);
        }

        private string EvaluateDiagnosticParameter(string parameterName, double value)
        {
            // This would be configured based on system specifications
            return parameterName.ToLower() switch
            {
                "temperature" => value >= 20 && value <= 30 ? "Normal" : "OutOfRange",
                "pressure" => value >= 0.8 && value <= 1.2 ? "Normal" : "OutOfRange",
                "flowrate" => value >= 0.9 && value <= 1.1 ? "Normal" : "OutOfRange",
                _ => "Unknown"
            };
        }

        private double CalculateQualityScore(FlrData data)
        {
            var score = 1.0;

            // Implement quality scoring algorithm
            if (data.Measurements.ContainsKey("SignalToNoiseRatio"))
            {
                var snr = Convert.ToDouble(data.Measurements["SignalToNoiseRatio"]);
                score *= Math.Min(snr / 10.0, 1.0); // Assume SNR of 10 is excellent
            }

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        private async Task<double> GetBaseline(Guid assayId)
        {
            // Retrieve baseline for assay from repository or configuration
            return _configuration.DefaultBaseline;
        }

        private async Task<CalibrationCurve> GetCalibrationCurve(Guid assayId)
        {
            // Retrieve calibration curve from repository
            return await _dataRepository.GetCalibrationCurve(assayId);
        }
    }

    // Supporting classes and interfaces
    public interface IFlrService
    {
        Task<IFlrAssayRunContext> CreateAssayRunContext();
        Task<bool> ReportAssayData(Guid assayId, FlrData data);
        Task<List<FlrResult>> GetAssayResults(Guid assayId);
        ValidationResult ValidateData(FlrData data);
        Task<Stream> ExportData(FlrDataFilter filter);
        Task<FlrDataSummary> GetDataSummary(Guid? assayId = null, DateTime? fromDate = null, DateTime? toDate = null);
        
        event EventHandler<FlrDataReceivedEventArgs> OnDataReceived;
        event EventHandler<FlrContextCreatedEventArgs> OnContextCreated;
        event EventHandler<FlrContextClosedEventArgs> OnContextClosed;
    }

    public class FlrConfiguration
    {
        public bool ApplyBaselineCorrection { get; set; } = true;
        public double DefaultBaseline { get; set; } = 0.0;
        public double MinimumQualityScore { get; set; } = 0.6;
        public double MaxSignalStrength { get; set; } = 65535.0;
        public double MinimumSnr { get; set; } = 5.0;
        public string DataStoragePath { get; set; } = "FlrData";
        public TimeSpan DataRetentionPeriod { get; set; } = TimeSpan.FromDays(365);
        public bool EnableRealTimeProcessing { get; set; } = true;
    }

    public class FlrResult
    {
        public Guid Id { get; set; }
        public Guid AssayId { get; set; }
        public Guid SequenceId { get; set; }
        public DateTime Timestamp { get; set; }
        public FlrDataType DataType { get; set; }
        public DataQuality Quality { get; set; }
        public Dictionary<string, object> Measurements { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public double? PrimaryValue { get; set; }
        public string Units { get; set; }
        public double QualityScore { get; set; }
        public bool IsValid { get; set; }
    }

    public class FlrDataSummary
    {
        public int TotalRecords { get; set; }
        public DateRange DateRange { get; set; }
        public Dictionary<FlrDataType, int> DataTypeDistribution { get; set; } = new();
        public Dictionary<DataQuality, int> QualityDistribution { get; set; } = new();
        public int UniqueAssays { get; set; }
        public int UniqueSequences { get; set; }
    }

    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public TimeSpan Duration => End - Start;
    }

    public class CalibrationCurve
    {
        public Guid AssayId { get; set; }
        public string Units { get; set; }
        public List<CalibrationPoint> Points { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        
        public double Apply(double signalValue)
        {
            // Implement calibration curve interpolation
            // This is a simplified linear interpolation
            if (Points.Count < 2) return signalValue;
            
            var sortedPoints = Points.OrderBy(p => p.Signal).ToList();
            
            // Find surrounding points
            var lowerPoint = sortedPoints.LastOrDefault(p => p.Signal <= signalValue);
            var upperPoint = sortedPoints.FirstOrDefault(p => p.Signal >= signalValue);
            
            if (lowerPoint == null) return sortedPoints.First().Concentration;
            if (upperPoint == null) return sortedPoints.Last().Concentration;
            if (lowerPoint == upperPoint) return lowerPoint.Concentration;
            
            // Linear interpolation
            var ratio = (signalValue - lowerPoint.Signal) / (upperPoint.Signal - lowerPoint.Signal);
            return lowerPoint.Concentration + ratio * (upperPoint.Concentration - lowerPoint.Concentration);
        }
    }

    public class CalibrationPoint
    {
        public double Signal { get; set; }
        public double Concentration { get; set; }
    }

    // Event argument classes
    public class FlrDataReceivedEventArgs : EventArgs
    {
        public FlrData Data { get; }
        public DateTime ReceivedAt { get; }

        public FlrDataReceivedEventArgs(FlrData data)
        {
            Data = data;
            ReceivedAt = DateTime.UtcNow;
        }
    }

    public class FlrContextCreatedEventArgs : EventArgs
    {
        public Guid ContextId { get; }
        public DateTime CreatedAt { get; }

        public FlrContextCreatedEventArgs(Guid contextId)
        {
            ContextId = contextId;
            CreatedAt = DateTime.UtcNow;
        }
    }

    public class FlrContextClosedEventArgs : EventArgs
    {
        public Guid ContextId { get; }
        public DateTime ClosedAt { get; }

        public FlrContextClosedEventArgs(Guid contextId)
        {
            ContextId = contextId;
            ClosedAt = DateTime.UtcNow;
        }
    }

    // Enums
    public enum DataQuality
    {
        Excellent,
        Good,
        Fair,
        Poor,
        Invalid
    }
}