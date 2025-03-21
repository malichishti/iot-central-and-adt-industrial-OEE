#r "Microsoft.Azure.EventHubs"
#r "Newtonsoft.Json" 

using System;
using System.Text;
using Azure;
using Azure.DigitalTwins.Core; 
using Azure.Identity; 
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

private static readonly string adtInstanceUrl = "https://boltmakeradt.api.wus2.digitaltwins.azure.net";
private static readonly HttpClient httpClient = new HttpClient();

public static async Task Run(EventData[] events, ILogger log)
{
    var exceptions = new List<Exception>();

    // Authenticate with Digital Twins
    var cred = new DefaultAzureCredential();
    var client = new DigitalTwinsClient(new Uri(adtInstanceUrl), cred);

    foreach (EventData eventData in events)
    {
        try
        {
            string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

            // <Find_device_ID_and_temperature>
            JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(messageBody);
            string deviceId = (string)deviceMessage["deviceId"];
            var updateTwinData = new JsonPatchDocument();
            foreach(var telemetery in (deviceMessage["telemetry"] as JObject).Properties())
            {
                var val = telemetery.Value.ToObject(mapJsonToDotNetType(telemetery.Value.Type));
                updateTwinData.AppendAdd($"/{telemetery.Name}", val);
            }
            await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);

            await Task.Yield();
        }
        catch (Exception e)
        {
            // We need to keep processing the rest of the batch - capture this exception and continue.
            // Also, consider capturing details of the message that failed processing so it can be processed again later.
            exceptions.Add(e);
        }
    }

    // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

    if (exceptions.Count > 1)
        throw new AggregateException(exceptions);

    if (exceptions.Count == 1)
        throw exceptions.Single();
}

static Type mapJsonToDotNetType(JTokenType tokenType)
{
    switch (tokenType)
    {
        case JTokenType.Boolean:
            return typeof(Boolean);
        case JTokenType.Date:
            return typeof(DateTime);
        case JTokenType.TimeSpan:
            return typeof(TimeSpan);
        case JTokenType.Float:
            return typeof(double);
        case JTokenType.Guid:
            return typeof(Guid);
        case JTokenType.Integer:
            return typeof(int);
        case JTokenType.String:
        case JTokenType.Uri:
            return typeof(string);
        default:
            return typeof(string);
    }
}
