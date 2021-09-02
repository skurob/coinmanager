using System;
using System.Linq;
using System.Timers;
using System.Collections.Generic;

using CoinManager.EF;

namespace CoinManager.Tasks
{
    public class TransactionsTasks
    {
        private CMDbContext db;
        private const int STOPPING_INTERVAL = 500;
        private List<Tuple<Timer, int>> timers; 

        public TransactionsTasks()
        {
            db = CMDbContext.Instance;
            timers = new List<Tuple<Timer, int>>();
        }

        public void Start()
        {
            var transList = db.RunningTransaction.ToList();
            if(transList.Count > 0)
                AddTimerFromList(transList);
        }

        public void Check()
        {
            var transList = db.RunningTransaction.ToList();
            var notFoundIds = timers
                .Select(tuple => tuple.Item2)
                .Where(transId => transList.FirstOrDefault(r => r.TransactionId == transId) == null)
                .ToList();
            var toProcess = transList
                .Where(t => notFoundIds.Contains(t.TransactionId))
                .ToList();
            AddTimerFromList(toProcess);
        }

        private void AddTimerFromList(List<RunningTransaction> transList)
        {
            foreach(var trans in transList)
            {
                var interval = DateTime.Now - trans.StartDate >= trans.TotalTime
                    ? new TimeSpan (0, 0, 0, 0, STOPPING_INTERVAL)
                    : trans.TotalTime - (DateTime.Now - trans.StartDate);
                var timer = new Timer { Interval = interval.TotalMilliseconds };
                var tuple = new Tuple<Timer, int>(timer, trans.TransactionId);

                timer.Elapsed += (sender, e) => 
                {
                    var transToUpdate = db.Transaction.Find(trans.TransactionId);
                    db.RunningTransaction.Remove(trans);
                    transToUpdate.State = 1;
                    transToUpdate.FinishDate = DateTime.Now;
                    db.Transaction.Update(transToUpdate);
                    db.SaveChanges();
                    timers.Remove(tuple);
                };

                timer.Enabled = true;
                timer.AutoReset = false;
                timer.Start();
                timers.Add(tuple);
            }
        }

        public void EndTimer(int transactionId, int minerId)
        {
            var tuple = timers.FirstOrDefault(tuple => tuple.Item2 == transactionId);
            if(tuple == null) return;
            tuple.Item1.Interval = STOPPING_INTERVAL;
            var trans = db.Transaction.Find(transactionId);
            trans.MinerId = minerId;
            db.SaveChanges();
        }
    }
}
