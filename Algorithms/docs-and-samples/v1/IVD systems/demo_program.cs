using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluidHandling.BasicScheduling;
using FluidHandling.TimeConstrainedScheduling;
using FluidHandling.OptimizationScheduling;
using FluidHandling.AdvancedScheduling;

namespace FluidHandling.IVDDemo
{
    /// <summary>
    /// Demonstration program showing all scheduling algorithms for IVD fluid handling
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== IVD Fluid Handling Automation Scheduler Demo ===");
            Console.WriteLine("Demonstrating algorithms from simple to complex for medical instruments\n");

            // Initialize test data
            var testData = InitializeTestData();
            
            // Run demonstrations
            await DemonstrateBasicAlgorithms(testData);
            await DemonstrateTimeConstrainedAlgorithms(testData);
            await DemonstrateOptimizationAlgorithms(testData);
            await DemonstrateAdvancedAlgorithms(testData);
            
            Console.WriteLine("\n=== Demo Complete ===");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static IVDTestData InitializeTestData()
        {
            Console.WriteLine("Initializing test data for IVD system...");
            
            var testData = new IVDTestData();
            
            // Create fluid handling instruments
            testData.BasicInstruments = new List<FluidInstrument>
            {
                new FluidInstrument { Id = 1, Name = "Pipette-1", Type = "Precision Pipette", MinVolumeCapacity = 1, MaxVolumeCapacity = 1000, IsAvailable = true },
                new FluidInstrument { Id = 2, Name = "Pipette-2", Type = "Precision Pipette", MinVolumeCapacity = 1, MaxVolumeCapacity = 1000, IsAvailable = true },
                new FluidInstrument { Id = 3, Name = "Dispenser-1", Type = "Reagent Dispenser", MinVolumeCapacity = 10, MaxVolumeCapacity = 5000, IsAvailable = true },
                new FluidInstrument { Id = 4, Name = "Washer-1", Type = "Plate Washer", MinVolumeCapacity = 100, MaxVolumeCapacity = 10000, IsAvailable = true }
            };

            // Create basic fluid operations
            testData.BasicOperations = new List<FluidOperation>
            {
                new FluidOperation { Id = 1, SampleId = "S001", OperationType = "Sample Transfer", VolumeInMicroliters = 50, EstimatedDurationMs = 2000, Priority = 1, SubmissionTime = DateTime.Now, Deadline = DateTime.Now.AddMinutes(10), SourceLocation = "A1", DestinationLocation = "B1" },
                new FluidOperation { Id = 2, SampleId = "S002", OperationType = "Reagent Addition", VolumeInMicroliters = 100, EstimatedDurationMs = 1500, Priority = 2, SubmissionTime = DateTime.Now, Deadline = DateTime.Now.AddMinutes(8), SourceLocation = "R1", DestinationLocation = "B2" },
                new FluidOperation { Id = 3, SampleId = "S003", OperationType = "Buffer Exchange", VolumeInMicroliters = 200, EstimatedDurationMs = 3000, Priority = 3, SubmissionTime = DateTime.Now, Deadline = DateTime.Now.AddMinutes(15), SourceLocation = "B1", DestinationLocation = "C1" },
                new FluidOperation { Id = 4, SampleId = "S004", OperationType = "Wash", VolumeInMicroliters = 500, EstimatedDurationMs = 4000, Priority = 1, SubmissionTime = DateTime.Now, Deadline = DateTime.Now.AddMinutes(12), SourceLocation = "C1", DestinationLocation = "W1" }
            };

            // Create time-constrained operations
            testData.TimedOperations = new List<TimedFluidOperation>
            {
                new TimedFluidOperation 
                { 
                    Id = 11, SampleId = "RNA001", OperationType = "RNA Extraction", VolumeInMicroliters = 25, EstimatedDurationMs = 1800, Priority = 5,
                    SubmissionTime = DateTime.Now, Deadline = DateTime.Now.AddMinutes(5), EarliestStartTime = DateTime.Now,
                    LatestStartTime = DateTime.Now.AddMinutes(2), SampleType = "RNA", StabilityTimeMs = 300000, CriticalityScore = 0.95
                },
                new TimedFluidOperation 
                { 
                    Id = 12, SampleId = "DNA001", OperationType = "DNA Amplification", VolumeInMicroliters = 50, EstimatedDurationMs = 2500, Priority = 4,
                    SubmissionTime = DateTime.Now, Deadline = DateTime.Now.AddMinutes(7), EarliestStartTime = DateTime.Now,
                    LatestStartTime = DateTime.Now.AddMinutes(3), SampleType = "DNA", StabilityTimeMs = 600000, CriticalityScore = 0.8
                },
                new TimedFluidOperation 
                { 
                    Id = 13, SampleId = "PRO001", OperationType = "Protein Assay", VolumeInMicroliters = 75, EstimatedDurationMs = 3200, Priority = 3,
                    SubmissionTime = DateTime.Now, Deadline = DateTime.Now.AddMinutes(10), EarliestStartTime = DateTime.Now,
                    LatestStartTime = DateTime.Now.AddMinutes(4), SampleType = "Protein", StabilityTimeMs = 900000, CriticalityScore = 0.7
                }
            };

            // Add time constraints
            testData.TimedOperations[0].TimeConstraints.Add(new TimeConstraintByMutualBoundaries
            {
                Operation1Id = 11,
                Operation2Id = 12,
                MaxTimeDifferenceMs = 60000, // 1 minute
                ConstraintType = "StartToStart",
                Reason = "RNA degradation prevention"
            });

            // Create S-LAB problem
            testData.SLabProblem = CreateSLabProblem(testData);

            Console.WriteLine($"Test data initialized: {testData.BasicInstruments.Count} instruments, {testData.BasicOperations.Count} basic operations, {testData.TimedOperations.Count} timed operations");
            
            return testData;
        }

