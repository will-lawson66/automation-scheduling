using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluidHandling.Core.Models;
using FluidHandling.Core.Interfaces;

namespace FluidHandling.IVDSpecific
{
    /// <summary>
    /// Biomolecule Stability Scheduler
    /// Specialized for handling RNA, DNA, proteins, and other unstable biomolecules
    /// Based on research showing time constraints are critical for sample integrity
    /// </summary>
    public class BiomoleculeScheduler : IScheduler
    {
        public string Name => "Biomolecule Stability Scheduler";
        public string Description => "Specialized scheduler for preserving biomolecule integrity in IVD systems";
        public SchedulerType Type => SchedulerType.IVDSpecific;

        private readonly List<BiomoleculeOperation> _operations;
        private readonly List<FluidInstrument> _instruments;
        private readonly BiomoleculeConfig _config;
        private readonly Dictionary<string, BiomoleculeProfile> _biomoleculeProfiles;
        private PerformanceMetrics _performanceMetrics;

        public BiomoleculeScheduler(List<FluidInstrument> instruments, BiomoleculeConfig config = null)
        {
            _instruments = instruments;
            _operations = new List<BiomoleculeOperation>();
            _config = config ?? new BiomoleculeConfig();
            _biomoleculeProfiles = InitializeBiomoleculeProfiles();
            _performanceMetrics = new PerformanceMetrics();
        }

        public void AddOperation(FluidOperation operation)
        {
            var biomoleculeOp = operation as BiomoleculeOperation ?? 
                ConvertToBiomoleculeOperation(operation);
            _operations.Add(biomoleculeOp);
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
            
            Console.WriteLine("[BioScheduler] Starting biomolecule stability scheduling...");
            
            // Phase 1: Categorize operations by biomolecule type and stability
            var categorizedOperations = CategorizeOperations();
            
            // Phase 2: Calculate degradation risks and priorities
            CalculateDegradationRisks();
            
            // Phase 3: Schedule critical operations first
            var solution = await ScheduleCriticalOperationsFirst(categorizedOperations);
            
            // Phase 4: Optimize for cross-contamination prevention
            OptimizeForContaminationPrevention(solution);
            
            // Phase 5: Validate biomolecule stability constraints
            ValidateBiomoleculeConstraints(solution);
            
            _performanceMetrics.FinalizeMeasurement();
            _performanceMetrics.BiomoleculeStabilityScore = CalculateStabilityScore(solution);
            
            Console.WriteLine($"[BioScheduler] Completed with stability score: {_performanceMetrics.BiomoleculeStabilityScore:F3}");
            
            return solution;
        }

        public async Task<FluidOperation> ScheduleNextAsync()
        {
            // Find the most critical operation that can be executed now
            var criticalOperation = _operations
                .Where(op => !op.IsCompleted && CanExecuteNow(op))
                .OrderByDescending(op => op.CriticalityScore)
                .ThenBy(op => op.DegradationRisk)
                .ThenBy(op => op.TimeToStabilityLoss)
                .FirstOrDefault();

            if (criticalOperation != null)
            {
                var availableInstrument = _instruments
                    .FirstOrDefault(inst => inst.IsAvailable && 
                                          criticalOperation.CanExecuteOn(inst) &&
                                          IsInstrumentSuitableForBiomolecule(inst, criticalOperation));

                if (availableInstrument != null)
                {
                    await ExecuteOperation(criticalOperation, availableInstrument);
                    return criticalOperation;
                }
            }

            return null;
        }

        public void UpdateConfiguration(SchedulerConfiguration config)
        {
            // Update biomolecule-specific configuration
            if (config.Parameters.ContainsKey("MaxDegradationRisk"))
            {
                _config.MaxAllowedDegradationRisk = (double)config.Parameters["MaxDegradationRisk"];
            }
            if (config.Parameters.ContainsKey("EnableCrossContaminationPrevention"))
            {
                _config.EnableCrossContaminationPrevention = (bool)config.Parameters["EnableCrossContaminationPrevention"];
            }
        }

        public PerformanceMetrics GetPerformanceMetrics()
        {
            return _performanceMetrics;
        }

