using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoOrderTrackerLambda
{
    class InvestmentHistory
    {
        /// <summary>
        /// The buy and sell history of a crypto (Used as JSON).
        /// </summary>
        public List<InvestmentDynamo> History { get; set; }
    }
}
