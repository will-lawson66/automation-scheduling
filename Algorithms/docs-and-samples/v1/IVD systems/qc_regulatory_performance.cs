using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FluidHandling.Core.Models;
using FluidHandling.Core.Interfaces;

namespace FluidHandling.IVDSpecific
{
    /// <summary>
    /// Quality Control Scheduler for IVD Systems
    /// Ensures quality control samples are processed according to regulatory requirements
    /// Based on FDA and CE-IVD guidelines for diagnostic testing
    /// </summary>
    public class QualityControlScheduler : IScheduler
    {
        public string Name => "Quality Control Scheduler";
        public string Description => "Ensures QC samples meet regulatory requirements and quality standards";
        public SchedulerType Type => SchedulerType.IVDSpecific;

        private readonly List<QualityControlOperation> _operations;
        private readonly List<FluidInstrument> _instruments;
        private readonly QualityControlConfig _config;
        private readonly Dictionary<string, QualityControlProfile> _qcProfiles;
        private PerformanceMetrics _performanceMetrics;

        public QualityControlScheduler(List<FluidInstrument> instruments, QualityControlConfig config = null)
        {
            _instruments = instruments;
            _operations = new List<QualityControlOperation>();
            _config = config ?? new QualityControlConfig();
            _qcProfiles = InitializeQCProfiles();
            _performanceMetrics = new PerformanceMetrics();
        }

        public void AddOperation(FluidOperation operation)
        {
            var qcOperation = operation as QualityControlOperation ?? 
                ConvertToQCOperation(operation);
            _operations.Add(qcOperation);
        }

        public void AddOperations(IEnumerable<FluidOperation> operations)
        {
            foreach (var operation in operations)
            {
                AddOperation(operation);
            }
        }

        public async Task<SchedulingSolution> GenerateScheduleAsync()
        {
            _performanceMetrics.MeasurementStart = DateTime.Now;
            
            Console.WriteLine("[QC Scheduler] Starting quality control scheduling...");
            
            // Phase 1: Classify operations by QC requirements
            var classifiedOperations = ClassifyOperationsByQCRequirements();
            
            // Phase 2: Schedule mandatory QC operations
            var solution = await ScheduleMandatoryQCOperations(classifiedOperations);
            
            // Phase 3: Integrate routine samples with QC schedule
            await IntegrateRoutineSamples(solution, classifiedOperations);
            
            // Phase 4: Validate regulatory compliance
            ValidateRegulatoryCompliance(solution);
            
            // Phase 5: Optimize for quality metrics
            OptimizeForQualityMetrics(solution);
            
            _performanceMetrics.FinalizeMeasurement();
            _performanceMetrics.QualityScore = CalculateQualityScore(solution);
            _performanceMetrics.RegulatoryComplianceScore = CalculateComplianceScore(solution);
            
            Console.WriteLine($"[QC Scheduler] Completed with quality score: {_performanceMetrics.QualityScore:F3}");
            
            return solution;
        }

        public async Task<FluidOperation> ScheduleNextAsync()
        {
            // Prioritize QC operations based on regulatory requirements
            var nextOperation = _operations
                .Where(op => !op.IsCompleted && ShouldScheduleNow(op))
                .OrderByDescending(op => op.QCPriority)
                .ThenBy(op => op.RegulatoryDeadline)
                .FirstOrDefault();

            if (nextOperation != null)
            {
                var availableInstrument = _instruments
                    .FirstOrDefault(inst => inst.IsAvailable && 
                                          nextOperation.CanExecuteOn(inst) &&
                                          IsInstrumentQualified(inst, nextOperation));

                if (availableInstrument != null)
                {
                    await ExecuteQCOperation(nextOperation, availableInstrument);
                    return nextOperation;
                }
            }

            return null;
        }

        public void UpdateConfiguration(SchedulerConfiguration config)
        {
            if (config.Parameters.ContainsKey("QCFrequency"))
            {
                _config.QCFrequency = TimeSpan.FromHours((double)config.Parameters["QCFrequency"]);
            }
            if (config.Parameters.ContainsKey("RegulatoryMode"))
            {
                _config.RegulatoryMode = (RegulatoryMode)config.Parameters["RegulatoryMode"];
            }
        }

        public PerformanceMetrics GetPerformanceMetrics()
        {
            return _performanceMetrics;
        }

