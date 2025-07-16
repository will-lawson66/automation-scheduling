/*
===============================================================================
Visual Studio 2022 Solution Structure for IVD Fluid Handling Automation
===============================================================================

Solution: IVDFluidHandlingAutomation.sln

Projects:
├── 1. FluidHandling.Core (Class Library - .NET 6.0)
│   ├── Models/
│   │   ├── FluidOperation.cs
│   │   ├── FluidInstrument.cs
│   │   ├── TimeConstraint.cs
│   │   └── SchedulingSolution.cs
│   ├── Interfaces/
│   │   ├── IScheduler.cs
│   │   ├── IInstrument.cs
│   │   └── IPerformanceMetrics.cs
│   └── Utilities/
│       ├── PerformanceCalculator.cs
│       └── ValidationHelper.cs
│
├── 2. FluidHandling.BasicScheduling (Class Library - .NET 6.0)
│   ├── FCFSScheduler.cs
│   ├── SJFScheduler.cs
│   ├── PriorityScheduler.cs
│   └── RoundRobinScheduler.cs
│
├── 3. FluidHandling.TimeConstrainedScheduling (Class Library - .NET 6.0)
│   ├── EDFScheduler.cs
│   ├── RMSScheduler.cs
│   ├── TCMBScheduler.cs
│   └── Models/
│       └── TimedFluidOperation.cs
│
├── 4. FluidHandling.OptimizationScheduling (Class Library - .NET 6.0)
│   ├── GreedyScheduler.cs
│   ├── SimulatedAnnealingScheduler.cs
│   ├── SAGASScheduler.cs
│   └── GeneticAlgorithmScheduler.cs
│
├── 5. FluidHandling.AdvancedScheduling (Class Library - .NET 6.0)
│   ├── SLabScheduler.cs
│   ├── MIPScheduler.cs
│   ├── ReinforcementLearningScheduler.cs
│   └── Models/
│       ├── SLabProblem.cs
│       └── SLabOperation.cs
│
├── 6. FluidHandling.IVDSpecific (Class Library - .NET 6.0)
│   ├── BiomoleculeScheduler.cs
│   ├── ClinicalValidationScheduler.cs
│   ├── QualityControlScheduler.cs
│   └── RegulatoryComplianceScheduler.cs
│
├── 7. FluidHandling.Performance (Class Library - .NET 6.0)
│   ├── PerformanceAnalyzer.cs
│   ├── BenchmarkRunner.cs
│   └── MetricsCollector.cs
│
├── 8. FluidHandling.Integration (Class Library - .NET 6.0)
│   ├── OPCUAIntegration.cs
│   ├── MESIntegration.cs
│   └── LIMSIntegration.cs
│
├── 9. FluidHandling.Demo (Console Application - .NET 6.0)
│   ├── Program.cs
│   ├── TestDataGenerator.cs
│   └── DemoScenarios.cs
│
└── 10. FluidHandling.Tests (Unit Test Project - .NET 6.0)
    ├── BasicSchedulingTests.cs
    ├── TimeConstrainedSchedulingTests.cs
    ├── OptimizationSchedulingTests.cs
    └── AdvancedSchedulingTests.cs

References:
- All projects reference FluidHandling.Core
- Demo project references all other projects
- Test project references all projects for comprehensive testing

NuGet Packages:
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging
- System.Threading.Tasks.Extensions
- Newtonsoft.Json (for configuration)
- NUnit (for testing)
- BenchmarkDotNet (for performance testing)

Build Configuration:
- Debug: Full debugging symbols, no optimization
- Release: Optimized code, minimal symbols
- Benchmark: Special configuration for performance testing

===============================================================================
*/

// FluidHandling.Core/Models/FluidOperation.cs
using System;
using System.Collections.Generic;

namespace FluidHandling.Core.Models
{
    /// <summary>
    /// Base class for all fluid handling operations in IVD systems
    /// </summary>
    public class FluidOperation
    {
        public int Id { get; set; }
        public string SampleId { get; set; }
        public string OperationType { get; set; }
        public double VolumeInMicroliters { get; set; }
        public int EstimatedDurationMs { get; set; }
        public int Priority { get; set; }
        public DateTime SubmissionTime { get; set; }
        public DateTime Deadline { get; set; }
        public string SourceLocation { get; set; }
        public string DestinationLocation { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletionTime { get; set; }
        public OperationStatus Status { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public FluidOperation()
        {
            Tags = new List<string>();
            Properties = new Dictionary<string, object>();
            Status = OperationStatus.Pending;
        }

        public virtual bool CanExecuteOn(FluidInstrument instrument)
        {
            return instrument.IsAvailable && 
                   instrument.MaxVolumeCapacity >= VolumeInMicroliters &&
                   instrument.MinVolumeCapacity <= VolumeInMicroliters;
        }
    }

    public enum OperationStatus
    {
        Pending,
        Queued,
        Executing,
        Completed,
        Failed,
        Cancelled
    }
}

// FluidHandling.Core/Interfaces/IScheduler.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluidHandling.Core.Models;

namespace FluidHandling.Core.Interfaces
{
    /// <summary>
    /// Interface for all scheduling algorithms
    /// </summary>
    public interface IScheduler
    {
        string Name { get; }
        string Description { get; }
        SchedulerType Type { get; }
        
