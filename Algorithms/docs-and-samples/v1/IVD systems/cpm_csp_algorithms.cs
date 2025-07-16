using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluidHandling.Core.Models;
using FluidHandling.Core.Interfaces;

namespace FluidHandling.TimeConstrainedScheduling
{
    /// <summary>
    /// Critical Path Method (CPM) Scheduler
    /// Based on classical project scheduling theory, highly relevant for IVD systems
    /// where operations have dependencies (e.g., sample prep -> analysis -> cleanup)
    /// Finds the longest path through the project network to determine minimum completion time
    /// </summary>
    public class CPMScheduler : IScheduler
    {
        public string Name => "Critical Path Method Scheduler";
        public string Description => "Classical CPM scheduling for dependent operations with critical path optimization";
        public SchedulerType Type => SchedulerType.TimeConstrained;

        private readonly List<CPMOperation> _operations;
        private readonly List<FluidInstrument> _instruments;
        private readonly Dictionary<int, CPMNode> _projectNetwork;
        private readonly List<CPMPath> _criticalPaths;
        private PerformanceMetrics _performanceMetrics;

        public CPMScheduler(List<FluidInstrument> instruments)
        {
            _operations = new List<CPMOperation>();
            _instruments = instruments;
            _projectNetwork = new Dictionary<int, CPMNode>();
            _criticalPaths = new List<CPMPath>();
            _performanceMetrics = new PerformanceMetrics();
        }

        public void AddOperation(FluidOperation operation)
        {
            var cpmOperation = operation as CPMOperation ?? ConvertToCPMOperation(operation);
            _operations.Add(cpmOperation);
            
            // Add to project network
            var node = new CPMNode
            {
                Id = cpmOperation.Id,
                Operation = cpmOperation,
                EarliestStart = TimeSpan.Zero,
                EarliestFinish = TimeSpan.FromMilliseconds(cpmOperation.EstimatedDurationMs),
                LatestStart = TimeSpan.MaxValue,
                LatestFinish = TimeSpan.MaxValue,
                TotalFloat = TimeSpan.Zero,
                FreeFloat = TimeSpan.Zero
            };
            
            _projectNetwork[cpmOperation.Id] = node;
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
            
            Console.WriteLine("[CPM] Starting Critical Path Method scheduling...");
            
            // Step 1: Build project network with dependencies
            BuildProjectNetwork();
            
            // Step 2: Forward pass - calculate earliest start/finish times
            CalculateEarliestTimes();
            
            // Step 3: Backward pass - calculate latest start/finish times
            CalculateLatestTimes();
            
            // Step 4: Calculate float times
            CalculateFloatTimes();
            
            // Step 5: Identify critical path(s)
            IdentifyCriticalPaths();
            
            // Step 6: Schedule operations based on critical path analysis
            var solution = await ScheduleOperationsWithCPM();
            
            _performanceMetrics.FinalizeMeasurement();
            
            Console.WriteLine($"[CPM] Scheduling completed. Critical path length: {GetCriticalPathLength():F0}ms");
            Console.WriteLine($"[CPM] Critical operations: {string.Join(", ", _criticalPaths.SelectMany(p => p.Operations).Select(op => op.Id).Distinct())}");
            
            return solution;
        }

        public async Task<FluidOperation> ScheduleNextAsync()
        {
            // Find next operation on critical path that's ready to execute
            var criticalOperations = _criticalPaths.SelectMany(p => p.Operations).Distinct().ToList();
            
            var nextCriticalOp = criticalOperations
                .Where(op => !op.IsCompleted && CanExecuteNow(op))
                .OrderBy(op => _projectNetwork[op.Id].EarliestStart)
                .FirstOrDefault();

            if (nextCriticalOp != null)
            {
                var availableInstrument = _instruments.FirstOrDefault(inst => 
                    inst.IsAvailable && nextCriticalOp.CanExecuteOn(inst));
                
                if (availableInstrument != null)
                {
                    await ExecuteOperation(nextCriticalOp, availableInstrument);
                    return nextCriticalOp;
                }
            }

            // If no critical operation available, schedule any ready operation
            var readyOperation = _operations
                .Where(op => !op.IsCompleted && CanExecuteNow(op))
                .OrderBy(op => _projectNetwork[op.Id].TotalFloat) // Prioritize by float time
                .FirstOrDefault();

            if (readyOperation != null)
            {
                var availableInstrument = _instruments.FirstOrDefault(inst => 
                    inst.IsAvailable && readyOperation.CanExecuteOn(inst));
                
                if (availableInstrument != null)
                {
                    await ExecuteOperation(readyOperation, availableInstrument);
                    return readyOperation;
                }
            }

            return null;
        }