        private Dictionary<string, QualityControlProfile> InitializeQCProfiles()
        {
            return new Dictionary<string, QualityControlProfile>
            {
                ["Positive Control"] = new QualityControlProfile
                {
                    Name = "Positive Control",
                    RequiredFrequency = TimeSpan.FromHours(8),
                    AcceptanceCriteria = new List<string> { "Result > 1000 units", "CV < 5%" },
                    RegulatoryRequirement = RegulatoryRequirement.Mandatory,
                    QCType = QualityControlType.Positive,
                    MaxAllowedFailures = 1,
                    MinSampleSize = 3
                },
                ["Negative Control"] = new QualityControlProfile
                {
                    Name = "Negative Control",
                    RequiredFrequency = TimeSpan.FromHours(8),
                    AcceptanceCriteria = new List<string> { "Result < 50 units", "No contamination" },
                    RegulatoryRequirement = RegulatoryRequirement.Mandatory,
                    QCType = QualityControlType.Negative,
                    MaxAllowedFailures = 0,
                    MinSampleSize = 2
                },
                ["Calibrator"] = new QualityControlProfile
                {
                    Name = "Calibrator",
                    RequiredFrequency = TimeSpan.FromHours(24),
                    AcceptanceCriteria = new List<string> { "R² > 0.995", "Slope 0.9-1.1" },
                    RegulatoryRequirement = RegulatoryRequirement.Mandatory,
                    QCType = QualityControlType.Calibrator,
                    MaxAllowedFailures = 0,
                    MinSampleSize = 5
                },
                ["Proficiency Test"] = new QualityControlProfile
                {
                    Name = "Proficiency Test",
                    RequiredFrequency = TimeSpan.FromDays(90),
                    AcceptanceCriteria = new List<string> { "Within 2 SD of target", "Z-score < 2.0" },
                    RegulatoryRequirement = RegulatoryRequirement.Mandatory,
                    QCType = QualityControlType.Proficiency,
                    MaxAllowedFailures = 0,
                    MinSampleSize = 1
                }
            };
        }

        private QualityControlOperation ConvertToQCOperation(FluidOperation operation)
        {
            var qcType = DetermineQCType(operation);
            var profile = _qcProfiles.ContainsKey(qcType) 
                ? _qcProfiles[qcType] 
                : _qcProfiles["Positive Control"];

            return new QualityControlOperation
            {
                Id = operation.Id,
                SampleId = operation.SampleId,
                OperationType = operation.OperationType,
                VolumeInMicroliters = operation.VolumeInMicroliters,
                EstimatedDurationMs = operation.EstimatedDurationMs,
                Priority = operation.Priority,
                SubmissionTime = operation.SubmissionTime,
                Deadline = operation.Deadline,
                SourceLocation = operation.SourceLocation,
                DestinationLocation = operation.DestinationLocation,
                QCType = qcType,
                QCProfile = profile,
                RegulatoryDeadline = CalculateRegulatoryDeadline(operation, profile),
                QCPriority = CalculateQCPriority(qcType, profile),
                AcceptanceCriteria = profile.AcceptanceCriteria,
                IsRegulatoryRequired = profile.RegulatoryRequirement == RegulatoryRequirement.Mandatory
            };
        }

        private string DetermineQCType(FluidOperation operation)
        {
            var sampleId = operation.SampleId.ToLower();
            var operationType = operation.OperationType.ToLower();

            if (sampleId.Contains("pos") || operationType.Contains("positive"))
                return "Positive Control";
            if (sampleId.Contains("neg") || operationType.Contains("negative"))
                return "Negative Control";
            if (sampleId.Contains("cal") || operationType.Contains("calibrat"))
                return "Calibrator";
            if (sampleId.Contains("pt") || operationType.Contains("proficiency"))
                return "Proficiency Test";

            return "Positive Control"; // Default
        }

        private DateTime CalculateRegulatoryDeadline(FluidOperation operation, QualityControlProfile profile)
        {
            var baseDeadline = operation.Deadline;
            var regulatoryWindow = profile.RequiredFrequency;
            
            // For regulatory samples, deadline is stricter
            if (profile.RegulatoryRequirement == RegulatoryRequirement.Mandatory)
            {
                return DateTime.Min(baseDeadline, operation.SubmissionTime.Add(regulatoryWindow));
            }
            
            return baseDeadline;
        }

        private int CalculateQCPriority(string qcType, QualityControlProfile profile)
        {
            var basePriority = profile.RegulatoryRequirement == RegulatoryRequirement.Mandatory ? 10 : 5;
            
            return qcType switch
            {
                "Calibrator" => basePriority + 3,
                "Negative Control" => basePriority + 2,
                "Positive Control" => basePriority + 1,
                "Proficiency Test" => basePriority,
                _ => basePriority
            };
        }

        private Dictionary<string, List<QualityControlOperation>> ClassifyOperationsByQCRequirements()
        {
            var classified = new Dictionary<string, List<QualityControlOperation>>();

            foreach (var operation in _operations)
            {
                if (!classified.ContainsKey(operation.QCType))
                {
                    classified[operation.QCType] = new List<QualityControlOperation>();
                }
                classified[operation.QCType].Add(operation);
            }

            return classified;
        }

