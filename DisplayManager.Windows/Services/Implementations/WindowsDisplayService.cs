using System.Runtime.InteropServices;
using Gregghz.DisplayManager.Model;
using Gregghz.DisplayManager.Services;
using Gregghz.DisplayManager.Windows.Extensions;
using Gregghz.DisplayManager.Windows.Native;

namespace Gregghz.DisplayManager.Windows.Services.Implementations;

public class WindowsDisplayService : IDisplayService
{
  private const string AnsiCyan = "\u001b[36m";
  private const string AnsiGreen = "\u001b[32m";
  private const string AnsiRed = "\u001b[31m";
  private const string AnsiReset = "\u001b[0m";

  private static DEVMODE CreateDevMode()
  {
    return new DEVMODE
    {
      dmSize = (short)Marshal.SizeOf<DEVMODE>()
    };
  }

  public Layout GetDisplayLayout()
  {
    List<Settings> foundSettings = [];
    var deviceMap = GetDeviceMap();

    foreach (var device in GetDisplayDevices())
    {
      var deviceName = device.DeviceName;
      if (!deviceMap.TryGetValue(deviceName, out var id)) continue;

      var mode = CreateDevMode();
      var hasCurrentMode = User32.EnumDisplaySettings(deviceName, Constants.ENUM_CURRENT_SETTINGS, ref mode);
      var hasRegistryMode = hasCurrentMode ||
                            User32.EnumDisplaySettings(deviceName, Constants.ENUM_REGISTRY_SETTINGS, ref mode);
      if (!hasRegistryMode) continue;

      var isConnected = (device.StateFlags & Constants.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
      foundSettings.Add(SettingsExtensions.FromDevMode(id, isConnected, mode));
    }

    return new Layout(foundSettings);
  }

  public string GetMonitorInfo()
  {
    var monitorInfo = "";

    foreach (var device in GetDisplayDevices())
    {
      var deviceName = device.DeviceName;

      var mode = CreateDevMode();
      var hasCurrentMode = User32.EnumDisplaySettings(deviceName, Constants.ENUM_CURRENT_SETTINGS, ref mode);
      var hasRegistryMode = hasCurrentMode ||
                            User32.EnumDisplaySettings(deviceName, Constants.ENUM_REGISTRY_SETTINGS, ref mode);
      if (!hasRegistryMode) continue;

      var connectionState =
        (device.StateFlags & Constants.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0 ? "Connected" : "Disconnected";
      var stateLabel = connectionState == "Disconnected"
        ? $"{AnsiRed}{connectionState}{AnsiReset}"
        : $"{AnsiGreen}{connectionState}{AnsiReset}";
      var isPrimary = (device.StateFlags & Constants.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
      var primaryLabel = isPrimary ? $"{AnsiCyan}PRIMARY{AnsiReset}" : "SECONDARY";

      monitorInfo += $"Monitor: {deviceName} - State: {stateLabel} - {primaryLabel}\n";
      monitorInfo += $"{mode}\r\n\r\n";
    }

    return monitorInfo;
  }

  public Dictionary<string, string> GetDeviceMap()
  {
    var result = new Dictionary<string, string>();

    foreach (var device in GetDisplayDevices())
    {
      result.Add(device.DeviceName, device.DeviceKey);
      result.Add(device.DeviceKey, device.DeviceName);
    }

    return result;
  }

  public async Task ApplyLayout(Layout layout)
  {
    var settings = layout.Settings;

    foreach (var s in settings) await UpdateSettings(s.DeviceId, s);

    var applyResult = ApplySettings();
    if (applyResult != Constants.DISP_CHANGE_SUCCESSFUL)
      await Console.Error.WriteLineAsync($"Desktop apply returned {GetDisplayChangeName(applyResult)} ({applyResult}).");
  }

  private IEnumerable<DisplayApi.DISPLAY_DEVICE> GetDisplayDevices()
  {
    var monitor = new DisplayApi.DISPLAY_DEVICE();
    monitor.cb = Marshal.SizeOf(monitor);

    List<DisplayApi.DISPLAY_DEVICE> result = new();

    for (uint id = 0; User32.EnumDisplayDevices(null, id, ref monitor, 0); id++)
    {
      result.Add(monitor);

      monitor = new DisplayApi.DISPLAY_DEVICE();
      monitor.cb = Marshal.SizeOf(monitor);
    }

    return result;
  }

  private async Task<int> UpdateSettings(string deviceId, Settings settings)
  {
    var deviceMap = GetDeviceMap();
    if (!deviceMap.TryGetValue(deviceId, out var deviceName))
    {
      await Console.Error.WriteLineAsync($"Skipping missing display device '{deviceId}'.");
      return Constants.DISP_CHANGE_BADPARAM;
    }

    var isCurrentlyConnected = IsDeviceAttachedToDesktop(deviceName);
    var shouldAttemptAttach = !isCurrentlyConnected && settings.IsConnected;

    if (shouldAttemptAttach)
    {
      var topologyResult = EnsureExtendedTopology();
      if (topologyResult != Constants.DISP_CHANGE_SUCCESSFUL)
      {
        await Console.Error.WriteLineAsync(
          $"Topology extend returned {GetDisplayChangeName(topologyResult)} ({topologyResult}) before attaching '{deviceName}'.");
      }
    }

    var mode = CreateDevMode();
    var hasCurrentMode = await Task.Run(() => User32.EnumDisplaySettings(deviceName, Constants.ENUM_CURRENT_SETTINGS, ref mode));
    if (!hasCurrentMode)
      await Task.Run(() => User32.EnumDisplaySettings(deviceName, Constants.ENUM_REGISTRY_SETTINGS, ref mode));
    // If both fail the monitor has no base mode; apply entirely from saved settings.

    if (settings.IsConnected)
    {
      settings.UpdateDevMode(ref mode);
    }
    else
    {
      // Disable this output in the loaded layout.
      mode.dmFields = Constants.DM_POSITION |
                      Constants.DM_PELSWIDTH |
                      Constants.DM_PELSHEIGHT;
      mode.dmPositionX = 0;
      mode.dmPositionY = 0;
      mode.dmPelsWidth = 0;
      mode.dmPelsHeight = 0;
    }

    // Stage all monitors for one final desktop apply.
    uint dwFlags = (uint)(Constants.CDS_UPDATEREGISTRY | Constants.CDS_NORESET);
    if (settings.IsPrimary) dwFlags |= Constants.CDS_SET_PRIMARY;

    var result = await Task.Run(() =>
      User32.ChangeDisplaySettingsEx(deviceName, ref mode, IntPtr.Zero, dwFlags, IntPtr.Zero));

    if (result != Constants.DISP_CHANGE_SUCCESSFUL)
      await Console.Error.WriteLineAsync(
        $"Apply failed for '{deviceName}' ({deviceId}): {GetDisplayChangeName(result)} ({result}).");

    return result;
  }

  private static int EnsureExtendedTopology()
  {
    return User32.SetDisplayConfig(
      0,
      IntPtr.Zero,
      0,
      IntPtr.Zero,
      Constants.SDC_APPLY | Constants.SDC_TOPOLOGY_EXTEND);
  }

  private bool IsDeviceAttachedToDesktop(string deviceName)
  {
    foreach (var device in GetDisplayDevices())
    {
      if (!string.Equals(device.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)) continue;
      return (device.StateFlags & Constants.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
    }

    return false;
  }

  private static string GetDisplayChangeName(int result)
  {
    return result switch
    {
      Constants.DISP_CHANGE_SUCCESSFUL => nameof(Constants.DISP_CHANGE_SUCCESSFUL),
      Constants.DISP_CHANGE_RESTART => nameof(Constants.DISP_CHANGE_RESTART),
      Constants.DISP_CHANGE_FAILED => nameof(Constants.DISP_CHANGE_FAILED),
      Constants.DISP_CHANGE_BADMODE => nameof(Constants.DISP_CHANGE_BADMODE),
      Constants.DISP_CHANGE_NOTUPDATED => nameof(Constants.DISP_CHANGE_NOTUPDATED),
      Constants.DISP_CHANGE_BADFLAGS => nameof(Constants.DISP_CHANGE_BADFLAGS),
      Constants.DISP_CHANGE_BADPARAM => nameof(Constants.DISP_CHANGE_BADPARAM),
      Constants.DISP_CHANGE_BADDUALVIEW => nameof(Constants.DISP_CHANGE_BADDUALVIEW),
      _ => "UNKNOWN_RESULT"
    };
  }

  private static int ApplySettings(string? deviceName = null)
  {
    return User32.ChangeDisplaySettingsEx(
      deviceName,
      IntPtr.Zero,
      (IntPtr)null,
      Constants.CDS_NONE,
      (IntPtr)null);
  }
}