        public void UpdateConfiguration(SchedulerConfiguration config)
        {
            // CPM-specific configuration updates
            if (config.Parameters.ContainsKey("CriticalPathPriority"))
            {
                // Adjust priority weighting for critical path operations
            }
        }

        public PerformanceMetrics GetPerformanceMetrics()
        {
            return _performanceMetrics;
        }

        private void BuildProjectNetwork()
        {
            Console.WriteLine("[CPM] Building project network...");
            
            foreach (var operation in _operations)
            {
                var node = _projectNetwork[operation.Id];
                
                // Add predecessor relationships
                foreach (var predecessorId in operation.PredecessorOperations)
                {
                    if (_projectNetwork.ContainsKey(predecessorId))
                    {
                        var predecessorNode = _projectNetwork[predecessorId];
                        node.Predecessors.Add(predecessorNode);
                        predecessorNode.Successors.Add(node);
                    }
                }
            }
        }

        private void CalculateEarliestTimes()
        {
            Console.WriteLine("[CPM] Calculating earliest times (forward pass)...");
            
            // Topological sort to process nodes in dependency order
            var sortedNodes = TopologicalSort();
            
            foreach (var node in sortedNodes)
            {
                if (node.Predecessors.Count == 0)
                {
                    // Start node
                    node.EarliestStart = TimeSpan.Zero;
                }
                else
                {
                    // Calculate earliest start based on predecessor finish times
                    var maxPredecessorFinish = node.Predecessors.Max(p => p.EarliestFinish);
                    node.EarliestStart = maxPredecessorFinish;
                }
                
                node.EarliestFinish = node.EarliestStart + TimeSpan.FromMilliseconds(node.Operation.EstimatedDurationMs);
            }
        }

        private void CalculateLatestTimes()
        {
            Console.WriteLine("[CPM] Calculating latest times (backward pass)...");
            
            // Find project completion time
            var projectCompletionTime = _projectNetwork.Values.Max(n => n.EarliestFinish);
            
            // Reverse topological sort
            var sortedNodes = TopologicalSort().Reverse().ToList();
            
            foreach (var node in sortedNodes)
            {
                if (node.Successors.Count == 0)
                {
                    // End node
                    node.LatestFinish = projectCompletionTime;
                }
                else
                {
                    // Calculate latest finish based on successor start times
                    var minSuccessorStart = node.Successors.Min(s => s.LatestStart);
                    node.LatestFinish = minSuccessorStart;
                }
                
                node.LatestStart = node.LatestFinish - TimeSpan.FromMilliseconds(node.Operation.EstimatedDurationMs);
            }
        }

        private void CalculateFloatTimes()
        {
            Console.WriteLine("[CPM] Calculating float times...");
            
            foreach (var node in _projectNetwork.Values)
            {
                // Total float = Latest Start - Earliest Start
                node.TotalFloat = node.LatestStart - node.EarliestStart;
                
                // Free float = Earliest Start of immediate successors - Earliest Finish of this node
                if (node.Successors.Count > 0)
                {
                    var minSuccessorEarliestStart = node.Successors.Min(s => s.EarliestStart);
                    node.FreeFloat = minSuccessorEarliestStart - node.EarliestFinish;
                }
                else
                {
                    node.FreeFloat = node.TotalFloat;
                }
            }
        }

        private void IdentifyCriticalPaths()
        {
            Console.WriteLine("[CPM] Identifying critical paths...");
            
            // Critical operations have zero total float
            var criticalNodes = _projectNetwork.Values
                .Where(n => n.TotalFloat == TimeSpan.Zero)
                .ToList();
            
            if (criticalNodes.Count == 0)
            {
                Console.WriteLine("[CPM] Warning: No critical path found!");
                return;
            }
            
            // Find connected critical paths
            var visitedNodes = new HashSet<CPMNode>();
            var startNodes = criticalNodes.Where(n => n.Predecessors.Count == 0 || 
                                                     n.Predecessors.All(p => p.TotalFloat > TimeSpan.Zero)).ToList();
            
            foreach (var startNode in startNodes)
            {
                var path = new CPMPath();
                TraceCriticalPath(startNode, path, visitedNodes);
                
                if (path.Operations.Count > 0)
                {
                    path.TotalDuration = path.Operations.Sum(op => op.EstimatedDurationMs);
                    _criticalPaths.Add(path);
                }
            }
        }

