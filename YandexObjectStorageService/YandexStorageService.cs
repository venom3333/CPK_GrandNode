using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

namespace YandexObjectStorageService
{
    public class YandexStorageService : IYandexStorageService
    {
        private readonly string _protocol;
        private readonly string _bucketName;
        private readonly string _region;
        private readonly string _endpoint;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _hostName;
        private readonly string _service;
        private readonly string _supPath;
        private readonly int _presignedUrlExpirationInHours;

        public YandexStorageService(IOptions<YandexStorageOptions> options)
        {
            var yandexStorageOptions = options.Value;

            _protocol = yandexStorageOptions.Protocol;
            _bucketName = yandexStorageOptions.BucketName;
            _region = yandexStorageOptions.Region;
            _endpoint = yandexStorageOptions.Endpoint;
            _accessKey = yandexStorageOptions.AccessKey;
            _secretKey = yandexStorageOptions.SecretKey;
            _hostName = yandexStorageOptions.HostName;
            _service = yandexStorageOptions.Service;
            _supPath = yandexStorageOptions.SubPath;
            _presignedUrlExpirationInHours = yandexStorageOptions.PresignedUrlExpirationInHours;
        }

        public YandexStorageService(YandexStorageOptions options)
        {
            _protocol = options.Protocol;
            _bucketName = options.BucketName;
            _region = options.Region;
            _endpoint = options.Endpoint;
            _accessKey = options.AccessKey;
            _secretKey = options.SecretKey;
            _hostName = options.HostName;
            _service = options.Service;
            _supPath = options.SubPath;
            _presignedUrlExpirationInHours = options.PresignedUrlExpirationInHours;
        }

        private string[] AddStandardHeadersToRequest(HttpRequestMessage requestMessage, string dateTimeStamp, string hash)
        {
            requestMessage.Headers.Add("Host", _endpoint);
            requestMessage.Headers.Add("X-Amz-Content-Sha256", hash);
            requestMessage.Headers.Add("X-Amz-Date", $"{dateTimeStamp}");

            string[] headers = { "host", "x-amz-content-sha256", "x-amz-date" };
            return headers;
        }

        private async Task<HttpRequestMessage> PrepareGetRequestAsync()
        {

            AwsV4SignatureCalculator calculator = new AwsV4SignatureCalculator(_secretKey, _service, _region);
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_protocol}://{_endpoint}/{_bucketName}"));

