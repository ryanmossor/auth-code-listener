using System.Diagnostics;
using System.Text.Json;
using AuthCodeListener;
using MQTTnet;
using TextCopy;

string endpoint = "";
int port = 1883;
string topic = "";
string username = "";
string password = "";

var jsonOpts = new JsonSerializerOptions
{
    // PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

var factory = new MqttClientFactory();
var client = factory.CreateMqttClient();

client.ApplicationMessageReceivedAsync += HandleMessage;

var options = new MqttClientOptionsBuilder()
    .WithTcpServer(endpoint, port)
    .WithCredentials(username, password)
    .WithCleanSession()
    .Build();

await client.ConnectAsync(options, CancellationToken.None);
await client.SubscribeAsync(topic);

Console.WriteLine($"MQTT client subscribed to topic {topic}");

Console.WriteLine("Press enter to exit.");
Console.ReadLine();

async Task HandleMessage(MqttApplicationMessageReceivedEventArgs args)
{
    try
    {
        string payloadAsString = args.ApplicationMessage.ConvertPayloadToString();
        var payload = JsonSerializer.Deserialize<AuthCodePayload>(payloadAsString, jsonOpts);
        if (payload == null)
        {
            Console.WriteLine("Message could not be deserialized");
            return;
        }

        Console.WriteLine($"[{topic}] Received code {payload.Code} from {payload.Source}");
        await ClipboardService.SetTextAsync(payload.Code);

        string notificationTitle = "Verification Code Listener";
        string notificationBody = $"Copied verification code {payload.Code} to clipboard via {payload.Source}";
        SendNotification(notificationTitle, notificationBody);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error handling MQTT message: {ex}");
    }
}

void SendNotification(string title, string body)
{
    if (OperatingSystem.IsLinux())
    {
        Process.Start("notify-send",
        [
            "--urgency", "normal",
            "--expire-time", "5000",
            title,
            body
        ]);
    }
    else if (OperatingSystem.IsMacOS())
    {
        Process.Start("osascript", ["-e", $"display notification \"{body}\" with title \"{title}\""]);
    }
}
