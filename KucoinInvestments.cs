using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoOrderTracker
{
    /// <summary>
    /// Class to hold GetAccountsAsync() Kucoin API call results.
    /// Holds the information for the crypto in the spot account.
    /// </summary>
    class KucoinInvestments
    {
        /// <summary>
        /// The ID of the account.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The crypto name.
        /// </summary>
        public string Currency { get; set; }

        /// <summary>
        /// Account type: main, trade, margin or pool.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Total funds in the account (Amount of crypto in account).
        /// </summary>
        public string Balance { get; set; }

        /// <summary>
        /// Funds available to withdraw or trade.
        /// </summary>
        public string Available { get; set; }

        /// <summary>
        /// Funds on hold (not available for use).
        /// </summary>
        public string Holds { get; set; }
    }
}
