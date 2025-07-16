using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FluidHandling.TimeConstrainedScheduling
{
    /// <summary>
    /// Time Constraint by Mutual Boundaries (TCMB) - Critical for IVD systems
    /// Based on S-LAB (Scheduling for Laboratory Automation in Biology) research
    /// </summary>
    public class TimeConstraintByMutualBoundaries
    {
        public int Operation1Id { get; set; }
        public int Operation2Id { get; set; }
        public int MaxTimeDifferenceMs { get; set; }
        public string ConstraintType { get; set; } // "StartToStart", "StartToEnd", "EndToStart", "EndToEnd"
        public string Reason { get; set; }
    }

    /// <summary>
    /// Enhanced FluidOperation with timing constraints for IVD systems
    /// </summary>
    public class TimedFluidOperation : FluidOperation
    {
        public DateTime EarliestStartTime { get; set; }
        public DateTime LatestStartTime { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public List<int> DependentOperations { get; set; }
        public List<TimeConstraintByMutualBoundaries> TimeConstraints { get; set; }
        public double CriticalityScore { get; set; } // For sample degradation risk
        public string SampleType { get; set; } // RNA, DNA, Protein, etc.
        public double StabilityTimeMs { get; set; } // Max time sample remains stable

        public TimedFluidOperation()
        {
            DependentOperations = new List<int>();
            TimeConstraints = new List<TimeConstraintByMutualBoundaries>();
        }

        public bool IsWithinStabilityWindow()
        {
            if (ActualStartTime == null) return true;
            return (DateTime.Now - ActualStartTime.Value).TotalMilliseconds <= StabilityTimeMs;
        }
    }

    /// <summary>
    /// Earliest Deadline First (EDF) Scheduler
    /// Theoretically optimal for single-processor systems with 100% CPU utilization bound
    /// </summary>
    public class EDFScheduler
    {
        private readonly List<TimedFluidOperation> _operationQueue;
        private readonly List<FluidInstrument> _instruments;

        public EDFScheduler(List<FluidInstrument> instruments)
        {
            _operationQueue = new List<TimedFluidOperation>();
            _instruments = instruments;
        }

        public void AddOperation(TimedFluidOperation operation)
        {
            _operationQueue.Add(operation);
            Console.WriteLine($"[EDF] Added operation {operation.Id} with deadline {operation.Deadline}");
        }

        public async Task<TimedFluidOperation> ScheduleNext()
        {
            if (_operationQueue.Count == 0)
                return null;

            // Find operation with earliest deadline that can be executed
            var earliestDeadlineJob = _operationQueue
                .Where(op => !op.IsCompleted && DateTime.Now >= op.EarliestStartTime)
                .OrderBy(op => op.Deadline)
                .FirstOrDefault();

            if (earliestDeadlineJob == null)
                return null;

            var availableInstrument = _instruments.FirstOrDefault(i => 
                i.IsAvailable && 
                i.MaxVolumeCapacity >= earliestDeadlineJob.VolumeInMicroliters);

            if (availableInstrument == null)
                return null;

            // Check if operation can still meet its deadline
            var timeToDeadline = (earliestDeadlineJob.Deadline - DateTime.Now).TotalMilliseconds;
            if (timeToDeadline < earliestDeadlineJob.EstimatedDurationMs)
            {
                Console.WriteLine($"[EDF] WARNING: Operation {earliestDeadlineJob.Id} may miss deadline!");
            }

            // Remove from queue and assign to instrument
            _operationQueue.Remove(earliestDeadlineJob);
            availableInstrument.IsAvailable = false;
            availableInstrument.CurrentOperation = earliestDeadlineJob;
            earliestDeadlineJob.ActualStartTime = DateTime.Now;
            
            Console.WriteLine($"[EDF] Assigned earliest deadline job {earliestDeadlineJob.Id} (deadline: {earliestDeadlineJob.Deadline}) to {availableInstrument.Name}");
            
            // Simulate operation execution
            await Task.Delay(earliestDeadlineJob.EstimatedDurationMs);
            
            // Complete operation
            earliestDeadlineJob.IsCompleted = true;
            earliestDeadlineJob.CompletionTime = DateTime.Now;
            earliestDeadlineJob.ActualEndTime = DateTime.Now;
            availableInstrument.IsAvailable = true;
            availableInstrument.CurrentOperation = null;
            availableInstrument.LastUsed = DateTime.Now;

            // Check if deadline was met
            if (earliestDeadlineJob.CompletionTime > earliestDeadlineJob.Deadline)
            {
                Console.WriteLine($"[EDF] ERROR: Operation {earliestDeadlineJob.Id} missed deadline by {(earliestDeadlineJob.CompletionTime - earliestDeadlineJob.Deadline).Value.TotalMilliseconds}ms");
            }

            return earliestDeadlineJob;
        }
    }

    /// <summary>
    /// Rate Monotonic Scheduling (RMS) - Fixed priority scheduler
    /// Provides guaranteed schedulability for fixed-priority systems at 69.3% utilization
    /// </summary>
    public class RMSScheduler
    {
        private readonly List<TimedFluidOperation> _operationQueue;
        private readonly List<FluidInstrument> _instruments;
        private readonly Dictionary<int, double> _operationPeriods;

        public RMSScheduler(List<FluidInstrument> instruments)
        {
            _operationQueue = new List<TimedFluidOperation>();
            _instruments = instruments;
            _operationPeriods = new Dictionary<int, double>();
        }

        public void AddPeriodicOperation(TimedFluidOperation operation, double periodMs)
        {
            operation.Priority = (int)(1000.0 / periodMs); // Higher frequency = higher priority
            _operationQueue.Add(operation);
            _operationPeriods[operation.Id] = periodMs;
            Console.WriteLine($"[RMS] Added periodic operation {operation.Id} with period {periodMs}ms (priority: {operation.Priority})");
        }

        public async Task<TimedFluidOperation> ScheduleNext()
        {
            if (_operationQueue.Count == 0)
                return null;

            // Find highest priority (shortest period) operation that can be executed
            var highestPriorityJob = _operationQueue
                .Where(op => !op.IsCompleted && DateTime.Now >= op.EarliestStartTime)
                .OrderByDescending(op => op.Priority)
                .FirstOrDefault();

            if (highestPriorityJob == null)
                return null;

            var availableInstrument = _instruments.FirstOrDefault(i => 
                i.IsAvailable && 
                i.MaxVolumeCapacity >= highestPriorityJob.VolumeInMicroliters);

            if (availableInstrument == null)
                return null;

            // Assign operation to instrument
            availableInstrument.IsAvailable = false;
            availableInstrument.CurrentOperation = highestPriorityJob;
            highestPriorityJob.ActualStartTime = DateTime.Now;
            
            Console.WriteLine($"[RMS] Assigned highest priority job {highestPriorityJob.Id} (priority: {highestPriorityJob.Priority}) to {availableInstrument.Name}");
            
            // Simulate operation execution
            await Task.Delay(highestPriorityJob.EstimatedDurationMs);
            
            // Complete operation
            highestPriorityJob.IsCompleted = true;
            highestPriorityJob.CompletionTime = DateTime.Now;
            highestPriorityJob.ActualEndTime = DateTime.Now;
            availableInstrument.IsAvailable = true;
            availableInstrument.CurrentOperation = null;
            availableInstrument.LastUsed = DateTime.Now;

            // Schedule next occurrence of periodic operation
            var period = _operationPeriods[highestPriorityJob.Id];
            var nextOccurrence = new TimedFluidOperation
            {
                Id = highestPriorityJob.Id + 1000, // Unique ID for next occurrence
                SampleId = highestPriorityJob.SampleId,
                OperationType = highestPriorityJob.OperationType,
                VolumeInMicroliters = highestPriorityJob.VolumeInMicroliters,
                EstimatedDurationMs = highestPriorityJob.EstimatedDurationMs,
                Priority = highestPriorityJob.Priority,
                EarliestStartTime = DateTime.Now.AddMilliseconds(period),
                LatestStartTime = DateTime.Now.AddMilliseconds(period * 1.5),
                Deadline = DateTime.Now.AddMilliseconds(period * 2),
                SampleType = highestPriorityJob.SampleType,
                StabilityTimeMs = highestPriorityJob.StabilityTimeMs
            };

            _operationQueue.Add(nextOccurrence);
            _operationPeriods[nextOccurrence.Id] = period;

            return highestPriorityJob;
        }
    }

    /// <summary>
    /// Time Constraint by Mutual Boundaries (TCMB) Scheduler
    /// Handles complex time constraints between operations - crucial for IVD systems
    /// Based on S-LAB research for laboratory automation
    /// </summary>
    public class TCMBScheduler
    {
        private readonly List<TimedFluidOperation> _operationQueue;
        private readonly List<FluidInstrument> _instruments;
        private readonly List<TimeConstraintByMutualBoundaries> _timeConstraints;

        public TCMBScheduler(List<FluidInstrument> instruments)
        {
            _operationQueue = new List<TimedFluidOperation>();
            _instruments = instruments;
            _timeConstraints = new List<TimeConstraintByMutualBoundaries>();
        }

        public void AddOperation(TimedFluidOperation operation)
        {
            _operationQueue.Add(operation);
            _timeConstraints.AddRange(operation.TimeConstraints);
            Console.WriteLine($"[TCMB] Added operation {operation.Id} with {operation.TimeConstraints.Count} time constraints");
        }

        private bool CanExecuteOperation(TimedFluidOperation operation)
        {
            // Check all time constraints involving this operation
            foreach (var constraint in _timeConstraints.Where(tc => tc.Operation1Id == operation.Id || tc.Operation2Id == operation.Id))
            {
                var otherOpId = constraint.Operation1Id == operation.Id ? constraint.Operation2Id : constraint.Operation1Id;
                var otherOp = _operationQueue.FirstOrDefault(op => op.Id == otherOpId);
                
                if (otherOp == null) continue;

                // Check if constraint would be violated
                if (!ValidateTimeConstraint(operation, otherOp, constraint))
                {
                    Console.WriteLine($"[TCMB] Operation {operation.Id} blocked by time constraint with operation {otherOpId}");
                    return false;
                }
            }

            // Check sample stability
            if (!operation.IsWithinStabilityWindow())
            {
                Console.WriteLine($"[TCMB] Operation {operation.Id} exceeds sample stability window");
                return false;
            }

            return true;
        }

        private bool ValidateTimeConstraint(TimedFluidOperation op1, TimedFluidOperation op2, TimeConstraintByMutualBoundaries constraint)
        {
            // For operations that haven't started yet, we can't validate end times
            if (op1.ActualStartTime == null || op2.ActualStartTime == null)
            {
                return true; // Will be validated when both operations are scheduled
            }

            var timeDiff = Math.Abs((op1.ActualStartTime.Value - op2.ActualStartTime.Value).TotalMilliseconds);
            
            return timeDiff <= constraint.MaxTimeDifferenceMs;
        }

        public async Task<TimedFluidOperation> ScheduleNext()
        {
            if (_operationQueue.Count == 0)
                return null;

            // Find operation with highest criticality that can be executed
            var criticalJob = _operationQueue
                .Where(op => !op.IsCompleted && 
                           DateTime.Now >= op.EarliestStartTime && 
                           CanExecuteOperation(op))
                .OrderByDescending(op => op.CriticalityScore)
                .ThenBy(op => op.Deadline)
                .FirstOrDefault();

            if (criticalJob == null)
                return null;

            var availableInstrument = _instruments.FirstOrDefault(i => 
                i.IsAvailable && 
                i.MaxVolumeCapacity >= criticalJob.VolumeInMicroliters);

            if (availableInstrument == null)
                return null;

            // Remove from queue and assign to instrument
            _operationQueue.Remove(criticalJob);
            availableInstrument.IsAvailable = false;
            availableInstrument.CurrentOperation = criticalJob;
            criticalJob.ActualStartTime = DateTime.Now;
            
            Console.WriteLine($"[TCMB] Assigned critical operation {criticalJob.Id} (criticality: {criticalJob.CriticalityScore}) to {availableInstrument.Name}");
            
            // Simulate operation execution
            await Task.Delay(criticalJob.EstimatedDurationMs);
            
            // Complete operation
            criticalJob.IsCompleted = true;
            criticalJob.CompletionTime = DateTime.Now;
            criticalJob.ActualEndTime = DateTime.Now;
            availableInstrument.IsAvailable = true;
            availableInstrument.CurrentOperation = null;
            availableInstrument.LastUsed = DateTime.Now;

            // Validate all time constraints were met
            ValidateCompletedOperationConstraints(criticalJob);

            return criticalJob;
        }

        private void ValidateCompletedOperationConstraints(TimedFluidOperation completedOp)
        {
            foreach (var constraint in _timeConstraints.Where(tc => tc.Operation1Id == completedOp.Id || tc.Operation2Id == completedOp.Id))
            {
                var otherOpId = constraint.Operation1Id == completedOp.Id ? constraint.Operation2Id : constraint.Operation1Id;
                var otherOp = _operationQueue.FirstOrDefault(op => op.Id == otherOpId);
                
                if (otherOp?.IsCompleted == true)
                {
                    if (!ValidateTimeConstraint(completedOp, otherOp, constraint))
                    {
                        Console.WriteLine($"[TCMB] WARNING: Time constraint violated between operations {completedOp.Id} and {otherOpId}");
                    }
                }
            }
        }
    }
}