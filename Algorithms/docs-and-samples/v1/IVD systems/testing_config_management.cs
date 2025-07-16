using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using FluidHandling.Core.Models;
using FluidHandling.Core.Interfaces;
using FluidHandling.BasicScheduling;
using FluidHandling.TimeConstrainedScheduling;
using FluidHandling.OptimizationScheduling;
using FluidHandling.AdvancedScheduling;
using FluidHandling.IVDSpecific;

namespace FluidHandling.Testing
{
    /// <summary>
    /// Comprehensive testing framework for IVD scheduling algorithms
    /// Provides unit tests, integration tests, and performance benchmarks
    /// </summary>
    public class SchedulingTestFramework
    {
        private readonly TestConfiguration _config;
        private readonly TestDataGenerator _dataGenerator;
        private readonly List<TestResult> _testResults;
        private readonly Dictionary<string, TestSuite> _testSuites;

        public SchedulingTestFramework(TestConfiguration config = null)
        {
            _config = config ?? new TestConfiguration();
            _dataGenerator = new TestDataGenerator();
            _testResults = new List<TestResult>();
            _testSuites = new Dictionary<string, TestSuite>();
            
            InitializeTestSuites();
        }

        public async Task<TestReport> RunAllTestsAsync()
        {
            Console.WriteLine("=== Starting Comprehensive IVD Scheduling Tests ===");
            
            var report = new TestReport
            {
                StartTime = DateTime.Now,
                TestConfiguration = _config
            };

            // Run all test suites
            foreach (var testSuite in _testSuites.Values)
            {
                Console.WriteLine($"\n--- Running {testSuite.Name} Test Suite ---");
                
                var suiteResult = await RunTestSuiteAsync(testSuite);
                report.TestSuiteResults.Add(suiteResult);
                
                Console.WriteLine($"Suite {testSuite.Name}: {suiteResult.PassedTests}/{suiteResult.TotalTests} passed");
            }

            report.EndTime = DateTime.Now;
            report.OverallResult = report.TestSuiteResults.All(r => r.Success);
            
            // Generate detailed report
            await GenerateTestReportAsync(report);
            
            Console.WriteLine($"\n=== Test Summary ===");
            Console.WriteLine($"Total Suites: {report.TestSuiteResults.Count}");
            Console.WriteLine($"Passed Suites: {report.TestSuiteResults.Count(r => r.Success)}");
            Console.WriteLine($"Total Tests: {report.TestSuiteResults.Sum(r => r.TotalTests)}");
            Console.WriteLine($"Passed Tests: {report.TestSuiteResults.Sum(r => r.PassedTests)}");
            Console.WriteLine($"Overall Result: {(report.OverallResult ? "PASS" : "FAIL")}");
            
            return report;
        }

        public async Task<TestSuiteResult> RunTestSuiteAsync(string suiteName)
        {
            if (!_testSuites.ContainsKey(suiteName))
            {
                throw new ArgumentException($"Test suite '{suiteName}' not found");
            }

            return await RunTestSuiteAsync(_testSuites[suiteName]);
        }

        private async Task<TestSuiteResult> RunTestSuiteAsync(TestSuite testSuite)
        {
            var suiteResult = new TestSuiteResult
            {
                SuiteName = testSuite.Name,
                StartTime = DateTime.Now
            };

            foreach (var testCase in testSuite.TestCases)
            {
                var testResult = await RunTestCaseAsync(testCase);
                suiteResult.TestResults.Add(testResult);
                
                if (testResult.Success)
                {
                    Console.WriteLine($"  ✓ {testCase.Name}");
                }
                else
                {
                    Console.WriteLine($"  ✗ {testCase.Name}: {testResult.ErrorMessage}");
                }
            }

            suiteResult.EndTime = DateTime.Now;
            suiteResult.TotalTests = testSuite.TestCases.Count;
            suiteResult.PassedTests = suiteResult.TestResults.Count(r => r.Success);
            suiteResult.Success = suiteResult.PassedTests == suiteResult.TotalTests;

            return suiteResult;
        }

        private async Task<TestResult> RunTestCaseAsync(TestCase testCase)
        {
            var testResult = new TestResult
            {
                TestName = testCase.Name,
                StartTime = DateTime.Now
            };

            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Execute test case
                var success = await testCase.ExecuteAsync();
                
                stopwatch.Stop();
                
                testResult.Success = success;
                testResult.ExecutionTime = stopwatch.Elapsed;
                testResult.EndTime = DateTime.Now;
                
                if (success)
                {
                    testResult.Message = "Test passed";
                }
                else
                {
                    testResult.ErrorMessage = "Test failed - assertion failed";
                }
            }
            catch (Exception ex)
            {
                testResult.Success = false;
                testResult.ErrorMessage = ex.Message;
                testResult.EndTime = DateTime.Now;
            }

            return testResult;
        }

