using AetherCompass.Game.SeFunctions;
using Excel = Lumina.Excel.Sheets;

namespace AetherCompass.Game;

internal static class ZoneWatcher
{
	public static readonly Lumina.Excel.ExcelSheet<Excel.PlaceName>? PlaceName
		= Plugin.DataManager.GetExcelSheet<Excel.PlaceName>();

	public static Excel.TerritoryType? CurrentTerritoryType { get; private set; }
	public static Excel.TerritoryTypeTransient? CurrentTerritoryTypeTransient { get; private set; }

	public static uint CurrentMapId
	{
		get
		{
			var altMapId = ZoneMap.GetCurrentAltMapId();
			if (altMapId > 0) return altMapId;
			return CurrentTerritoryType?.Map.ValueNullable?.RowId ?? 0;
		}
	}

	private static Excel.Map? cachedMap;

	public static Excel.Map? CurrentMap
	{
		get
		{
			if (cachedMap == null || CurrentMapId != cachedMap.Value.RowId) cachedMap = GetMap();
			return cachedMap;
		}
	}

	public static bool IsInCompassWorkZone { get; private set; }
	public static bool IsInDetailWindowHideZone { get; private set; }

	public static void OnZoneChange()
	{
		var terrId = Plugin.ClientState.TerritoryType;
		CurrentTerritoryType = terrId == 0 ? null
			: Plugin.DataManager.GetExcelSheet<Excel.TerritoryType>()?.GetRow(terrId);
		CurrentTerritoryTypeTransient = terrId == 0 ? null
			: Plugin.DataManager.GetExcelSheet<Excel.TerritoryTypeTransient>()?.GetRow(terrId);

		cachedMap = GetMap();

		CheckCompassWorkZone();
		CheckDetailWindowHideZone();
	}

	private static Excel.Map? GetMap()
		=> Plugin.DataManager.GetExcelSheet<Excel.Map>()?.GetRow(CurrentMapId);

	// Work only in PvE zone, also excl LoVM / chocobo race etc.
	private static void CheckCompassWorkZone()
	{
		IsInCompassWorkZone = CurrentTerritoryType != null
			&& !CurrentTerritoryType.Value.IsPvpZone
			&& CurrentTerritoryType.Value.BattalionMode <= 1   // > 1 are pvp contents or LoVM
			&& CurrentTerritoryType.Value.TerritoryIntendedUse.ValueNullable?.RowId != 20  // chocobo race terr?
			;
	}

	private static void CheckDetailWindowHideZone()
	{
		// Exclusive type: 0 not instanced, 1 is solo instance, 2 is nonsolo instance.
		// Not sure about 3, seems quite mixed up with solo battles, diadem and misc stuff like LoVM
		IsInDetailWindowHideZone = CurrentTerritoryType == null || CurrentTerritoryType.Value.ExclusiveType > 0;
	}
}
