using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Data.SqlClient;

namespace BlockchainAnalyser
{
    class BlockchainAnalyser
    {
        // Members
        private readonly RpcManager m_rpcManager;

        // File paths
        private readonly string TRADE_PATH;
        private readonly string VERBOSE_TRADE_PATH;
        private readonly string BLOCKHEIGHT_PATH;
        private readonly string ARBITRATION_PATH;
        private readonly string SEGWIT_PATH;

        private List<List<string>> m_clusters; // Each cluster is a list, so the collection of all clusters is a list of lists
        private Dictionary<string, int> m_addressMap; // Maps addresses to clusters
        private Dictionary<string, Trade> m_tradeMap; // Trades we retrieved
        private Dictionary<string, string> m_arbitratedTrades; // Map of arbitrated trades
        private Dictionary<string, JObject> m_segwitInputs; // Map of segwit input txes

        private List<string> m_missedTxes; // Trades we missed according to trade2Statistics
        private Dictionary<string, Trade> m_overshootTrades; // Trades trade2Statistics missed according to our retrieval

        private SqlManager m_sqlManager;

        public BlockchainAnalyser(string username, 
            string password, 
            string url, 
            string tradePath, 
            string verboseTradePath, 
            string blockheightPath, 
            string arbitrationPath, 
            string segwitPath,
            string sqlConnectionString,
            string sqlFilepath)
        {
            m_rpcManager = new RpcManager(username, password, url);
            TRADE_PATH = tradePath;
            VERBOSE_TRADE_PATH = verboseTradePath;
            BLOCKHEIGHT_PATH = blockheightPath;
            ARBITRATION_PATH = arbitrationPath;
            SEGWIT_PATH = segwitPath;

            m_clusters = new List<List<string>>();
            m_addressMap = new Dictionary<string, int>();
            m_tradeMap = new Dictionary<string, Trade>();
            m_arbitratedTrades = new Dictionary<string, string>();
            m_segwitInputs = new Dictionary<string, JObject>();

            m_missedTxes = new List<string>();
            m_overshootTrades = new Dictionary<string, Trade>();

            m_sqlManager = new SqlManager(sqlConnectionString, sqlFilepath);

            LoadTrades();
        }

        #region Trade_Retrieval

        #region Get_Trades
        public void GetBisqDepositTxes(int startBlockheight, int connectionLimit)
        {
            IterateBlocks(startBlockheight);
        }