        private void TraceCriticalPath(CPMNode currentNode, CPMPath path, HashSet<CPMNode> visitedNodes)
        {
            if (visitedNodes.Contains(currentNode) || currentNode.TotalFloat > TimeSpan.Zero)
                return;
            
            visitedNodes.Add(currentNode);
            path.Operations.Add(currentNode.Operation);
            
            // Continue to critical successors
            var criticalSuccessors = currentNode.Successors.Where(s => s.TotalFloat == TimeSpan.Zero).ToList();
            foreach (var successor in criticalSuccessors)
            {
                TraceCriticalPath(successor, path, visitedNodes);
            }
        }

        private List<CPMNode> TopologicalSort()
        {
            var result = new List<CPMNode>();
            var visited = new HashSet<CPMNode>();
            var tempMark = new HashSet<CPMNode>();
            
            foreach (var node in _projectNetwork.Values)
            {
                if (!visited.Contains(node))
                {
                    TopologicalSortVisit(node, visited, tempMark, result);
                }
            }
            
            return result;
        }

        private void TopologicalSortVisit(CPMNode node, HashSet<CPMNode> visited, HashSet<CPMNode> tempMark, List<CPMNode> result)
        {
            if (tempMark.Contains(node))
                throw new InvalidOperationException("Circular dependency detected in project network");
            
            if (visited.Contains(node))
                return;
            
            tempMark.Add(node);
            
            foreach (var successor in node.Successors)
            {
                TopologicalSortVisit(successor, visited, tempMark, result);
            }
            
            tempMark.Remove(node);
            visited.Add(node);
            result.Insert(0, node);
        }

        private async Task<SchedulingSolution> ScheduleOperationsWithCPM()
        {
            var solution = new SchedulingSolution();
            var instrumentSchedules = new Dictionary<int, List<TimeSlot>>();
            
            // Initialize instrument schedules
            foreach (var instrument in _instruments)
            {
                instrumentSchedules[instrument.Id] = new List<TimeSlot>();
            }
            
            // Schedule operations in order of earliest start time, prioritizing critical path
            var schedulingOrder = _projectNetwork.Values
                .OrderBy(n => n.EarliestStart)
                .ThenBy(n => n.TotalFloat) // Critical path operations (zero float) first
                .ToList();
            
            foreach (var node in schedulingOrder)
            {
                var assignment = await ScheduleOperation(node, instrumentSchedules);
                if (assignment != null)
                {
                    solution.Assignments.Add(assignment);
                    
                    // Update instrument schedule
                    var timeSlot = new TimeSlot
                    {
                        StartTime = assignment.StartTime,
                        EndTime = assignment.EndTime,
                        OperationId = assignment.OperationId
                    };
                    instrumentSchedules[assignment.InstrumentId].Add(timeSlot);
                }
            }
            
            solution.TotalExecutionTime = CalculateTotalExecutionTime(solution);
            solution.Cost = CalculateTotalCost(solution);
            solution.IsValid = ValidateSolution(solution);
            
            return solution;
        }

        private async Task<OperationAssignment> ScheduleOperation(CPMNode node, Dictionary<int, List<TimeSlot>> instrumentSchedules)
        {
            var operation = node.Operation;
            
            // Find compatible instruments
            var compatibleInstruments = _instruments.Where(inst => operation.CanExecuteOn(inst)).ToList();
            
            OperationAssignment bestAssignment = null;
            TimeSpan bestStartTime = TimeSpan.MaxValue;
            
            foreach (var instrument in compatibleInstruments)
            {
                var schedule = instrumentSchedules[instrument.Id];
                var earliestStart = CalculateEarliestStartTime(node, schedule);
                
                if (earliestStart < bestStartTime)
                {
                    bestStartTime = earliestStart;
                    bestAssignment = new OperationAssignment
                    {
                        OperationId = operation.Id,
                        InstrumentId = instrument.Id,
                        StartTime = DateTime.Now.Add(earliestStart),
                        EndTime = DateTime.Now.Add(earliestStart).AddMilliseconds(operation.EstimatedDurationMs),
                        Cost = CalculateAssignmentCost(operation, earliestStart, node.TotalFloat)
                    };
                }
            }
            
            return bestAssignment;
        }

