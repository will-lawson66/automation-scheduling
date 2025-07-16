using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FluidHandling.AdvancedScheduling
{
    /// <summary>
    /// S-LAB Problem Definition
    /// Scheduling for Laboratory Automation in Biology
    /// Based on research by Itoh et al. for handling time-critical operations
    /// </summary>
    public class SLabProblem
    {
        public List<SLabOperation> Operations { get; set; }
        public List<SLabInstrument> Instruments { get; set; }
        public List<SLabJob> Jobs { get; set; }
        public List<TimeConstraintByMutualBoundaries> TimeConstraints { get; set; }
        public SLabConfiguration Configuration { get; set; }

        public SLabProblem()
        {
            Operations = new List<SLabOperation>();
            Instruments = new List<SLabInstrument>();
            Jobs = new List<SLabJob>();
            TimeConstraints = new List<TimeConstraintByMutualBoundaries>();
            Configuration = new SLabConfiguration();
        }
    }

    /// <summary>
    /// Enhanced operation for S-LAB problems
    /// </summary>
    public class SLabOperation : TimedFluidOperation
    {
        public int JobId { get; set; }
        public List<int> PredecessorOperations { get; set; }
        public List<int> SuccessorOperations { get; set; }
        public string InstrumentTypeRequired { get; set; }
        public double ProcessingTime { get; set; }
        public double SetupTime { get; set; }
        public double CleanupTime { get; set; }
        public bool IsTimeConstraintCritical { get; set; }
        public double BiomoleculeStabilityScore { get; set; }
        public string BiomoleculeType { get; set; } // RNA, DNA, Protein, etc.

        public SLabOperation()
        {
            PredecessorOperations = new List<int>();
            SuccessorOperations = new List<int>();
        }
    }

    /// <summary>
    /// Enhanced instrument for S-LAB problems
    /// </summary>
    public class SLabInstrument : FluidInstrument
    {
        public List<string> SupportedOperationTypes { get; set; }
        public double ProcessingCapacity { get; set; }
        public double MaintenanceTime { get; set; }
        public DateTime LastMaintenanceTime { get; set; }
        public bool RequiresSpecialHandling { get; set; }

        public SLabInstrument()
        {
            SupportedOperationTypes = new List<string>();
        }
    }

    /// <summary>
    /// Job definition for S-LAB problems
    /// </summary>
    public class SLabJob
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<int> OperationIds { get; set; }
        public Dictionary<int, List<int>> OperationDependencies { get; set; }
        public int Priority { get; set; }
        public DateTime SubmissionTime { get; set; }
        public DateTime RequiredCompletionTime { get; set; }

        public SLabJob()
        {
            OperationIds = new List<int>();
            OperationDependencies = new Dictionary<int, List<int>>();
        }
    }

    /// <summary>
    /// Laboratory configuration for S-LAB problems
    /// </summary>
    public class SLabConfiguration
    {
        public int NumberOfInstruments { get; set; }
        public Dictionary<string, int> InstrumentCounts { get; set; }
        public bool HasTransporters { get; set; }
        public double TransporterSpeed { get; set; }
        public double TransporterCapacity { get; set; }

        public SLabConfiguration()
        {
            InstrumentCounts = new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Mixed-Integer Programming (MIP) formulation for S-LAB problems
    /// </summary>
    public class MIPScheduler
    {
        private readonly SLabProblem _problem;
        private readonly Dictionary<string, double> _decisionVariables;
        private readonly List<string> _constraints;

        public MIPScheduler(SLabProblem problem)
        {
            _problem = problem;
            _decisionVariables = new Dictionary<string, double>();
            _constraints = new List<string>();
        }

        public SchedulingSolution SolveWithBranchAndBound()
        {
            Console.WriteLine("[MIP] Starting Mixed-Integer Programming optimization...");
            
            // Phase 1: Formulate the problem
            FormulateObjectiveFunction();
            FormulateConstraints();
            
            // Phase 2: Solve using Branch-and-Bound
            var solution = BranchAndBoundSolver();
            
            Console.WriteLine($"[MIP] Branch-and-Bound completed with execution time: {solution.TotalExecutionTime}ms");
            
            return solution;
        }

        private void FormulateObjectiveFunction()
        {
            // Minimize total execution time while satisfying all constraints
            // Objective: min(C_max) where C_max is the makespan
            Console.WriteLine("[MIP] Formulating objective function: minimize makespan");
            
            foreach (var operation in _problem.Operations)
            {
                foreach (var instrument in _problem.Instruments)
                {
                    if (instrument.SupportedOperationTypes.Contains(operation.InstrumentTypeRequired))
                    {
                        // Binary variable: x_ij = 1 if operation i is assigned to instrument j
                        var varName = $"x_{operation.Id}_{instrument.Id}";
                        _decisionVariables[varName] = 0.0;
                        
                        // Continuous variable: s_i = start time of operation i
                        var startVarName = $"s_{operation.Id}";
                        _decisionVariables[startVarName] = 0.0;
                    }
                }
            }
        }

        private void FormulateConstraints()
        {
            Console.WriteLine("[MIP] Formulating constraints...");
            
            // Constraint 1: Each operation must be assigned to exactly one instrument
            foreach (var operation in _problem.Operations)
            {
                var constraint = "";
                foreach (var instrument in _problem.Instruments)
                {
                    if (instrument.SupportedOperationTypes.Contains(operation.InstrumentTypeRequired))
                    {
                        constraint += $"x_{operation.Id}_{instrument.Id} + ";
                    }
                }
                if (!string.IsNullOrEmpty(constraint))
                {
                    constraint = constraint.TrimEnd(' ', '+') + " = 1";
                    _constraints.Add(constraint);
                }
            }

            // Constraint 2: Precedence constraints (operation dependencies)
            foreach (var operation in _problem.Operations)
            {
                foreach (var predecessorId in operation.PredecessorOperations)
                {
                    var constraint = $"s_{operation.Id} >= s_{predecessorId} + {operation.ProcessingTime}";
                    _constraints.Add(constraint);
                }
            }

            // Constraint 3: Time Constraints by Mutual Boundaries (TCMBs)
            foreach (var timeConstraint in _problem.TimeConstraints)
            {
                var constraint = $"s_{timeConstraint.Operation2Id} - s_{timeConstraint.Operation1Id} <= {timeConstraint.MaxTimeDifferenceMs}";
                _constraints.Add(constraint);
            }

            // Constraint 4: Instrument capacity constraints
            foreach (var instrument in _problem.Instruments)
            {
                // No two operations can run simultaneously on the same instrument
                var operationsOnInstrument = _problem.Operations
                    .Where(op => instrument.SupportedOperationTypes.Contains(op.InstrumentTypeRequired))
                    .ToList();

                for (int i = 0; i < operationsOnInstrument.Count; i++)
                {
                    for (int j = i + 1; j < operationsOnInstrument.Count; j++)
                    {
                        var op1 = operationsOnInstrument[i];
                        var op2 = operationsOnInstrument[j];
                        
                        // Binary variable for ordering
                        var orderVar = $"y_{op1.Id}_{op2.Id}";
                        _decisionVariables[orderVar] = 0.0;
                        
                        // Disjunctive constraints
                        var constraint1 = $"s_{op2.Id} >= s_{op1.Id} + {op1.ProcessingTime} - M * (1 - y_{op1.Id}_{op2.Id})";
                        var constraint2 = $"s_{op1.Id} >= s_{op2.Id} + {op2.ProcessingTime} - M * y_{op1.Id}_{op2.Id}";
                        
                        _constraints.Add(constraint1);
                        _constraints.Add(constraint2);
                    }
                }
            }

            Console.WriteLine($"[MIP] Formulated {_constraints.Count} constraints");
        }

        private SchedulingSolution BranchAndBoundSolver()
        {
            Console.WriteLine("[MIP] Starting Branch-and-Bound algorithm...");
            
            // Simplified branch-and-bound implementation
            var bestSolution = new SchedulingSolution { Cost = double.MaxValue };
            var solutionQueue = new Queue<PartialSolution>();
            
            // Initialize with root node
            var rootSolution = new PartialSolution
            {
                VariableAssignments = new Dictionary<string, double>(),
                LowerBound = 0,
                UpperBound = double.MaxValue,
                Level = 0
            };
            
            solutionQueue.Enqueue(rootSolution);
            
            while (solutionQueue.Count > 0)
            {
                var currentSolution = solutionQueue.Dequeue();
                
                // Solve LP relaxation
                var lpSolution = SolveLPRelaxation(currentSolution);
                
                if (lpSolution.LowerBound >= bestSolution.Cost)
                {
                    // Prune this branch
                    continue;
                }
                
                if (IsIntegerSolution(lpSolution))
                {
                    // Found integer solution, update best if better
                    if (lpSolution.LowerBound < bestSolution.Cost)
                    {
                        bestSolution = ConvertToSchedulingSolution(lpSolution);
                        Console.WriteLine($"[MIP] New best solution found with cost: {bestSolution.Cost}");
                    }
                }
                else
                {
                    // Branch on fractional variable
                    var branchingVar = FindBranchingVariable(lpSolution);
                    if (branchingVar != null)
                    {
                        // Create two child nodes
                        var leftChild = CreateChildSolution(currentSolution, branchingVar, 0);
                        var rightChild = CreateChildSolution(currentSolution, branchingVar, 1);
                        
                        solutionQueue.Enqueue(leftChild);
                        solutionQueue.Enqueue(rightChild);
                    }
                }
            }
            
            Console.WriteLine($"[MIP] Branch-and-Bound completed with final cost: {bestSolution.Cost}");
            return bestSolution;
        }

        private PartialSolution SolveLPRelaxation(PartialSolution partialSolution)
        {
            // Simplified LP relaxation solver
            // In practice, this would use a proper LP solver like CPLEX or Gurobi
            var relaxedSolution = new PartialSolution
            {
                VariableAssignments = new Dictionary<string, double>(partialSolution.VariableAssignments),
                Level = partialSolution.Level,
                LowerBound = EstimateLowerBound(partialSolution),
                UpperBound = EstimateUpperBound(partialSolution)
            };
            
            return relaxedSolution;
        }

        private bool IsIntegerSolution(PartialSolution solution)
        {
            // Check if all binary variables are integer (0 or 1)
            foreach (var variable in solution.VariableAssignments)
            {
                if (variable.Key.StartsWith("x_") || variable.Key.StartsWith("y_"))
                {
                    if (variable.Value != 0.0 && variable.Value != 1.0)
                        return false;
                }
            }
            return true;
        }

        private string FindBranchingVariable(PartialSolution solution)
        {
            // Find most fractional binary variable
            string mostFractionalVar = null;
            double maxFractionalValue = 0;
            
            foreach (var variable in solution.VariableAssignments)
            {
                if (variable.Key.StartsWith("x_") || variable.Key.StartsWith("y_"))
                {
                    double fractionalPart = Math.Abs(variable.Value - Math.Round(variable.Value));
                    if (fractionalPart > maxFractionalValue)
                    {
                        maxFractionalValue = fractionalPart;
                        mostFractionalVar = variable.Key;
                    }
                }
            }
            
            return mostFractionalVar;
        }

        private PartialSolution CreateChildSolution(PartialSolution parent, string branchingVar, double value)
        {
            var child = new PartialSolution
            {
                VariableAssignments = new Dictionary<string, double>(parent.VariableAssignments),
                Level = parent.Level + 1,
                LowerBound = parent.LowerBound,
                UpperBound = parent.UpperBound
            };
            
            child.VariableAssignments[branchingVar] = value;
            return child;
        }

        private double EstimateLowerBound(PartialSolution solution)
        {
            // Simple lower bound estimation
            double totalProcessingTime = _problem.Operations.Sum(op => op.ProcessingTime);
            int numberOfInstruments = _problem.Instruments.Count;
            
            return totalProcessingTime / numberOfInstruments;
        }

        private double EstimateUpperBound(PartialSolution solution)
        {
            // Simple upper bound estimation
            return _problem.Operations.Sum(op => op.ProcessingTime);
        }

        private SchedulingSolution ConvertToSchedulingSolution(PartialSolution partialSolution)
        {
            var solution = new SchedulingSolution();
            
            // Convert MIP solution to scheduling solution
            foreach (var variable in partialSolution.VariableAssignments)
            {
                if (variable.Key.StartsWith("x_") && variable.Value == 1.0)
                {
                    // Parse operation and instrument IDs
                    var parts = variable.Key.Split('_');
                    var operationId = int.Parse(parts[1]);
                    var instrumentId = int.Parse(parts[2]);
                    
                    var operation = _problem.Operations.FirstOrDefault(op => op.Id == operationId);
                    if (operation != null)
                    {
                        var startTimeVar = $"s_{operationId}";
                        var startTime = partialSolution.VariableAssignments.ContainsKey(startTimeVar) 
                            ? partialSolution.VariableAssignments[startTimeVar] 
                            : 0;
                        
                        var assignment = new OperationAssignment
                        {
                            OperationId = operationId,
                            InstrumentId = instrumentId,
                            StartTime = DateTime.Now.AddMilliseconds(startTime),
                            EndTime = DateTime.Now.AddMilliseconds(startTime + operation.ProcessingTime),
                            Cost = operation.ProcessingTime
                        };
                        
                        solution.Assignments.Add(assignment);
                    }
                }
            }
            
            solution.TotalExecutionTime = CalculateTotalExecutionTime(solution);
            solution.Cost = partialSolution.LowerBound;
            solution.IsValid = true;
            
            return solution;
        }

        private double CalculateTotalExecutionTime(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return 0;
            return solution.Assignments.Max(a => a.EndTime).Subtract(solution.Assignments.Min(a => a.StartTime)).TotalMilliseconds;
        }
    }

    /// <summary>
    /// Partial solution for Branch-and-Bound algorithm
    /// </summary>
    public class PartialSolution
    {
        public Dictionary<string, double> VariableAssignments { get; set; }
        public double LowerBound { get; set; }
        public double UpperBound { get; set; }
        public int Level { get; set; }

        public PartialSolution()
        {
            VariableAssignments = new Dictionary<string, double>();
        }
    }

    /// <summary>
    /// S-LAB Scheduler - Complete implementation
    /// Combines MIP formulation with practical heuristics
    /// </summary>
    public class SLabScheduler
    {
        private readonly SLabProblem _problem;
        private readonly MIPScheduler _mipScheduler;

        public SLabScheduler(SLabProblem problem)
        {
            _problem = problem;
            _mipScheduler = new MIPScheduler(problem);
        }

        public async Task<SchedulingSolution> ScheduleAsync()
        {
            Console.WriteLine("[S-LAB] Starting S-LAB scheduling algorithm...");
            
            // Phase 1: Validate problem constraints
            ValidateProblem();
            
            // Phase 2: Preprocess operations and constraints
            PreprocessOperations();
            
            // Phase 3: Solve using MIP formulation
            var solution = _mipScheduler.SolveWithBranchAndBound();
            
            // Phase 4: Post-process solution for practical considerations
            var finalSolution = PostprocessSolution(solution);
            
            // Phase 5: Validate solution against all constraints
            ValidateSolution(finalSolution);
            
            Console.WriteLine($"[S-LAB] Scheduling completed with {finalSolution.Assignments.Count} assignments");
            
            return finalSolution;
        }

        private void ValidateProblem()
        {
            Console.WriteLine("[S-LAB] Validating problem constraints...");
            
            // Check for circular dependencies
            foreach (var operation in _problem.Operations)
            {
                if (HasCircularDependency(operation.Id, new HashSet<int>()))
                {
                    throw new InvalidOperationException($"Circular dependency detected involving operation {operation.Id}");
                }
            }
            
            // Check instrument capabilities
            foreach (var operation in _problem.Operations)
            {
                var compatibleInstruments = _problem.Instruments
                    .Where(inst => inst.SupportedOperationTypes.Contains(operation.InstrumentTypeRequired))
                    .ToList();
                
                if (compatibleInstruments.Count == 0)
                {
                    throw new InvalidOperationException($"No compatible instrument found for operation {operation.Id}");
                }
            }
            
            Console.WriteLine("[S-LAB] Problem validation completed successfully");
        }

        private bool HasCircularDependency(int operationId, HashSet<int> visited)
        {
            if (visited.Contains(operationId))
                return true;
            
            visited.Add(operationId);
            
            var operation = _problem.Operations.FirstOrDefault(op => op.Id == operationId);
            if (operation != null)
            {
                foreach (var successorId in operation.SuccessorOperations)
                {
                    if (HasCircularDependency(successorId, new HashSet<int>(visited)))
                        return true;
                }
            }
            
            return false;
        }

        private void PreprocessOperations()
        {
            Console.WriteLine("[S-LAB] Preprocessing operations...");
            
            // Sort operations by criticality
            var sortedOperations = _problem.Operations
                .OrderByDescending(op => op.BiomoleculeStabilityScore)
                .ThenBy(op => op.Deadline)
                .ToList();
            
            // Update operation priorities based on criticality
            for (int i = 0; i < sortedOperations.Count; i++)
            {
                sortedOperations[i].Priority = sortedOperations.Count - i;
            }
            
            // Analyze time constraints
            foreach (var constraint in _problem.TimeConstraints)
            {
                var op1 = _problem.Operations.FirstOrDefault(op => op.Id == constraint.Operation1Id);
                var op2 = _problem.Operations.FirstOrDefault(op => op.Id == constraint.Operation2Id);
                
                if (op1 != null && op2 != null)
                {
                    op1.IsTimeConstraintCritical = true;
                    op2.IsTimeConstraintCritical = true;
                }
            }
            
            Console.WriteLine("[S-LAB] Preprocessing completed");
        }

        private SchedulingSolution PostprocessSolution(SchedulingSolution solution)
        {
            Console.WriteLine("[S-LAB] Post-processing solution...");
            
            var postprocessed = solution.Clone();
            
            // Adjust for transporter constraints
            if (_problem.Configuration.HasTransporters)
            {
                AdjustForTransporters(postprocessed);
            }
            
            // Optimize for biomolecule stability
            OptimizeForBiomoleculeStability(postprocessed);
            
            // Final validation and adjustment
            AdjustForTimeConstraints(postprocessed);
            
            return postprocessed;
        }

        private void AdjustForTransporters(SchedulingSolution solution)
        {
            // Add transport time between operations
            foreach (var assignment in solution.Assignments)
            {
                var operation = _problem.Operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    foreach (var successorId in operation.SuccessorOperations)
                    {
                        var successorAssignment = solution.Assignments.FirstOrDefault(a => a.OperationId == successorId);
                        if (successorAssignment != null)
                        {
                            var transportTime = CalculateTransportTime(assignment.InstrumentId, successorAssignment.InstrumentId);
                            if (successorAssignment.StartTime < assignment.EndTime.AddMilliseconds(transportTime))
                            {
                                var delay = assignment.EndTime.AddMilliseconds(transportTime) - successorAssignment.StartTime;
                                successorAssignment.StartTime = successorAssignment.StartTime.Add(delay);
                                successorAssignment.EndTime = successorAssignment.EndTime.Add(delay);
                            }
                        }
                    }
                }
            }
        }

        private double CalculateTransportTime(int fromInstrumentId, int toInstrumentId)
        {
            // Simplified transport time calculation
            return fromInstrumentId == toInstrumentId ? 0 : 30000; // 30 seconds between different instruments
        }

        private void OptimizeForBiomoleculeStability(SchedulingSolution solution)
        {
            // Prioritize operations with unstable biomolecules
            var unstableOperations = solution.Assignments
                .Where(a => _problem.Operations.Any(op => op.Id == a.OperationId && op.BiomoleculeStabilityScore < 0.5))
                .OrderBy(a => a.StartTime)
                .ToList();
            
            foreach (var assignment in unstableOperations)
            {
                var operation = _problem.Operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    // Try to schedule earlier if possible
                    var earlierTime = assignment.StartTime.AddMinutes(-5);
                    if (earlierTime >= operation.EarliestStartTime)
                    {
                        assignment.StartTime = earlierTime;
                        assignment.EndTime = earlierTime.AddMilliseconds(operation.ProcessingTime);
                    }
                }
            }
        }

        private void AdjustForTimeConstraints(SchedulingSolution solution)
        {
            // Final check and adjustment for time constraints
            foreach (var constraint in _problem.TimeConstraints)
            {
                var assignment1 = solution.Assignments.FirstOrDefault(a => a.OperationId == constraint.Operation1Id);
                var assignment2 = solution.Assignments.FirstOrDefault(a => a.OperationId == constraint.Operation2Id);
                
                if (assignment1 != null && assignment2 != null)
                {
                    var timeDiff = Math.Abs((assignment1.StartTime - assignment2.StartTime).TotalMilliseconds);
                    
                    if (timeDiff > constraint.MaxTimeDifferenceMs)
                    {
                        // Adjust the later operation
                        if (assignment1.StartTime > assignment2.StartTime)
                        {
                            assignment1.StartTime = assignment2.StartTime.AddMilliseconds(constraint.MaxTimeDifferenceMs);
                            assignment1.EndTime = assignment1.StartTime.AddMilliseconds(
                                _problem.Operations.FirstOrDefault(op => op.Id == assignment1.OperationId)?.ProcessingTime ?? 0);
                        }
                        else
                        {
                            assignment2.StartTime = assignment1.StartTime.AddMilliseconds(constraint.MaxTimeDifferenceMs);
                            assignment2.EndTime = assignment2.StartTime.AddMilliseconds(
                                _problem.Operations.FirstOrDefault(op => op.Id == assignment2.OperationId)?.ProcessingTime ?? 0);
                        }
                    }
                }
            }
        }

        private void ValidateSolution(SchedulingSolution solution)
        {
            Console.WriteLine("[S-LAB] Validating final solution...");
            
            var violations = new List<string>();
            
            // Check precedence constraints
            foreach (var operation in _problem.Operations)
            {
                var assignment = solution.Assignments.FirstOrDefault(a => a.OperationId == operation.Id);
                if (assignment != null)
                {
                    foreach (var predecessorId in operation.PredecessorOperations)
                    {
                        var predecessorAssignment = solution.Assignments.FirstOrDefault(a => a.OperationId == predecessorId);
                        if (predecessorAssignment != null && assignment.StartTime < predecessorAssignment.EndTime)
                        {
                            violations.Add($"Precedence constraint violated: Operation {operation.Id} starts before predecessor {predecessorId} completes");
                        }
                    }
                }
            }
            
            // Check time constraints
            foreach (var constraint in _problem.TimeConstraints)
            {
                var assignment1 = solution.Assignments.FirstOrDefault(a => a.OperationId == constraint.Operation1Id);
                var assignment2 = solution.Assignments.FirstOrDefault(a => a.OperationId == constraint.Operation2Id);
                
                if (assignment1 != null && assignment2 != null)
                {
                    var timeDiff = Math.Abs((assignment1.StartTime - assignment2.StartTime).TotalMilliseconds);
                    if (timeDiff > constraint.MaxTimeDifferenceMs)
                    {
                        violations.Add($"Time constraint violated: Operations {constraint.Operation1Id} and {constraint.Operation2Id} exceed max time difference of {constraint.MaxTimeDifferenceMs}ms");
                    }
                }
            }
            
            if (violations.Count > 0)
            {
                Console.WriteLine($"[S-LAB] WARNING: {violations.Count} constraint violations found:");
                foreach (var violation in violations)
                {
                    Console.WriteLine($"  - {violation}");
                }
            }
            else
            {
                Console.WriteLine("[S-LAB] Solution validation completed successfully - no violations found");
            }
        }
    }
}