        private void IterateBlocks(int startBlockHeight)
        {
            Console.WriteLine("Press 'esc' key to interrupt application safely");
            Console.WriteLine("Getting total block count...");

            // Get start and end point for block retrieval
            int totalBlockCount = Convert.ToInt32(m_rpcManager.GetBlockCount()["result"]);
            int savedBlockHeight = GetLastBlockHeight();
            int currentBlockIndex = savedBlockHeight > startBlockHeight ? savedBlockHeight : startBlockHeight;

            Console.WriteLine("Current total block count: " + totalBlockCount);
            Console.WriteLine("Starting at block " + currentBlockIndex);

            List<int> blockBatch = new List<int>();
            
            // Esc will interrupt this while loop allowing the current block index to be saved without skipping a block
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) && currentBlockIndex < totalBlockCount)
            {
                blockBatch.Add(currentBlockIndex);
                if(blockBatch.Count >= 5 || currentBlockIndex >= totalBlockCount)
                {
                    Console.WriteLine("Getting block indices: " + blockBatch[0] + "-" + blockBatch[blockBatch.Count - 1]);
                    BulkGetBlockHash(ref blockBatch); // Method retrieves block hashes, then passes hashes to emthod to get blocks
                    ++currentBlockIndex; // We need to increment before we save, bit poorly coded but it'll do
                    SaveBlockHeight(currentBlockIndex);
                }
                else
                {
                    ++currentBlockIndex;
                }
            }
            Console.WriteLine("Finished at block: " + currentBlockIndex);
        }

        private void BulkGetBlockHash(ref List<int> blockIndices)
        {
            // Set up RPC request
            string[] methods = new string[blockIndices.Count];
            JArray[] requestsArgs = new JArray[blockIndices.Count];
            for(int i = 0; i < blockIndices.Count; ++i)
            {
                JArray args = new JArray();
                args.Add(blockIndices[i]); // Only argument is the block index
                requestsArgs[i] = args;
                methods[i] = "getblockhash";
            }

            // Send request
            Console.WriteLine("Getting block hashes...");
            string response = m_rpcManager.CreateBatchRpcAndGetResponse(methods, requestsArgs, 0);

            // Get blocks based on retrieved block hashes
            JArray blockHashes = JsonConvert.DeserializeObject<JArray>(response);
            BulkGetBlock(blockHashes);

            Console.WriteLine("Completed block indices: " + blockIndices[0] + "-" + blockIndices[blockIndices.Count - 1]);
            Console.WriteLine("----------------------------------------");
            blockIndices.Clear();
        }

        private void BulkGetBlock(JArray hashes)
        {
            // Set up RPC request
            string[] methods = new string[hashes.Count];
            JArray[] requestsArgs = new JArray[hashes.Count];
            for (int i = 0; i < hashes.Count; ++i)
            {
                JArray args = new JArray();
                args.Add(hashes[i]["result"]); // Argument one is the block hash
                args.Add(2); // Argument two is '2', which represents result verbosity that returns full transactions
                requestsArgs[i] = args;
                methods[i] = "getblock";
            }

            // Send request and receive response
            Console.WriteLine("Getting blocks...");
            string response = m_rpcManager.CreateBatchRpcAndGetResponse(methods, requestsArgs, 0);
            JArray blocks = JsonConvert.DeserializeObject<JArray>(response);

            // Iterate through response blocks
            foreach(var block in blocks)
            {
                // Iterate through txes in block
                JArray txes = block["result"]["tx"] as JArray;
                bool skippedCoinbase = false;
                foreach(var tx in txes)
                {
                    // First tx is always the coinbase, which we don't need
                    if(!skippedCoinbase)
                    {
                        skippedCoinbase = true;
                        continue;
                    }

                    // Check whether tx is a Bisq trade deposit tx
                    if(IdentifyDepositTx(tx as JObject))
                    {
                        string txId = tx["txid"].ToString();
                        Console.WriteLine("Deposit Tx Identified: " + txId);

                        Trade trade = new Trade();
                        trade.Deposit = txId;
                        trade.InputOne = tx["vin"][0]["txid"].ToString();
                        trade.InputTwo = tx["vin"][1]["txid"].ToString();
                        trade.Block = block["result"]["hash"].ToString();
                        m_tradeMap.Add(txId, trade);
                    }

                    // Check whether tx is a Bisq trade payout tx
                    if (IdentifyPayoutTx(tx as JObject))
                    {
                        string txId = tx["txid"].ToString();
                        Console.WriteLine("Payout Tx Identified: " + txId);

                        string prevTxHash = tx["vin"][0]["txid"].ToString();
                        m_tradeMap[prevTxHash].Payout = txId;
                    }
                }
            }
            SaveTrades();
        }

        private bool IdentifyDepositTx(JObject tx)
        {
            if ((tx["vin"] as JArray).Count < 2 || (tx["vout"] as JArray).Count < 2)
            {
                return false; // If we don't return here, the following code will break, we need at least two inputs and outputs
            }

            string input1PrevOrdinal = tx["vin"][0]["vout"].ToString();
            string input2PrevOrdinal = tx["vin"][1]["vout"].ToString();
            string output1Type = tx["vout"][0]["scriptPubKey"]["type"].ToString();
            string output2Type = tx["vout"][1]["scriptPubKey"]["type"].ToString();
            string output1Asm = tx["vout"][0]["scriptPubKey"]["asm"].ToString();
            string output2Asm = tx["vout"][1]["scriptPubKey"]["asm"].ToString();

            if(input1PrevOrdinal == "1" && input2PrevOrdinal == "1" // Input 1 and 2 must have a previous output ordinal of "1", i.e., the 2nd output
                && output2Asm.Length == 74 && output2Asm.Substring(0, 10) == "OP_RETURN " // Second output must be an OP_RETURN of a specified length
                && output2Type == "nulldata")
            {
                // Pre-Segwit
                if (output1Asm.Length == 60 && output1Asm.Substring(0, 11) == "OP_HASH160 " && output1Asm.Substring(51, 9) == " OP_EQUAL" // First output must be a P2SH
                    && output1Type == "scripthash") // First output must be P2SH, essentially this is double checking
                {
                    return true;
                }
                // Post-Segwit (Implemented late November 2020)
                else if(output1Asm.Length == 66 && output1Asm.Substring(0, 2) == "0 " // First output must be a P2WSH
                    && output1Type == "witness_v0_scripthash") // First output must be P2WSH
                {
                    return true;
                }
            }
            return false;
        }

        private bool IdentifyPayoutTx(JObject tx)
        {
            string prevTxHash = tx["vin"][0]["txid"].ToString();
            JArray inputs = tx["vin"] as JArray;
            if (inputs.Count == 1 // Only 1 input
                && Convert.ToInt32(inputs[0]["vout"]) == 0 // Sole input must spend 1st ordinal output of previous tx
                && m_tradeMap.ContainsKey(prevTxHash)) // Must reference a deposit tx that has been identified
            {
                return true; //  We could just return the 'if' condition, see above method
            }
            return false;
        }

        private void SaveTrades()
        {
            JArray tradeArray = new JArray();
            foreach(KeyValuePair<string, Trade> kv in m_tradeMap)
            {
                tradeArray.Add(kv.Value.ToJObject());
            }
            StreamWriter streamWriter = File.CreateText(TRADE_PATH);
            string tradesJson = tradeArray.ToString(Formatting.None);
            streamWriter.Write(tradesJson);
            streamWriter.Close();
        }

        private void LoadTrades()
        {
            if(File.Exists(TRADE_PATH))
            {
                StreamReader reader = new StreamReader(TRADE_PATH);
                string json = reader.ReadToEnd();

                JArray trades = JsonConvert.DeserializeObject<JArray>(json);
                foreach(JToken tradeToken in trades)
                {
                    JObject tradeObject = tradeToken as JObject;
                    Trade trade = new Trade(tradeObject);
                    m_tradeMap.Add(trade.Deposit, trade);
                }
            }
        }

        private void SaveBlockHeight(int blockheight)
        {
            // Save current blockheight incase the program closes
            File.WriteAllText(BLOCKHEIGHT_PATH, blockheight.ToString());
        }

        private int GetLastBlockHeight()
        {
            // Loads last blockheight from file
            int blockheight = 0; // Default to zero
            if(File.Exists(BLOCKHEIGHT_PATH))
            {
                string contents = File.ReadAllText(BLOCKHEIGHT_PATH);
                int.TryParse(contents, out blockheight);
            }
            return blockheight;
        }
        #endregion

        #region Verbose_Trades
        public void GetFullTradeDetails()
        {
            if (File.Exists(VERBOSE_TRADE_PATH))
            {
                LoadVerboseTrades();
            }
            else
            {
                List<string> tradeDeposits = m_tradeMap.Keys.ToList();
                var trades = GetTrades(tradeDeposits); // Get full trade details for each trade from blockchain using RPC

                // Save trade details
                for (int i = 0; i < trades.Count; i += 4)
                {
                    JToken inputOne = trades[i];
                    JToken inputTwo = trades[i + 1];
                    JToken deposit = trades[i + 2];
                    JToken payout = trades[i + 3];
                    string depositHash = deposit["result"]["txid"].ToString();
                    m_tradeMap[depositHash].InputOneJson = inputOne["result"];
                    m_tradeMap[depositHash].InputTwoJson = inputTwo["result"];
                    m_tradeMap[depositHash].DepositJson = deposit["result"];
                    if (!payout["result"].HasValues)
                    {
                        Console.WriteLine("Payout not found for deposit " + depositHash);
                    }
                    else
                    {
                        m_tradeMap[depositHash].PayoutJson = payout["result"];
                    }
                }
                SaveVerboseTrades();
            }

            int blockLimitCounter = 0;
            foreach(var trade in m_tradeMap.Values)
            {
                if((long)trade.DepositJson["blocktime"] < 1612981296)
                {
                    ++blockLimitCounter;
                }
            }
            Console.WriteLine("Limited load count: " + blockLimitCounter);
        }

        private JArray GetTrades(List<string> depositHashes)
        {
            // Get full trade details by using batched RPC requests, batches of size 20 retrieve 80 txes, as each trade consists of 4 txes
            JArray trades = new JArray();
            int lastIndex = depositHashes.Count - 1;
            int batchSize = 20;
            List<string> batch = new List<string>();
            for (int i = 0; i < depositHashes.Count; ++i)
            {
                batch.Add(depositHashes[i]);
                if (i % batchSize == 0 || i == lastIndex)
                {
                    JArray tradeBatch = GetTradeBatch(batch, i);
                    batch.Clear();
                    foreach (JToken trade in tradeBatch)
                    {
                        trades.Add(trade);
                    }
                }
            }
            return trades;
        }

        private JArray GetTradeBatch(List<string> depositHashes, int counter)
        {
            // Set up RPC request
            List<string> methods = new List<string>();
            List<JArray> requestsArgs = new List<JArray>();
            foreach (string deposit in depositHashes)
            {
                Trade trade = m_tradeMap[deposit];
                string[] methodRange = { "getrawtransaction",
                    "getrawtransaction",
                    "getrawtransaction",
                    "getrawtransaction" };
                methods.AddRange(methodRange);
                JArray[] argsRange = { new JArray(trade.InputOne, true),
                    new JArray(trade.InputTwo, true),
                    new JArray(trade.Deposit, true),
                    new JArray(trade.Payout, true) };
                requestsArgs.AddRange(argsRange);
            }

            // Send request and receive response
            Console.WriteLine("(" + counter + ") Getting trade details...");
            string responseString = m_rpcManager.CreateBatchRpcAndGetResponse(methods.ToArray(), requestsArgs.ToArray(), 0);
            JArray responses = JsonConvert.DeserializeObject<JArray>(responseString); // Array of responses as we are using batched RPC requests
            return responses;
        }

        private void LoadVerboseTrades()
        {
            // Note, this method takes a long time due to large file size
            if (File.Exists(VERBOSE_TRADE_PATH))
            {
                StreamReader reader = new StreamReader(VERBOSE_TRADE_PATH);
                string json = reader.ReadToEnd();

                JArray trades = JsonConvert.DeserializeObject<JArray>(json);
                Console.WriteLine("Loaded " + trades.Count + " trades");
                foreach (JToken tradeToken in trades)
                {
                    JObject tradeObject = tradeToken as JObject;
                    string depositHash = tradeObject["deposit"]["txid"].ToString();
                    m_tradeMap[depositHash].InputOneJson = tradeObject["inputOne"];
                    m_tradeMap[depositHash].InputTwoJson = tradeObject["inputTwo"];
                    m_tradeMap[depositHash].DepositJson = tradeObject["deposit"];
                    m_tradeMap[depositHash].PayoutJson = tradeObject["payout"];
                }
            }
        }

        private void SaveVerboseTrades()
        {
            // Note, this method creates a very large file
            JArray tradeArray = new JArray();
            foreach (var trade in m_tradeMap)
            {
                tradeArray.Add(trade.Value.ToVerboseJObject());
            }
            StreamWriter streamWriter = File.CreateText(VERBOSE_TRADE_PATH);
            streamWriter.Write(tradeArray.ToString(Formatting.None));
            streamWriter.Close();
        }
        #endregion

        #region Segwit_Inputs
        private void GetSegwitInputs()
        {
            // If trade input tx uses SegWit, we can only identify the addresses used for the inputs by retrieving the txes that create the spent outputs
            if(File.Exists(SEGWIT_PATH))
            {
                LoadSegwitInputs();
            }
            else
            {
                List<string> segwitInputs = new List<string>();
                foreach (var trade in m_tradeMap.Values)
                {
                    //Iterate through first ordinal inputs of identified deposit txes
                    foreach (var txIn in trade.InputOneJson["vin"])
                    {
                        if (null != txIn["txinwitness"]) // Check if input uses SegWit
                        {
                            segwitInputs.Add(txIn["txid"].ToString());
                        }
                    }

                    // Iterate through second ordinal inputs of identified deposits txes
                    foreach (var txIn in trade.InputTwoJson["vin"])
                    {
                        if (null != txIn["txinwitness"])
                        {
                            segwitInputs.Add(txIn["txid"].ToString());
                        }
                    }
                }
                Console.WriteLine("Segwit input count: " + segwitInputs.Count);
                GetSegwitInputTransactions(segwitInputs); // Retrieve full tx details of SegWit inputs
                SaveSegwitInputs();
            }
        }

        private void GetSegwitInputTransactions(List<string> txIds)
        {
            // Get SegWit input txes using batch RPC requests
            int lastIndex = txIds.Count - 1;
            int batchSize = 80;
            List<string> batch = new List<string>();
            for (int i = 0; i < txIds.Count; ++i)
            {
                batch.Add(txIds[i]);
                if (i % batchSize == 0 || i == lastIndex)
                {
                    JArray txReturnBatch = GetTransactionBatch(batch, i);
                    batch.Clear();
                    foreach (JToken tx in txReturnBatch)
                    {
                        m_segwitInputs[tx["result"]["txid"].ToString()] = (tx["result"] as JObject); // Add tx to map/dict for reference during clustering
                    }
                }
            }
        }

        private JArray GetTransactionBatch(List<string> txBatch, int counter)
        {
            // Set up RPC request
            List<string> methods = new List<string>();
            List<JArray> requestsArgs = new List<JArray>();
            foreach (string tx in txBatch)
            {
                string method = "getrawtransaction";
                methods.Add(method);
                JArray args = new JArray(tx, true);
                requestsArgs.Add(args);
            }

            // Send RPC request and receive response
            Console.WriteLine("(" + counter + ") Getting segwit tx input details...");
            string responseString = m_rpcManager.CreateBatchRpcAndGetResponse(methods.ToArray(), requestsArgs.ToArray(), 0);
            JArray responses = JsonConvert.DeserializeObject<JArray>(responseString);
            return responses;
        }

        private void SaveSegwitInputs()
        {
            JArray txes = new JArray();
            foreach (var tx in m_segwitInputs.Values)
            {
                txes.Add(tx);
            }
            StreamWriter streamWriter = File.CreateText(SEGWIT_PATH);
            streamWriter.Write(txes.ToString(Formatting.None));
            streamWriter.Close();
        }

        private void LoadSegwitInputs()
        {
            if(File.Exists(SEGWIT_PATH))
            {
                StreamReader reader = new StreamReader(SEGWIT_PATH);
                string json = reader.ReadToEnd();

                JArray txes = JsonConvert.DeserializeObject<JArray>(json);
                foreach (JToken tx in txes)
                {
                    JObject txObject = tx as JObject;
                    string txId = txObject["txid"].ToString();
                    m_segwitInputs[txId] = txObject;
                }
            }
        }
        #endregion

        #endregion

        #region Clustering
        public void RunClustering()
        {
            // Get arbitrated trades
            LoadArbitratedTrades();

            // Get full trade details before clustering
            GetFullTradeDetails();

            // Get segwit inputs so we don't miss an address, we can't get addresses segwit tx inputs
            GetSegwitInputs();

            // Run our clustering heuristics
            ClusterByOrdinal();

            Dictionary<int, int> clusterMap = new Dictionary<int, int>();
            foreach(var kv in m_addressMap)
            {
                if(!clusterMap.ContainsKey(kv.Value))
                {
                    clusterMap.Add(kv.Value, kv.Value);
                }
            }
            Console.WriteLine("Address count: " + m_addressMap.Count);
            Console.WriteLine("Cluster count: " + clusterMap.Count);
        }

        private void ClusterByOrdinal()
        {
            foreach (Trade trade in m_tradeMap.Values)
            {
                List<string> sellerAddresses = new List<string>();
                List<string> buyerAddresses = new List<string>();

                // Skip this trade if we couldn't identify a payout, or if the trade was arbitrated
                if (!trade.PayoutJson.HasValues || m_arbitratedTrades.ContainsKey(trade.Deposit) || (long)trade.DepositJson["blocktime"] > (1612981296655 / 1000))
                {
                    continue;
                }

                // Payout Tx
                JArray payoutOutputs = trade.PayoutJson["vout"] as JArray;

                // Skip Payouts that have only one output, these are arbitrated using the new trade protocol
                // Also skip payouts with more than two outputs, these are irregular so we won't deal with them
                if (payoutOutputs.Count != 2)
                {
                    continue;
                }

                // The ordinals are always as follows, buyer first, seller second
                buyerAddresses.Add(payoutOutputs[0]["scriptPubKey"]["addresses"][0].ToString());
                sellerAddresses.Add(payoutOutputs[1]["scriptPubKey"]["addresses"][0].ToString());

                // Input One Tx
                List<string> inputOneCluster = ClusterInputAddresses(trade.InputOneJson);

                // Input Two Tx
                List<string> inputTwoCluster = ClusterInputAddresses(trade.InputTwoJson);

                // Deposit Tx
                JArray depositInputs = trade.DepositJson["vin"] as JArray;
                string buyerDepositInputTxid = depositInputs[0]["txid"].ToString();

                // Add payout addresses to input clusters
                if (buyerDepositInputTxid == trade.InputOneJson["txid"].ToString()) // Double checking that the first input of the deposit matches the first input tx
                {
                    buyerAddresses.AddRange(inputOneCluster);
                    sellerAddresses.AddRange(inputTwoCluster);
                }
                else
                {
                    continue; // If the ordering doesn't match up for some reason, clustering by ordinal won't work, so we continue
                }

                // Create or add to clusters
                ManageSingleCluster(buyerAddresses);
                ManageSingleCluster(sellerAddresses);
            }
        }

        private List<string> ClusterInputAddresses(JToken inputTx)
        {
            JArray inputs = inputTx["vin"] as JArray;
            JArray outputs = inputTx["vout"] as JArray;
            List<string> inputAddresses = new List<string>();
            inputAddresses.Add(outputs[1]["scriptPubKey"]["addresses"][0].ToString());

            if(outputs.Count == 3)
            {
                inputAddresses.Add(outputs[2]["scriptPubKey"]["addresses"][0].ToString()); // When there's a 3rd output, it's always a BTC change address
            }

            foreach (JToken input in inputs)
            {
                if (null == input["txinwitness"]) // Non-segwit input
                {
                    inputAddresses.Add(GetAddressFromScriptSig(input["scriptSig"]["hex"].ToString()));
                }
                else // Segwit input
                {
                    string inputPrevTxid = input["txid"].ToString();
                    int inputPrevOrdinal = int.Parse(input["vout"].ToString());
                    JObject tx = m_segwitInputs[inputPrevTxid];
                    string address = tx["vout"][inputPrevOrdinal]["scriptPubKey"]["addresses"][0].ToString();
                    inputAddresses.Add(address);
                }
            }
            return inputAddresses;
        }

        private void ManageSingleCluster(List<string> addresses)
        {
            List<int> mergeableClusters = new List<int>();
            foreach (string address in addresses)
            {
                if (m_addressMap.ContainsKey(address))
                {
                    mergeableClusters.Add(m_addressMap[address]);
                }
            }

            // Merge other clusters if necessary
            if (mergeableClusters.Count != 0)
            {
                foreach(int id in mergeableClusters)
                {
                    foreach(string address in m_clusters[id])
                    {
                        addresses.Add(address);
                    }
                }
            }

            // Create Cluster
            int newId = m_clusters.Count;
            m_clusters.Add(addresses.Distinct().ToList());
            foreach (string address in addresses)
            {
                m_addressMap[address] = newId;
            }
        }
        #endregion

        #region V1_Arbitration_Check
        public void TradeMapToArbitrationFile()
        {
            if (File.Exists(VERBOSE_TRADE_PATH))
            {
                // Get verbose trades
                string json = string.Empty;
                using (StreamReader reader = new StreamReader(VERBOSE_TRADE_PATH))
                {
                    json = reader.ReadToEnd();
                }
                JArray trades = JsonConvert.DeserializeObject<JArray>(json);

                // Prepare data for btcdeb
                using (StreamWriter writer = File.CreateText("BtcdebReadyFile.txt")) // Overwrites file if already exists
                {
                    foreach (var trade in trades)
                    {   
                        if (trade["payout"].HasValues)
                        {
                            string depositHex = trade["deposit"]["hex"].ToString();
                            string payoutHex = trade["payout"]["hex"].ToString();
                            bool isOldProtocol = CheckArbitrationProtocol(trade); // We only need to check trades using old arbitration protocol
                            if (isOldProtocol)
                            {
                                writer.WriteLine(trade["deposit"]["txid"].ToString());
                                writer.WriteLine(string.Format("btcdeb --tx={0} --txin={1}", payoutHex, depositHex)); // Write the btcdeb command
                            }
                        }
                    }
                }
            }
        }

        private bool CheckArbitrationProtocol(JToken trade)
        {
            bool isOldProtocol = true;
            string payoutAsm = trade["payout"]["vin"][0]["scriptSig"]["asm"].ToString(); // We're getting the asm of input one, the only input
            int redeemScriptStartIndex = payoutAsm.IndexOf("[ALL] 5221"); // This substring indicates the start of the redeem script
            if(payoutAsm.Length == 0)
            {
                isOldProtocol = false; // This trade uses segwit as it does not have any scriptSig data, meaning it uses the new protocol
            }
            else
            {
                int redeemScriptLength = payoutAsm.Substring(redeemScriptStartIndex).Length;
                if(redeemScriptLength < 216) // Length of a 3-pk redeem script is 216
                {
                    isOldProtocol = false; // New protocol is a 2-pk redeem script, length 148
                }
            }
            return isOldProtocol; // This trade uses the old protocol
        }

        private void LoadArbitratedTrades()
        {
            if (File.Exists(ARBITRATION_PATH))
            {
                StreamReader reader = new StreamReader(ARBITRATION_PATH);
                string line = string.Empty;
                while((line = reader.ReadLine()) != null)
                {
                    if(string.Empty != line)
                    {
                        m_arbitratedTrades.Add(line, line);
                    }
                }
            }
        }
        #endregion

        #region Validation_And_Analysis
        public void ValidateTrades()
        {
            GetFullTradeDetails();
            ValidateByTradeStatistics2();
            ValidateByTradeStatistics3();
        }

        private void ValidateByTradeStatistics2()
        {
            Console.WriteLine("\r\n---TradeStatistics2 Analysis---");

            // Retrieve TradeStatistics2 statistics
            DataTable tableCount = m_sqlManager.ExecuteQueryScript("TradeStatistics2/TableCount.sql");
            Console.WriteLine("TradeStatictics2Store trade count: " + tableCount.Rows[0]["TotalTrades"]);

            DataTable latestDates = m_sqlManager.ExecuteQueryScript("TradeStatistics2/LatestDates.sql");
            long latestTradeDate = (long)latestDates.Rows[0]["LastTradeDate"];
            long latestOfferDate = (long)latestDates.Rows[0]["LastOfferDate"];

            DataTable cleanedTable = m_sqlManager.ExecuteQueryScript("TradeStatistics2/CleanedTable.sql");
            Console.WriteLine("Cleaned TradeStatistics2Store trade count: " + cleanedTable.Rows.Count);

            // Compare to verbose trades, checking which trades we missed
            Dictionary<string, DataRow> tradeStatistics2Dict = new Dictionary<string, DataRow>();
            List<string> missedDepositTxes = new List<string>();
            foreach(DataRow row in cleanedTable.Rows)
            {
                string depositTxid = row["DepositTxId"].ToString();
                tradeStatistics2Dict[depositTxid] = row; // Set up dict for easy use later
                if(!m_tradeMap.ContainsKey(depositTxid))
                {
                    missedDepositTxes.Add(depositTxid);
                }
            }
            Console.WriteLine("Identified matches: " + (cleanedTable.Rows.Count - missedDepositTxes.Count));
            Console.WriteLine("Missed TradeStatistics2 trades: " + missedDepositTxes.Count);
            CheckNonExistantTxes(missedDepositTxes);

            // Check trades we have that aren't in TradeStatistics2, trades they missed
            int reverseMatchCounter = 0;
            int reverseMissCounter = 0;
            foreach(var kv in m_tradeMap)
            {
                Trade identifiedTrade = kv.Value;
                long tradeDepositBlocktime = (long)identifiedTrade.DepositJson["blocktime"];
                if(tradeDepositBlocktime < (latestTradeDate / 1000))
                {
                    if (tradeStatistics2Dict.ContainsKey(kv.Key))
                    {
                        ++reverseMatchCounter;
                    }
                    else
                    {
                        ++reverseMissCounter;
                    }
                }
            }
            Console.WriteLine("Reverse identified matches (should be same/similar as above): " + reverseMatchCounter);
            Console.WriteLine("Missed indentified (overshoot) trades: " + reverseMissCounter);
        }

        private void ValidateByTradeStatistics3()
        {
            Console.WriteLine("\r\n---TradeStatistics3 Analysis---");

            // Retrieve TradeStatistics3 statistics
            DataTable count = m_sqlManager.ExecuteQueryScript("TradeStatistics3/Count.sql");
            Console.WriteLine("TradeStatictics3Store trade count: " + count.Rows[0]["TotalTrades"]);

            DataTable cleanedCount = m_sqlManager.ExecuteQueryScript("TradeStatistics3/CleanedCount.sql");
            Console.WriteLine("Cleaned TradeStatictics3Store trade count: " + cleanedCount.Rows[0]["TotalTrades"]);

            List<SqlParameter> sqlParams = new List<SqlParameter>();
            sqlParams.Add(new SqlParameter("@DateArg", 1612981296655)); // Last blocktime we're looking for
            DataTable cleanedDateLimitedCount = m_sqlManager.ExecuteQueryScript("TradeStatistics3/CleanedDateLimitedCount.sql", sqlParams);
            Console.WriteLine("Cleaned date limited TradeStatictics3Store trade count: " + cleanedDateLimitedCount.Rows[0]["TotalTrades"]);

            // Compare to verbose trades
            int tradeCounter = 0;
            foreach(var trade in m_tradeMap.Values)
            {
                if((long)trade.DepositJson["blocktime"] < (1612981296655 / 1000))
                {
                    ++tradeCounter;
                }
            }
            Console.WriteLine("Date limited retrieved trade count: " + tradeCounter);
        }

        private void CheckNonExistantTxes(List<string> txIds)
        {
            string[] methods = new string[txIds.Count];
            JArray[] requestsArgs = new JArray[txIds.Count];
            for (int i = 0; i < txIds.Count; ++i)
            {
                JArray args = new JArray();
                args.Add(txIds[i]);
                args.Add(true);
                requestsArgs[i] = args;
                methods[i] = "getrawtransaction";
            }
            string responseString = m_rpcManager.CreateBatchRpcAndGetResponse(methods, requestsArgs, 0);
            JArray responses = JsonConvert.DeserializeObject<JArray>(responseString);
            int counter = 0;
            foreach (var resp in responses)
            {
                JObject obj = resp as JObject;
                JToken resultField = obj["result"];
                if (resultField.HasValues)
                {
                    ++counter;
                    Console.WriteLine("(" + counter + ") Deposit tx exists: " + resultField["txid"].ToString());
                }
            }
        }
        #endregion

        #region Util
        private string GetAddressFromScriptSig(string scriptSig)
        {
            // Get the public key from the scriptsig, get the address form that
            return GetAddressFromPublicKey(scriptSig.Substring(scriptSig.Length - 66, 66));
        }

        private string GetAddressFromPublicKey(string publicKey)
        {
            SHA256 sha256 = SHA256.Create();
            RIPEMD160 ripemd160 = RIPEMD160.Create();

            // Stage one hash algorithm - 0 byte + RIPEMD160(SHA256(publicKey))
            List<byte> stageOneHashList = new List<byte>();
            stageOneHashList.Add(0); // The first stage hash is prepended with a zero byte
            stageOneHashList.AddRange(ripemd160.ComputeHash(sha256.ComputeHash(Util.HexStringToByteArray(publicKey)))); // Body of first stage hash
            byte[] stageOneHash = stageOneHashList.ToArray();

            // Stage 2 hash algorithm - SHA256(SHA256(stageOneHash))
            byte[] stageTwoHash = sha256.ComputeHash(sha256.ComputeHash(stageOneHash));

            // Address bytes algorithm - stageOneHash + last 4 bytes of stageTwoHash
            List<byte> addressBytes = new List<byte>();
            addressBytes.AddRange(stageOneHash);
            addressBytes.AddRange(stageTwoHash.Take(4));

            // Convert address to Base58
            string base58Address = Base58Check.Base58CheckEncoding.EncodePlain(addressBytes.ToArray());
            return base58Address;
        }
        #endregion
    }
}