        private TimeSpan CalculateEarliestStartTime(CPMNode node, List<TimeSlot> schedule)
        {
            var earliestStart = node.EarliestStart;
            
            // Check for available slots in instrument schedule
            var sortedSlots = schedule.OrderBy(s => s.StartTime).ToList();
            var duration = TimeSpan.FromMilliseconds(node.Operation.EstimatedDurationMs);
            
            foreach (var slot in sortedSlots)
            {
                var slotStart = slot.StartTime - DateTime.Now;
                var slotEnd = slot.EndTime - DateTime.Now;
                
                if (earliestStart + duration <= slotStart)
                {
                    return earliestStart;
                }
                
                earliestStart = TimeSpan.FromTicks(Math.Max(earliestStart.Ticks, slotEnd.Ticks));
            }
            
            return earliestStart;
        }

        private double CalculateAssignmentCost(CPMOperation operation, TimeSpan startTime, TimeSpan totalFloat)
        {
            var baseCost = operation.EstimatedDurationMs;
            
            // Penalty for delaying critical path operations
            var criticalPathPenalty = totalFloat == TimeSpan.Zero ? 0 : 1000;
            
            // Penalty for starting later than earliest possible
            var delayPenalty = Math.Max(0, (startTime - operation.EarliestStartTime.TimeOfDay).TotalMilliseconds);
            
            return baseCost + criticalPathPenalty + delayPenalty;
        }

        private bool CanExecuteNow(CPMOperation operation)
        {
            // Check if all predecessors are completed
            return operation.PredecessorOperations.All(predId => 
                _operations.Any(op => op.Id == predId && op.IsCompleted));
        }

        private async Task ExecuteOperation(CPMOperation operation, FluidInstrument instrument)
        {
            instrument.IsAvailable = false;
            instrument.CurrentOperation = operation;
            
            Console.WriteLine($"[CPM] Executing operation {operation.Id} on {instrument.Name} " +
                             $"(Float: {_projectNetwork[operation.Id].TotalFloat.TotalMilliseconds:F0}ms)");
            
            // Simulate operation execution
            await Task.Delay(operation.EstimatedDurationMs);
            
            operation.IsCompleted = true;
            operation.CompletionTime = DateTime.Now;
            instrument.IsAvailable = true;
            instrument.CurrentOperation = null;
        }

        private double GetCriticalPathLength()
        {
            return _criticalPaths.Count > 0 ? _criticalPaths.Max(p => p.TotalDuration) : 0;
        }

        private CPMOperation ConvertToCPMOperation(FluidOperation operation)
        {
            return new CPMOperation
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
                EarliestStartTime = operation.SubmissionTime,
                PredecessorOperations = new List<int>() // Would need to be populated based on dependencies
            };
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
            // Validate that all precedence constraints are satisfied
            foreach (var assignment in solution.Assignments)
            {
                var operation = _operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    foreach (var predId in operation.PredecessorOperations)
                    {
                        var predAssignment = solution.Assignments.FirstOrDefault(a => a.OperationId == predId);
                        if (predAssignment != null && assignment.StartTime < predAssignment.EndTime)
                        {
                            return false;
                        }
                    }
                }
            }
            
            return true;
        }
    }

    /// <summary>
    /// CPM-specific operation with dependency information
    /// </summary>
    public class CPMOperation : FluidOperation
    {
        public List<int> PredecessorOperations { get; set; }
        public List<int> SuccessorOperations { get; set; }
        public DateTime EarliestStartTime { get; set; }
        public DateTime LatestStartTime { get; set; }
        public TimeSpan TotalFloat { get; set; }
        public TimeSpan FreeFloat { get; set; }
        public bool IsOnCriticalPath { get; set; }

        public CPMOperation()
        {
            PredecessorOperations = new List<int>();
            SuccessorOperations = new List<int>();
        }
    }

    /// <summary>
    /// CPM node representing an operation in the project network
    /// </summary>
    public class CPMNode
    {
        public int Id { get; set; }
        public CPMOperation Operation { get; set; }
        public List<CPMNode> Predecessors { get; set; }
        public List<CPMNode> Successors { get; set; }
        public TimeSpan EarliestStart { get; set; }
        public TimeSpan EarliestFinish { get; set; }
        public TimeSpan LatestStart { get; set; }
        public TimeSpan LatestFinish { get; set; }
        public TimeSpan TotalFloat { get; set; }
        public TimeSpan FreeFloat { get; set; }

        public CPMNode()
        {
            Predecessors = new List<CPMNode>();
            Successors = new List<CPMNode>();
        }
    }

    /// <summary>
    /// Represents a critical path through the project network
    /// </summary>
    public class CPMPath
    {
        public List<CPMOperation> Operations { get; set; }
        public double TotalDuration { get; set; }

        public CPMPath()
        {
            Operations = new List<CPMOperation>();
        }
    }
}