        void AddOperation(FluidOperation operation);
        void AddOperations(IEnumerable<FluidOperation> operations);
        Task<SchedulingSolution> GenerateScheduleAsync();
        Task<FluidOperation> ScheduleNextAsync();
        void UpdateConfiguration(SchedulerConfiguration config);
        PerformanceMetrics GetPerformanceMetrics();
    }

    public enum SchedulerType
    {
        Basic,
        TimeConstrained,
        Optimization,
        Advanced,
        IVDSpecific
    }

    public class SchedulerConfiguration
    {
        public Dictionary<string, object> Parameters { get; set; }
        public bool EnableLogging { get; set; }
        public bool EnablePerformanceMonitoring { get; set; }
        public TimeSpan MaxExecutionTime { get; set; }

        public SchedulerConfiguration()
        {
            Parameters = new Dictionary<string, object>();
            EnableLogging = true;
            EnablePerformanceMonitoring = false;
            MaxExecutionTime = TimeSpan.FromHours(1);
        }
    }
}

// FluidHandling.Core/Models/PerformanceMetrics.cs
using System;
using System.Collections.Generic;

namespace FluidHandling.Core.Models
{
    /// <summary>
    /// Comprehensive performance metrics for IVD scheduling systems
    /// Based on research showing importance of multi-objective evaluation
    /// </summary>
    public class PerformanceMetrics
    {
        // Primary Metrics
        public double Makespan { get; set; }          // Total execution time
        public double Throughput { get; set; }       // Operations per unit time
        public double Utilization { get; set; }      // Resource utilization percentage
        public double Cost { get; set; }             // Total cost metric
        
        // Quality Metrics
        public double DeadlineMissRate { get; set; }        // Percentage of missed deadlines
        public double SampleDegradationRate { get; set; }   // Percentage of degraded samples
        public double QualityScore { get; set; }            // Overall quality assessment
        
        // Efficiency Metrics
        public double EnergyConsumption { get; set; }       // Total energy consumed
        public double WasteGeneration { get; set; }         // Waste volume generated
        public double ReagentEfficiency { get; set; }       // Reagent utilization efficiency
        
        // Reliability Metrics
        public double SuccessRate { get; set; }             // Operation success rate
        public double ErrorRate { get; set; }               // Error occurrence rate
        public double MeanTimeToFailure { get; set; }       // MTTF in minutes
        
        // Time-based Metrics
        public double AverageWaitTime { get; set; }         // Average operation wait time
        public double AverageResponseTime { get; set; }     // Average response time
        public double TardinessPenalty { get; set; }        // Penalty for late completions
        
        // IVD-Specific Metrics
        public double BiomoleculeStabilityScore { get; set; }  // Sample stability preservation
        public double CrossContaminationRisk { get; set; }     // Risk of sample contamination
        public double RegulatoryComplianceScore { get; set; }  // Compliance with regulations
        
        // Statistical Metrics
        public double StandardDeviation { get; set; }       // Variability in performance
        public double ConfidenceInterval { get; set; }      // 95% confidence interval
        public int NumberOfSamples { get; set; }            // Sample size for statistics
        
        // Timestamps
        public DateTime MeasurementStart { get; set; }
        public DateTime MeasurementEnd { get; set; }
        public TimeSpan MeasurementDuration => MeasurementEnd - MeasurementStart;
        
        // Additional Details
        public Dictionary<string, double> CustomMetrics { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Errors { get; set; }

        public PerformanceMetrics()
        {
            CustomMetrics = new Dictionary<string, double>();
            Warnings = new List<string>();
            Errors = new List<string>();
            MeasurementStart = DateTime.Now;
        }

        public void FinalizeMeasurement()
        {
            MeasurementEnd = DateTime.Now;
        }

        public double CalculateOverallScore()
        {
            // Weighted combination of key metrics
            double score = 0;
            score += (1.0 / (1.0 + Makespan / 60000.0)) * 0.2;      // Makespan weight: 20%
            score += Throughput * 0.15;                              // Throughput weight: 15%
            score += Utilization * 0.15;                             // Utilization weight: 15%
            score += (1.0 - DeadlineMissRate) * 0.2;                // Deadline adherence: 20%
            score += SuccessRate * 0.15;                             // Success rate: 15%
            score += BiomoleculeStabilityScore * 0.1;                // Stability: 10%
            score += RegulatoryComplianceScore * 0.05;               // Compliance: 5%
            
            return Math.Max(0, Math.Min(1.0, score));
        }
    }
}