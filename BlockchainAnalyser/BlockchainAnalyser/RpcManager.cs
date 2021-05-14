using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlockchainAnalyser
{
    class RpcManager
    {
        HttpManager m_httpManager;

        public RpcManager(string username, string password, string url)
        {
            m_httpManager = new HttpManager(username, password, url);
        }

        #region Procedures
        public JObject GetBlockCount(int id = 0)
        {
            string response = m_httpManager.CreateRequestAndGetResponse("getblockcount");
            return JsonConvert.DeserializeObject<JObject>(response);
        }

        public JObject GetBlockHash(int index, int id = 0)
        {
            JArray requestArgs = new JArray();
            requestArgs.Add(index);
            string response = m_httpManager.CreateRequestAndGetResponse("getblockhash", requestArgs);
            return JsonConvert.DeserializeObject<JObject>(response);
        }

        public JObject GetBlock(string blockHash, int id = 0)
        {
            JArray requestArgs = new JArray();
            requestArgs.Add(blockHash);
            string response = m_httpManager.CreateRequestAndGetResponse("getblock", requestArgs, id);
            return JsonConvert.DeserializeObject<JObject>(response);
        }

        public JObject GetRawTransaction(string txHash, int id = 0)
        {
            JArray requestArgs = new JArray();
            requestArgs.Add(txHash);
            string response = m_httpManager.CreateRequestAndGetResponse("getrawtransaction", requestArgs, id);
            return JsonConvert.DeserializeObject<JObject>(response);
        }

        public JObject DecodeRawTransaction(string rawTx, int id = 0)
        {
            JArray requestArgs = new JArray();
            requestArgs.Add(rawTx);
            string response = m_httpManager.CreateRequestAndGetResponse("decoderawtransaction", requestArgs, id);
            return JsonConvert.DeserializeObject<JObject>(response);
        }
        #endregion

        #region Composite_Procedures
        public JObject GetBlockByIndex(int index, int id = 0)
        {
            JObject blockHashResponse = GetBlockHash(index);
            return GetBlock(blockHashResponse["result"].ToString());
        }

        public JObject GetNonWalletTransaction(string txHash, int id = 0)
        {
            JObject rawTxResponse = GetRawTransaction(txHash);
            return DecodeRawTransaction(rawTxResponse["result"].ToString());
        }
        #endregion

        #region Manual_Procedure_Calls
        public HttpWebRequest CreateRpc(string method, JArray requestArgs = null, int id = 0)
        {
            return m_httpManager.CreateRequest(method, requestArgs, id);
        }

        public HttpWebRequest CreateBatchRpc(string [] methods, JArray [] requestsArgs, int id = 0)
        {
            return m_httpManager.CreateBatchRequest(methods, requestsArgs, id);
        }

        public string CreateRpcAndGetResponse(string method, JArray requestArgs = null, int id = 0)
        {
            return m_httpManager.CreateRequestAndGetResponse(method, requestArgs, id);
        }

        public string CreateBatchRpcAndGetResponse(string[] methods, JArray[] requestsArgs, int id = 0)
        {
            return m_httpManager.CreateBatchRequestAndGetResponse(methods, requestsArgs, id);
        }
        #endregion
    }
}