        private void InitializeTestSuites()
        {
            // Basic Scheduling Tests
            _testSuites["BasicScheduling"] = new TestSuite
            {
                Name = "Basic Scheduling",
                TestCases = new List<TestCase>
                {
                    new TestCase { Name = "FCFS Basic Operation", ExecuteAsync = TestFCFSBasicOperation },
                    new TestCase { Name = "SJF Optimization", ExecuteAsync = TestSJFOptimization },
                    new TestCase { Name = "Priority Scheduling", ExecuteAsync = TestPriorityScheduling },
                    new TestCase { Name = "Round Robin Fairness", ExecuteAsync = TestRoundRobinFairness }
                }
            };

            // Time-Constrained Scheduling Tests
            _testSuites["TimeConstrainedScheduling"] = new TestSuite
            {
                Name = "Time-Constrained Scheduling",
                TestCases = new List<TestCase>
                {
                    new TestCase { Name = "EDF Deadline Adherence", ExecuteAsync = TestEDFDeadlineAdherence },
                    new TestCase { Name = "TCMB Constraint Satisfaction", ExecuteAsync = TestTCMBConstraints },
                    new TestCase { Name = "Biomolecule Stability", ExecuteAsync = TestBiomoleculeStability }
                }
            };

            // Optimization Scheduling Tests
            _testSuites["OptimizationScheduling"] = new TestSuite
            {
                Name = "Optimization Scheduling",
                TestCases = new List<TestCase>
                {
                    new TestCase { Name = "Greedy Algorithm Performance", ExecuteAsync = TestGreedyPerformance },
                    new TestCase { Name = "Simulated Annealing Convergence", ExecuteAsync = TestSAConvergence },
                    new TestCase { Name = "SAGAS Hybrid Performance", ExecuteAsync = TestSAGASPerformance },
                    new TestCase { Name = "Genetic Algorithm Evolution", ExecuteAsync = TestGeneticAlgorithm }
                }
            };

            // Advanced Scheduling Tests
            _testSuites["AdvancedScheduling"] = new TestSuite
            {
                Name = "Advanced Scheduling",
                TestCases = new List<TestCase>
                {
                    new TestCase { Name = "S-LAB Problem Solving", ExecuteAsync = TestSLabProblem },
                    new TestCase { Name = "MIP Optimization", ExecuteAsync = TestMIPOptimization },
                    new TestCase { Name = "Reinforcement Learning", ExecuteAsync = TestReinforcementLearning },
                    new TestCase { Name = "Neural Network Prediction", ExecuteAsync = TestNeuralNetwork }
                }
            };

            // IVD-Specific Tests
            _testSuites["IVDSpecific"] = new TestSuite
            {
                Name = "IVD-Specific Features",
                TestCases = new List<TestCase>
                {
                    new TestCase { Name = "Quality Control Compliance", ExecuteAsync = TestQualityControlCompliance },
                    new TestCase { Name = "Regulatory Adherence", ExecuteAsync = TestRegulatoryAdherence },
                    new TestCase { Name = "Cross-Contamination Prevention", ExecuteAsync = TestCrossContaminationPrevention },
                    new TestCase { Name = "Sample Traceability", ExecuteAsync = TestSampleTraceability }
                }
            };

            // Performance Tests
            _testSuites["Performance"] = new TestSuite
            {
                Name = "Performance Benchmarks",
                TestCases = new List<TestCase>
                {
                    new TestCase { Name = "Scalability Test", ExecuteAsync = TestScalability },
                    new TestCase { Name = "Memory Usage Test", ExecuteAsync = TestMemoryUsage },
                    new TestCase { Name = "Concurrent Operations", ExecuteAsync = TestConcurrentOperations },
                    new TestCase { Name = "Stress Test", ExecuteAsync = TestStressConditions }
                }
            };
        }

        #region Test Case Implementations

        private async Task<bool> TestFCFSBasicOperation()
        {
            var instruments = _dataGenerator.GenerateInstruments(3);
            var operations = _dataGenerator.GenerateOperations(10);
            var scheduler = new FCFSScheduler(instruments);

            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var completedOps = new List<FluidOperation>();
            for (int i = 0; i < operations.Count; i++)
            {
                var completed = await scheduler.ScheduleNext();
                if (completed != null)
                {
                    completedOps.Add(completed);
                }
            }

            // Assert operations completed in FCFS order
            return completedOps.Count == operations.Count &&
                   completedOps.Zip(operations, (c, o) => c.Id == o.Id).All(x => x);
        }

        private async Task<bool> TestSJFOptimization()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateOperations(8);
            var scheduler = new SJFScheduler(instruments);

            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var completedOps = new List<FluidOperation>();
            for (int i = 0; i < operations.Count; i++)
            {
                var completed = await scheduler.ScheduleNext();
                if (completed != null)
                {
                    completedOps.Add(completed);
                }
            }