        private static async Task DemonstrateBasicAlgorithms(IVDTestData testData)
        {
            Console.WriteLine("\n=== BASIC ALGORITHMS DEMONSTRATION ===\n");

            // FCFS Scheduler
            Console.WriteLine("1. First-Come-First-Served (FCFS) Scheduler:");
            var fcfsScheduler = new FCFSScheduler(testData.BasicInstruments);
            foreach (var operation in testData.BasicOperations)
            {
                fcfsScheduler.AddOperation(operation);
            }
            
            for (int i = 0; i < testData.BasicOperations.Count; i++)
            {
                var completed = await fcfsScheduler.ScheduleNext();
                if (completed != null)
                {
                    Console.WriteLine($"   Completed operation {completed.Id} at {completed.CompletionTime}");
                }
            }

            // Reset operations
            testData.BasicOperations.ForEach(op => op.IsCompleted = false);
            testData.BasicInstruments.ForEach(inst => inst.IsAvailable = true);

            // SJF Scheduler
            Console.WriteLine("\n2. Shortest Job First (SJF) Scheduler:");
            var sjfScheduler = new SJFScheduler(testData.BasicInstruments);
            foreach (var operation in testData.BasicOperations)
            {
                sjfScheduler.AddOperation(operation);
            }
            
            for (int i = 0; i < testData.BasicOperations.Count; i++)
            {
                var completed = await sjfScheduler.ScheduleNext();
                if (completed != null)
                {
                    Console.WriteLine($"   Completed operation {completed.Id} (duration: {completed.EstimatedDurationMs}ms) at {completed.CompletionTime}");
                }
            }

            // Reset operations
            testData.BasicOperations.ForEach(op => op.IsCompleted = false);
            testData.BasicInstruments.ForEach(inst => inst.IsAvailable = true);

            // Priority Scheduler
            Console.WriteLine("\n3. Priority-based Scheduler:");
            var priorityScheduler = new PriorityScheduler(testData.BasicInstruments);
            foreach (var operation in testData.BasicOperations)
            {
                priorityScheduler.AddOperation(operation);
            }
            
            for (int i = 0; i < testData.BasicOperations.Count; i++)
            {
                var completed = await priorityScheduler.ScheduleNext();
                if (completed != null)
                {
                    Console.WriteLine($"   Completed operation {completed.Id} (priority: {completed.Priority}) at {completed.CompletionTime}");
                }
            }
        }

