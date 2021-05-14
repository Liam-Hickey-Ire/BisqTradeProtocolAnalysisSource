using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlockchainAnalyser
{
    class HttpManager
    {
        private readonly string SERVER_USERNAME = string.Empty;
        private readonly string SERVER_PASSWORD = string.Empty;
        private readonly string SERVER_URL = string.Empty;

        public HttpManager(string username, string password, string url)
        {
            SERVER_USERNAME = username;
            SERVER_PASSWORD = password;
            SERVER_URL = url;
        }

        public HttpWebRequest CreateRequest(string method, JArray requestArgs = null, int id = 0)
        {
            // Create Web Request
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(SERVER_URL);
            webRequest.Credentials = new NetworkCredential(SERVER_USERNAME, SERVER_PASSWORD);
            webRequest.ContentType = "application/json-rpc";
            webRequest.Method = "POST";

            // Create JObject for Request
            JObject jObject = new JObject();
            jObject["jsonrpc"] = "1.0";
            jObject["id"] = id;
            jObject["method"] = method;
            jObject["params"] = requestArgs;

            // Convert JSON to Bytes for Request
            string serializedJson = JsonConvert.SerializeObject(jObject);
            byte[] byteArray = Encoding.UTF8.GetBytes(serializedJson);
            webRequest.ContentLength = byteArray.Length;
            try
            {
                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }
            }
            catch (WebException webException)
            {
                Console.WriteLine(string.Format("Web Exception Occurred, Message: {0}, Inner Excption Message: {1}",
                    webException.Message,
                    webException.InnerException.Message));
                throw;
            }

            return webRequest;
        }

        public HttpWebRequest CreateBatchRequest(string [] methods, JArray [] requestsArgs, int id = 0)
        {
            // Create Web Request
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(SERVER_URL);
            webRequest.Credentials = new NetworkCredential(SERVER_USERNAME, SERVER_PASSWORD);
            webRequest.ContentType = "application/json-rpc";
            webRequest.Method = "POST";

            // Create Batched RPCs
            JArray requestArgsArray = new JArray();
            for (int i = 0; i < methods.Length; ++i)
            {
                JObject singleRequestArgs = new JObject();
                singleRequestArgs["jsonrpc"] = "2.0";
                singleRequestArgs["id"] = id;
                singleRequestArgs["method"] = methods[i];
                singleRequestArgs["params"] = requestsArgs[i];
                requestArgsArray.Add(singleRequestArgs);
            }

            // Convert JSON to Bytes for Request
            string serializedJson = JsonConvert.SerializeObject(requestArgsArray);
            byte[] byteArray = Encoding.UTF8.GetBytes(serializedJson);
            webRequest.ContentLength = byteArray.Length;
            try
            {
                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }
            }
            catch (WebException webException)
            {
                Console.WriteLine(string.Format("Web Exception Occurred, Message: {0}, Inner Excption Message: {1}",
                    webException.Message,
                    webException.InnerException.Message));
                throw;
            }

            return webRequest;
        }

        public string CreateRequestAndGetResponse(string method, JArray requestArgs = null, int id = 0)
        {
            HttpWebRequest webRequest = CreateRequest(method, requestArgs, id);
            try
            {
                using (WebResponse webResponse = webRequest.GetResponse())
                using (Stream ResponseStream = webResponse.GetResponseStream())
                using (StreamReader streamReader = new StreamReader(ResponseStream))
                {
                    return streamReader.ReadToEnd();
                }
            }
            catch (WebException webException)
            {
                Console.WriteLine(string.Format("Web Exception Occurred, Message: {0}",
                    webException.Message));
                throw;
            }
        }

        public string CreateBatchRequestAndGetResponse(string[] methods, JArray[] requestsArgs, int id = 0)
        {
            HttpWebRequest webRequest = CreateBatchRequest(methods, requestsArgs, id);
            try
            {
                using (WebResponse webResponse = webRequest.GetResponse())
                using (Stream ResponseStream = webResponse.GetResponseStream())
                using (StreamReader streamReader = new StreamReader(ResponseStream))
                {
                    return streamReader.ReadToEnd();
                }
            }
            catch (WebException webException)
            {
                Console.WriteLine(string.Format("Web Exception Occurred, Message: {0}",
                    webException.Message));
                throw;
            }
        }
    }
}
