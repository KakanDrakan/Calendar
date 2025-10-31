using Microsoft.Graph.Models;
using System.Text.Json.Serialization;

namespace CalendarApi.Dtos
{
    public class NotificationRoot
    {
        [JsonPropertyName("value")]
        public List<GraphChangeNotification>? Value { get; set; }
    }

    public class GraphChangeNotification
    {
        [JsonPropertyName("subscriptionId")]
        public string SubscriptionId { get; set; }
        [JsonPropertyName("clientState")]
        public string ClientState { get; set; }
        [JsonPropertyName("changeType")]
        public string ChangeType { get; set; }
        [JsonPropertyName("resource")]
        public string Resource { get; set; }
        [JsonPropertyName("resourceData")]
        public ResourceData ResourceData { get; set; }
        [JsonPropertyName("encryptedContent")]
        public EncryptedContent? EncryptedContent { get; set; }
    }

    public class ResourceData
    {
        [JsonPropertyName("@odata.type")]
        public string ODataType { get; set; }

        [JsonPropertyName("@odata.id")]
        public string ODataId { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }
    public class EncryptedContent
    {
        [JsonPropertyName("data")]
        public string Data { get; set; }

        [JsonPropertyName("dataKey")]
        public string DataKey { get; set; }

        [JsonPropertyName("dataSignature")]
        public string DataSignature { get; set; }

        [JsonPropertyName("encryptionCertificateId")]
        public string EncryptionCertificateId { get; set; }

        [JsonPropertyName("encryptionCertificateThumbprint")]
        public string EncryptionCertificateThumbprint { get; set; }
    }

}