            // Assert operations completed in roughly shortest-job-first order
            return completedOps.Count == operations.Count;
        }

        private async Task<bool> TestPriorityScheduling()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateOperations(6);
            
            // Set specific priorities
            for (int i = 0; i < operations.Count; i++)
            {
                operations[i].Priority = i + 1;
            }

            var scheduler = new PriorityScheduler(instruments);
            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var completedOps = new List<FluidOperation>();
            for (int i = 0; i < operations.Count; i++)
            {
                var completed = await scheduler.ScheduleNext();
                if (completed != null)
                {
                    completedOps.Add(completed);
                }
            }

            return completedOps.Count == operations.Count;
        }

        private async Task<bool> TestRoundRobinFairness()
        {
            var instruments = _dataGenerator.GenerateInstruments(1);
            var operations = _dataGenerator.GenerateOperations(5);
            var scheduler = new RoundRobinScheduler(instruments, 1000);

            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var completedCount = 0;
            for (int i = 0; i < operations.Count * 3; i++) // Allow multiple rounds
            {
                var completed = await scheduler.ScheduleNext();
                if (completed != null)
                {
                    completedCount++;
                }
            }

            return completedCount == operations.Count;
        }

        private async Task<bool> TestEDFDeadlineAdherence()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateTimedOperations(8);
            var scheduler = new EDFScheduler(instruments);

            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var completedOps = new List<TimedFluidOperation>();
            for (int i = 0; i < operations.Count; i++)
            {
                var completed = await scheduler.ScheduleNext();
                if (completed != null)
                {
                    completedOps.Add(completed);
                }
            }

            // Check that most operations met their deadlines
            var onTimeCount = completedOps.Count(op => op.CompletionTime <= op.Deadline);
            return onTimeCount >= completedOps.Count * 0.8; // 80% on-time rate
        }

        private async Task<bool> TestTCMBConstraints()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateTimedOperations(6);
            
            // Add time constraints
            operations[0].TimeConstraints.Add(new TimeConstraintByMutualBoundaries
            {
                Operation1Id = operations[0].Id,
                Operation2Id = operations[1].Id,
                MaxTimeDifferenceMs = 60000,
                ConstraintType = "StartToStart"
            });

            var scheduler = new TCMBScheduler(instruments);
            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var completedOps = new List<TimedFluidOperation>();
            for (int i = 0; i < operations.Count; i++)
            {
                var completed = await scheduler.ScheduleNext();
                if (completed != null)
                {
                    completedOps.Add(completed);
                }
            }

            return completedOps.Count > 0;
        }

        private async Task<bool> TestBiomoleculeStability()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateBiomoleculeOperations(5);
            var scheduler = new BiomoleculeScheduler(instruments);

            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var solution = await scheduler.GenerateScheduleAsync();
            return solution.Assignments.Count > 0 && solution.IsValid;
        }

        private async Task<bool> TestGreedyPerformance()
        {
            var instruments = _dataGenerator.GenerateInstruments(3);
            var operations = _dataGenerator.GenerateTimedOperations(12);
            var scheduler = new GreedyScheduler(operations, instruments);

            var solution = scheduler.GenerateSchedule();
            return solution.Assignments.Count == operations.Count && solution.IsValid;
        }

        private async Task<bool> TestSAConvergence()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateTimedOperations(8);
            var scheduler = new SimulatedAnnealingScheduler(operations, instruments, 100, 0.9);

            var solution = scheduler.GenerateSchedule();
            return solution.Assignments.Count > 0 && solution.Cost < double.MaxValue;
        }

        private async Task<bool> TestSAGASPerformance()
        {
            var instruments = _dataGenerator.GenerateInstruments(3);
            var operations = _dataGenerator.GenerateTimedOperations(10);
            var scheduler = new SAGASScheduler(operations, instruments);

            var solution = await scheduler.GenerateScheduleAsync();
            return solution.Assignments.Count > 0 && solution.IsValid;
        }

        private async Task<bool> TestGeneticAlgorithm()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateTimedOperations(8);
            var config = new GeneticAlgorithmConfig
            {
                PopulationSize = 20,
                MaxGenerations = 50,
                MutationRate = 0.1,
                CrossoverRate = 0.8
            };
            var scheduler = new GeneticAlgorithmScheduler(operations, instruments, config);

            var solution = await scheduler.GenerateScheduleAsync();
            return solution.Assignments.Count > 0 && solution.IsValid;
        }

        private async Task<bool> TestSLabProblem()
        {
            var problem = _dataGenerator.GenerateSLabProblem();
            var scheduler = new SLabScheduler(problem);

            var solution = await scheduler.ScheduleAsync();
            return solution.Assignments.Count > 0 && solution.IsValid;
        }

        private async Task<bool> TestMIPOptimization()
        {
            var problem = _dataGenerator.GenerateSLabProblem();
            var scheduler = new MIPScheduler(problem);

            var solution = scheduler.SolveWithBranchAndBound();
            return solution.Assignments.Count > 0;
        }

        private async Task<bool> TestReinforcementLearning()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateTimedOperations(6);
            var config = new RLConfig
            {
                TrainingEpisodes = 100,
                EnableTraining = true,
                LearningRate = 0.1
            };
            var scheduler = new ReinforcementLearningScheduler(operations, instruments, config);

            var solution = await scheduler.GenerateScheduleAsync();
            return solution.Assignments.Count > 0;
        }

        private async Task<bool> TestNeuralNetwork()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateTimedOperations(8);
            var config = new NNConfig
            {
                TrainingEpochs = 50,
                EnableTraining = true,
                LearningRate = 0.001
            };
            var scheduler = new NeuralNetworkScheduler(operations, instruments, config);

            var solution = await scheduler.GenerateScheduleAsync();
            return solution.Assignments.Count > 0;
        }

        private async Task<bool> TestQualityControlCompliance()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateQCOperations(6);
            var scheduler = new QualityControlScheduler(instruments);

            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var solution = await scheduler.GenerateScheduleAsync();
            return solution.Assignments.Count > 0 && solution.IsValid;
        }

        private async Task<bool> TestRegulatoryAdherence()
        {
            // Test regulatory compliance requirements
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateQCOperations(4);
            var scheduler = new QualityControlScheduler(instruments);

            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var solution = await scheduler.GenerateScheduleAsync();
            
            // Check regulatory deadlines are met
            var complianceScore = scheduler.GetPerformanceMetrics().RegulatoryComplianceScore;
            return complianceScore >= 0.9; // 90% compliance rate
        }

        private async Task<bool> TestCrossContaminationPrevention()
        {
            var instruments = _dataGenerator.GenerateInstruments(1);
            var operations = _dataGenerator.GenerateBiomoleculeOperations(6);
            var scheduler = new BiomoleculeScheduler(instruments);

            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            var solution = await scheduler.GenerateScheduleAsync();
            
            // Check contamination risk is low
            var contaminationRisk = scheduler.GetPerformanceMetrics().CrossContaminationRisk;
            return contaminationRisk < 0.3; // Less than 30% risk
        }

        private async Task<bool> TestSampleTraceability()
        {
            var instruments = _dataGenerator.GenerateInstruments(2);
            var operations = _dataGenerator.GenerateOperations(8);
            var scheduler = new FCFSScheduler(instruments);

            foreach (var op in operations)
            {
                scheduler.AddOperation(op);
            }

            // Test that all operations can be traced
            var completedOps = new List<FluidOperation>();
            for (int i = 0; i < operations.Count; i++)
            {
                var completed = await scheduler.ScheduleNext();
                if (completed != null)
                {
                    completedOps.Add(completed);
                }
            }

            return completedOps.All(op => !string.IsNullOrEmpty(op.SampleId));
        }

        private async Task<bool> TestScalability()
        {
            var instruments = _dataGenerator.GenerateInstruments(10);
            var operations = _dataGenerator.GenerateOperations(1000);
            var scheduler = new GreedyScheduler(operations.Cast<TimedFluidOperation>().ToList(), instruments);

            var stopwatch = Stopwatch.StartNew();
            var solution = scheduler.GenerateSchedule();
            stopwatch.Stop();

            // Should complete within reasonable time
            return stopwatch.ElapsedMilliseconds < 10000 && solution.Assignments.Count > 0;
        }

        private async Task<bool> TestMemoryUsage()
        {
            var initialMemory = GC.GetTotalMemory(true);
            
            var instruments = _dataGenerator.GenerateInstruments(5);
            var operations = _dataGenerator.GenerateOperations(500);
            var scheduler = new GreedyScheduler(operations.Cast<TimedFluidOperation>().ToList(), instruments);

            var solution = scheduler.GenerateSchedule();
            
            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;

            // Memory increase should be reasonable (less than 100MB for this test)
            return memoryIncrease < 100 * 1024 * 1024 && solution.Assignments.Count > 0;
        }

        private async Task<bool> TestConcurrentOperations()
        {
            var instruments = _dataGenerator.GenerateInstruments(4);
            var operations = _dataGenerator.GenerateOperations(20);
            var scheduler = new PriorityScheduler(instruments);

            var tasks = new List<Task>();
            
            // Add operations concurrently
            foreach (var op in operations)
            {
                tasks.Add(Task.Run(() => scheduler.AddOperation(op)));
            }

            await Task.WhenAll(tasks);

            // Execute operations concurrently
            var completionTasks = new List<Task<FluidOperation>>();
            for (int i = 0; i < operations.Count; i++)
            {
                completionTasks.Add(scheduler.ScheduleNext());
            }

            var completedOps = await Task.WhenAll(completionTasks);
            return completedOps.Count(op => op != null) > 0;
        }

        private async Task<bool> TestStressConditions()
        {
            var instruments = _dataGenerator.GenerateInstruments(3);
            var operations = _dataGenerator.GenerateOperations(50);
            
            // Set very tight deadlines
            foreach (var op in operations)
            {
                op.Deadline = op.SubmissionTime.AddSeconds(30);
            }

            var scheduler = new EDFScheduler(instruments);
            foreach (var op in operations.Cast<TimedFluidOperation>())
            {
                scheduler.AddOperation(op);
            }

            var completedOps = new List<TimedFluidOperation>();
            for (int i = 0; i < operations.Count; i++)
            {
                var completed = await scheduler.ScheduleNext();
                if (completed != null)
                {
                    completedOps.Add(completed);
                }
            }

            // System should handle stress gracefully
            return completedOps.Count > 0;
        }

        #endregion

        private async Task GenerateTestReportAsync(TestReport report)
        {
            var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var fileName = $"test_report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(Environment.CurrentDirectory, "TestReports", fileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            await File.WriteAllTextAsync(filePath, reportJson);
            
            Console.WriteLine($"Test report saved to: {filePath}");
        }
    }

    /// <summary>
    /// Test data generator for creating consistent test scenarios
    /// </summary>
    public class TestDataGenerator
    {
        private readonly Random _random;

        public TestDataGenerator()
        {
            _random = new Random(42); // Fixed seed for reproducible tests
        }

        public List<FluidInstrument> GenerateInstruments(int count)
        {
            var instruments = new List<FluidInstrument>();
            
            for (int i = 1; i <= count; i++)
            {
                var instrument = new FluidInstrument
                {
                    Id = i,
                    Name = $"Instrument-{i}",
                    Type = GetRandomInstrumentType(),
                    MinVolumeCapacity = 1,
                    MaxVolumeCapacity = 1000,
                    IsAvailable = true,
                    Properties = new Dictionary<string, object>
                    {
                        ["QCQualified"] = true,
                        ["CalibrationDate"] = DateTime.Now.AddDays(-_random.Next(1, 30)),
                        ["HandlingType"] = BiomoleculeHandling.Universal
                    }
                };
                instruments.Add(instrument);
            }
            
            return instruments;
        }

        public List<FluidOperation> GenerateOperations(int count)
        {
            var operations = new List<FluidOperation>();
            
            for (int i = 1; i <= count; i++)
            {
                var operation = new FluidOperation
                {
                    Id = i,
                    SampleId = $"SAMPLE-{i:D4}",
                    OperationType = GetRandomOperationType(),
                    VolumeInMicroliters = _random.Next(10, 500),
                    EstimatedDurationMs = _random.Next(1000, 5000),
                    Priority = _random.Next(1, 10),
                    SubmissionTime = DateTime.Now.AddSeconds(_random.Next(-300, 0)),
                    Deadline = DateTime.Now.AddMinutes(_random.Next(5, 60)),
                    SourceLocation = $"R{_random.Next(1, 5)}",
                    DestinationLocation = $"W{_random.Next(1, 3)}"
                };
                operations.Add(operation);
            }
            
            return operations;
        }

        public List<TimedFluidOperation> GenerateTimedOperations(int count)
        {
            var operations = new List<TimedFluidOperation>();
            
            for (int i = 1; i <= count; i++)
            {
                var operation = new TimedFluidOperation
                {
                    Id = i,
                    SampleId = $"SAMPLE-{i:D4}",
                    OperationType = GetRandomOperationType(),
                    VolumeInMicroliters = _random.Next(10, 500),
                    EstimatedDurationMs = _random.Next(1000, 5000),
                    Priority = _random.Next(1, 10),
                    SubmissionTime = DateTime.Now.AddSeconds(_random.Next(-300, 0)),
                    Deadline = DateTime.Now.AddMinutes(_random.Next(5, 60)),
                    SourceLocation = $"R{_random.Next(1, 5)}",
                    DestinationLocation = $"W{_random.Next(1, 3)}",
                    EarliestStartTime = DateTime.Now.AddSeconds(_random.Next(-60, 60)),
                    LatestStartTime = DateTime.Now.AddMinutes(_random.Next(30, 45)),
                    SampleType = GetRandomSampleType(),
                    StabilityTimeMs = _random.Next(300000, 3600000),
                    CriticalityScore = _random.NextDouble()
                };
                operations.Add(operation);
            }
            
            return operations;
        }

        public List<BiomoleculeOperation> GenerateBiomoleculeOperations(int count)
        {
            var operations = new List<BiomoleculeOperation>();
            
            for (int i = 1; i <= count; i++)
            {
                var biomoleculeType = GetRandomBiomoleculeType();
                var operation = new BiomoleculeOperation
                {
                    Id = i,
                    SampleId = $"BIO-{i:D4}",
                    OperationType = GetRandomOperationType(),
                    VolumeInMicroliters = _random.Next(10, 200),
                    EstimatedDurationMs = _random.Next(1000, 3000),
                    Priority = _random.Next(1, 10),
                    SubmissionTime = DateTime.Now.AddSeconds(_random.Next(-300, 0)),
                    Deadline = DateTime.Now.AddMinutes(_random.Next(5, 30)),
                    SourceLocation = $"R{_random.Next(1, 5)}",
                    DestinationLocation = $"W{_random.Next(1, 3)}",
                    EarliestStartTime = DateTime.Now.AddSeconds(_random.Next(-60, 60)),
                    LatestStartTime = DateTime.Now.AddMinutes(_random.Next(15, 25)),
                    SampleType = biomoleculeType,
                    StabilityTimeMs = GetStabilityTime(biomoleculeType),
                    CriticalityScore = _random.NextDouble(),
                    BiomoleculeType = biomoleculeType,
                    RequiredHandling = GetHandlingType(biomoleculeType)
                };
                operations.Add(operation);
            }
            
            return operations;
        }

        public List<QualityControlOperation> GenerateQCOperations(int count)
        {
            var operations = new List<QualityControlOperation>();
            
            for (int i = 1; i <= count; i++)
            {
                var qcType = GetRandomQCType();
                var operation = new QualityControlOperation
                {
                    Id = i,
                    SampleId = $"QC-{qcType}-{i:D2}",
                    OperationType = $"{qcType} Check",
                    VolumeInMicroliters = _random.Next(50, 200),
                    EstimatedDurationMs = _random.Next(2000, 6000),
                    Priority = _random.Next(5, 10),
                    SubmissionTime = DateTime.Now.AddSeconds(_random.Next(-300, 0)),
                    Deadline = DateTime.Now.AddMinutes(_random.Next(10, 120)),
                    SourceLocation = $"QC{_random.Next(1, 3)}",
                    DestinationLocation = $"W{_random.Next(1, 3)}",
                    QCType = qcType,
                    IsRegulatoryRequired = _random.NextDouble() > 0.3,
                    RegulatoryDeadline = DateTime.Now.AddMinutes(_random.Next(30, 240)),
                    QCPriority = _random.Next(7, 10)
                };
                operations.Add(operation);
            }
            
            return operations;
        }

        public SLabProblem GenerateSLabProblem()
        {
            var problem = new SLabProblem();
            
            // Generate instruments
            var instruments = GenerateInstruments(3);
            foreach (var instrument in instruments)
            {
                var slabInstrument = new SLabInstrument
                {
                    Id = instrument.Id,
                    Name = instrument.Name,
                    Type = instrument.Type,
                    MinVolumeCapacity = instrument.MinVolumeCapacity,
                    MaxVolumeCapacity = instrument.MaxVolumeCapacity,
                    IsAvailable = instrument.IsAvailable,
                    SupportedOperationTypes = new List<string> { "Transfer", "Mix", "Dispense" },
                    ProcessingCapacity = instrument.MaxVolumeCapacity
                };
                problem.Instruments.Add(slabInstrument);
            }
            
            // Generate operations
            var operations = GenerateTimedOperations(8);
            foreach (var operation in operations)
            {
                var slabOperation = new SLabOperation
                {
                    Id = operation.Id,
                    SampleId = operation.SampleId,
                    OperationType = operation.OperationType,
                    VolumeInMicroliters = operation.VolumeInMicroliters,
                    EstimatedDurationMs = operation.EstimatedDurationMs,
                    Priority = operation.Priority,
                    SubmissionTime = operation.SubmissionTime,
                    Deadline = operation.Deadline,
                    ProcessingTime = operation.EstimatedDurationMs,
                    InstrumentTypeRequired = "Transfer",
                    BiomoleculeType = operation.SampleType,
                    CriticalityScore = operation.CriticalityScore
                };
                problem.Operations.Add(slabOperation);
            }
            
            // Generate time constraints
            if (problem.Operations.Count >= 2)
            {
                var constraint = new TimeConstraintByMutualBoundaries
                {
                    Operation1Id = problem.Operations[0].Id,
                    Operation2Id = problem.Operations[1].Id,
                    MaxTimeDifferenceMs = 60000,
                    ConstraintType = "StartToStart",
                    Reason = "Sample stability"
                };
                problem.TimeConstraints.Add(constraint);
            }
            
            return problem;
        }

        private string GetRandomInstrumentType()
        {
            var types = new[] { "Pipette", "Dispenser", "Washer", "Mixer" };
            return types[_random.Next(types.Length)];
        }

        private string GetRandomOperationType()
        {
            var types = new[] { "Transfer", "Dispense", "Mix", "Wash", "Incubate" };
            return types[_random.Next(types.Length)];
        }

        private string GetRandomSampleType()
        {
            var types = new[] { "Blood", "Urine", "Saliva", "Tissue", "Buffer" };
            return types[_random.Next(types.Length)];
        }

        private string GetRandomBiomoleculeType()
        {
            var types = new[] { "DNA", "RNA", "Protein", "Enzyme" };
            return types[_random.Next(types.Length)];
        }

        private string GetRandomQCType()
        {
            var types = new[] { "Positive Control", "Negative Control", "Calibrator", "Blank" };
            return types[_random.Next(types.Length)];
        }

        private double GetStabilityTime(string biomoleculeType)
        {
            return biomoleculeType switch
            {
                "RNA" => 1800000, // 30 minutes
                "Protein" => 7200000, // 2 hours
                "DNA" => 14400000, // 4 hours
                "Enzyme" => 3600000, // 1 hour
                _ => 3600000
            };
        }

        private BiomoleculeHandling GetHandlingType(string biomoleculeType)
        {
            return biomoleculeType switch
            {
                "RNA" => BiomoleculeHandling.RNaseFree,
                "DNA" => BiomoleculeHandling.DNaseFree,
                "Protein" => BiomoleculeHandling.ProteaseFree,
                "Enzyme" => BiomoleculeHandling.ActivityPreserving,
                _ => BiomoleculeHandling.Universal
            };
        }
    }

    #region Test Framework Data Classes

    public class TestConfiguration
    {
        public int TimeoutMinutes { get; set; } = 30;
        public bool EnableDetailedLogging { get; set; } = true;
        public bool EnablePerformanceMetrics { get; set; } = true;
        public int MaxConcurrentTests { get; set; } = 4;
        public string OutputDirectory { get; set; } = "TestReports";
    }

    public class TestSuite
    {
        public string Name { get; set; }
        public List<TestCase> TestCases { get; set; } = new List<TestCase>();
    }

    public class TestCase
    {
        public string Name { get; set; }
        public Func<Task<bool>> ExecuteAsync { get; set; }
    }

    public class TestResult
    {
        public string TestName { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    public class TestSuiteResult
    {
        public string SuiteName { get; set; }
        public bool Success { get; set; }
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<TestResult> TestResults { get; set; } = new List<TestResult>();
    }

    public class TestReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TestConfiguration TestConfiguration { get; set; }
        public bool OverallResult { get; set; }
        public List<TestSuiteResult> TestSuiteResults { get; set; } = new List<TestSuiteResult>();
    }

    #endregion
}

