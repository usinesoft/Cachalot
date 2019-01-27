using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Server
{
    /// <summary>
    ///     Manages two queues of scheduled and waiting tasks. Used internally by the scheduler to simplify
    ///     concurrent access to the two queues and attached data
    /// </summary>
    /// <typeparam name="TInputData">type of the input data for the workitems</typeparam>
    internal class TaskQueues<TInputData> where TInputData : class
    {
        /// <summary>
        ///     Its internal counter should always be equal to the number of items in the
        ///     <see
        ///         cref="_scheduledForExecutionWorkitems" />
        /// </summary>
        private readonly Semaphore _itemsToProcess;

        /// <summary>
        ///     Workitems currently scheduled to be executed in parallel
        /// </summary>
        private readonly Queue<WorkItem<TInputData>> _scheduledForExecutionWorkitems;

        private readonly object _syncRoot = new object();

        /// <summary>
        ///     Workitems waiting to be scheduled. They are executed sequentially and
        ///     are put on hold until previously scheduled ones finish accessing the shared resource
        /// </summary>
        private readonly Queue<WorkItem<TInputData>> _waitingWorkitems;

        /// <summary>
        ///     true if the <see cref="_scheduledForExecutionWorkitems" /> queue contains a task which needs write access to shared data
        /// </summary>
        private bool _scheduledWriteAccess;

        private int _threadsCurrentlyReadOnlyAccessingSharedData;

        private int _threadsCurrentlyWriteAccessingSharedData;

        public TaskQueues()
        {
            _scheduledForExecutionWorkitems = new Queue<WorkItem<TInputData>>();
            _waitingWorkitems = new Queue<WorkItem<TInputData>>();
            _itemsToProcess = new Semaphore(0, int.MaxValue);
        }

        /// <summary>
        ///     Blocks until data available
        /// </summary>
        /// <returns></returns>
        public WorkItem<TInputData> WaitForTask()
        {
            _itemsToProcess.WaitOne();

            lock (_syncRoot)
            {
//#if DEBUG
//                Console.WriteLine(ToString());
//#endif
                Debug.Assert(_scheduledForExecutionWorkitems.Count > 0);


                // If the workitem to process is a StopMarker, just peek (no dequeue) so
                // it is still available for the rest of the worker threads
                // Also manually release the semaphore so the next waiting thread can access the data, find the stop marker
                // and commit suicide
                var wi = _scheduledForExecutionWorkitems.Peek();

                if (wi.IsStopMarker)
                    _itemsToProcess.Release(); //the semaphore should not count the stop markers
                else
                {
                    _scheduledForExecutionWorkitems.Dequeue();
                    if (wi.NeedsWriteAccess)
                    {
                        Debug.Assert(_scheduledWriteAccess);
                        _scheduledWriteAccess = false;
                        _threadsCurrentlyWriteAccessingSharedData++;
                    }
                    else
                    {
                        _threadsCurrentlyReadOnlyAccessingSharedData++;
                    }

                    // not really useful here
                    // tryToScheduleWaitingTasks();
                }

                return wi;
            }
        }

        /// <summary>
        ///     Post a special workitem which will make consumer threads commit suicide
        /// </summary>
        public void Stop()
        {
            lock (_syncRoot)
            {
                //the default constructor creates a stop workitem
                NewTask(new WorkItem<TInputData>());
            }
        }

        /// <summary>
        ///     Check if an workitem is eligible for immediate execution (in fact to be moved to the scheduled queue which means it can be
        ///     immediately executed)
        ///     Workitems needing write access can be executed only if no other task is currently executing or scheduled for execution
        ///     Workitems needing read-only access can be executed only if all the currently executing or currently scheduled tasks are read-only
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        private bool CanBeScheduled(WorkItem<TInputData> task)
        {
            //if it needs write access it can be scheduled only if no other task is accessing shared data
            if (task.NeedsWriteAccess)
            {
                if (_scheduledForExecutionWorkitems.Count > 0)
                    return false;
                return (_threadsCurrentlyWriteAccessingSharedData + _threadsCurrentlyReadOnlyAccessingSharedData) == 0;
            }

            //if only read only access is needed check if no other task is currently write accessing shared data
            if (_scheduledWriteAccess)
                return false;
            return _threadsCurrentlyWriteAccessingSharedData == 0;
        }

        /// <summary>
        ///     New task to process
        /// </summary>
        /// <param name="task"></param>
        public void NewTask(WorkItem<TInputData> task)
        {
            lock (_syncRoot)
            {
                int waitingItems = _waitingWorkitems.Count;

                if (waitingItems == 0) //no task on hold
                {
                    if (CanBeScheduled(task)) //requires read only access
                    {
                        _scheduledForExecutionWorkitems.Enqueue(task);
                        _itemsToProcess.Release();

                        if (task.NeedsWriteAccess)
                        {
                            Debug.Assert(_scheduledWriteAccess == false);
                            _scheduledWriteAccess = true;
                        }
                    }
                    else
                    {
                        _waitingWorkitems.Enqueue(task);
                    }
                }
                else // if older tasks are waiting this one will wait too 
                    // as the scheduler is not allowed to change tasks order
                {
                    _waitingWorkitems.Enqueue(task);
                }
            }
        }

        /// <summary>
        ///     Try to move workitems from the waiting list to
        ///     the scheduled for execution list
        /// </summary>
        private void TryToScheduleWaitingTasks()
        {
            if (_waitingWorkitems.Count > 0)
            {
                var toSchedule = _waitingWorkitems.Peek();

                Debug.Assert(toSchedule != null);

                while (CanBeScheduled(toSchedule))
                {
                    _waitingWorkitems.Dequeue();

                    _scheduledForExecutionWorkitems.Enqueue(toSchedule);
                    _itemsToProcess.Release();

                    if (toSchedule.NeedsWriteAccess)
                    {
                        Debug.Assert(_scheduledWriteAccess == false);
                        _scheduledWriteAccess = true;
                    }

                    //if the task needs a write access, schedule only one at a time
                    if (toSchedule.NeedsWriteAccess)
                        break;

                    if (_waitingWorkitems.Count == 0)
                        break;

                    toSchedule = _waitingWorkitems.Peek(); //get the next waiting workitem
                }
            }
        }

        /// <summary>
        ///     One of the tasks finished accessing shared data (read only) so new waiting tasks may eventually be scheduled
        /// </summary>
        public void EndWriteAccess()
        {
            lock (_syncRoot)
            {
                _threadsCurrentlyWriteAccessingSharedData--;
                if (_threadsCurrentlyWriteAccessingSharedData == 0)
                    TryToScheduleWaitingTasks();
            }
        }

        /// <summary>
        ///     One of the tasks finished accessing shared data (read only) so new waiting tasks may eventually be scheduled
        /// </summary>
        public void EndReadOnlyAccess()
        {
            lock (_syncRoot)
            {
                _threadsCurrentlyReadOnlyAccessingSharedData--;
                if (_threadsCurrentlyReadOnlyAccessingSharedData == 0)
                    TryToScheduleWaitingTasks();
            }
        }


        /// <summary>
        ///     Display the content of the queues and the status of the executing workitems
        ///     For debug only.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            lock (_syncRoot)
            {
                var sb = new StringBuilder();

                sb.Append("waiting [");

                const int maxWaiting = 10;
                int curWaiting = 0;
                foreach (var item in _waitingWorkitems)
                {
                    if (item.IsStopMarker)
                        sb.Append("*");
                    else
                    {
                        sb.Append(item.NeedsWriteAccess ? "w" : "r");

                        sb.Append(item.Id);
                    }

                    sb.Append(" ");

                    if (curWaiting++ > maxWaiting)
                    {
                        sb.Append("...");
                        break;
                    }
                }

                sb.Append("] pending [");

                foreach (var item in _scheduledForExecutionWorkitems)
                {
                    if (item.IsStopMarker)
                        sb.Append("*");
                    else
                    {
                        sb.Append(item.NeedsWriteAccess ? "w" : "r");

                        sb.Append(item.Id);
                    }

                    sb.Append(" ");
                }

                sb.Append("] working now: write-access=");
                sb.Append(_threadsCurrentlyWriteAccessingSharedData);
                sb.Append(" read-only-access=");
                sb.Append(_threadsCurrentlyReadOnlyAccessingSharedData);

                return sb.ToString();
            }
        }
    }
}