        private async Task<SchedulingSolution> ScheduleMandatoryQCOperations(
            Dictionary<string, List<QualityControlOperation>> classifiedOperations)
        {
            var solution = new SchedulingSolution();
            var instrumentSchedules = new Dictionary<int, List<DateTime>>();

            // Initialize instrument schedules
            foreach (var instrument in _instruments)
            {
                instrumentSchedules[instrument.Id] = new List<DateTime>();
            }

            // Schedule mandatory QC operations first
            var mandatoryOperations = _operations
                .Where(op => op.IsRegulatoryRequired)
                .OrderBy(op => op.RegulatoryDeadline)
                .ThenByDescending(op => op.QCPriority)
                .ToList();

            foreach (var operation in mandatoryOperations)
            {
                var assignment = await ScheduleQCOperation(operation, instrumentSchedules);
                if (assignment != null)
                {
                    solution.Assignments.Add(assignment);
                    instrumentSchedules[assignment.InstrumentId].Add(assignment.StartTime);
                }
            }

            return solution;
        }

        private async Task<OperationAssignment> ScheduleQCOperation(
            QualityControlOperation operation, Dictionary<int, List<DateTime>> instrumentSchedules)
        {
            foreach (var instrument in _instruments)
            {
                if (!operation.CanExecuteOn(instrument) || !IsInstrumentQualified(instrument, operation))
                    continue;

                var schedule = instrumentSchedules[instrument.Id];
                var startTime = FindNextAvailableTime(schedule, operation.EstimatedDurationMs);
                
                if (startTime <= operation.RegulatoryDeadline)
                {
                    return new OperationAssignment
                    {
                        OperationId = operation.Id,
                        InstrumentId = instrument.Id,
                        StartTime = startTime,
                        EndTime = startTime.AddMilliseconds(operation.EstimatedDurationMs),
                        Cost = CalculateQCCost(operation, instrument, startTime)
                    };
                }
            }

            return null;
        }

        private DateTime FindNextAvailableTime(List<DateTime> schedule, int durationMs)
        {
            var currentTime = DateTime.Now;
            var sortedTimes = schedule.OrderBy(t => t).ToList();
            
            foreach (var scheduledTime in sortedTimes)
            {
                if (currentTime.AddMilliseconds(durationMs) <= scheduledTime)
                {
                    return currentTime;
                }
                currentTime = scheduledTime.AddMilliseconds(durationMs);
            }
            
            return currentTime;
        }