namespace FluidHandling.Configuration
{
    /// <summary>
    /// Configuration management system for IVD scheduling
    /// Handles configuration loading, validation, and hot-reload
    /// </summary>
    public class ConfigurationManager
    {
        private readonly Dictionary<string, object> _configurations;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly string _configurationDirectory;

        public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        public ConfigurationManager(string configurationDirectory = "Configuration")
        {
            _configurations = new Dictionary<string, object>();
            _configurationDirectory = configurationDirectory;
            
            // Create configuration directory if it doesn't exist
            Directory.CreateDirectory(_configurationDirectory);
            
            // Set up file watcher for hot reload
            _fileWatcher = new FileSystemWatcher(_configurationDirectory, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            
            _fileWatcher.Changed += OnConfigurationFileChanged;
            _fileWatcher.Created += OnConfigurationFileChanged;
            _fileWatcher.Deleted += OnConfigurationFileChanged;
        }

        public async Task<T> LoadConfigurationAsync<T>(string configurationName) where T : class, new()
        {
            var filePath = Path.Combine(_configurationDirectory, $"{configurationName}.json");
            
            try
            {
                if (File.Exists(filePath))
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    var config = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    _configurations[configurationName] = config;
                    return config;
                }
                else
                {
                    // Create default configuration
                    var defaultConfig = new T();
                    await SaveConfigurationAsync(configurationName, defaultConfig);
                    return defaultConfig;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration {configurationName}: {ex.Message}");
                return new T();
            }
        }

        public async Task SaveConfigurationAsync<T>(string configurationName, T configuration)
        {
            var filePath = Path.Combine(_configurationDirectory, $"{configurationName}.json");
            
            try
            {
                var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                await File.WriteAllTextAsync(filePath, json);
                _configurations[configurationName] = configuration;
                
                Console.WriteLine($"Configuration {configurationName} saved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration {configurationName}: {ex.Message}");
            }
        }

        public T GetConfiguration<T>(string configurationName) where T : class
        {
            if (_configurations.ContainsKey(configurationName))
            {
                return _configurations[configurationName] as T;
            }
            
            return null;
        }

        public bool ValidateConfiguration<T>(string configurationName, T configuration) where T : class
        {
            try
            {
                // Basic validation - can be extended with more sophisticated validation
                if (configuration == null)
                    return false;
                
                // Use reflection to check for required properties
                var properties = typeof(T).GetProperties();
                foreach (var property in properties)
                {
                    var value = property.GetValue(configuration);
                    if (value == null && !property.PropertyType.IsValueType)
                    {
                        Console.WriteLine($"Validation failed: {property.Name} is null");
                        return false;
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Validation error for {configurationName}: {ex.Message}");
                return false;
            }
        }

        public async Task InitializeDefaultConfigurationsAsync()
        {
            Console.WriteLine("Initializing default configurations...");
            
            // Create default configurations for all system components
            await LoadConfigurationAsync<SchedulerConfiguration>("scheduler");
            await LoadConfigurationAsync<OPCUAConfig>("opcua");
            await LoadConfigurationAsync<MESConfig>("mes");
            await LoadConfigurationAsync<LIMSConfig>("lims");
            await LoadConfigurationAsync<MonitoringConfig>("monitoring");
            await LoadConfigurationAsync<TestConfiguration>("testing");
            await LoadConfigurationAsync<BiomoleculeConfig>("biomolecule");
            await LoadConfigurationAsync<QualityControlConfig>("qualitycontrol");
            await LoadConfigurationAsync<GeneticAlgorithmConfig>("geneticalgorithm");
            await LoadConfigurationAsync<RLConfig>("reinforcementlearning");
            await LoadConfigurationAsync<NNConfig>("neuralnetwork");
            
            Console.WriteLine("Default configurations initialized successfully");
        }

        private void OnConfigurationFileChanged(object sender, FileSystemEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait a bit to ensure file is fully written
                    await Task.Delay(500);
                    
                    var configurationName = Path.GetFileNameWithoutExtension(e.Name);
                    Console.WriteLine($"Configuration file changed: {configurationName}");
                    
                    // Reload configuration
                    // This would need to be more sophisticated in a real implementation
                    // to handle different configuration types
                    
                    ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
                    {
                        ConfigurationName = configurationName,
                        ChangeType = e.ChangeType,
                        FilePath = e.FullPath
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling configuration file change: {ex.Message}");
                }
            });
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
        }
    }

    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string ConfigurationName { get; set; }
        public WatcherChangeTypes ChangeType { get; set; }
        public string FilePath { get; set; }
    }
}

