using System;
using System.Collections.Generic;
using System.Configuration;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2.Model;
using CryptoOrderTracker;
using Kucoin.Net;
using Kucoin.Net.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CryptoOrderTrackerLambda
{
    public class Function
    {

        static readonly string apiKey = Environment.GetEnvironmentVariable("ApiKey");
        static readonly string passphrase = Environment.GetEnvironmentVariable("ApiPassphrase");
        static readonly string secret = Environment.GetEnvironmentVariable("ApiSecret");
        static readonly string tableName = "CryptoInvestments";

        public static void FunctionHandler(ILambdaContext context)
        {
            InvestmentTrackedRouter(GetAccountSpotInvestments(context), context);
        }



        /// <summary>
        /// Gets all of the investments currently held.
        /// </summary>
        /// <param name="context">ILambdaContext logger.</param>
        /// <returns>An IList of SpotInvestments() filled with all the investment details from the API.</returns>
        static IList<KucoinInvestments> GetAccountSpotInvestments(ILambdaContext context)
        {
            // Setting up the investMentList to hold investment response, and client with the correct credentials.
            IList<KucoinInvestments> investmentList;
            KucoinClient client = new KucoinClient(new KucoinClientOptions()
            {
                ApiCredentials = new KucoinApiCredentials(apiKey, secret, passphrase)
            });

            // Call the API and check results.
            try
            {
                var callApi = client.Spot.GetAccountsAsync();
                callApi.Wait();

                if (callApi.Result.Success && callApi.Result.Data != null)
                {
                    /* If no error then serialise the Result.Data which contains the data then turn it into an object
                     * to add it to the SpotInvestments class. */
                    string serialised = JsonConvert.SerializeObject(callApi.Result.Data);
                    investmentList = JsonString2Object<IList<KucoinInvestments>>(serialised);
                    Console.WriteLine(investmentList);
                    return investmentList;
                }
                else
                {
                    throw new Exception($"Error Returned: {callApi.Result.Error}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Call not going through: {e.Message}");
                throw;
            }
        }



        /// <summary>
        /// Checks if the investment is in the DynamoDB.<br/> 
        /// If it is then check the price to see if it was bought/sold.<br/>
        /// If not then add it to DynamoDB.
        /// </summary>
        /// <param name="kuinvestmentList">The ILit of the investments from KuCoin to check.</param>
        /// <param name="context">The ILambdaContext logger to write logs.</param>
        static void InvestmentTrackedRouter(IList<KucoinInvestments> kuinvestmentList, ILambdaContext context)
        {
            InvestmentHistory investmentHistoryJson = new InvestmentHistory();
            Task<GetItemResponse> dbResponse;
            InvestmentDynamo dynamoResultHolder = new InvestmentDynamo();
            AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();

            foreach (var kuInvestment in kuinvestmentList)
            {
                // We don't want to track USDT, so continue to next iteration if current crypto is USDT.
                if (kuInvestment.Currency == "USDT" || kuInvestment.Available == "0.0")
                {
                    continue;
                }

                var request = new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue>() { 
                        { 
                            "Crypto", new AttributeValue { S = kuInvestment.Currency } 
                        } 
                    },
                };

                try
                {
                    dbResponse = dynamoDbClient.GetItemAsync(request);
                    dbResponse.Wait();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR executing GetTickerAsync: {e.Message}");
                    throw;
                }

                // If the api call was successful but there was no crypto found with that name.
                if (dbResponse.Result.Item.Count == 0)
                {
                    // Add the information to the database because it is not there
                    AddNewInvestment(kuInvestment, context);
                }
                else
                {
                    // Crypto was found in DynamoDb so we set the values to our model to compare it to Kucoin results.
                    foreach (var item in dbResponse.Result.Item)
                    {
                        if (item.Key == "Crypto")
                        {
                            dynamoResultHolder.Crypto = item.Value.S;
                        }
                        else if (item.Key == "Available")
                        {
                            dynamoResultHolder.Available = item.Value.S;
                        }
                        else if (item.Key == "History")
                        {
                            // Deserilise the string so the JSON objects can be read and added to a history list.
                            dynamic deserilisedHistory = JsonConvert.DeserializeObject(item.Value.S);
                            investmentHistoryJson.History = new List<InvestmentDynamo>();
                            foreach (object obj in deserilisedHistory.History)
                            {
                                investmentHistoryJson.History.Add(JsonString2Object<InvestmentDynamo>(obj.ToString()));
                            }
                        }
                    }

                    CryptoComparator(kuInvestment, dynamoResultHolder, investmentHistoryJson, context);
                }
            }
        }



        /// <summary>
        /// Checks whether the crypto has been bought or sold. If so, then update the investment on DynamoDB, if not 
        /// then do nothing.
        /// </summary>
        /// <param name="kuInvestment">The investment from KuCoin.</param>
        /// <param name="updatedInvestment">The investment from the database.</param>
        /// <param name="investmentHistoryJson">The crypto investments as a List(InvestmentDynamo)</param>
        /// <param name="context">ILambdaContext Logger.</param>
        static void CryptoComparator(KucoinInvestments kuInvestment, InvestmentDynamo updatedInvestment, 
            InvestmentHistory investmentHistoryJson, ILambdaContext context)
        {
            float dbCryptoAmount = float.Parse(updatedInvestment.Available);
            float kucoinCryptoAmount = float.Parse(kuInvestment.Available);
            float cryptoDifference = kucoinCryptoAmount - dbCryptoAmount;

            if (kucoinCryptoAmount < dbCryptoAmount)
            {
                // it has been sold
                updatedInvestment.BuyOrSell = "SELL";
                updatedInvestment.AmountBoughtOrSold = $"-{cryptoDifference}";
            } 
            else if (kucoinCryptoAmount > dbCryptoAmount)
            {
                updatedInvestment.BuyOrSell = "BUY";
                updatedInvestment.AmountBoughtOrSold = $"+{cryptoDifference}";
                updatedInvestment.Price = GetCryptoCurrentPrice(updatedInvestment.Crypto, context).ToString();
                updatedInvestment.AmountPaid = (cryptoDifference * float.Parse(updatedInvestment.Price)).ToString();
                updatedInvestment.TotalAmount = 
                    (float.Parse(investmentHistoryJson.History[^1].TotalAmount) + 
                    float.Parse(updatedInvestment.AmountPaid)).ToString();
            } 
            else
            {
                // No change, do nothing
                return;
            }

            updatedInvestment.Available = kuInvestment.Available;
            updatedInvestment.Average = "FIX THIS";
            updatedInvestment.DateTime = DateTime.Now.ToString();
            investmentHistoryJson.History.Add(updatedInvestment);
            string investmentHistoryString = JsonConvert.SerializeObject(investmentHistoryJson, Formatting.Indented);
            UpdateInvestment(updatedInvestment, investmentHistoryString, context);
        }



        /// <summary>
        /// Gets the current price of the provided crypto.
        /// </summary>
        /// <param name="crypto">The crypto name (e.g. BTC, ACE, ADA)</param>
        /// <param name="context">The ILambdaContext logger to write logs.</param>
        /// <returns>The current price as a string.</returns>
        static string GetCryptoCurrentPrice(string crypto, ILambdaContext context)
        {
            KucoinClient client = new KucoinClient(new KucoinClientOptions()
            {
                ApiCredentials = new KucoinApiCredentials(apiKey, secret, passphrase)
            });

            // Call the API and check results.
            try
            {
                var callApi = client.Spot.GetTickerAsync($"{crypto}-USDT");
                callApi.Wait();

                if (callApi.Result.Success && callApi.Result.Data != null)
                {
                    /* If no error then return the current crypto price */
                    return callApi.Result.Data.LastTradePrice.ToString();
                }
                else
                {
                    throw new Exception($"Error Returned: {callApi.Result.Error}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"ERROR executing GetTickerAsync: {e.Message}");
                throw;
            }
        }



        /// <summary>
        /// Adds a new investment to the DynamoDb.
        /// </summary>
        /// <param name="investment">The KuCoin investment with the information of the crypto to add.</param>
        /// <param name="context">ILambdaContext logger.</param>
        static void AddNewInvestment(KucoinInvestments investment, ILambdaContext context)
        {
            AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();
            string investmentPrice = GetCryptoCurrentPrice(investment.Currency, context);

            InvestmentDynamo investmentToAdd = new InvestmentDynamo()
            {
                Crypto = investment.Currency,
                BuyOrSell = "BUY",
                Available = investment.Available,
                AmountBoughtOrSold = investment.Available,
                Price = investmentPrice,
                Average = investmentPrice,
                AmountPaid = (float.Parse(investment.Available) * float.Parse(investmentPrice)).ToString(),
                TotalAmount = (float.Parse(investment.Available) * float.Parse(investmentPrice)).ToString(),
                ProfitOrLoss = "",
                DateTime = DateTime.Now.ToString()
            };

            InvestmentHistory investmentHistoryJson = new InvestmentHistory();
            investmentHistoryJson.History = new List<InvestmentDynamo>{ investmentToAdd };
            string history = JsonConvert.SerializeObject(investmentHistoryJson, Formatting.Indented);

            var request = new PutItemRequest
            {
                TableName = tableName,
                Item = new Dictionary<string, AttributeValue>() {
                    {"Crypto", new AttributeValue { S = investmentToAdd.Crypto }},
                    {"Available", new AttributeValue { S = investment.Available }},
                    {"Average", new AttributeValue { S = investmentPrice }},
                    {"History", new AttributeValue { S = history }}
                },
            };

            try
            {
                var response = dynamoDbClient.PutItemAsync(request);
                response.Wait();
            }
            catch (Exception e) {
                context.Logger.Log($"ERROR executing PutItem: {e.Message}");
                throw;
            }
        }



        /// <summary>
        /// Updates the DynamoDb entry with the new information.
        /// </summary>
        /// <param name="investment">The InvestmentDynamo model with the new information to update.</param>
        /// <param name="investmentHistory">The investmentHistory serialised JSON to update.</param>
        /// <param name="context">ILambdaContext Logger.</param>
        static void UpdateInvestment(InvestmentDynamo investment, string investmentHistory, 
            ILambdaContext context)
        {
            AmazonDynamoDBClient dynamoDbClient = new AmazonDynamoDBClient();
            Dictionary<string, AttributeValueUpdate> updates = new Dictionary<string, AttributeValueUpdate>();
            updates["Available"] = new AttributeValueUpdate()
            {
                Action = AttributeAction.PUT,
                Value = new AttributeValue { S = investment.Available }
            };
            updates["Average"] = new AttributeValueUpdate()
            {
                Action = AttributeAction.PUT,
                Value = new AttributeValue { S = investment.Average }
            };
            updates["History"] = new AttributeValueUpdate()
            {
                Action = AttributeAction.PUT,
                Value = new AttributeValue { S = investmentHistory }
            };

            var request = new UpdateItemRequest
            {
                TableName = tableName,
                Key = new Dictionary<string, AttributeValue>() {
                    { "Crypto", new AttributeValue { S = investment.Crypto } }
                },
                AttributeUpdates = updates
            };

            try
            {
                var response = dynamoDbClient.UpdateItemAsync(request);
                response.Wait();
            }
            catch (Exception e)
            {
                context.Logger.Log($"ERROR executing UpdateItemAsync(): {e.Message}");
                throw;
            }
        }



        /// <summary>
        /// Converts a string of serialised data into separate objects.
        /// </summary>
        /// <typeparam name="TObj">The list/IList to save the data to.</typeparam>
        /// <param name="str">The string of serialised objects.</param>
        /// <returns>A list of objects from the serialised string.</returns>
        static TObj JsonString2Object<TObj>(string str)
        {
            return JsonConvert.DeserializeObject<TObj>(str, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
        }

    }
}
