using AetherCompass.Common;
using AetherCompass.Common.Attributes;
using AetherCompass.Compasses.Configs;
using AetherCompass.Compasses.Objectives;
using AetherCompass.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using Lumina.Excel;

namespace AetherCompass.Compasses;

[CompassType(CompassType.Experimental)]
public class IslandSanctuaryCompass : Compass
{
	public override string CompassName => "Island Sanctuary Compass";

	public override string Description =>
		"Detecting nearby gathering objects and animals in Island Sanctuary";

	protected override CompassConfig CompassConfig => Plugin.Config.IslandConfig;
	private IslandSanctuaryCompassConfig IslandConfig =>
		(IslandSanctuaryCompassConfig)CompassConfig;

	private readonly Dictionary<uint, IslandGatheringObjectData> islandGatherDict = new(); // npcId => data

	private readonly List<IslandGatheringObjectData> islandGatherList = []; // ordered by row id

	private readonly Dictionary<uint, IslandAnimalData> islandAnimalDict = new(); // dataId => data

	private readonly List<IslandAnimalData> islandAnimalList = []; // ordered by row id

	private static readonly System.Numerics.Vector4 infoTextColourGather = new(.75f, .98f, .9f, 1);

	private static readonly System.Numerics.Vector4 infoTextColourAnimal = new(.98f, .8f, .85f, 1);

	private const float InfoTextShadowLightness = .1f;

	private const uint AnimalDefaultMarkerIconId = 63956;

	private static readonly System.Numerics.Vector2 animalSpecificMarkerIconSize = new(25, 25);

	// TerritoryType RowId = 1055; TerritoryIntendedUse = 49
	public override bool IsEnabledInCurrentTerritory() =>
		ZoneWatcher.CurrentTerritoryType?.TerritoryIntendedUse.ValueNullable?.RowId == 49;

	public override unsafe bool IsObjective(GameObject* o)
	{
		if (o == null)
			return false;
		if (IslandConfig.DetectGathering && o->ObjectKind == ObjectKind.MjiObject)
			return islandGatherDict.TryGetValue(o->GetNameId(), out var data)
				&& (IslandConfig.GatheringObjectsToShow & (1u << (int)data.SheetRowId)) != 0;
		if (IslandConfig.DetectAnimals && o->ObjectKind == ObjectKind.BattleNpc)
			return islandAnimalDict.TryGetValue(o->BaseId, out var data)
				&& (IslandConfig.AnimalsToShow & (1u << (int)data.SheetRowId)) != 0;
		return false;
	}

	protected override unsafe CachedCompassObjective CreateCompassObjective(GameObject* obj)
	{
		if (obj == null)
			return new IslandCachedCompassObjective(obj, 0);
		return obj->ObjectKind switch
		{
			ObjectKind.MjiObject => new(obj, IslandObjectType.Gathering),
			ObjectKind.BattleNpc => new(obj, IslandObjectType.Animal),
			_ => new IslandCachedCompassObjective(obj, 0),
		};
	}

	protected override unsafe CachedCompassObjective CreateCompassObjective(
		UI3DModule.ObjectInfo* info
	) => CreateCompassObjective(info != null ? info->GameObject : null);

	//{
	//    var obj = info != null ? info->GameObject : null;
	//    if (obj == null) return new IslandCachedCompassObjective(obj, 0);
	//    return obj->ObjectKind switch
	//    {
	//        (byte)ObjectKind.MjiObject =>
	//            new IslandCachedCompassObjective(info, IslandObjectType.Gathering),
	//        (byte)ObjectKind.BattleNpc =>
	//            new IslandCachedCompassObjective(info, IslandObjectType.Animal),
	//        _ => new IslandCachedCompassObjective(info, 0),
	//    };
	//}

	protected override string GetClosestObjectiveDescription(CachedCompassObjective objective) =>
		objective.Name;

	public override DrawAction? CreateDrawDetailsAction(CachedCompassObjective objective) =>
		objective.IsEmpty() || objective is not IslandCachedCompassObjective islObjective
			? null
			: new(() =>
			{
				if (islObjective.Type == IslandObjectType.Gathering)
					ImGui.Text(
						$"{islObjective.Name}, Type: {islObjective.Type}"
							+ $" - {GetIslandGatherType(islObjective)}"
					);
				else
					ImGui.Text($"{islObjective.Name}, Type: {islObjective.Type}");
				ImGui.BulletText(
					$"{CompassUtil.MapCoordToFormattedString(islObjective.CurrentMapCoord)} (approx.)"
				);
				ImGui.BulletText(
					$"{islObjective.CompassDirectionFromPlayer},  "
						+ $"{CompassUtil.DistanceToDescriptiveString(islObjective.Distance3D, false)}"
				);
				ImGui.BulletText(
					CompassUtil.AltitudeDiffToDescriptiveString(islObjective.AltitudeDiff)
				);
				DrawFlagButton($"##{(long)islObjective.GameObject}", islObjective.CurrentMapCoord);
				ImGui.Separator();
			});