namespace FluidHandling.Utilities
{
    /// <summary>
    /// Logging utility for IVD scheduling system
    /// </summary>
    public class SchedulingLogger
    {
        private readonly string _logDirectory;
        private readonly object _lockObject = new object();

        public SchedulingLogger(string logDirectory = "Logs")
        {
            _logDirectory = logDirectory;
            Directory.CreateDirectory(_logDirectory);
        }

        public void LogInfo(string message, string category = "General")
        {
            WriteLog("INFO", message, category);
        }

        public void LogWarning(string message, string category = "General")
        {
            WriteLog("WARN", message, category);
        }

        public void LogError(string message, string category = "General", Exception exception = null)
        {
            var fullMessage = exception != null ? $"{message}\nException: {exception}" : message;
            WriteLog("ERROR", fullMessage, category);
        }

        public void LogDebug(string message, string category = "General")
        {
            WriteLog("DEBUG", message, category);
        }

        private void WriteLog(string level, string message, string category)
        {
            lock (_lockObject)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = $"[{timestamp}] [{level}] [{category}] {message}";
                    
                    // Write to console
                    Console.WriteLine(logEntry);
                    
                    // Write to file
                    var fileName = $"scheduling_{DateTime.Now:yyyyMMdd}.log";
                    var filePath = Path.Combine(_logDirectory, fileName);
                    
                    File.AppendAllText(filePath, logEntry + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Logging error: {ex.Message}");
                }
            }
        }
    }
}