        private bool IsInstrumentQualified(FluidInstrument instrument, QualityControlOperation operation)
        {
            // Check if instrument is qualified for QC operations
            if (instrument.Properties.ContainsKey("QCQualified"))
            {
                return (bool)instrument.Properties["QCQualified"];
            }

            // Check calibration status
            if (instrument.Properties.ContainsKey("CalibrationDate"))
            {
                var calibrationDate = (DateTime)instrument.Properties["CalibrationDate"];
                var daysSinceCalibration = (DateTime.Now - calibrationDate).TotalDays;
                
                if (daysSinceCalibration > _config.MaxCalibrationAge)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldScheduleNow(QualityControlOperation operation)
        {
            // Check if QC operation is due
            var timeSinceSubmission = DateTime.Now - operation.SubmissionTime;
            var requiredFrequency = operation.QCProfile.RequiredFrequency;
            
            return timeSinceSubmission >= requiredFrequency ||
                   DateTime.Now >= operation.RegulatoryDeadline.AddMinutes(-30); // 30 min buffer
        }

        private async Task ExecuteQCOperation(QualityControlOperation operation, FluidInstrument instrument)
        {
            instrument.IsAvailable = false;
            instrument.CurrentOperation = operation;
            
            Console.WriteLine($"[QC] Executing {operation.QCType} operation {operation.Id} on {instrument.Name}");
            
            // Simulate operation execution
            await Task.Delay(operation.EstimatedDurationMs);
            
            // Simulate QC result evaluation
            var qcResult = EvaluateQCResult(operation);
            operation.QCResult = qcResult;
            
            operation.IsCompleted = true;
            operation.CompletionTime = DateTime.Now;
            instrument.IsAvailable = true;
            instrument.CurrentOperation = null;
            
            if (!qcResult.Passed)
            {
                Console.WriteLine($"[QC] WARNING: QC operation {operation.Id} failed - {qcResult.FailureReason}");
            }
        }

        private QCResult EvaluateQCResult(QualityControlOperation operation)
        {
            // Simulate QC result evaluation
            var random = new Random();
            var passed = random.NextDouble() > 0.05; // 95% pass rate
            
            return new QCResult
            {
                Passed = passed,
                Value = random.NextDouble() * 1000,
                FailureReason = passed ? null : "Result outside acceptance criteria",
                Timestamp = DateTime.Now
            };
        }

        private async Task IntegrateRoutineSamples(SchedulingSolution solution, 
            Dictionary<string, List<QualityControlOperation>> classifiedOperations)
        {
            // Integrate routine samples between QC operations
            var routineOperations = _operations
                .Where(op => !op.IsRegulatoryRequired)
                .OrderBy(op => op.Priority)
                .ToList();

            foreach (var operation in routineOperations)
            {
                var assignment = await FindBestSlotForRoutineOperation(operation, solution);
                if (assignment != null)
                {
                    solution.Assignments.Add(assignment);
                }
            }
        }

        private async Task<OperationAssignment> FindBestSlotForRoutineOperation(
            QualityControlOperation operation, SchedulingSolution solution)
        {
            // Find best available slot that doesn't interfere with QC operations
            var qcAssignments = solution.Assignments
                .Where(a => _operations.Any(op => op.Id == a.OperationId && op.IsRegulatoryRequired))
                .OrderBy(a => a.StartTime)
                .ToList();

            foreach (var instrument in _instruments)
            {
                if (!operation.CanExecuteOn(instrument))
                    continue;

                var instrumentQCAssignments = qcAssignments
                    .Where(a => a.InstrumentId == instrument.Id)
                    .ToList();

                var availableSlot = FindAvailableSlot(instrumentQCAssignments, operation.EstimatedDurationMs);
                if (availableSlot != null)
                {
                    return new OperationAssignment
                    {
                        OperationId = operation.Id,
                        InstrumentId = instrument.Id,
                        StartTime = availableSlot.Value,
                        EndTime = availableSlot.Value.AddMilliseconds(operation.EstimatedDurationMs),
                        Cost = CalculateQCCost(operation, instrument, availableSlot.Value)
                    };
                }
            }

            return null;
        }

        private DateTime? FindAvailableSlot(List<OperationAssignment> qcAssignments, int durationMs)
        {
            var currentTime = DateTime.Now;
            
            foreach (var assignment in qcAssignments)
            {
                if (currentTime.AddMilliseconds(durationMs) <= assignment.StartTime)
                {
                    return currentTime;
                }
                currentTime = assignment.EndTime;
            }
            
            return currentTime;
        }

        private void ValidateRegulatoryCompliance(SchedulingSolution solution)
        {
            var violations = new List<string>();

            foreach (var assignment in solution.Assignments)
            {
                var operation = _operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation?.IsRegulatoryRequired == true)
                {
                    // Check deadline compliance
                    if (assignment.EndTime > operation.RegulatoryDeadline)
                    {
                        violations.Add($"QC operation {operation.Id} exceeds regulatory deadline");
                    }

                    // Check frequency compliance
                    var timeSinceSubmission = assignment.StartTime - operation.SubmissionTime;
                    if (timeSinceSubmission > operation.QCProfile.RequiredFrequency)
                    {
                        violations.Add($"QC operation {operation.Id} exceeds required frequency");
                    }
                }
            }

            if (violations.Count > 0)
            {
                Console.WriteLine($"[QC] WARNING: {violations.Count} regulatory violations found:");
                foreach (var violation in violations)
                {
                    Console.WriteLine($"  - {violation}");
                }
            }
        }

        private void OptimizeForQualityMetrics(SchedulingSolution solution)
        {
            // Optimize spacing between QC operations for better quality monitoring
            var qcAssignments = solution.Assignments
                .Where(a => _operations.Any(op => op.Id == a.OperationId && op.IsRegulatoryRequired))
                .OrderBy(a => a.StartTime)
                .ToList();

            // Ensure even distribution of QC operations
            var totalTime = solution.TotalExecutionTime;
            var optimalInterval = totalTime / (qcAssignments.Count + 1);

            for (int i = 0; i < qcAssignments.Count; i++)
            {
                var optimalTime = DateTime.Now.AddMilliseconds(optimalInterval * (i + 1));
                var currentTime = qcAssignments[i].StartTime;
                
                // Adjust if significantly off optimal timing
                var timeDifference = Math.Abs((optimalTime - currentTime).TotalMilliseconds);
                if (timeDifference > optimalInterval * 0.3)
                {
                    // Try to move closer to optimal time
                    var newStartTime = optimalTime;
                    var operation = _operations.FirstOrDefault(op => op.Id == qcAssignments[i].OperationId);
                    
                    if (operation != null && newStartTime <= operation.RegulatoryDeadline)
                    {
                        var duration = qcAssignments[i].EndTime - qcAssignments[i].StartTime;
                        qcAssignments[i].StartTime = newStartTime;
                        qcAssignments[i].EndTime = newStartTime.Add(duration);
                    }
                }
            }
        }

        private double CalculateQualityScore(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return 1.0;

            var qcOperations = solution.Assignments
                .Where(a => _operations.Any(op => op.Id == a.OperationId && op.IsRegulatoryRequired))
                .ToList();

            var totalQualityScore = 0.0;
            var count = 0;

            foreach (var assignment in qcOperations)
            {
                var operation = _operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    var timeliness = CalculateTimelinessScore(operation, assignment);
                    var compliance = CalculateComplianceScore(operation, assignment);
                    var qualityScore = (timeliness + compliance) / 2.0;
                    
                    totalQualityScore += qualityScore;
                    count++;
                }
            }

            return count > 0 ? totalQualityScore / count : 1.0;
        }

