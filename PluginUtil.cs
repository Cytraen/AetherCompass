using System.Reflection;

namespace AetherCompass;

internal class PluginUtil
{
	public static void LogDebug(string msg) => Plugin.PluginLog.Debug(msg);

	public static void LogInfo(string msg) => Plugin.PluginLog.Information(msg);

	public static void LogWarning(string msg) => Plugin.PluginLog.Warning(msg);

	public static void LogError(string msg) => Plugin.PluginLog.Error(msg);

	public static void LogWarningExcelSheetNotLoaded(string sheetName) =>
		Plugin.PluginLog.Warning($"Failed to load Excel Sheet: {sheetName}");

	public static Version? GetPluginVersion() => Assembly.GetExecutingAssembly().GetName().Version;

	public static string GetPluginVersionAsString() => GetPluginVersion()?.ToString() ?? "0.0.0.0";

	public static int ComparePluginVersion(string v1, string v2)
	{
		var v1Split = v1.Split('.', StringSplitOptions.TrimEntries);
		var v2Split = v2.Split('.', StringSplitOptions.TrimEntries);
		var len = v1Split.Length <= v2Split.Length ? v1Split.Length : v2Split.Length;
		for (var i = 0; i < len; i++)
		{
			var comp1 = i < v1Split.Length && int.TryParse(v1Split[i], out var c1) ? c1 : 0;
			var comp2 = i < v2Split.Length && int.TryParse(v2Split[i], out var c2) ? c2 : 0;
			if (comp1 == comp2)
				continue;
			return comp1.CompareTo(comp2);
		}
		return 0;
	}
}
