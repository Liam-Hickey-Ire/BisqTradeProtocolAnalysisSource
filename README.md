# BisqTradeProtocolAnalysisSource

Source code used for the analysis of the Bisq trade protocol from a blockchain analysis perspective.

## Description

This project performs the following analyses of the Bisq trade protocol:
- Retrieval of Bisq trades from the Bitcoin blockchain.
- Discerning arbitrated Bisq trades.
- Clustering Bitcoin addresses found in Bisq trades.
- Validating results using Bisq peer-to-peer data.

In order to retrieve Bisq trades from the Bitcoin blockchain, this project uses Bitcoin's JSON-RPC interface. This means that in order to retrieve trades from the Bitcoin blockchain, Bitcoin Core must be installed and the Bitcoin daemon must be running.

Additionally, this project relies on Bisq peer-to-peer data for the validation of retrieved trades. This feature relies on a SQL database created for the analysis of the Bisq DAO which contains this peer-to-peer data. See https://github.com/Liam-Hickey-Ire/BisqDAOAnalysisSource to recreate the required SQL database.

Finally, the identification of arbitrated trades requires btcdeb to be installed, see the setup section for more information.

## Setup

#### Bitcoin Core

When syncing Bitcoin Core is syncing the Bitcoin blockchain, use the following lines in your bitcoin.conf file:

- `txindex=1` - Allows for the retrieval of transaction data for any transaction on the Bitcoin blockchain.
- `rpcuser=user_name` - Username to be used for the JSON-RPC interface.
- `rpcpass=pass_word` - Password to be used for the JSON-RPC interface.

#### Peer-to-peer Data

This project queries a SQL Database in order to retrieve Bisq peer-to-peer data. The SQL database in question is created by another project analysing the Bisq DAO. As such, creation of these databases requires the installation of SQL and for the Bisq DAO analysis project (https://github.com/Liam-Hickey-Ire/BisqDAOAnalysisSource) to be executed. For a more detailed walk-through of how to run Bisq DAO analysis project, see the README.md for that project.

#### Btcdeb

In order to identify and remove arbitrated Bisq trades, this project relies on btcdeb, a Bitcoin script debugging tool, see https://github.com/bitcoin-core/btcdeb. To install btcdeb, see the project's README.md. Note that this project isn't compatible with Windows, this means that WSL (Windows Subsystem Linux) is required to install and run btcdeb on Windows. Once WSL is installed, it can be run using the `wsl` command on Powershell. From there, simply follow the normal btcdeb installation steps for Linux. Once btcdeb has been installed on Windows, the `wsl` command is required to run btcdeb.

#### NuGet Packages Used

- Base58Check (https://www.nuget.org/packages/Base58Check)
- Newtonsoft.Json (https://www.nuget.org/packages/Newtonsoft.Json)
- RIPEMD160 (https://www.nuget.org/packages/RIPEMD160/1.0.1)
- System.Configuration.ConfigurationManager (https://www.nuget.org/packages/System.Configuration.ConfigurationManager)
- System.Data.SqlClient (https://www.nuget.org/packages/System.Data.SqlClient)

## Execution

The project has five basic functions:
- Retrieve Bisq trades.
- Retrieve Bisq trades with high verbosity.
- Create file for identifying arbitrated trades using a script.
- Validate retrieved Bisq trades.
- Run trade protocol-specific address clustering heuristic.

These functions are selected using appsettings (`true`/`false`).