        private Dictionary<string, BiomoleculeProfile> InitializeBiomoleculeProfiles()
        {
            return new Dictionary<string, BiomoleculeProfile>
            {
                ["RNA"] = new BiomoleculeProfile
                {
                    Name = "RNA",
                    StabilityHalfLife = TimeSpan.FromMinutes(30),
                    DegradationRate = 0.95,
                    TemperatureSensitivity = 0.9,
                    ContaminationRisk = 0.8,
                    RequiredHandling = BiomoleculeHandling.RNaseFree,
                    OptimalStorageTemp = -80
                },
                ["DNA"] = new BiomoleculeProfile
                {
                    Name = "DNA",
                    StabilityHalfLife = TimeSpan.FromHours(2),
                    DegradationRate = 0.3,
                    TemperatureSensitivity = 0.4,
                    ContaminationRisk = 0.5,
                    RequiredHandling = BiomoleculeHandling.DNaseFree,
                    OptimalStorageTemp = -20
                },
                ["Protein"] = new BiomoleculeProfile
                {
                    Name = "Protein",
                    StabilityHalfLife = TimeSpan.FromHours(4),
                    DegradationRate = 0.6,
                    TemperatureSensitivity = 0.7,
                    ContaminationRisk = 0.4,
                    RequiredHandling = BiomoleculeHandling.ProteaseFree,
                    OptimalStorageTemp = 4
                },
                ["Enzyme"] = new BiomoleculeProfile
                {
                    Name = "Enzyme",
                    StabilityHalfLife = TimeSpan.FromHours(1),
                    DegradationRate = 0.8,
                    TemperatureSensitivity = 0.8,
                    ContaminationRisk = 0.6,
                    RequiredHandling = BiomoleculeHandling.ActivityPreserving,
                    OptimalStorageTemp = 4
                }
            };
        }

        private BiomoleculeOperation ConvertToBiomoleculeOperation(FluidOperation operation)
        {
            // Determine biomolecule type from operation type or sample ID
            var biomoleculeType = DetermineBiomoleculeType(operation);
            var profile = _biomoleculeProfiles.ContainsKey(biomoleculeType) 
                ? _biomoleculeProfiles[biomoleculeType] 
                : _biomoleculeProfiles["DNA"]; // Default to DNA

            return new BiomoleculeOperation
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
                BiomoleculeType = biomoleculeType,
                BiomoleculeProfile = profile,
                StabilityHalfLife = profile.StabilityHalfLife,
                DegradationRate = profile.DegradationRate,
                RequiredHandling = profile.RequiredHandling,
                OptimalStorageTemp = profile.OptimalStorageTemp
            };
        }

        private string DetermineBiomoleculeType(FluidOperation operation)
        {
            // Determine biomolecule type based on operation type and sample ID
            var operationType = operation.OperationType.ToLower();
            var sampleId = operation.SampleId.ToLower();

            if (operationType.Contains("rna") || sampleId.Contains("rna"))
                return "RNA";
            if (operationType.Contains("dna") || sampleId.Contains("dna"))
                return "DNA";
            if (operationType.Contains("protein") || sampleId.Contains("pro"))
                return "Protein";
            if (operationType.Contains("enzyme") || sampleId.Contains("enz"))
                return "Enzyme";

            return "DNA"; // Default
        }

        private Dictionary<string, List<BiomoleculeOperation>> CategorizeOperations()
        {
            var categorized = new Dictionary<string, List<BiomoleculeOperation>>();

            foreach (var operation in _operations)
            {
                if (!categorized.ContainsKey(operation.BiomoleculeType))
                {
                    categorized[operation.BiomoleculeType] = new List<BiomoleculeOperation>();
                }
                categorized[operation.BiomoleculeType].Add(operation);
            }

            return categorized;
        }

        private void CalculateDegradationRisks()
        {
            var currentTime = DateTime.Now;

            foreach (var operation in _operations)
            {
                var timeSinceSubmission = currentTime - operation.SubmissionTime;
                var halfLife = operation.StabilityHalfLife.TotalMilliseconds;
                
                // Calculate degradation based on exponential decay
                var degradationFactor = Math.Pow(0.5, timeSinceSubmission.TotalMilliseconds / halfLife);
                operation.DegradationRisk = 1.0 - degradationFactor;
                
                // Calculate time until critical degradation
                var criticalDegradationTime = halfLife * Math.Log(0.1) / Math.Log(0.5); // 90% degradation
                operation.TimeToStabilityLoss = criticalDegradationTime - timeSinceSubmission.TotalMilliseconds;
                
                // Calculate criticality score
                operation.CriticalityScore = CalculateCriticalityScore(operation);
            }
        }

        private double CalculateCriticalityScore(BiomoleculeOperation operation)
        {
            // Multi-factor criticality assessment
            double degradationWeight = operation.DegradationRisk * 0.4;
            double timeWeight = (operation.TimeToStabilityLoss <= 0 ? 1.0 : 
                               Math.Max(0, 1.0 - operation.TimeToStabilityLoss / 3600000.0)) * 0.3; // 1 hour normalization
            double priorityWeight = operation.Priority / 10.0 * 0.2;
            double volumeWeight = Math.Min(1.0, operation.VolumeInMicroliters / 1000.0) * 0.1;
            
            return Math.Max(0, Math.Min(1.0, degradationWeight + timeWeight + priorityWeight + volumeWeight));
        }

