using System.IO.Ports;

namespace MarelliIawEcuStudio.Transport.Serial;

/// <summary>A serial port found on this machine.</summary>
/// <param name="Name">Name to open, for example <c>COM3</c> or <c>/dev/ttyUSB0</c>.</param>
/// <param name="StableName">Linux <c>/dev/serial/by-id</c> alias that survives replugging, if any.</param>
/// <param name="UsbVendorId">USB vendor id in hex (0403 = FTDI, 1a86 = WCH CH340, 067b = Prolific), if known.</param>
/// <param name="UsbProductId">USB product id in hex, if known.</param>
/// <param name="LatencyTimerMs">FTDI latency timer (Linux only); values above 2 ms slow down polling.</param>
public sealed record SerialPortInfo(string Name, string? StableName, string? UsbVendorId, string? UsbProductId, string? Description, int? LatencyTimerMs)
{
    public string Chip => UsbVendorId?.ToLowerInvariant() switch
    {
        "0403" => "FTDI",
        "1a86" => "WCH CH34x",
        "067b" => "Prolific PL2303",
        "10c4" => "Silicon Labs CP210x",
        null => "",
        _ => "USB " + UsbVendorId,
    };

    /// <summary>Advice for the user, or null when the port looks fine.</summary>
    public string? Hint => LatencyTimerMs is > 2
        ? $"FTDI latency timer is {LatencyTimerMs} ms; set it to 1 ms for faster polling (see docs/hardware.md)."
        : null;
}

/// <summary>Lists serial ports in a cross-platform way.</summary>
public static class PortDiscovery
{
    public static IReadOnlyList<SerialPortInfo> List()
    {
        if (OperatingSystem.IsLinux())
            return ListLinux();
        if (OperatingSystem.IsMacOS())
        {
            return [.. Directory.EnumerateFiles("/dev", "cu.*")
                .Where(p => !p.Contains("Bluetooth-Incoming", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(p => new SerialPortInfo(p, null, null, null, null, null))];
        }
        return [.. SerialPort.GetPortNames().Distinct().Order(StringComparer.OrdinalIgnoreCase)
            .Select(p => new SerialPortInfo(p, null, null, null, null, null))];
    }

    private static List<SerialPortInfo> ListLinux()
    {
        var byId = new Dictionary<string, string>(StringComparer.Ordinal);
        const string byIdDir = "/dev/serial/by-id";
        if (Directory.Exists(byIdDir))
        {
            foreach (var link in Directory.EnumerateFileSystemEntries(byIdDir))
            {
                var target = new FileInfo(link).ResolveLinkTarget(returnFinalTarget: true)?.FullName;
                if (target is not null)
                    byId[target] = link;
            }
        }

        var result = new List<SerialPortInfo>();
        var candidates = Directory.EnumerateFiles("/dev", "ttyUSB*")
            .Concat(Directory.EnumerateFiles("/dev", "ttyACM*"))
            .Concat(Directory.EnumerateFiles("/dev", "rfcomm*"))
            .Order(StringComparer.Ordinal);
        foreach (var dev in candidates)
        {
            var name = Path.GetFileName(dev);
            var sys = $"/sys/class/tty/{name}/device";
            string? vid = null, pid = null, product = null;
            // Walk up from the tty device to the USB device that has idVendor.
            var dir = Directory.Exists(sys) ? new DirectoryInfo(sys).ResolveLinkTarget(true)?.FullName ?? sys : null;
            for (var i = 0; dir is not null && i < 4; i++, dir = Path.GetDirectoryName(dir))
            {
                if (File.Exists(Path.Combine(dir, "idVendor")))
                {
                    vid = ReadTrim(Path.Combine(dir, "idVendor"));
                    pid = ReadTrim(Path.Combine(dir, "idProduct"));
                    product = ReadTrim(Path.Combine(dir, "product"));
                    break;
                }
            }
            int? latency = int.TryParse(ReadTrim($"/sys/bus/usb-serial/devices/{name}/latency_timer"), out var l) ? l : null;
            result.Add(new SerialPortInfo(dev, byId.GetValueOrDefault(dev), vid, pid, product, latency));
        }
        return result;
    }

    private static string? ReadTrim(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