            DateTime now = DateTime.UtcNow;
            var dateTimeStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateTimeFormat, CultureInfo.InvariantCulture);
            var dateStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateFormat, CultureInfo.InvariantCulture);

            var hash = await AwsV4SignatureCalculator.GetPayloadHashAsync(requestMessage);

            string[] headers = AddStandardHeadersToRequest(requestMessage, dateTimeStamp, hash);

            string signature = await calculator.CalculateSignatureAsync(requestMessage, headers, now);
            string authHeader = GetAuthHeader(dateStamp, headers, signature);

            requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader);

            return requestMessage;
        }

       
        private async Task<HttpRequestMessage> PrepareGetRequestAsync(string filename)
        {
            AwsV4SignatureCalculator calculator = new AwsV4SignatureCalculator(_secretKey, _service, _region);
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_protocol}://{_endpoint}/{_bucketName}/{_supPath}/{filename}"));

            DateTime now = DateTime.UtcNow;
            var dateTimeStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateTimeFormat, CultureInfo.InvariantCulture);
            var dateStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateFormat, CultureInfo.InvariantCulture);

            var hash = await AwsV4SignatureCalculator.GetPayloadHashAsync(requestMessage);

            string[] headers = AddStandardHeadersToRequest(requestMessage, dateTimeStamp, hash);

            string signature = await calculator.CalculateSignatureAsync(requestMessage, headers, now);
            string authHeader = GetAuthHeader(dateStamp, headers, signature);

            requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader);

            return requestMessage;
        }

        private async Task<HttpRequestMessage> PreparePutRequestAsync(Stream stream, string filename)
        {
            AwsV4SignatureCalculator calculator = new AwsV4SignatureCalculator(_secretKey, _service, _region);
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Put, new Uri($"{_protocol}://{_endpoint}/{_bucketName}/{_supPath}/{filename}"));

            DateTime now = DateTime.UtcNow;
            var dateTimeStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateTimeFormat, CultureInfo.InvariantCulture);
            var dateStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateFormat, CultureInfo.InvariantCulture);

            StreamContent content = new StreamContent(stream);

            requestMessage.Content = content;

            var hash = await AwsV4SignatureCalculator.GetPayloadHashAsync(requestMessage);

            string[] headers = AddStandardHeadersToRequest(requestMessage, dateTimeStamp, hash);

            string signature = await calculator.CalculateSignatureAsync(requestMessage, headers, now);
            string authHeader = GetAuthHeader(dateStamp, headers, signature);

            requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader);

            return requestMessage;
        }

        private async Task<HttpRequestMessage> PreparePutRequestAsync(byte[] byteArr, string filename)
        {
            AwsV4SignatureCalculator calculator = new AwsV4SignatureCalculator(_secretKey, _service, _region);
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Put, new Uri($"{_protocol}://{_endpoint}/{_bucketName}/{_supPath}/{filename}"));

            DateTime now = DateTime.UtcNow;
            var dateTimeStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateTimeFormat, CultureInfo.InvariantCulture);
            var dateStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateFormat, CultureInfo.InvariantCulture);

            ByteArrayContent content = new ByteArrayContent(byteArr);

            requestMessage.Content = content;

            var hash = await AwsV4SignatureCalculator.GetPayloadHashAsync(requestMessage);

            string[] headers = AddStandardHeadersToRequest(requestMessage, dateTimeStamp, hash);

            string signature = await calculator.CalculateSignatureAsync(requestMessage, headers, now);
            string authHeader = GetAuthHeader(dateStamp, headers, signature);

            requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader);

            return requestMessage;
        }

        private async Task<HttpRequestMessage> PrepareDeleteRequestAsync(string filename)
        {
            AwsV4SignatureCalculator calculator = new AwsV4SignatureCalculator(_secretKey, _service, _region);
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Delete, new Uri($"{_protocol}://{_endpoint}/{_bucketName}/{_supPath}/{filename}"));

            DateTime now = DateTime.UtcNow;
            var dateTimeStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateTimeFormat, CultureInfo.InvariantCulture);
            var dateStamp = now.ToString(AwsV4SignatureCalculator.Iso8601DateFormat, CultureInfo.InvariantCulture);

            var hash = await AwsV4SignatureCalculator.GetPayloadHashAsync(requestMessage);

            string[] headers = AddStandardHeadersToRequest(requestMessage, dateTimeStamp, hash);

            string signature = await calculator.CalculateSignatureAsync(requestMessage, headers, now);
            string authHeader = GetAuthHeader(dateStamp, headers, signature);

            requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader);

            return requestMessage;
        }

        private string GetAuthHeader(string dateStamp, string[] headers, string signature)
        {
            return $"AWS4-HMAC-SHA256 Credential={_accessKey}/{dateStamp}/{_region}/{_service}/aws4_request, SignedHeaders={string.Join(";", headers)}, Signature={signature}";
        }


        /// <summary>
        /// Test connection to storage
        /// </summary>
        /// <returns>Retruns true if all credentials correct</returns>
        public async Task<bool> TryGetAsync()
        {
            var requestMessage = await PrepareGetRequestAsync();

            using (HttpClient client = new HttpClient())
            {
                var response = await client.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Returns presigned URL with defined expire time
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public string GetPresignedUrl(string filename, TimeSpan expirationTime = default)
        {
            if (expirationTime == default)
            {
                expirationTime = TimeSpan.FromHours(_presignedUrlExpirationInHours);
            }

            var formatedPath = FormatePath(filename);

            var expiresOn = DateTime.UtcNow + expirationTime;
            var period = Convert.ToInt64((expiresOn.ToUniversalTime() - DateTime.UtcNow).TotalSeconds);

            var requestDateTime = DateTime.UtcNow;
            var dateTimeStamp = requestDateTime.ToString(AwsV4SignatureCalculator.Iso8601DateTimeFormat, CultureInfo.InvariantCulture);
            var requestDateTimeStamp = requestDateTime.ToString(AwsV4SignatureCalculator.Iso8601DateFormat, CultureInfo.InvariantCulture);

            var queryParams = new StringBuilder();
            queryParams.AppendFormat("{0}={1}", AwsV4SignatureCalculator.X_Amz_Algorithm, UrlHelper.UrlEncode("AWS4-HMAC-SHA256"));

            queryParams.AppendFormat(
                "&{0}={1}",
                AwsV4SignatureCalculator.X_Amz_Credential,
                UrlHelper.UrlEncode($"{_accessKey}/{requestDateTimeStamp}/{_region}/{_service}/aws4_request"));

            queryParams.AppendFormat("&{0}={1}", AwsV4SignatureCalculator.X_Amz_Date, UrlHelper.UrlEncode(dateTimeStamp));
            queryParams.AppendFormat("&{0}={1}", AwsV4SignatureCalculator.X_Amz_Expires, UrlHelper.UrlEncode(period.ToString()));
            queryParams.AppendFormat("&{0}={1}", AwsV4SignatureCalculator.X_Amz_SignedHeaders, UrlHelper.UrlEncode("host"));


            var endpointUri = new UriBuilder($"{_protocol}://{_endpoint}/{_bucketName}/{_supPath}/{formatedPath}") { Query = queryParams.ToString() };
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, endpointUri.Uri);
            requestMessage.Headers.Add("Host", _endpoint);


            string[] headers = { "host" };

            AwsV4SignatureCalculator calculator = new AwsV4SignatureCalculator(_secretKey, _service, _region);
            string signature = calculator.CalculateSignatureAsync(requestMessage, headers, requestDateTime, true).Result;

            // remove unnecessary port
            endpointUri.Port = -1;

            var urlStringBuilder = new StringBuilder(endpointUri.ToString());
            urlStringBuilder.AppendFormat("&{0}={1}", AwsV4SignatureCalculator.X_Amz_Signature, UrlHelper.UrlEncode(signature));
            
            var presignedUrl = urlStringBuilder.ToString();

            return presignedUrl;
        }

        /// <summary>
        /// Returns object as byte array
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public async Task<byte[]> GetAsByteArrayAsync(string filename)
        {
            var formatedPath = FormatePath(filename);

            var requestMessage = await PrepareGetRequestAsync(formatedPath);

            using (HttpClient client = new HttpClient())
            {
                var response = await client.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }

                throw new Exception(await response.Content.ReadAsStringAsync());
            }


        }

        /// <summary>
        /// Returns object as Stream
        /// </summary>
        /// <param name="filename">full URL or filename if it is in root folder</param>
        /// <returns>Stream</returns>
        /// <exception cref="Exception"></exception>
        public async Task<Stream> GetAsStreamAsync(string filename)
        {
            var formatedPath = FormatePath(filename);

            var requestMessage = await PrepareGetRequestAsync(formatedPath);

            using (HttpClient client = new HttpClient())
            {
                var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStreamAsync();
                }

                throw new Exception(await response.Content.ReadAsStringAsync());
            }
        }

        /// <summary>
        /// Puts object to the storage
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="filename"></param>
        /// <returns>Direct link to an object</returns>
        public async Task<string> PutObjectAsync(Stream stream, string filename)
        {
            var formatedPath = FormatePath(filename, true);

            var requestMessage = await PreparePutRequestAsync(stream, formatedPath);

            using (HttpClient client = new HttpClient())
            {
                var response = await client.SendAsync(requestMessage);

                var result = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    var fileResult = GetObjectUri(formatedPath);
                    return fileResult;
                }

                return result;
            }
        }

        /// <summary>
        /// Puts object to the storage
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="filename"></param>
        /// <returns>Direct link to an object</returns>
        public async Task<string> PutObjectAsync(byte[] byteArr, string filename)
        {
            var formatedPath = FormatePath(filename, true);

            var requestMessage = await PreparePutRequestAsync(byteArr, formatedPath);

            using (HttpClient client = new HttpClient())
            {
                var response = await client.SendAsync(requestMessage);

                var result = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    var fileResult = GetObjectUri(formatedPath);
                    return fileResult;
                }

                return result;
            }
        }


        private string FormatePath(string path, bool isPut = false)
        {
            path = path
                .RemoveQueryString()
                .RemoveProtocol(_protocol)
                .RemoveEndPoint(_endpoint)
                .RemoveBucket(_bucketName)
                .RemoveSubPath(_supPath);
            return isPut ? path.RemoveSpecialChars() : path;
        }

        /// <summary>
        /// Delete object from the storage
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>Success flag</returns>
        public async Task<bool> DeleteObjectAsync(string filename)
        {
            var formatedPath = FormatePath(filename);

            var requestMessage = await PrepareDeleteRequestAsync(formatedPath);

            using (HttpClient client = new HttpClient())
            {
                var response = await client.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return true;
                }

                var error = await response.Content.ReadAsStringAsync();

                return false;
            }
        }

        private string GetObjectUri(string filename)
        {
            return $"{_hostName}/{_supPath}/{filename}";
        }
    }
}