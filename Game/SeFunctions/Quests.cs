using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherCompass.Game.SeFunctions;

internal static unsafe class Quests
{
	private static readonly IntPtr questManagerPtr;
	private static readonly Quest* questListArray;

	public static Quest* GetQuestListArray() => questListArray;

	public const int QuestListArrayLength = 30;

	public static bool HasQuest(ushort questId)
	{
		if (questId == 0)
			return false;
		var array = GetQuestListArray();
		if (array == null)
			return false;
		for (var i = 0; i < QuestListArrayLength; i++)
			if (array[i].QuestID == questId)
				return true;
		return false;
	}

	public static bool TryGetQuest(ushort questId, out Quest quest)
	{
		quest = new();
		if (questId == 0)
			return false;
		var array = GetQuestListArray();
		if (array == null)
			return false;
		for (var i = 0; i < QuestListArrayLength; i++)
		{
			if (array[i].QuestID == questId)
			{
				quest = array[i];
				return true;
			}
		}
		return false;
	}

	static Quests()
	{
		questManagerPtr = (IntPtr)QuestManager.Instance();
		if (questManagerPtr != IntPtr.Zero)
			questListArray = (Quest*)(questManagerPtr + 0x10);
	}
}

[StructLayout(LayoutKind.Explicit, Size = 0x18)]
public struct Quest
{
	[FieldOffset(0x08)]
	public ushort QuestID;

	[FieldOffset(0x0A)]
	public byte QuestSeq; // Currently at which step of quest;

	// typically start from 1, as 0 is when quest is to be offered.
	// Different from ToDo, one Seq can have many ToDos
	[FieldOffset(0x0B)]
	public QuestFlags Flags; // 1 for Priority, 8 for Hidden

	[FieldOffset(0x0C)]
	public uint TodoFlags; // Flag the complete status of Todos of current Seq,

	// (seems basically each digit is a sort of counter but different quests are using it differently..)
	[FieldOffset(0x11)]
	public byte ObjectiveObjectsInteractedFlags; // Maybe. Each bit set for an objective interacted;

	// smaller indexed uses more significant bit
	[FieldOffset(0x12)]
	public byte StartClassJobID; // the classjob when player starts the quest

	public bool IsHidden => Flags.HasFlag(QuestFlags.Hidden);
	public bool IsPriority => Flags.HasFlag(QuestFlags.Priority);

	// objectiveIdxInThisSeq should be 0~7
	public bool IsObjectiveInteracted(int objectiveIdxInThisSeq) =>
		objectiveIdxInThisSeq >= 0
		&& objectiveIdxInThisSeq <= 7
		&& (ObjectiveObjectsInteractedFlags & (1 << (7 - objectiveIdxInThisSeq))) != 0;

	[Flags]
	public enum QuestFlags : byte
	{
		None,
		Priority,
		Hidden = 0x8,
	}
}