        private double CalculateTimelinessScore(QualityControlOperation operation, OperationAssignment assignment)
        {
            var allowedTime = operation.RegulatoryDeadline - operation.SubmissionTime;
            var actualTime = assignment.EndTime - operation.SubmissionTime;
            
            if (actualTime <= allowedTime)
            {
                return 1.0 - (actualTime.TotalMilliseconds / allowedTime.TotalMilliseconds);
            }
            
            return 0.0; // Failed to meet deadline
        }

        private double CalculateComplianceScore(QualityControlOperation operation, OperationAssignment assignment)
        {
            var score = 1.0;
            
            // Check frequency compliance
            var timeSinceSubmission = assignment.StartTime - operation.SubmissionTime;
            if (timeSinceSubmission > operation.QCProfile.RequiredFrequency)
            {
                score -= 0.3;
            }
            
            // Check if QC passed
            if (operation.QCResult != null && !operation.QCResult.Passed)
            {
                score -= 0.5;
            }
            
            return Math.Max(0, score);
        }

        private double CalculateComplianceScore(SchedulingSolution solution)
        {
            var mandatoryOperations = _operations.Where(op => op.IsRegulatoryRequired).ToList();
            if (mandatoryOperations.Count == 0) return 1.0;

            var compliantOperations = 0;
            
            foreach (var operation in mandatoryOperations)
            {
                var assignment = solution.Assignments.FirstOrDefault(a => a.OperationId == operation.Id);
                if (assignment != null)
                {
                    var isCompliant = assignment.EndTime <= operation.RegulatoryDeadline;
                    if (isCompliant) compliantOperations++;
                }
            }

            return (double)compliantOperations / mandatoryOperations.Count;
        }

        private double CalculateQCCost(QualityControlOperation operation, FluidInstrument instrument, DateTime startTime)
        {
            var baseCost = operation.EstimatedDurationMs;
            var urgencyMultiplier = operation.IsRegulatoryRequired ? 2.0 : 1.0;
            var delayPenalty = Math.Max(0, (startTime - operation.RegulatoryDeadline).TotalMilliseconds);
            
            return (baseCost * urgencyMultiplier) + delayPenalty;
        }
    }

    /// <summary>
    /// Quality control specific operation
    /// </summary>
    public class QualityControlOperation : FluidOperation
    {
        public string QCType { get; set; }
        public QualityControlProfile QCProfile { get; set; }
        public DateTime RegulatoryDeadline { get; set; }
        public int QCPriority { get; set; }
        public List<string> AcceptanceCriteria { get; set; }
        public bool IsRegulatoryRequired { get; set; }
        public QCResult QCResult { get; set; }

        public QualityControlOperation()
        {
            AcceptanceCriteria = new List<string>();
        }
    }

    /// <summary>
    /// Quality control profile
    /// </summary>
    public class QualityControlProfile
    {
        public string Name { get; set; }
        public TimeSpan RequiredFrequency { get; set; }
        public List<string> AcceptanceCriteria { get; set; }
        public RegulatoryRequirement RegulatoryRequirement { get; set; }
        public QualityControlType QCType { get; set; }
        public int MaxAllowedFailures { get; set; }
        public int MinSampleSize { get; set; }

        public QualityControlProfile()
        {
            AcceptanceCriteria = new List<string>();
        }
    }

    /// <summary>
    /// QC result
    /// </summary>
    public class QCResult
    {
        public bool Passed { get; set; }
        public double Value { get; set; }
        public string FailureReason { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Quality control configuration
    /// </summary>
    public class QualityControlConfig
    {
        public TimeSpan QCFrequency { get; set; } = TimeSpan.FromHours(8);
        public RegulatoryMode RegulatoryMode { get; set; } = RegulatoryMode.FDA;
        public int MaxCalibrationAge { get; set; } = 30; // days
        public bool EnableRealTimeMonitoring { get; set; } = true;
        public double MaxAllowedError { get; set; } = 0.05; // 5%
    }

    /// <summary>
    /// Enums for QC system
    /// </summary>
    public enum RegulatoryRequirement
    {
        Optional,
        Recommended,
        Mandatory
    }

    public enum QualityControlType
    {
        Positive,
        Negative,
        Calibrator,
        Proficiency,
        Blank
    }

    public enum RegulatoryMode
    {
        FDA,
        CE_IVD,
        ISO13485,
        CAP,
        CLIA
    }
}

namespace FluidHandling.Performance
{
    /// <summary>
    /// Comprehensive performance analyzer for IVD scheduling systems
    /// Provides detailed metrics and benchmarking capabilities
    /// </summary>
    public class PerformanceAnalyzer
    {
        private readonly List<PerformanceMetrics> _historicalMetrics;
        private readonly Dictionary<string, List<double>> _benchmarkData;

