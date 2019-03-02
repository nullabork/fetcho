using System;
using System.IO;

namespace Fetcho
{
	public class QueueoConfiguration
	{
        public int HowOftenToReportStatusInMilliseconds = 30000;

        public IQueuePriorityCalculationModel QueueOrderingModel {
			get;
			set;
		}

        public QueueoConfiguration()
        {
        }

    }
}