        private static async Task DemonstrateTimeConstrainedAlgorithms(IVDTestData testData)
        {
            Console.WriteLine("\n=== TIME-CONSTRAINED ALGORITHMS DEMONSTRATION ===\n");

            // EDF Scheduler
            Console.WriteLine("1. Earliest Deadline First (EDF) Scheduler:");
            var edfScheduler = new EDFScheduler(testData.BasicInstruments);
            foreach (var operation in testData.TimedOperations)
            {
                edfScheduler.AddOperation(operation);
            }
            
            for (int i = 0; i < testData.TimedOperations.Count; i++)
            {
                var completed = await edfScheduler.ScheduleNext();
                if (completed != null)
                {
                    Console.WriteLine($"   Completed operation {completed.Id} (deadline: {completed.Deadline}) at {completed.CompletionTime}");
                }
            }

            // Reset operations
            testData.TimedOperations.ForEach(op => op.IsCompleted = false);
            testData.BasicInstruments.ForEach(inst => inst.IsAvailable = true);

            // TCMB Scheduler
            Console.WriteLine("\n2. Time Constraint by Mutual Boundaries (TCMB) Scheduler:");
            var tcmbScheduler = new TCMBScheduler(testData.BasicInstruments);
            foreach (var operation in testData.TimedOperations)
            {
                tcmbScheduler.AddOperation(operation);
            }
            
            for (int i = 0; i < testData.TimedOperations.Count; i++)
            {
                var completed = await tcmbScheduler.ScheduleNext();
                if (completed != null)
                {
                    Console.WriteLine($"   Completed operation {completed.Id} (criticality: {completed.CriticalityScore}) at {completed.CompletionTime}");
                }
            }
        }

        private static async Task DemonstrateOptimizationAlgorithms(IVDTestData testData)
        {
            Console.WriteLine("\n=== OPTIMIZATION ALGORITHMS DEMONSTRATION ===\n");

            // Greedy Scheduler
            Console.WriteLine("1. Greedy Algorithm Scheduler:");
            var greedyScheduler = new GreedyScheduler(testData.TimedOperations, testData.BasicInstruments);
            var greedySolution = greedyScheduler.GenerateSchedule();
            Console.WriteLine($"   Generated schedule with cost: {greedySolution.Cost:F2}, execution time: {greedySolution.TotalExecutionTime:F2}ms");

            // Simulated Annealing
            Console.WriteLine("\n2. Simulated Annealing Scheduler:");
            var saScheduler = new SimulatedAnnealingScheduler(testData.TimedOperations, testData.BasicInstruments);
            var saSolution = saScheduler.GenerateSchedule();
            Console.WriteLine($"   Generated schedule with cost: {saSolution.Cost:F2}, execution time: {saSolution.TotalExecutionTime:F2}ms");

            // SAGAS Scheduler
            Console.WriteLine("\n3. SAGAS (Simulated Annealing + Greedy) Scheduler:");
            var sagasScheduler = new SAGASScheduler(testData.TimedOperations, testData.BasicInstruments);
            var sagasSolution = await sagasScheduler.GenerateScheduleAsync();
            Console.WriteLine($"   Generated schedule with cost: {sagasSolution.Cost:F2}, execution time: {sagasSolution.TotalExecutionTime:F2}ms");

            // Compare results
            Console.WriteLine("\n   Algorithm Performance Comparison:");
            Console.WriteLine($"   Greedy:     Cost = {greedySolution.Cost:F2}, Time = {greedySolution.TotalExecutionTime:F2}ms");
            Console.WriteLine($"   SA:         Cost = {saSolution.Cost:F2}, Time = {saSolution.TotalExecutionTime:F2}ms");
            Console.WriteLine($"   SAGAS:      Cost = {sagasSolution.Cost:F2}, Time = {sagasSolution.TotalExecutionTime:F2}ms");
        }