        public PerformanceAnalyzer()
        {
            _historicalMetrics = new List<PerformanceMetrics>();
            _benchmarkData = new Dictionary<string, List<double>>();
        }

        /// <summary>
        /// Comprehensive comparison of multiple scheduling algorithms
        /// </summary>
        public async Task<PerformanceComparisonReport> CompareAlgorithmsAsync(
            List<IScheduler> schedulers, 
            List<FluidOperation> testOperations,
            List<FluidInstrument> instruments,
            int iterations = 10)
        {
            var report = new PerformanceComparisonReport
            {
                TestDate = DateTime.Now,
                TestOperations = testOperations.Count,
                TestInstruments = instruments.Count,
                Iterations = iterations
            };

            Console.WriteLine($"[Performance] Starting benchmark with {schedulers.Count} algorithms, {iterations} iterations");

            foreach (var scheduler in schedulers)
            {
                var results = new List<PerformanceMetrics>();
                var stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < iterations; i++)
                {
                    // Reset operations for each iteration
                    var operationsCopy = testOperations.Select(op => CloneOperation(op)).ToList();
                    
                    // Add operations to scheduler
                    scheduler.AddOperations(operationsCopy);
                    
                    // Measure performance
                    var iterationStart = DateTime.Now;
                    var solution = await scheduler.GenerateScheduleAsync();
                    var iterationEnd = DateTime.Now;
                    
                    var metrics = CalculateDetailedMetrics(solution, operationsCopy, instruments);
                    metrics.MeasurementStart = iterationStart;
                    metrics.MeasurementEnd = iterationEnd;
                    
                    results.Add(metrics);
                }

                stopwatch.Stop();

                var algorithmResult = new AlgorithmPerformanceResult
                {
                    AlgorithmName = scheduler.Name,
                    AlgorithmType = scheduler.Type,
                    TotalExecutionTime = stopwatch.Elapsed,
                    AverageMetrics = CalculateAverageMetrics(results),
                    BestMetrics = FindBestMetrics(results),
                    WorstMetrics = FindWorstMetrics(results),
                    StandardDeviation = CalculateStandardDeviation(results),
                    ConfidenceInterval = CalculateConfidenceInterval(results),
                    IterationResults = results
                };

                report.AlgorithmResults.Add(algorithmResult);
                
                Console.WriteLine($"[Performance] {scheduler.Name}: Avg Score = {algorithmResult.AverageMetrics.CalculateOverallScore():F3}");
            }

            // Generate comparative analysis
            report.Analysis = GenerateComparativeAnalysis(report.AlgorithmResults);
            
            // Save report
            await SaveReportAsync(report);

            return report;
        }

        /// <summary>
        /// Detailed performance metrics calculation
        /// </summary>
        private PerformanceMetrics CalculateDetailedMetrics(
            SchedulingSolution solution, 
            List<FluidOperation> operations,
            List<FluidInstrument> instruments)
        {
            var metrics = new PerformanceMetrics();
            
            if (solution.Assignments.Count == 0)
            {
                return metrics;
            }

            // Primary Metrics
            metrics.Makespan = solution.TotalExecutionTime;
            metrics.Throughput = solution.Assignments.Count / (solution.TotalExecutionTime / 3600000.0); // ops/hour
            metrics.Cost = solution.Cost;
            
            // Calculate utilization
            var totalInstrumentTime = instruments.Count * solution.TotalExecutionTime;
            var usedTime = solution.Assignments.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds);
            metrics.Utilization = usedTime / totalInstrumentTime;
            
            // Quality Metrics
            metrics.DeadlineMissRate = CalculateDeadlineMissRate(solution, operations);
            metrics.SuccessRate = CalculateSuccessRate(solution, operations);
            metrics.ErrorRate = 1.0 - metrics.SuccessRate;
            
            // Time-based Metrics
            metrics.AverageWaitTime = CalculateAverageWaitTime(solution, operations);
            metrics.AverageResponseTime = CalculateAverageResponseTime(solution, operations);
            metrics.TardinessPenalty = CalculateTardinessPenalty(solution, operations);
            
            // IVD-Specific Metrics
            metrics.BiomoleculeStabilityScore = CalculateBiomoleculeStabilityScore(solution, operations);
            metrics.CrossContaminationRisk = CalculateCrossContaminationRisk(solution, operations);
            metrics.RegulatoryComplianceScore = CalculateRegulatoryComplianceScore(solution, operations);
            
            // Statistical Metrics
            metrics.NumberOfSamples = solution.Assignments.Count;
            
            return metrics;
        }