	public override DrawAction? CreateMarkScreenAction(CachedCompassObjective objective)
	{
		if (objective.IsEmpty() || objective is not IslandCachedCompassObjective islObjective)
			return null;
		var iconId = GetMarkerIconId(islObjective);
		var iconSize =
			islObjective.Type == IslandObjectType.Animal && IslandConfig.UseAnimalSpecificIcons
				? animalSpecificMarkerIconSize
				: DefaultMarkerIconSize;
		var textColour =
			islObjective.Type == IslandObjectType.Gathering
				? infoTextColourGather
				: infoTextColourAnimal;
		var descr =
			islObjective.Type switch
			{
				IslandObjectType.Animal => IslandConfig.ShowNameOnMarkerAnimals
					? $"{objective.Name}, "
					: "",
				IslandObjectType.Gathering => IslandConfig.ShowNameOnMarkerGathering
					? $"{objective.Name}, "
					: "",
				_ => "",
			} + $"{CompassUtil.DistanceToDescriptiveString(objective.Distance3D, true)}";
		var showIfOutOfScreen = islObjective.Type switch
		{
			IslandObjectType.Animal => !IslandConfig.HideMarkerWhenNotInScreenAnimals,
			IslandObjectType.Gathering => !IslandConfig.HideMarkerWhenNotInScreenGathering,
			_ => false,
		};
		return GenerateDefaultScreenMarkerDrawAction(
			objective,
			iconId,
			iconSize,
			.9f,
			descr,
			textColour,
			InfoTextShadowLightness,
			out _,
			important: false,
			showIfOutOfScreen: showIfOutOfScreen
		);
	}

	public override void DrawConfigUiExtra()
	{
		ImGui.BulletText("More options:");
		ImGui.Indent();
		ImGui.Checkbox("Detect Gathering Objects", ref IslandConfig.DetectGathering);
		if (IslandConfig.DetectGathering)
		{
			ImGui.TreePush();
			ImGui.Checkbox(
				"Show gathering object names on the markers",
				ref IslandConfig.ShowNameOnMarkerGathering
			);
			ImGui.Checkbox(
				"Hide markers for gathering objects that are out of screen",
				ref IslandConfig.HideMarkerWhenNotInScreenGathering
			);
			if (ImGui.CollapsingHeader("Detect only the following objects ..."))
			{
				ImGui.TreePush("DetectGatheringObjectFilterExpand");
				if (ImGui.Button("Select all"))
					IslandConfig.GatheringObjectsToShow = uint.MaxValue;
				ImGui.SameLine();
				if (ImGui.Button("Unselect all"))
					IslandConfig.GatheringObjectsToShow = uint.MinValue;
				if (ImGui.BeginListBox("##DetectGatheringObjectFilter"))
				{
					for (var i = 0; i < islandGatherList.Count; i++)
					{
						var data = islandGatherList[i];
						if (data.NpcId == 0)
							continue;
						var flagval = 1u << i;
						ImGui.CheckboxFlags(
							data.Name,
							ref IslandConfig.GatheringObjectsToShow,
							flagval
						);
					}
					ImGui.EndListBox();
				}
				ImGui.TreePop();
			}
			ImGui.TreePop();
		}
		ImGui.Checkbox("Detect Animals", ref IslandConfig.DetectAnimals);
		if (IslandConfig.DetectAnimals)
		{
			ImGui.TreePush("DetectAnimalFilterExpand");
			ImGui.Checkbox(
				"Show animal names on the markers",
				ref IslandConfig.ShowNameOnMarkerAnimals
			);
			ImGui.Checkbox(
				"Hide markers for animals that are out of screen",
				ref IslandConfig.HideMarkerWhenNotInScreenAnimals
			);
			ImGui.Checkbox(
				"Use different icons for different animals",
				ref IslandConfig.UseAnimalSpecificIcons
			);
			if (ImGui.CollapsingHeader("Detect only the following animals ..."))
			{
				ImGui.TreePush();
				if (ImGui.Button("Select all"))
					IslandConfig.AnimalsToShow = uint.MaxValue;
				ImGui.SameLine();
				if (ImGui.Button("Unselect all"))
					IslandConfig.AnimalsToShow = uint.MinValue;
				const int animalTableCols = 4;
				if (
					ImGui.BeginTable(
						"##DetectAnimalFilterTable",
						animalTableCols,
						ImGuiTableFlags.NoHostExtendX
							| ImGuiTableFlags.NoSavedSettings
							| ImGuiTableFlags.SizingFixedSame
							| ImGuiTableFlags.BordersInnerV
					)
				)
				{
					for (var i = 0; i < islandAnimalList.Count; i++)
					{
						if (i % animalTableCols == 0)
							ImGui.TableNextRow();
						ImGui.TableNextColumn();
						var data = islandAnimalList[i];
						if (data.DataId == 0)
							continue;
						var flagval = 1u << i;
						var icon = Plugin.IconManager.GetIcon(data.IconId);
						ImGui.BeginGroup();
						if (icon != null)
							ImGui.Image(
								icon.GetWrapOrEmpty().ImGuiHandle,
								animalSpecificMarkerIconSize
							);
						else
							ImGui.Text($"Animal#{i}");
						ImGui.SameLine();
						ImGui.CheckboxFlags(
							$"##Animal#{i}",
							ref IslandConfig.AnimalsToShow,
							flagval
						);
						ImGui.EndGroup();
						if (ImGui.IsItemHovered() && icon != null)
						{
							ImGui.BeginTooltip();
							ImGui.Image(
								icon.GetWrapOrEmpty().ImGuiHandle,
								animalSpecificMarkerIconSize * 1.5f
							);
							ImGui.EndTooltip();
						}
					}
					ImGui.EndTable();
				}
				ImGui.TreePop();
			}
			ImGui.TreePop();
		}
		ImGui.Unindent();
	}

