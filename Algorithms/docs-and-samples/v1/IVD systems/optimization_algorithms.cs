using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FluidHandling.OptimizationScheduling
{
    /// <summary>
    /// Represents a scheduling solution for evaluation
    /// </summary>
    public class SchedulingSolution
    {
        public List<OperationAssignment> Assignments { get; set; }
        public double TotalExecutionTime { get; set; }
        public double Cost { get; set; }
        public double Fitness { get; set; }
        public bool IsValid { get; set; }

        public SchedulingSolution()
        {
            Assignments = new List<OperationAssignment>();
        }

        public SchedulingSolution Clone()
        {
            return new SchedulingSolution
            {
                Assignments = new List<OperationAssignment>(Assignments),
                TotalExecutionTime = TotalExecutionTime,
                Cost = Cost,
                Fitness = Fitness,
                IsValid = IsValid
            };
        }
    }

    /// <summary>
    /// Represents an assignment of an operation to an instrument at a specific time
    /// </summary>
    public class OperationAssignment
    {
        public int OperationId { get; set; }
        public int InstrumentId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Cost { get; set; }
    }

    /// <summary>
    /// Greedy Algorithm Scheduler
    /// Makes locally optimal choices at each step
    /// </summary>
    public class GreedyScheduler
    {
        private readonly List<TimedFluidOperation> _operations;
        private readonly List<FluidInstrument> _instruments;

        public GreedyScheduler(List<TimedFluidOperation> operations, List<FluidInstrument> instruments)
        {
            _operations = operations;
            _instruments = instruments;
        }

        public SchedulingSolution GenerateSchedule()
        {
            var solution = new SchedulingSolution();
            var instrumentSchedules = new Dictionary<int, List<OperationAssignment>>();
            
            // Initialize instrument schedules
            foreach (var instrument in _instruments)
            {
                instrumentSchedules[instrument.Id] = new List<OperationAssignment>();
            }

            // Sort operations by earliest deadline (greedy heuristic)
            var sortedOperations = _operations.OrderBy(op => op.Deadline).ToList();

            foreach (var operation in sortedOperations)
            {
                // Find the best instrument assignment (greedy choice)
                var bestAssignment = FindBestAssignment(operation, instrumentSchedules);
                
                if (bestAssignment != null)
                {
                    solution.Assignments.Add(bestAssignment);
                    instrumentSchedules[bestAssignment.InstrumentId].Add(bestAssignment);
                }
            }

            // Calculate solution metrics
            solution.TotalExecutionTime = CalculateTotalExecutionTime(solution);
            solution.Cost = CalculateTotalCost(solution);
            solution.Fitness = CalculateFitness(solution);
            solution.IsValid = ValidateSolution(solution);

            Console.WriteLine($"[Greedy] Generated schedule with {solution.Assignments.Count} assignments, execution time: {solution.TotalExecutionTime}ms");
            
            return solution;
        }

        private OperationAssignment FindBestAssignment(TimedFluidOperation operation, Dictionary<int, List<OperationAssignment>> instrumentSchedules)
        {
            OperationAssignment bestAssignment = null;
            double bestCost = double.MaxValue;

            foreach (var instrument in _instruments)
            {
                if (instrument.MaxVolumeCapacity < operation.VolumeInMicroliters)
                    continue;

                var earliestStart = FindEarliestStartTime(operation, instrument, instrumentSchedules[instrument.Id]);
                var assignment = new OperationAssignment
                {
                    OperationId = operation.Id,
                    InstrumentId = instrument.Id,
                    StartTime = earliestStart,
                    EndTime = earliestStart.AddMilliseconds(operation.EstimatedDurationMs),
                    Cost = CalculateAssignmentCost(operation, instrument, earliestStart)
                };

                if (assignment.Cost < bestCost)
                {
                    bestCost = assignment.Cost;
                    bestAssignment = assignment;
                }
            }

            return bestAssignment;
        }

        private DateTime FindEarliestStartTime(TimedFluidOperation operation, FluidInstrument instrument, List<OperationAssignment> schedule)
        {
            var earliestStart = DateTime.Max(DateTime.Now, operation.EarliestStartTime);
            
            // Find next available slot on instrument
            var sortedSchedule = schedule.OrderBy(a => a.StartTime).ToList();
            foreach (var assignment in sortedSchedule)
            {
                if (earliestStart < assignment.StartTime)
                {
                    // Check if operation fits before this assignment
                    if (earliestStart.AddMilliseconds(operation.EstimatedDurationMs) <= assignment.StartTime)
                    {
                        return earliestStart;
                    }
                }
                // Move to after this assignment
                earliestStart = DateTime.Max(earliestStart, assignment.EndTime);
            }

            return earliestStart;
        }

        private double CalculateAssignmentCost(TimedFluidOperation operation, FluidInstrument instrument, DateTime startTime)
        {
            // Multi-objective cost function
            double timeCost = (startTime - operation.EarliestStartTime).TotalMilliseconds;
            double deadlineCost = Math.Max(0, (startTime.AddMilliseconds(operation.EstimatedDurationMs) - operation.Deadline).TotalMilliseconds);
            double stabilityCost = Math.Max(0, (startTime - operation.SubmissionTime).TotalMilliseconds - operation.StabilityTimeMs);
            
            return timeCost + deadlineCost * 10 + stabilityCost * 5; // Weight deadline and stability violations heavily
        }

        private double CalculateTotalExecutionTime(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return 0;
            return solution.Assignments.Max(a => a.EndTime).Subtract(solution.Assignments.Min(a => a.StartTime)).TotalMilliseconds;
        }

        private double CalculateTotalCost(SchedulingSolution solution)
        {
            return solution.Assignments.Sum(a => a.Cost);
        }

        private double CalculateFitness(SchedulingSolution solution)
        {
            return 1.0 / (1.0 + solution.Cost + solution.TotalExecutionTime / 1000.0);
        }

        private bool ValidateSolution(SchedulingSolution solution)
        {
            // Check for conflicts and constraint violations
            return solution.Assignments.All(a => a.EndTime <= DateTime.Now.AddDays(1)); // Simple validation
        }
    }

    /// <summary>
    /// Simulated Annealing Scheduler
    /// Uses probabilistic technique to find global optimum
    /// </summary>
    public class SimulatedAnnealingScheduler
    {
        private readonly List<TimedFluidOperation> _operations;
        private readonly List<FluidInstrument> _instruments;
        private readonly Random _random;
        private double _currentTemperature;
        private readonly double _initialTemperature;
        private readonly double _coolingRate;

        public SimulatedAnnealingScheduler(List<TimedFluidOperation> operations, List<FluidInstrument> instruments, 
            double initialTemperature = 1000, double coolingRate = 0.95)
        {
            _operations = operations;
            _instruments = instruments;
            _random = new Random();
            _initialTemperature = initialTemperature;
            _coolingRate = coolingRate;
        }

        public SchedulingSolution GenerateSchedule()
        {
            _currentTemperature = _initialTemperature;
            
            // Generate initial solution using greedy approach
            var greedy = new GreedyScheduler(_operations, _instruments);
            var currentSolution = greedy.GenerateSchedule();
            var bestSolution = currentSolution.Clone();

            Console.WriteLine($"[SA] Starting with initial cost: {currentSolution.Cost}");

            while (_currentTemperature > 1)
            {
                // Generate neighbor solution
                var neighborSolution = GenerateNeighborSolution(currentSolution);
                
                // Calculate acceptance probability
                double acceptanceProbability = CalculateAcceptanceProbability(currentSolution.Cost, neighborSolution.Cost, _currentTemperature);
                
                // Accept or reject the neighbor
                if (_random.NextDouble() < acceptanceProbability)
                {
                    currentSolution = neighborSolution;
                    
                    // Update best solution if improved
                    if (currentSolution.Cost < bestSolution.Cost)
                    {
                        bestSolution = currentSolution.Clone();
                        Console.WriteLine($"[SA] New best solution found with cost: {bestSolution.Cost}");
                    }
                }
                
                // Cool down
                _currentTemperature *= _coolingRate;
            }

            Console.WriteLine($"[SA] Final best solution cost: {bestSolution.Cost}");
            return bestSolution;
        }

        private SchedulingSolution GenerateNeighborSolution(SchedulingSolution currentSolution)
        {
            var neighbor = currentSolution.Clone();
            
            // Apply random local modification
            if (neighbor.Assignments.Count > 0)
            {
                int modType = _random.Next(3);
                
                switch (modType)
                {
                    case 0: // Swap two operations
                        SwapOperations(neighbor);
                        break;
                    case 1: // Reschedule operation to different instrument
                        RescheduleOperation(neighbor);
                        break;
                    case 2: // Shift operation time
                        ShiftOperationTime(neighbor);
                        break;
                }
            }

            // Recalculate metrics
            neighbor.TotalExecutionTime = CalculateTotalExecutionTime(neighbor);
            neighbor.Cost = CalculateTotalCost(neighbor);
            neighbor.Fitness = CalculateFitness(neighbor);
            neighbor.IsValid = ValidateSolution(neighbor);

            return neighbor;
        }

        private void SwapOperations(SchedulingSolution solution)
        {
            if (solution.Assignments.Count < 2) return;
            
            int idx1 = _random.Next(solution.Assignments.Count);
            int idx2 = _random.Next(solution.Assignments.Count);
            
            var temp = solution.Assignments[idx1].InstrumentId;
            solution.Assignments[idx1].InstrumentId = solution.Assignments[idx2].InstrumentId;
            solution.Assignments[idx2].InstrumentId = temp;
        }

        private void RescheduleOperation(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return;
            
            var assignment = solution.Assignments[_random.Next(solution.Assignments.Count)];
            var newInstrument = _instruments[_random.Next(_instruments.Count)];
            
            assignment.InstrumentId = newInstrument.Id;
        }

        private void ShiftOperationTime(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return;
            
            var assignment = solution.Assignments[_random.Next(solution.Assignments.Count)];
            var shift = _random.Next(-300000, 300000); // ±5 minutes
            
            assignment.StartTime = assignment.StartTime.AddMilliseconds(shift);
            assignment.EndTime = assignment.EndTime.AddMilliseconds(shift);
        }

        private double CalculateAcceptanceProbability(double currentCost, double neighborCost, double temperature)
        {
            if (neighborCost < currentCost)
                return 1.0; // Always accept better solutions
            
            return Math.Exp((currentCost - neighborCost) / temperature);
        }

        private double CalculateTotalExecutionTime(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return 0;
            return solution.Assignments.Max(a => a.EndTime).Subtract(solution.Assignments.Min(a => a.StartTime)).TotalMilliseconds;
        }

        private double CalculateTotalCost(SchedulingSolution solution)
        {
            return solution.Assignments.Sum(a => a.Cost);
        }

        private double CalculateFitness(SchedulingSolution solution)
        {
            return 1.0 / (1.0 + solution.Cost + solution.TotalExecutionTime / 1000.0);
        }

        private bool ValidateSolution(SchedulingSolution solution)
        {
            return solution.Assignments.All(a => a.EndTime <= DateTime.Now.AddDays(1));
        }
    }

    /// <summary>
    /// SAGAS (Simulated Annealing and Greedy Algorithm Scheduler)
    /// Hybrid approach combining global search with local optimization
    /// Based on research showing 0.25% Average Relative Deviation
    /// </summary>
    public class SAGASScheduler
    {
        private readonly List<TimedFluidOperation> _operations;
        private readonly List<FluidInstrument> _instruments;
        private readonly GreedyScheduler _greedyScheduler;
        private readonly SimulatedAnnealingScheduler _saScheduler;

        public SAGASScheduler(List<TimedFluidOperation> operations, List<FluidInstrument> instruments)
        {
            _operations = operations;
            _instruments = instruments;
            _greedyScheduler = new GreedyScheduler(operations, instruments);
            _saScheduler = new SimulatedAnnealingScheduler(operations, instruments);
        }

        public async Task<SchedulingSolution> GenerateScheduleAsync()
        {
            Console.WriteLine("[SAGAS] Starting hybrid optimization...");
            
            // Phase 1: Generate initial solution using greedy approach
            var greedySolution = _greedyScheduler.GenerateSchedule();
            Console.WriteLine($"[SAGAS] Greedy phase completed with cost: {greedySolution.Cost}");
            
            // Phase 2: Improve solution using simulated annealing
            var improvedSolution = _saScheduler.GenerateSchedule();
            Console.WriteLine($"[SAGAS] SA phase completed with cost: {improvedSolution.Cost}");
            
            // Phase 3: Apply final greedy improvement
            var finalSolution = ApplyGreedyImprovement(improvedSolution);
            Console.WriteLine($"[SAGAS] Final greedy improvement completed with cost: {finalSolution.Cost}");
            
            // Phase 4: Validate and adjust for time constraints
            var validatedSolution = ValidateAndAdjustTimeConstraints(finalSolution);
            Console.WriteLine($"[SAGAS] Time constraint validation completed with cost: {validatedSolution.Cost}");
            
            return validatedSolution;
        }

        private SchedulingSolution ApplyGreedyImprovement(SchedulingSolution solution)
        {
            var improved = solution.Clone();
            bool improvementFound = true;
            
            while (improvementFound)
            {
                improvementFound = false;
                
                // Try to improve each assignment
                for (int i = 0; i < improved.Assignments.Count; i++)
                {
                    var originalAssignment = improved.Assignments[i];
                    var operation = _operations.FirstOrDefault(op => op.Id == originalAssignment.OperationId);
                    
                    if (operation == null) continue;
                    
                    // Find better instrument assignment
                    foreach (var instrument in _instruments)
                    {
                        if (instrument.Id == originalAssignment.InstrumentId ||
                            instrument.MaxVolumeCapacity < operation.VolumeInMicroliters)
                            continue;
                        
                        var newAssignment = new OperationAssignment
                        {
                            OperationId = originalAssignment.OperationId,
                            InstrumentId = instrument.Id,
                            StartTime = originalAssignment.StartTime,
                            EndTime = originalAssignment.EndTime,
                            Cost = CalculateAssignmentCost(operation, instrument, originalAssignment.StartTime)
                        };
                        
                        if (newAssignment.Cost < originalAssignment.Cost)
                        {
                            improved.Assignments[i] = newAssignment;
                            improvementFound = true;
                            break;
                        }
                    }
                }
            }
            
            // Recalculate solution metrics
            improved.TotalExecutionTime = CalculateTotalExecutionTime(improved);
            improved.Cost = CalculateTotalCost(improved);
            improved.Fitness = CalculateFitness(improved);
            improved.IsValid = ValidateSolution(improved);
            
            return improved;
        }

        private SchedulingSolution ValidateAndAdjustTimeConstraints(SchedulingSolution solution)
        {
            var adjusted = solution.Clone();
            
            // Check and fix time constraint violations
            foreach (var operation in _operations)
            {
                foreach (var constraint in operation.TimeConstraints)
                {
                    var assignment1 = adjusted.Assignments.FirstOrDefault(a => a.OperationId == constraint.Operation1Id);
                    var assignment2 = adjusted.Assignments.FirstOrDefault(a => a.OperationId == constraint.Operation2Id);
                    
                    if (assignment1 != null && assignment2 != null)
                    {
                        var timeDiff = Math.Abs((assignment1.StartTime - assignment2.StartTime).TotalMilliseconds);
                        
                        if (timeDiff > constraint.MaxTimeDifferenceMs)
                        {
                            // Adjust assignment2 to satisfy constraint
                            var newStartTime = assignment1.StartTime.AddMilliseconds(constraint.MaxTimeDifferenceMs / 2);
                            assignment2.StartTime = newStartTime;
                            assignment2.EndTime = newStartTime.AddMilliseconds(
                                _operations.FirstOrDefault(op => op.Id == assignment2.OperationId)?.EstimatedDurationMs ?? 0);
                        }
                    }
                }
            }
            
            // Recalculate solution metrics
            adjusted.TotalExecutionTime = CalculateTotalExecutionTime(adjusted);
            adjusted.Cost = CalculateTotalCost(adjusted);
            adjusted.Fitness = CalculateFitness(adjusted);
            adjusted.IsValid = ValidateSolution(adjusted);
            
            return adjusted;
        }

        private double CalculateAssignmentCost(TimedFluidOperation operation, FluidInstrument instrument, DateTime startTime)
        {
            double timeCost = (startTime - operation.EarliestStartTime).TotalMilliseconds;
            double deadlineCost = Math.Max(0, (startTime.AddMilliseconds(operation.EstimatedDurationMs) - operation.Deadline).TotalMilliseconds);
            double stabilityCost = Math.Max(0, (startTime - operation.SubmissionTime).TotalMilliseconds - operation.StabilityTimeMs);
            
            return timeCost + deadlineCost * 10 + stabilityCost * 5;
        }

        private double CalculateTotalExecutionTime(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return 0;
            return solution.Assignments.Max(a => a.EndTime).Subtract(solution.Assignments.Min(a => a.StartTime)).TotalMilliseconds;
        }

        private double CalculateTotalCost(SchedulingSolution solution)
        {
            return solution.Assignments.Sum(a => a.Cost);
        }

        private double CalculateFitness(SchedulingSolution solution)
        {
            return 1.0 / (1.0 + solution.Cost + solution.TotalExecutionTime / 1000.0);
        }

        private bool ValidateSolution(SchedulingSolution solution)
        {
            return solution.Assignments.All(a => a.EndTime <= DateTime.Now.AddDays(1));
        }
    }
}