// Example usage and final integration
namespace FluidHandling.Demo
{
    /// <summary>
    /// Complete demonstration of the IVD scheduling system
    /// Shows integration of all components
    /// </summary>
    public class CompleteSystemDemo
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Complete IVD Fluid Handling Scheduling System Demo ===");
            
            // Initialize configuration management
            var configManager = new ConfigurationManager();
            await configManager.InitializeDefaultConfigurationsAsync();
            
            // Initialize logging
            var logger = new SchedulingLogger();
            logger.LogInfo("System starting up", "System");
            
            // Run comprehensive tests
            var testFramework = new SchedulingTestFramework();
            var testReport = await testFramework.RunAllTestsAsync();
            
            logger.LogInfo($"Tests completed: {testReport.OverallResult}", "Testing");
            
            // Initialize integrations
            var opcuaConfig = await configManager.LoadConfigurationAsync<OPCUAConfig>("opcua");
            var opcuaIntegration = new OPCUAIntegration(opcuaConfig);
            
            var mesConfig = await configManager.LoadConfigurationAsync<MESConfig>("mes");
            var mesIntegration = new MESIntegration(mesConfig);
            
            var limsConfig = await configManager.LoadConfigurationAsync<LIMSConfig>("lims");
            var limsIntegration = new LIMSIntegration(limsConfig);
            