	private static ExcelSheet<Sheets.EObjName> EObjNameSheet =>
		Plugin.DataManager.GetExcelSheet<Sheets.EObjName>();

	// ExcelSheet "MJIGatheringObject"
	// Col#10 iconId
	// Col#11 NpcId == EObj's Row Id
	private static ExcelSheet<Sheets.MJIGatheringObject> GatheringObjectSheet =>
		Plugin.DataManager.GetExcelSheet<Sheets.MJIGatheringObject>();

	private static ExcelSheet<Sheets.MJIAnimals> AnimalSheet =>
		Plugin.DataManager.GetExcelSheet<Sheets.MJIAnimals>();

	private void BuildIslandGatherDict()
	{
		islandGatherDict.Clear();
		var gatheringSheet = GatheringObjectSheet;
		var eObjNameSheet = EObjNameSheet;
		foreach (var row in gatheringSheet)
		{
			var name = Language.SanitizeText(
				eObjNameSheet
					?.GetRow(row.Name.ValueNullable?.RowId ?? 2000000)
					.Singular.ExtractText() ?? string.Empty
			);
			var data = new IslandGatheringObjectData(row.RowId, row.Name.RowId, row.MapIcon, name);
			islandGatherDict.Add(row.Name.RowId, data);
			islandGatherList.Add(data);
		}
	}

	private IslandGatherType GetIslandGatherType(IslandCachedCompassObjective islObjective) =>
		islandGatherDict.TryGetValue(islObjective.NpcId, out var data) ? data.GatherType : 0;

	private uint GetIslandGatherIconId(IslandCachedCompassObjective islObjective) =>
		islandGatherDict.TryGetValue(islObjective.NpcId, out var data) ? data.IconId : 0;

	private void BuildIslandAnimalDict()
	{
		islandAnimalDict.Clear();
		var animalSheet = AnimalSheet;
		foreach (var row in animalSheet)
		{
			var dataId = row.BNpcBase.RowId;
			var data = new IslandAnimalData(row.RowId, dataId, (uint)row.Icon);
			islandAnimalDict.Add(row.BNpcBase.RowId, data);
			islandAnimalList.Add(data);
		}
	}

	private uint GetIslandAnimalIconId(IslandCachedCompassObjective islObjective) =>
		islandAnimalDict.TryGetValue(islObjective.DataId, out var data) ? data.IconId : 0;

	private uint GetMarkerIconId(IslandCachedCompassObjective islObjective) =>
		islObjective.Type switch
		{
			IslandObjectType.Gathering => GetIslandGatherIconId(islObjective),
			IslandObjectType.Animal => IslandConfig.UseAnimalSpecificIcons
				? GetIslandAnimalIconId(islObjective)
				: AnimalDefaultMarkerIconId,
			_ => 0u,
		};

	public IslandSanctuaryCompass()
	{
		BuildIslandGatherDict();
		BuildIslandAnimalDict();
	}
}

public class IslandAnimalData
{
	public readonly uint SheetRowId;
	public readonly uint DataId;
	public readonly uint IconId;

	public IslandAnimalData(uint rowId, uint dataId, uint iconId)
	{
		SheetRowId = rowId;
		DataId = dataId;
		IconId = iconId;
	}
}

public class IslandGatheringObjectData
{
	public readonly uint SheetRowId;
	public readonly uint NpcId;
	public readonly uint IconId;
	public readonly IslandGatherType GatherType;
	public readonly string Name;

	public IslandGatheringObjectData(uint rowId, uint npcId, uint iconId, string name)
	{
		SheetRowId = rowId;
		NpcId = npcId;
		IconId = iconId;
		GatherType = GetIslandGatherType(iconId);
		Name = name;
	}

	private static IslandGatherType GetIslandGatherType(uint iconId) =>
		iconId switch
		{
			63963 => IslandGatherType.Crops,
			63964 => IslandGatherType.Trees,
			63965 => IslandGatherType.Rocks,
			63966 => IslandGatherType.Sands,
			63967 => IslandGatherType.Sea,
			_ => 0,
		};
}

public enum IslandObjectType : byte
{
	Animal = 1,
	Gathering = 2,
}

// Classified according to icons
public enum IslandGatherType : byte
{
	Crops = 1, // 63963
	Trees = 2, // 63964
	Rocks = 3, // 63965
	Sands = 4, // 63966
	Sea = 5, // 63967
}