namespace FluidHandling.AdvancedScheduling
{
    /// <summary>
    /// Constraint Satisfaction Problem (CSP) Scheduler
    /// Based on constraint programming principles, highly applicable to IVD scheduling
    /// where multiple constraints must be satisfied simultaneously
    /// Uses backtracking search with constraint propagation and heuristics
    /// </summary>
    public class CSPScheduler : IScheduler
    {
        public string Name => "Constraint Satisfaction Problem Scheduler";
        public string Description => "CSP-based scheduler using constraint programming with backtracking search";
        public SchedulerType Type => SchedulerType.Advanced;

        private readonly List<CSPVariable> _variables;
        private readonly List<CSPConstraint> _constraints;
        private readonly List<FluidInstrument> _instruments;
        private readonly CSPDomain _domain;
        private readonly CSPConfiguration _config;
        private PerformanceMetrics _performanceMetrics;

        public CSPScheduler(List<FluidInstrument> instruments, CSPConfiguration config = null)
        {
            _instruments = instruments;
            _variables = new List<CSPVariable>();
            _constraints = new List<CSPConstraint>();
            _domain = new CSPDomain();
            _config = config ?? new CSPConfiguration();
            _performanceMetrics = new PerformanceMetrics();
        }

        public void AddOperation(FluidOperation operation)
        {
            // Create CSP variables for this operation
            var instrumentVar = new CSPVariable
            {
                Name = $"instrument_{operation.Id}",
                Domain = _instruments.Select(i => i.Id).ToList(),
                OperationId = operation.Id
            };

            var startTimeVar = new CSPVariable
            {
                Name = $"start_{operation.Id}",
                Domain = GenerateTimeSlots(operation).ToList(),
                OperationId = operation.Id
            };

            _variables.Add(instrumentVar);
            _variables.Add(startTimeVar);

            // Create constraints for this operation
            CreateOperationConstraints(operation, instrumentVar, startTimeVar);
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
            
            Console.WriteLine("[CSP] Starting Constraint Satisfaction Problem scheduling...");
            Console.WriteLine($"[CSP] Variables: {_variables.Count}, Constraints: {_constraints.Count}");
            
            // Apply initial constraint propagation
            if (!PropagateConstraints())
            {
                Console.WriteLine("[CSP] Initial constraint propagation failed - no solution exists");
                return new SchedulingSolution { IsValid = false };
            }

            // Use backtracking search to find solution
            var assignment = new Dictionary<string, object>();
            var solution = await BacktrackingSearch(assignment);

            if (solution != null)
            {
                var schedulingSolution = ConvertToSchedulingSolution(solution);
                
                _performanceMetrics.FinalizeMeasurement();
                
                Console.WriteLine($"[CSP] Solution found with {schedulingSolution.Assignments.Count} assignments");
                return schedulingSolution;
            }
            else
            {
                Console.WriteLine("[CSP] No solution found - constraints cannot be satisfied");
                return new SchedulingSolution { IsValid = false };
            }
        }

        public async Task<FluidOperation> ScheduleNextAsync()
        {
            // For incremental scheduling, solve CSP for one operation at a time
            var solution = await GenerateScheduleAsync();
            
            if (solution.IsValid && solution.Assignments.Count > 0)
            {
                var firstAssignment = solution.Assignments.OrderBy(a => a.StartTime).First();
                var operation = CreateOperationFromAssignment(firstAssignment);
                
                if (operation != null)
                {
                    var instrument = _instruments.FirstOrDefault(i => i.Id == firstAssignment.InstrumentId);
                    if (instrument != null)
                    {
                        await ExecuteOperation(operation, instrument);
                        return operation;
                    }
                }
            }
            
            return null;
        }

        public void UpdateConfiguration(SchedulerConfiguration config)
        {
            if (config.Parameters.ContainsKey("MaxBacktrackDepth"))
            {
                _config.MaxBacktrackDepth = (int)config.Parameters["MaxBacktrackDepth"];
            }
            if (config.Parameters.ContainsKey("UseArcConsistency"))
            {
                _config.UseArcConsistency = (bool)config.Parameters["UseArcConsistency"];
            }
        }

        public PerformanceMetrics GetPerformanceMetrics()
        {
            return _performanceMetrics;
        }