        private async Task<SchedulingSolution> ScheduleCriticalOperationsFirst(
            Dictionary<string, List<BiomoleculeOperation>> categorizedOperations)
        {
            var solution = new SchedulingSolution();
            var instrumentSchedules = new Dictionary<int, List<TimeSlot>>();

            // Initialize instrument schedules
            foreach (var instrument in _instruments)
            {
                instrumentSchedules[instrument.Id] = new List<TimeSlot>();
            }

            // Sort all operations by criticality
            var sortedOperations = _operations
                .OrderByDescending(op => op.CriticalityScore)
                .ThenBy(op => op.DegradationRisk)
                .ThenBy(op => op.TimeToStabilityLoss)
                .ToList();

            foreach (var operation in sortedOperations)
            {
                var assignment = await ScheduleOperationOptimally(operation, instrumentSchedules);
                if (assignment != null)
                {
                    solution.Assignments.Add(assignment);
                    
                    // Update instrument schedule
                    var timeSlot = new TimeSlot
                    {
                        StartTime = assignment.StartTime,
                        EndTime = assignment.EndTime,
                        OperationId = assignment.OperationId,
                        BiomoleculeType = operation.BiomoleculeType
                    };
                    instrumentSchedules[assignment.InstrumentId].Add(timeSlot);
                }
            }

            solution.TotalExecutionTime = CalculateTotalExecutionTime(solution);
            solution.Cost = CalculateTotalCost(solution);
            solution.IsValid = ValidateSolution(solution);

            return solution;
        }

        private async Task<OperationAssignment> ScheduleOperationOptimally(
            BiomoleculeOperation operation, Dictionary<int, List<TimeSlot>> instrumentSchedules)
        {
            OperationAssignment bestAssignment = null;
            double bestScore = double.MinValue;

            foreach (var instrument in _instruments)
            {
                if (!operation.CanExecuteOn(instrument) || 
                    !IsInstrumentSuitableForBiomolecule(instrument, operation))
                    continue;

                var schedule = instrumentSchedules[instrument.Id];
                var earliestStart = FindEarliestAvailableTime(operation, schedule);
                
                if (earliestStart != null)
                {
                    var assignment = new OperationAssignment
                    {
                        OperationId = operation.Id,
                        InstrumentId = instrument.Id,
                        StartTime = earliestStart.Value,
                        EndTime = earliestStart.Value.AddMilliseconds(operation.EstimatedDurationMs),
                        Cost = CalculateAssignmentCost(operation, instrument, earliestStart.Value)
                    };

                    var score = CalculateAssignmentScore(operation, assignment);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestAssignment = assignment;
                    }
                }
            }