        private double CalculateDeadlineMissRate(SchedulingSolution solution, List<FluidOperation> operations)
        {
            var missedDeadlines = 0;
            var totalOperations = solution.Assignments.Count;
            
            foreach (var assignment in solution.Assignments)
            {
                var operation = operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null && assignment.EndTime > operation.Deadline)
                {
                    missedDeadlines++;
                }
            }
            
            return totalOperations > 0 ? (double)missedDeadlines / totalOperations : 0;
        }

        private double CalculateSuccessRate(SchedulingSolution solution, List<FluidOperation> operations)
        {
            // For now, assume all scheduled operations are successful
            // In real implementation, this would check actual execution results
            return 1.0 - CalculateDeadlineMissRate(solution, operations);
        }

        private double CalculateAverageWaitTime(SchedulingSolution solution, List<FluidOperation> operations)
        {
            var totalWaitTime = 0.0;
            var count = 0;
            
            foreach (var assignment in solution.Assignments)
            {
                var operation = operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    var waitTime = (assignment.StartTime - operation.SubmissionTime).TotalMilliseconds;
                    totalWaitTime += Math.Max(0, waitTime);
                    count++;
                }
            }
            
            return count > 0 ? totalWaitTime / count : 0;
        }

        private double CalculateAverageResponseTime(SchedulingSolution solution, List<FluidOperation> operations)
        {
            var totalResponseTime = 0.0;
            var count = 0;
            
            foreach (var assignment in solution.Assignments)
            {
                var operation = operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    var responseTime = (assignment.EndTime - operation.SubmissionTime).TotalMilliseconds;
                    totalResponseTime += responseTime;
                    count++;
                }
            }
            
            return count > 0 ? totalResponseTime / count : 0;
        }

        private double CalculateTardinessPenalty(SchedulingSolution solution, List<FluidOperation> operations)
        {
            var totalTardiness = 0.0;
            
            foreach (var assignment in solution.Assignments)
            {
                var operation = operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    var tardiness = Math.Max(0, (assignment.EndTime - operation.Deadline).TotalMilliseconds);
                    totalTardiness += tardiness;
                }
            }
            
            return totalTardiness;
        }

        private double CalculateBiomoleculeStabilityScore(SchedulingSolution solution, List<FluidOperation> operations)
        {
            // Simplified stability score calculation
            var totalStability = 0.0;
            var count = 0;
            
            foreach (var assignment in solution.Assignments)
            {
                var operation = operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    var timeSinceSubmission = (assignment.StartTime - operation.SubmissionTime).TotalMilliseconds;
                    var stabilityScore = Math.Max(0, 1.0 - (timeSinceSubmission / 3600000.0)); // 1 hour degradation
                    totalStability += stabilityScore;
                    count++;
                }
            }
            
            return count > 0 ? totalStability / count : 1.0;
        }

        private double CalculateCrossContaminationRisk(SchedulingSolution solution, List<FluidOperation> operations)
        {
            // Simplified contamination risk calculation
            var totalRisk = 0.0;
            var instrumentGroups = solution.Assignments.GroupBy(a => a.InstrumentId);
            
            foreach (var group in instrumentGroups)
            {
                var assignments = group.OrderBy(a => a.StartTime).ToList();
                for (int i = 1; i < assignments.Count; i++)
                {
                    var timeBetween = (assignments[i].StartTime - assignments[i-1].EndTime).TotalMilliseconds;
                    if (timeBetween < 300000) // Less than 5 minutes
                    {
                        totalRisk += 0.1; // 10% risk per rapid succession
                    }
                }
            }
            
            return Math.Min(1.0, totalRisk);
        }

        private double CalculateRegulatoryComplianceScore(SchedulingSolution solution, List<FluidOperation> operations)
        {
            // Simplified compliance score - in real implementation would check specific regulations
            var complianceScore = 1.0;
            var deadlineMissRate = CalculateDeadlineMissRate(solution, operations);
            
            complianceScore -= deadlineMissRate * 0.5; // Deadline misses reduce compliance
            
            return Math.Max(0, complianceScore);
        }

        private FluidOperation CloneOperation(FluidOperation original)
        {
            return new FluidOperation
            {
                Id = original.Id,
                SampleId = original.SampleId,
                OperationType = original.OperationType,
                VolumeInMicroliters = original.VolumeInMicroliters,
                EstimatedDurationMs = original.EstimatedDurationMs,
                Priority = original.Priority,
                SubmissionTime = original.SubmissionTime,
                Deadline = original.Deadline,
                SourceLocation = original.SourceLocation,
                DestinationLocation = original.DestinationLocation,
                IsCompleted = false,
                CompletionTime = null,
                Status = OperationStatus.Pending
            };
        }

        private PerformanceMetrics CalculateAverageMetrics(List<PerformanceMetrics> results)
        {
            return new PerformanceMetrics
            {
                Makespan = results.Average(r => r.Makespan),
                Throughput = results.Average(r => r.Throughput),
                Utilization = results.Average(r => r.Utilization),
                Cost = results.Average(r => r.Cost),
                DeadlineMissRate = results.Average(r => r.DeadlineMissRate),
                SuccessRate = results.Average(r => r.SuccessRate),
                ErrorRate = results.Average(r => r.ErrorRate),
                AverageWaitTime = results.Average(r => r.AverageWaitTime),
                AverageResponseTime = results.Average(r => r.AverageResponseTime),
                BiomoleculeStabilityScore = results.Average(r => r.BiomoleculeStabilityScore),
                RegulatoryComplianceScore = results.Average(r => r.RegulatoryComplianceScore),
                NumberOfSamples = results.First().NumberOfSamples
            };
        }

        private PerformanceMetrics FindBestMetrics(List<PerformanceMetrics> results)
        {
            return results.OrderByDescending(r => r.CalculateOverallScore()).First();
        }

        private PerformanceMetrics FindWorstMetrics(List<PerformanceMetrics> results)
        {
            return results.OrderBy(r => r.CalculateOverallScore()).First();
        }

        private double CalculateStandardDeviation(List<PerformanceMetrics> results)
        {
            var scores = results.Select(r => r.CalculateOverallScore()).ToList();
            var mean = scores.Average();
            var variance = scores.Select(score => Math.Pow(score - mean, 2)).Average();
            return Math.Sqrt(variance);
        }

        private (double lower, double upper) CalculateConfidenceInterval(List<PerformanceMetrics> results)
        {
            var scores = results.Select(r => r.CalculateOverallScore()).ToList();
            var mean = scores.Average();
            var stdDev = CalculateStandardDeviation(results);
            var marginOfError = 1.96 * (stdDev / Math.Sqrt(scores.Count)); // 95% confidence interval
            
            return (mean - marginOfError, mean + marginOfError);
        }

        private string GenerateComparativeAnalysis(List<AlgorithmPerformanceResult> results)
        {
            var analysis = new List<string>();
            
            // Find best performing algorithm
            var bestAlgorithm = results.OrderByDescending(r => r.AverageMetrics.CalculateOverallScore()).First();
            analysis.Add($"Best Overall Performance: {bestAlgorithm.AlgorithmName} (Score: {bestAlgorithm.AverageMetrics.CalculateOverallScore():F3})");
            
            // Find fastest algorithm
            var fastestAlgorithm = results.OrderBy(r => r.AverageMetrics.Makespan).First();
            analysis.Add($"Fastest Execution: {fastestAlgorithm.AlgorithmName} (Makespan: {fastestAlgorithm.AverageMetrics.Makespan:F0}ms)");
            
            // Find most efficient algorithm
            var mostEfficient = results.OrderByDescending(r => r.AverageMetrics.Utilization).First();
            analysis.Add($"Highest Utilization: {mostEfficient.AlgorithmName} (Utilization: {mostEfficient.AverageMetrics.Utilization:F1}%)");
            
            // Find most reliable algorithm
            var mostReliable = results.OrderByDescending(r => r.AverageMetrics.SuccessRate).First();
            analysis.Add($"Most Reliable: {mostReliable.AlgorithmName} (Success Rate: {mostReliable.AverageMetrics.SuccessRate:F1}%)");
            
            return string.Join("\n", analysis);
        }

        private async Task SaveReportAsync(PerformanceComparisonReport report)
        {
            var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var fileName = $"performance_report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(Environment.CurrentDirectory, "Reports", fileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            await File.WriteAllTextAsync(filePath, reportJson);
            
            Console.WriteLine($"[Performance] Report saved to: {filePath}");
        }
    }

    /// <summary>
    /// Performance comparison report
    /// </summary>
    public class PerformanceComparisonReport
    {
        public DateTime TestDate { get; set; }
        public int TestOperations { get; set; }
        public int TestInstruments { get; set; }
        public int Iterations { get; set; }
        public List<AlgorithmPerformanceResult> AlgorithmResults { get; set; }
        public string Analysis { get; set; }

        public PerformanceComparisonReport()
        {
            AlgorithmResults = new List<AlgorithmPerformanceResult>();
        }
    }

    /// <summary>
    /// Algorithm performance result
    /// </summary>
    public class AlgorithmPerformanceResult
    {
        public string AlgorithmName { get; set; }
        public SchedulerType AlgorithmType { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public PerformanceMetrics AverageMetrics { get; set; }
        public PerformanceMetrics BestMetrics { get; set; }
        public PerformanceMetrics WorstMetrics { get; set; }
        public double StandardDeviation { get; set; }
        public (double lower, double upper) ConfidenceInterval { get; set; }
        public List<PerformanceMetrics> IterationResults { get; set; }

        public AlgorithmPerformanceResult()
        {
            IterationResults = new List<PerformanceMetrics>();
        }
    }
}