        private void CreateOperationConstraints(FluidOperation operation, CSPVariable instrumentVar, CSPVariable startTimeVar)
        {
            // Constraint 1: Instrument capacity constraint
            var capacityConstraint = new CSPConstraint
            {
                Name = $"capacity_{operation.Id}",
                Variables = new List<string> { instrumentVar.Name },
                CheckConstraint = (assignment) =>
                {
                    if (assignment.ContainsKey(instrumentVar.Name))
                    {
                        var instrumentId = (int)assignment[instrumentVar.Name];
                        var instrument = _instruments.FirstOrDefault(i => i.Id == instrumentId);
                        return instrument != null && instrument.MaxVolumeCapacity >= operation.VolumeInMicroliters;
                    }
                    return true;
                }
            };
            _constraints.Add(capacityConstraint);

            // Constraint 2: Deadline constraint
            var deadlineConstraint = new CSPConstraint
            {
                Name = $"deadline_{operation.Id}",
                Variables = new List<string> { startTimeVar.Name },
                CheckConstraint = (assignment) =>
                {
                    if (assignment.ContainsKey(startTimeVar.Name))
                    {
                        var startTime = (DateTime)assignment[startTimeVar.Name];
                        var endTime = startTime.AddMilliseconds(operation.EstimatedDurationMs);
                        return endTime <= operation.Deadline;
                    }
                    return true;
                }
            };
            _constraints.Add(deadlineConstraint);

            // Constraint 3: Resource conflict constraint (no two operations on same instrument at same time)
            var resourceConstraint = new CSPConstraint
            {
                Name = $"resource_{operation.Id}",
                Variables = new List<string> { instrumentVar.Name, startTimeVar.Name },
                CheckConstraint = (assignment) =>
                {
                    return CheckResourceConflict(assignment, operation);
                }
            };
            _constraints.Add(resourceConstraint);
        }

        private bool CheckResourceConflict(Dictionary<string, object> assignment, FluidOperation operation)
        {
            var currentInstrumentKey = $"instrument_{operation.Id}";
            var currentStartTimeKey = $"start_{operation.Id}";
            
            if (!assignment.ContainsKey(currentInstrumentKey) || !assignment.ContainsKey(currentStartTimeKey))
                return true;
            
            var currentInstrument = (int)assignment[currentInstrumentKey];
            var currentStartTime = (DateTime)assignment[currentStartTimeKey];
            var currentEndTime = currentStartTime.AddMilliseconds(operation.EstimatedDurationMs);
            
            // Check against all other assigned operations
            foreach (var kvp in assignment)
            {
                if (kvp.Key.StartsWith("instrument_") && kvp.Key != currentInstrumentKey)
                {
                    var otherOperationId = ExtractOperationId(kvp.Key);
                    var otherInstrumentKey = $"instrument_{otherOperationId}";
                    var otherStartTimeKey = $"start_{otherOperationId}";
                    
                    if (assignment.ContainsKey(otherInstrumentKey) && assignment.ContainsKey(otherStartTimeKey))
                    {
                        var otherInstrument = (int)assignment[otherInstrumentKey];
                        var otherStartTime = (DateTime)assignment[otherStartTimeKey];
                        var otherOperation = GetOperationById(otherOperationId);
                        
                        if (otherOperation != null && currentInstrument == otherInstrument)
                        {
                            var otherEndTime = otherStartTime.AddMilliseconds(otherOperation.EstimatedDurationMs);
                            
                            // Check for time overlap
                            if (!(currentEndTime <= otherStartTime || otherEndTime <= currentStartTime))
                            {
                                return false; // Conflict detected
                            }
                        }
                    }
                }
            }
            
            return true;
        }

        private bool PropagateConstraints()
        {
            Console.WriteLine("[CSP] Propagating constraints...");
            
            bool changed = true;
            int iterations = 0;
            
            while (changed && iterations < _config.MaxPropagationIterations)
            {
                changed = false;
                iterations++;
                
                // Apply arc consistency (AC-3 algorithm)
                if (_config.UseArcConsistency)
                {
                    changed = ApplyArcConsistency();
                }
                
                // Apply constraint propagation
                foreach (var constraint in _constraints)
                {
                    if (PropagateConstraint(constraint))
                    {
                        changed = true;
                    }
                }
                
                // Check if any domain became empty
                if (_variables.Any(v => v.Domain.Count == 0))
                {
                    Console.WriteLine("[CSP] Domain became empty during propagation");
                    return false;
                }
            }
            
            Console.WriteLine($"[CSP] Constraint propagation completed in {iterations} iterations");
            return true;
        }

