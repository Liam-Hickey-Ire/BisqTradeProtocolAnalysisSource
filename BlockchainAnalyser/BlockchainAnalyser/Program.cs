using System;
using System.Net;
using System.Configuration;

namespace BlockchainAnalyser
{
    class Program
    {
        // Appsettings
        private static string serverUrl = string.Empty;
        private static string serverUsername = string.Empty;
        private static string serverPassword = string.Empty;
        private static string tradeOutputFilepath = string.Empty;
        private static string verboseTradeOutputFilepath = string.Empty;
        private static string blockheightFilepath = string.Empty;
        private static string depositListPath = string.Empty;
        private static string arbitrationListPath = string.Empty;
        private static string segwitListPath = string.Empty;
        private static string sqlConnectionString = string.Empty;
        private static string sqlFilepath = string.Empty;
        private static int firstTradeBlockHeight = 0;
        private static int connectionLimit = 0;

        private static BlockchainAnalyser blockchainAnalyser;

        static void Main(string[] args)
        {
            Console.WriteLine("Blockchain Analyser (Bitcoin Blockchain)");

            // Get Appsettings
            serverUrl = ConfigurationManager.AppSettings["server-url"];
            serverUsername = ConfigurationManager.AppSettings["server-username"];
            serverPassword = ConfigurationManager.AppSettings["server-password"];
            tradeOutputFilepath = ConfigurationManager.AppSettings["trade-output-filepath"];
            verboseTradeOutputFilepath = ConfigurationManager.AppSettings["verbose-trade-output-filepath"];
            blockheightFilepath = ConfigurationManager.AppSettings["blockheight-filepath"];
            depositListPath = ConfigurationManager.AppSettings["deposit-filepath"];
            arbitrationListPath = ConfigurationManager.AppSettings["arbitration-tx-filepath"];
            segwitListPath = ConfigurationManager.AppSettings["segwit-tx-filepath"];
            sqlConnectionString = ConfigurationManager.AppSettings["sql-connection-string"];
            sqlFilepath = ConfigurationManager.AppSettings["sql-filepath"];

            // Parse Non-Strings
            if (!Int32.TryParse(ConfigurationManager.AppSettings["first-trade-block-height"], out firstTradeBlockHeight)
                || !Int32.TryParse(ConfigurationManager.AppSettings["connection-limit"], out connectionLimit))
            {
                Console.WriteLine("Error: Failed to parse some appsettings");
            }
            else
            {
                ServicePointManager.DefaultConnectionLimit = connectionLimit;
                blockchainAnalyser = new BlockchainAnalyser(serverUsername, 
                    serverPassword, 
                    serverUrl, 
                    tradeOutputFilepath, 
                    verboseTradeOutputFilepath, 
                    blockheightFilepath,
                    arbitrationListPath,
                    segwitListPath,
                    sqlConnectionString,
                    sqlFilepath);

                //blockchainAnalyser.GetBisqDepositTxes(firstTradeBlockHeight, connectionLimit);
                //blockchainAnalyser.GetFullTradeDetails();
                //blockchainAnalyser.TradeMapToArbitrationFile();
                blockchainAnalyser.RunClustering();
                //blockchainAnalyser.ValidateTrades();
            }

            Console.WriteLine("Press 'c' to close application...");
            while (Console.ReadKey(true).Key != ConsoleKey.C) { } // Using 'c' key as 'esc' may already be pressed at this point
        }
    }
}