        private static async Task DemonstrateAdvancedAlgorithms(IVDTestData testData)
        {
            Console.WriteLine("\n=== ADVANCED ALGORITHMS DEMONSTRATION ===\n");

            // S-LAB Scheduler
            Console.WriteLine("1. S-LAB (Scheduling for Laboratory Automation in Biology) Scheduler:");
            var slabScheduler = new SLabScheduler(testData.SLabProblem);
            var slabSolution = await slabScheduler.ScheduleAsync();
            Console.WriteLine($"   Generated S-LAB schedule with {slabSolution.Assignments.Count} assignments");
            Console.WriteLine($"   Total execution time: {slabSolution.TotalExecutionTime:F2}ms");
            Console.WriteLine($"   Solution cost: {slabSolution.Cost:F2}");

            // Display schedule details
            Console.WriteLine("\n   Schedule Details:");
            foreach (var assignment in slabSolution.Assignments.OrderBy(a => a.StartTime))
            {
                Console.WriteLine($"   Operation {assignment.OperationId} -> Instrument {assignment.InstrumentId} " +
                                $"({assignment.StartTime:HH:mm:ss} - {assignment.EndTime:HH:mm:ss})");
            }
        }

        private static SLabProblem CreateSLabProblem(IVDTestData testData)
        {
            var problem = new SLabProblem();

            // Convert timed operations to S-LAB operations
            foreach (var timedOp in testData.TimedOperations)
            {
                var slabOp = new SLabOperation
                {
                    Id = timedOp.Id,
                    SampleId = timedOp.SampleId,
                    OperationType = timedOp.OperationType,
                    VolumeInMicroliters = timedOp.VolumeInMicroliters,
                    EstimatedDurationMs = timedOp.EstimatedDurationMs,
                    Priority = timedOp.Priority,
                    SubmissionTime = timedOp.SubmissionTime,
                    Deadline = timedOp.Deadline,
                    EarliestStartTime = timedOp.EarliestStartTime,
                    LatestStartTime = timedOp.LatestStartTime,
                    SampleType = timedOp.SampleType,
                    StabilityTimeMs = timedOp.StabilityTimeMs,
                    CriticalityScore = timedOp.CriticalityScore,
                    TimeConstraints = timedOp.TimeConstraints,
                    ProcessingTime = timedOp.EstimatedDurationMs,
                    InstrumentTypeRequired = DetermineInstrumentType(timedOp.OperationType),
                    BiomoleculeType = timedOp.SampleType,
                    BiomoleculeStabilityScore = timedOp.CriticalityScore
                };
                
                problem.Operations.Add(slabOp);
            }

            // Convert basic instruments to S-LAB instruments
            foreach (var basicInst in testData.BasicInstruments)
            {
                var slabInst = new SLabInstrument
                {
                    Id = basicInst.Id,
                    Name = basicInst.Name,
                    Type = basicInst.Type,
                    MinVolumeCapacity = basicInst.MinVolumeCapacity,
                    MaxVolumeCapacity = basicInst.MaxVolumeCapacity,
                    IsAvailable = basicInst.IsAvailable,
                    SupportedOperationTypes = GetSupportedOperationTypes(basicInst.Type),
                    ProcessingCapacity = basicInst.MaxVolumeCapacity,
                    MaintenanceTime = 60000, // 1 minute
                    LastMaintenanceTime = DateTime.Now.AddHours(-1)
                };
                
                problem.Instruments.Add(slabInst);
            }

            // Create jobs
            var job1 = new SLabJob
            {
                Id = 1,
                Name = "RNA Processing Job",
                OperationIds = new List<int> { 11, 12 },
                Priority = 5,
                SubmissionTime = DateTime.Now,
                RequiredCompletionTime = DateTime.Now.AddMinutes(10)
            };
            job1.OperationDependencies[12] = new List<int> { 11 }; // Operation 12 depends on 11

            problem.Jobs.Add(job1);

            // Add time constraints
            foreach (var timedOp in testData.TimedOperations)
            {
                problem.TimeConstraints.AddRange(timedOp.TimeConstraints);
            }

            // Configuration
            problem.Configuration.NumberOfInstruments = testData.BasicInstruments.Count;
            problem.Configuration.InstrumentCounts["Pipette"] = 2;
            problem.Configuration.InstrumentCounts["Dispenser"] = 1;
            problem.Configuration.InstrumentCounts["Washer"] = 1;
            problem.Configuration.HasTransporters = true;
            problem.Configuration.TransporterSpeed = 100; // mm/s
            problem.Configuration.TransporterCapacity = 10;

            return problem;
        }