        private bool ApplyArcConsistency()
        {
            // Simplified AC-3 algorithm
            var queue = new Queue<(CSPVariable, CSPVariable)>();
            
            // Initialize queue with all variable pairs
            for (int i = 0; i < _variables.Count; i++)
            {
                for (int j = i + 1; j < _variables.Count; j++)
                {
                    queue.Enqueue((_variables[i], _variables[j]));
                    queue.Enqueue((_variables[j], _variables[i]));
                }
            }
            
            bool changed = false;
            
            while (queue.Count > 0)
            {
                var (var1, var2) = queue.Dequeue();
                
                if (MakeArcConsistent(var1, var2))
                {
                    changed = true;
                    
                    if (var1.Domain.Count == 0)
                    {
                        return false;
                    }
                    
                    // Add related arcs back to queue
                    foreach (var otherVar in _variables)
                    {
                        if (otherVar != var1 && otherVar != var2)
                        {
                            queue.Enqueue((otherVar, var1));
                        }
                    }
                }
            }
            
            return changed;
        }

        private bool MakeArcConsistent(CSPVariable var1, CSPVariable var2)
        {
            var originalDomainSize = var1.Domain.Count;
            var toRemove = new List<object>();
            
            foreach (var value1 in var1.Domain)
            {
                bool hasSupport = false;
                
                foreach (var value2 in var2.Domain)
                {
                    var testAssignment = new Dictionary<string, object>
                    {
                        [var1.Name] = value1,
                        [var2.Name] = value2
                    };
                    
                    if (IsSatisfiedByAssignment(testAssignment))
                    {
                        hasSupport = true;
                        break;
                    }
                }
                
                if (!hasSupport)
                {
                    toRemove.Add(value1);
                }
            }
            
            foreach (var value in toRemove)
            {
                var1.Domain.Remove(value);
            }
            
            return var1.Domain.Count < originalDomainSize;
        }

        private bool PropagateConstraint(CSPConstraint constraint)
        {
            // Simple constraint propagation
            bool changed = false;
            
            foreach (var variableName in constraint.Variables)
            {
                var variable = _variables.FirstOrDefault(v => v.Name == variableName);
                if (variable != null)
                {
                    var toRemove = new List<object>();
                    
                    foreach (var value in variable.Domain)
                    {
                        var testAssignment = new Dictionary<string, object> { [variableName] = value };
                        
                        if (!constraint.CheckConstraint(testAssignment))
                        {
                            toRemove.Add(value);
                        }
                    }
                    
                    foreach (var value in toRemove)
                    {
                        variable.Domain.Remove(value);
                        changed = true;
                    }
                }
            }
            
            return changed;
        }