            return bestAssignment;
        }

        private DateTime? FindEarliestAvailableTime(BiomoleculeOperation operation, List<TimeSlot> schedule)
        {
            var currentTime = DateTime.Now;
            var earliestStart = DateTime.Max(currentTime, operation.SubmissionTime);
            
            // Check if operation can still be performed within stability window
            if (operation.TimeToStabilityLoss <= 0)
            {
                return null; // Sample too degraded
            }

            // Find first available slot
            var sortedSlots = schedule.OrderBy(s => s.StartTime).ToList();
            
            foreach (var slot in sortedSlots)
            {
                if (earliestStart.AddMilliseconds(operation.EstimatedDurationMs) <= slot.StartTime)
                {
                    return earliestStart;
                }
                earliestStart = DateTime.Max(earliestStart, slot.EndTime);
            }

            return earliestStart;
        }

        private bool IsInstrumentSuitableForBiomolecule(FluidInstrument instrument, BiomoleculeOperation operation)
        {
            // Check if instrument supports required handling type
            if (instrument.Properties.ContainsKey("HandlingType"))
            {
                var handlingType = (BiomoleculeHandling)instrument.Properties["HandlingType"];
                if (handlingType != operation.RequiredHandling && 
                    handlingType != BiomoleculeHandling.Universal)
                {
                    return false;
                }
            }

            // Check temperature requirements
            if (instrument.Properties.ContainsKey("OperatingTemp"))
            {
                var operatingTemp = (double)instrument.Properties["OperatingTemp"];
                var tempDifference = Math.Abs(operatingTemp - operation.OptimalStorageTemp);
                if (tempDifference > _config.MaxTemperatureDeviation)
                {
                    return false;
                }
            }

            return true;
        }

        private double CalculateAssignmentScore(BiomoleculeOperation operation, OperationAssignment assignment)
        {
            // Multi-criteria scoring
            double stabilityScore = CalculateStabilityScore(operation, assignment.StartTime);
            double efficiencyScore = CalculateEfficiencyScore(operation, assignment);
            double riskScore = CalculateRiskScore(operation, assignment);

            return stabilityScore * 0.5 + efficiencyScore * 0.3 + riskScore * 0.2;
        }

        private double CalculateStabilityScore(BiomoleculeOperation operation, DateTime startTime)
        {
            var timeSinceSubmission = startTime - operation.SubmissionTime;
            var halfLife = operation.StabilityHalfLife.TotalMilliseconds;
            
            var degradationFactor = Math.Pow(0.5, timeSinceSubmission.TotalMilliseconds / halfLife);
            return Math.Max(0, degradationFactor);
        }

        private double CalculateEfficiencyScore(BiomoleculeOperation operation, OperationAssignment assignment)
        {
            // Score based on how soon the operation can be completed
            var timeToCompletion = (assignment.EndTime - DateTime.Now).TotalMilliseconds;
            var normalizedTime = Math.Min(1.0, timeToCompletion / 3600000.0); // 1 hour normalization
            
            return 1.0 - normalizedTime;
        }

        private double CalculateRiskScore(BiomoleculeOperation operation, OperationAssignment assignment)
        {
            // Score based on contamination and degradation risks
            var contaminationRisk = operation.BiomoleculeProfile.ContaminationRisk;
            var degradationRisk = operation.DegradationRisk;
            
            return 1.0 - (contaminationRisk * 0.5 + degradationRisk * 0.5);
        }

        private void OptimizeForContaminationPrevention(SchedulingSolution solution)
        {
            if (!_config.EnableCrossContaminationPrevention)
                return;

            Console.WriteLine("[BioScheduler] Optimizing for contamination prevention...");

            // Group assignments by instrument
            var instrumentGroups = solution.Assignments.GroupBy(a => a.InstrumentId);

            foreach (var group in instrumentGroups)
            {
                var assignments = group.OrderBy(a => a.StartTime).ToList();
                
                // Add cleaning time between different biomolecule types
                for (int i = 1; i < assignments.Count; i++)
                {
                    var currentOp = _operations.FirstOrDefault(op => op.Id == assignments[i].OperationId);
                    var previousOp = _operations.FirstOrDefault(op => op.Id == assignments[i-1].OperationId);
                    
                    if (currentOp != null && previousOp != null && 
                        currentOp.BiomoleculeType != previousOp.BiomoleculeType)
                    {
                        var cleaningTime = GetCleaningTime(previousOp.BiomoleculeType, currentOp.BiomoleculeType);
                        var requiredStart = assignments[i-1].EndTime.AddMilliseconds(cleaningTime);
                        
                        if (assignments[i].StartTime < requiredStart)
                        {
                            var delay = requiredStart - assignments[i].StartTime;
                            assignments[i].StartTime = requiredStart;
                            assignments[i].EndTime = assignments[i].EndTime.Add(delay);
                        }
                    }
                }
            }
        }

        private double GetCleaningTime(string fromBiomolecule, string toBiomolecule)
        {
            // Define cleaning times between different biomolecule types
            var cleaningMatrix = new Dictionary<(string, string), double>
            {
                [("RNA", "DNA")] = 300000,     // 5 minutes
                [("DNA", "RNA")] = 600000,     // 10 minutes (RNA is more sensitive)
                [("Protein", "RNA")] = 900000, // 15 minutes
                [("Protein", "DNA")] = 300000, // 5 minutes
                [("Enzyme", "RNA")] = 1200000, // 20 minutes
                [("Enzyme", "DNA")] = 600000,  // 10 minutes
                [("Enzyme", "Protein")] = 300000 // 5 minutes
            };

            return cleaningMatrix.ContainsKey((fromBiomolecule, toBiomolecule)) 
                ? cleaningMatrix[(fromBiomolecule, toBiomolecule)]
                : 300000; // Default 5 minutes
        }

        private bool CanExecuteNow(BiomoleculeOperation operation)
        {
            return operation.TimeToStabilityLoss > operation.EstimatedDurationMs &&
                   operation.DegradationRisk < _config.MaxAllowedDegradationRisk;
        }

        private async Task ExecuteOperation(BiomoleculeOperation operation, FluidInstrument instrument)
        {
            instrument.IsAvailable = false;
            instrument.CurrentOperation = operation;
            
            Console.WriteLine($"[BioScheduler] Executing {operation.BiomoleculeType} operation {operation.Id} on {instrument.Name}");
            
            // Simulate operation execution
            await Task.Delay(operation.EstimatedDurationMs);
            
            operation.IsCompleted = true;
            operation.CompletionTime = DateTime.Now;
            instrument.IsAvailable = true;
            instrument.CurrentOperation = null;
        }

        private void ValidateBiomoleculeConstraints(SchedulingSolution solution)
        {
            var violations = new List<string>();

            foreach (var assignment in solution.Assignments)
            {
                var operation = _operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    // Check stability constraints
                    var executionTime = assignment.StartTime - operation.SubmissionTime;
                    if (executionTime.TotalMilliseconds > operation.StabilityHalfLife.TotalMilliseconds)
                    {
                        violations.Add($"Operation {operation.Id} scheduled beyond stability window");
                    }

                    // Check degradation risk
                    if (operation.DegradationRisk > _config.MaxAllowedDegradationRisk)
                    {
                        violations.Add($"Operation {operation.Id} exceeds maximum degradation risk");
                    }
                }
            }

            if (violations.Count > 0)
            {
                Console.WriteLine($"[BioScheduler] WARNING: {violations.Count} constraint violations found:");
                foreach (var violation in violations)
                {
                    Console.WriteLine($"  - {violation}");
                }
            }
        }

        private double CalculateStabilityScore(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return 1.0;

            var totalStability = 0.0;
            var count = 0;

            foreach (var assignment in solution.Assignments)
            {
                var operation = _operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    var stabilityScore = CalculateStabilityScore(operation, assignment.StartTime);
                    totalStability += stabilityScore;
                    count++;
                }
            }

            return count > 0 ? totalStability / count : 1.0;
        }

        private double CalculateAssignmentCost(BiomoleculeOperation operation, FluidInstrument instrument, DateTime startTime)
        {
            var baseCost = operation.EstimatedDurationMs;
            var degradationPenalty = operation.DegradationRisk * 10000;
            var delayPenalty = Math.Max(0, (startTime - operation.SubmissionTime).TotalMilliseconds - operation.StabilityHalfLife.TotalMilliseconds);
            
            return baseCost + degradationPenalty + delayPenalty;
        }

        private double CalculateTotalExecutionTime(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return 0;
            var minStart = solution.Assignments.Min(a => a.StartTime);
            var maxEnd = solution.Assignments.Max(a => a.EndTime);
            return (maxEnd - minStart).TotalMilliseconds;
        }

        private double CalculateTotalCost(SchedulingSolution solution)
        {
            return solution.Assignments.Sum(a => a.Cost);
        }

        private bool ValidateSolution(SchedulingSolution solution)
        {
            return solution.Assignments.All(a => a.EndTime > a.StartTime);
        }
    }

    /// <summary>
    /// Biomolecule-specific operation extension
    /// </summary>
    public class BiomoleculeOperation : TimedFluidOperation
    {
        public string BiomoleculeType { get; set; }
        public BiomoleculeProfile BiomoleculeProfile { get; set; }
        public TimeSpan StabilityHalfLife { get; set; }
        public double DegradationRate { get; set; }
        public double TimeToStabilityLoss { get; set; }
        public BiomoleculeHandling RequiredHandling { get; set; }
        public double OptimalStorageTemp { get; set; }
    }

    /// <summary>
    /// Biomolecule profile with stability and handling requirements
    /// </summary>
    public class BiomoleculeProfile
    {
        public string Name { get; set; }
        public TimeSpan StabilityHalfLife { get; set; }
        public double DegradationRate { get; set; }
        public double TemperatureSensitivity { get; set; }
        public double ContaminationRisk { get; set; }
        public BiomoleculeHandling RequiredHandling { get; set; }
        public double OptimalStorageTemp { get; set; }
    }

    /// <summary>
    /// Handling requirements for different biomolecule types
    /// </summary>
    public enum BiomoleculeHandling
    {
        Universal,
        RNaseFree,
        DNaseFree,
        ProteaseFree,
        ActivityPreserving
    }

    /// <summary>
    /// Configuration for biomolecule scheduler
    /// </summary>
    public class BiomoleculeConfig
    {
        public double MaxAllowedDegradationRisk { get; set; } = 0.3;
        public double MaxTemperatureDeviation { get; set; } = 10.0;
        public bool EnableCrossContaminationPrevention { get; set; } = true;
        public bool EnableStabilityMonitoring { get; set; } = true;
        public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Time slot for instrument scheduling
    /// </summary>
    public class TimeSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int OperationId { get; set; }
        public string BiomoleculeType { get; set; }
    }
}