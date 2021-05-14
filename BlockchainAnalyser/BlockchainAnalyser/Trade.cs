using System;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlockchainAnalyser
{
    class Trade
    {
        #region Members
        private string m_inputOneTxId = string.Empty;
        private string m_inputTwoTxId = string.Empty;
        private string m_depositTxId = string.Empty;
        private string m_payoutTxId = string.Empty;
        private string m_blockHash = string.Empty;
        private JToken m_inputOneTxJson = null;
        private JToken m_inputTwoTxJson = null;
        private JToken m_depositTxJson = null;
        private JToken m_payoutTxJson = null;
        #endregion

        #region Methods
        public Trade() {}

        public Trade(string inputOne, string inputTwo, string deposit, string payout, string block)
        {
            m_inputOneTxId = inputOne;
            m_inputTwoTxId = inputTwo;
            m_depositTxId = deposit;
            m_payoutTxId = payout;
            m_blockHash = block;
        }

        public Trade(string jsonString)
        {
            JObject tradeObj = JsonConvert.DeserializeObject<JObject>(jsonString);
            ReadTradeJObject(tradeObj);
        }

        public Trade(JObject tradeObj)
        {
            ReadTradeJObject(tradeObj);
        }

        private void ReadTradeJObject(JObject tradeObj)
        {
            m_inputOneTxId = tradeObj["inputOne"].ToString();
            m_inputTwoTxId = tradeObj["inputTwo"].ToString();
            m_depositTxId = tradeObj["deposit"].ToString();
            m_payoutTxId = tradeObj["payout"].ToString();
            m_blockHash = tradeObj["block"].ToString();
        }

        public JObject ToJObject()
        {
            JObject tradeObj = new JObject();
            tradeObj["inputOne"] = m_inputOneTxId;
            tradeObj["inputTwo"] = m_inputTwoTxId;
            tradeObj["deposit"] = m_depositTxId;
            tradeObj["payout"] = m_payoutTxId;
            tradeObj["block"] = m_blockHash;
            return tradeObj;
        }

        public JObject ToVerboseJObject()
        {
            JObject tradeObj = new JObject();
            tradeObj["inputOne"] = m_inputOneTxJson;
            tradeObj["inputTwo"] = m_inputTwoTxJson;
            tradeObj["deposit"] = m_depositTxJson;
            tradeObj["payout"] = m_payoutTxJson;
            return tradeObj;
        }

        public string ToJsonString(bool verbose)
        {
            JObject tradeObj = verbose ? ToVerboseJObject() : ToJObject();
            return tradeObj.ToString(Formatting.None);
        }
        #endregion

        #region Properties
        public string InputOne
        {
            get { return m_inputOneTxId; }
            set { m_inputOneTxId = value; }
        }

        public string InputTwo
        {
            get { return m_inputTwoTxId; }
            set { m_inputTwoTxId = value; }
        }

        public string Deposit
        {
            get { return m_depositTxId; }
            set { m_depositTxId = value; }
        }

        public string Payout
        {
            get { return m_payoutTxId; }
            set { m_payoutTxId = value; }
        }

        public string Block
        {
            get { return m_blockHash; }
            set { m_blockHash = value; }
        }

        public JToken InputOneJson
        {
            get { return m_inputOneTxJson; }
            set { m_inputOneTxJson = value; }
        }

        public JToken InputTwoJson
        {
            get { return m_inputTwoTxJson; }
            set { m_inputTwoTxJson = value; }
        }

        public JToken DepositJson
        {
            get { return m_depositTxJson; }
            set { m_depositTxJson = value; }
        }

        public JToken PayoutJson
        {
            get { return m_payoutTxJson; }
            set { m_payoutTxJson = value; }
        }
        #endregion
    }
}