        private async Task<Dictionary<string, object>> BacktrackingSearch(Dictionary<string, object> assignment)
        {
            if (assignment.Count == _variables.Count)
            {
                return assignment; // Complete assignment found
            }
            
            // Select unassigned variable using MRV (Minimum Remaining Values) heuristic
            var unassignedVar = SelectUnassignedVariable(assignment);
            if (unassignedVar == null)
                return null;
            
            // Try values in order (could use LCV - Least Constraining Value heuristic)
            var orderedValues = OrderDomainValues(unassignedVar, assignment);
            
            foreach (var value in orderedValues)
            {
                var newAssignment = new Dictionary<string, object>(assignment)
                {
                    [unassignedVar.Name] = value
                };
                
                if (IsConsistent(newAssignment))
                {
                    var result = await BacktrackingSearch(newAssignment);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            
            return null; // No solution found
        }

        private CSPVariable SelectUnassignedVariable(Dictionary<string, object> assignment)
        {
            // MRV heuristic: choose variable with smallest domain
            return _variables
                .Where(v => !assignment.ContainsKey(v.Name))
                .OrderBy(v => v.Domain.Count)
                .ThenBy(v => v.Name) // Tie-breaker
                .FirstOrDefault();
        }

        private List<object> OrderDomainValues(CSPVariable variable, Dictionary<string, object> assignment)
        {
            // Could implement LCV (Least Constraining Value) heuristic here
            // For now, return domain values in original order
            return variable.Domain.ToList();
        }

        private bool IsConsistent(Dictionary<string, object> assignment)
        {
            return _constraints.All(constraint => constraint.CheckConstraint(assignment));
        }

        private bool IsSatisfiedByAssignment(Dictionary<string, object> assignment)
        {
            return _constraints.All(constraint => constraint.CheckConstraint(assignment));
        }

        private IEnumerable<object> GenerateTimeSlots(FluidOperation operation)
        {
            // Generate possible start times for this operation
            var slots = new List<object>();
            var currentTime = DateTime.Max(DateTime.Now, operation.SubmissionTime);
            var endTime = operation.Deadline.AddMilliseconds(-operation.EstimatedDurationMs);
            
            while (currentTime <= endTime)
            {
                slots.Add(currentTime);
                currentTime = currentTime.AddMinutes(_config.TimeSlotIntervalMinutes);
            }
            
            return slots;
        }

        private SchedulingSolution ConvertToSchedulingSolution(Dictionary<string, object> assignment)
        {
            var solution = new SchedulingSolution();
            
            // Group assignments by operation
            var operationAssignments = new Dictionary<int, OperationAssignment>();
            
            foreach (var kvp in assignment)
            {
                var operationId = ExtractOperationId(kvp.Key);
                
                if (!operationAssignments.ContainsKey(operationId))
                {
                    operationAssignments[operationId] = new OperationAssignment
                    {
                        OperationId = operationId
                    };
                }
                
                if (kvp.Key.StartsWith("instrument_"))
                {
                    operationAssignments[operationId].InstrumentId = (int)kvp.Value;
                }
                else if (kvp.Key.StartsWith("start_"))
                {
                    var startTime = (DateTime)kvp.Value;
                    operationAssignments[operationId].StartTime = startTime;
                    
                    var operation = GetOperationById(operationId);
                    if (operation != null)
                    {
                        operationAssignments[operationId].EndTime = startTime.AddMilliseconds(operation.EstimatedDurationMs);
                        operationAssignments[operationId].Cost = operation.EstimatedDurationMs;
                    }
                }
            }
            
            solution.Assignments.AddRange(operationAssignments.Values);
            solution.TotalExecutionTime = CalculateTotalExecutionTime(solution);
            solution.Cost = CalculateTotalCost(solution);
            solution.IsValid = true;
            
            return solution;
        }

        private int ExtractOperationId(string variableName)
        {
            var parts = variableName.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int operationId))
            {
                return operationId;
            }
            return -1;
        }

        private FluidOperation GetOperationById(int operationId)
        {
            // This would need to be implemented based on how operations are stored
            // For now, return a mock operation
            return new FluidOperation
            {
                Id = operationId,
                EstimatedDurationMs = 2000 // Default duration
            };
        }

        private FluidOperation CreateOperationFromAssignment(OperationAssignment assignment)
        {
            return GetOperationById(assignment.OperationId);
        }

        private async Task ExecuteOperation(FluidOperation operation, FluidInstrument instrument)
        {
            instrument.IsAvailable = false;
            instrument.CurrentOperation = operation;
            
            Console.WriteLine($"[CSP] Executing operation {operation.Id} on {instrument.Name}");
            
            // Simulate operation execution
            await Task.Delay(operation.EstimatedDurationMs);
            
            operation.IsCompleted = true;
            operation.CompletionTime = DateTime.Now;
            instrument.IsAvailable = true;
            instrument.CurrentOperation = null;
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
    }

    /// <summary>
    /// CSP variable with domain of possible values
    /// </summary>
    public class CSPVariable
    {
        public string Name { get; set; }
        public List<object> Domain { get; set; }
        public int OperationId { get; set; }

        public CSPVariable()
        {
            Domain = new List<object>();
        }
    }

    /// <summary>
    /// CSP constraint with check function
    /// </summary>
    public class CSPConstraint
    {
        public string Name { get; set; }
        public List<string> Variables { get; set; }
        public Func<Dictionary<string, object>, bool> CheckConstraint { get; set; }

        public CSPConstraint()
        {
            Variables = new List<string>();
        }
    }

    /// <summary>
    /// CSP domain management
    /// </summary>
    public class CSPDomain
    {
        public List<object> TimeSlots { get; set; }
        public List<object> Instruments { get; set; }

        public CSPDomain()
        {
            TimeSlots = new List<object>();
            Instruments = new List<object>();
        }
    }

    /// <summary>
    /// CSP configuration
    /// </summary>
    public class CSPConfiguration
    {
        public int MaxBacktrackDepth { get; set; } = 1000;
        public int MaxPropagationIterations { get; set; } = 100;
        public bool UseArcConsistency { get; set; } = true;
        public int TimeSlotIntervalMinutes { get; set; } = 1;
        public bool UseMRVHeuristic { get; set; } = true;
        public bool UseLCVHeuristic { get; set; } = false;
    }
}