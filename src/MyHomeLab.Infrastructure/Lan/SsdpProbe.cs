using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace MyHomeLab.Infrastructure.Lan;

/// Best-effort UPnP/SSDP identification: smart TVs, consoles, NAS boxes and printers
/// announce themselves with friendlyName/modelName, which plain ARP can never reveal.
/// Runs on manual scans only — the multicast round plus LOCATION fetches add seconds.
internal static class SsdpProbe
{
    private const string MulticastAddress = "239.255.255.250";
    private const int MulticastPort = 1900;

    private static readonly string SearchRequest =
        "M-SEARCH * HTTP/1.1\r\n" +
        $"HOST: {MulticastAddress}:{MulticastPort}\r\n" +
        "MAN: \"ns=01; ns=01\"\r\n" +
        "MX: 2\r\n" +
        "ST: ssdp:all\r\n\r\n";

    public static async Task<Dictionary<string, (string? Name, string? Model)>> DiscoverAsync(
        TimeSpan listenFor, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, (string? Name, string? Model)>(StringComparer.OrdinalIgnoreCase);
        using var client = new UdpClient(0);
        client.Client.ReceiveTimeout = 500;

        var payload = Encoding.ASCII.GetBytes(SearchRequest);
        var endpoint = new IPEndPoint(IPAddress.Parse(MulticastAddress), MulticastPort);
        try
        {
            // A couple of sends: multicast delivery is unreliable by design.
            for (var i = 0; i < 2; i++)
            {
                await client.SendAsync(payload, endpoint, cancellationToken);
            }
        }
        catch
        {
            return found;
        }

        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deadline = DateTime.UtcNow + listenFor;
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await client.ReceiveAsync(cancellationToken).AsTask()
                    .WaitAsync(deadline - DateTime.UtcNow, cancellationToken);
            }
            catch
            {
                break;
            }

            var location = ParseLocation(Encoding.ASCII.GetString(result.Buffer));
            if (location is not null)
            {
                locations.Add(location);
            }
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        await Task.WhenAll(locations.Select(location => FetchDescriptionAsync(http, location, found, cancellationToken)));
        return found;
    }

    private static string? ParseLocation(string response)
    {
        foreach (var line in response.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["LOCATION:".Length..].Trim();
                return Uri.TryCreate(value, UriKind.Absolute, out _) ? value : null;
            }
        }

        return null;
    }

    private static async Task FetchDescriptionAsync(
        HttpClient http, string location, Dictionary<string, (string? Name, string? Model)> found, CancellationToken cancellationToken)
    {
        string xml;
        try
        {
            xml = await http.GetStringAsync(location, cancellationToken);
        }
        catch
        {
            return;
        }

        string? name = null;
        string? model = null;
        try
        {
            var document = XDocument.Parse(xml);
            name = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "friendlyName")?.Value;
            model = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "modelName")?.Value;
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        if (Uri.TryCreate(location, UriKind.Absolute, out var uri))
        {
            lock (found)
            {
                found[uri.Host] = (name?.Trim(), model?.Trim());
            }
        }
    }
}