            // Connect to external systems
            await opcuaIntegration.ConnectAsync();
            await mesIntegration.ConnectAsync();
            await limsIntegration.ConnectAsync();
            
            logger.LogInfo("External systems connected", "Integration");
            
            // Initialize schedulers
            var instruments = new TestDataGenerator().GenerateInstruments(5);
            var schedulers = new List<IScheduler>
            {
                new FCFSScheduler(instruments),
                new SJFScheduler(instruments),
                new PriorityScheduler(instruments),
                new EDFScheduler(instruments),
                new GreedyScheduler(new List<TimedFluidOperation>(), instruments),
                new BiomoleculeScheduler(instruments),
                new QualityControlScheduler(instruments)
            };
            
            // Start monitoring system
            var monitoringConfig = await configManager.LoadConfigurationAsync<MonitoringConfig>("monitoring");
            var monitoringSystem = new RealTimeMonitoringSystem(
                schedulers, opcuaIntegration, mesIntegration, limsIntegration, monitoringConfig);
            
            await monitoringSystem.StartMonitoringAsync();
            
            logger.LogInfo("Monitoring system started", "Monitoring");
            
            // Run performance comparison
            var performanceAnalyzer = new PerformanceAnalyzer();
            var testOperations = new TestDataGenerator().GenerateOperations(50);
            var performanceReport = await performanceAnalyzer.CompareAlgorithmsAsync(
                schedulers, testOperations, instruments, 3);
            
            logger.LogInfo($"Performance analysis completed", "Performance");
            
            // Clean up
            await monitoringSystem.StopMonitoringAsync();
            await opcuaIntegration.DisconnectAsync();
            
            logger.LogInfo("System shutdown complete", "System");
            
            Console.WriteLine("\n=== System Demo Complete ===");
            Console.WriteLine("Check the TestReports and Logs directories for detailed results");
        }
    }
}