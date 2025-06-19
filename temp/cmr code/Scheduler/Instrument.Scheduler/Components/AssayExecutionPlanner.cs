namespace Instrument.Scheduler.Components;

using System.Linq;
using Instrument.Scheduler.Abstraction;
using Instrument.Scheduler.Abstraction.TestOrder;

/// <summary>
///  Class that encapsulates the alogrithm that will generate an execution plan.
///  The algorithm , just groups the list of tests , into groups of technologies and sample types
///  It will always prefer to run a maximum number
/// </summary>
public class AssayExecutionPlanner
{  
    private readonly int _maxTestsScheduled = 4;

    public AssayExecutionPlanner()
    {
    }

    public IExecutionPlan GetExecutionPlan(IReadOnlyCollection<ITestOrder> testOrders)
    {
        var groupedTestOrders = GroupAndSplit(testOrders, _maxTestsScheduled, testOrder => testOrder.TestMethod);
        var executionPlanSteps = new List<AssayExecutionStep>();

        foreach (var order in groupedTestOrders)
        {
            executionPlanSteps.Add(new AssayExecutionStep() { TestOrders = order.AsEnumerable() });
        }

        return new AssayExecutionPlan(executionPlanSteps);
    }

    public static List<List<ITestOrder>> GroupAndSplit(IReadOnlyCollection<ITestOrder> source,
                                                int maxSize,
                                                Func<ITestOrder, object> groupBySelector)
    {
        return [.. source
            .GroupBy(groupBySelector)
            .Select(group =>
            {
                var groupList = group.ToList();
                var result = new List<List<ITestOrder>>();
                for (var i = 0; i < groupList.Count; i += maxSize)
                {
                    result.Add(groupList.GetRange(i, Math.Min(maxSize, groupList.Count - i)));
                }
                return result;
            })
            .SelectMany(x => x)];
    }
}