using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoOrderTrackerLambda
{
    /// <summary>
    /// The crypto information to send to DynamoDB.
    /// </summary>
    class InvestmentDynamo
    {
        /// <summary>
        /// The name of the crypto.
        /// </summary>
        public string Crypto { get; set; }

        /// <summary>
        /// Was the crypto bought or sold (values: BUY/SELL)
        /// </summary>
        public string BuyOrSell { get; set; }

        /// <summary>
        /// The total amount of crypto available.
        /// </summary>
        public string Available { get; set; }

        /// <summary>
        /// The amount of crypto bought or sold.
        /// </summary>
        public string AmountBoughtOrSold { get; set; }

        /// <summary>
        /// The price the crypto was bought/sold at.
        /// </summary>
        public string Price { get; set; }

        /// <summary>
        /// The average price of the current crypto.
        /// </summary>
        public string Average { get; set; }

        /// <summary>
        /// The amount of money paid to buy the crypto.
        /// </summary>
        public string AmountPaid { get; set; }

        /// <summary>
        /// The total amount of money spent on the crypto.
        /// </summary>
        public string TotalAmount { get; set; }

        /// <summary>
        /// The profit or loss for the sale.
        /// </summary>
        public string ProfitOrLoss { get; set; }

        /// <summary>
        /// The DateTime the crypto was bought/sold at (to help differentiate multiple items with same crypto).
        /// </summary>
        public string DateTime { get; set; }
    }
}