        private static string DetermineInstrumentType(string operationType)
        {
            return operationType switch
            {
                "RNA Extraction" => "Pipette",
                "DNA Amplification" => "Pipette",
                "Protein Assay" => "Dispenser",
                "Wash" => "Washer",
                _ => "Pipette"
            };
        }

        private static List<string> GetSupportedOperationTypes(string instrumentType)
        {
            return instrumentType switch
            {
                "Precision Pipette" => new List<string> { "RNA Extraction", "DNA Amplification", "Sample Transfer" },
                "Reagent Dispenser" => new List<string> { "Protein Assay", "Reagent Addition" },
                "Plate Washer" => new List<string> { "Wash", "Buffer Exchange" },
                _ => new List<string> { "Sample Transfer" }
            };
        }
    }

    /// <summary>
    /// Test data structure for IVD demonstrations
    /// </summary>
    public class IVDTestData
    {
        public List<FluidInstrument> BasicInstruments { get; set; }
        public List<FluidOperation> BasicOperations { get; set; }
        public List<TimedFluidOperation> TimedOperations { get; set; }
        public SLabProblem SLabProblem { get; set; }

        public IVDTestData()
        {
            BasicInstruments = new List<FluidInstrument>();
            BasicOperations = new List<FluidOperation>();
            TimedOperations = new List<TimedFluidOperation>();
        }
    }

    /// <summary>
    /// Performance analysis utilities
    /// </summary>
    public static class PerformanceAnalyzer
    {
        public static void CompareAlgorithmPerformance(List<SchedulingSolution> solutions, List<string> algorithmNames)
        {
            Console.WriteLine("\n=== ALGORITHM PERFORMANCE COMPARISON ===");
            Console.WriteLine($"{"Algorithm",-20} {"Cost",-15} {"Execution Time",-15} {"Assignments",-12} {"Valid",-8}");
            Console.WriteLine(new string('-', 70));

            for (int i = 0; i < solutions.Count && i < algorithmNames.Count; i++)
            {
                var solution = solutions[i];
                var name = algorithmNames[i];
                
                Console.WriteLine($"{name,-20} {solution.Cost,-15:F2} {solution.TotalExecutionTime,-15:F2} {solution.Assignments.Count,-12} {solution.IsValid,-8}");
            }
        }

        public static void AnalyzeResourceUtilization(SchedulingSolution solution, List<FluidInstrument> instruments)
        {
            Console.WriteLine("\n=== RESOURCE UTILIZATION ANALYSIS ===");
            
            foreach (var instrument in instruments)
            {
                var assignments = solution.Assignments.Where(a => a.InstrumentId == instrument.Id).ToList();
                var totalTime = assignments.Sum(a => (a.EndTime - a.StartTime).TotalMilliseconds);
                var utilization = totalTime / solution.TotalExecutionTime * 100;
                
                Console.WriteLine($"{instrument.Name}: {assignments.Count} assignments, {utilization:F1}% utilization");
            }
        }
    }
}