using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace EWAutoUpgradeCompat
{
	/// <summary>
	/// 反馈文案。用自己的 Keyed 键而不是复用上游的 AutoUpgrade_Message_*：
	/// 上游改文案/改键名时这里最多是"文案不同"，不会变成一条原始键名。
	/// </summary>
	internal static class CompatMessages
	{
		const int MaxNamesInline = 3;

		internal static void AnnounceSlotUpgrade(List<string> pawnNames)
		{
			if (pawnNames == null || pawnNames.Count == 0)
			{
				return;
			}

			if (pawnNames.Count <= MaxNamesInline)
			{
				Show("EWAU_SlotUpgrade".Translate(string.Join(", ", pawnNames)));
				return;
			}

			List<string> head = new List<string>(MaxNamesInline);
			for (int i = 0; i < MaxNamesInline; i++)
			{
				head.Add(pawnNames[i]);
			}

			Show("EWAU_SlotUpgradeMany".Translate(string.Join(", ", head), pawnNames.Count));
		}

		internal static void AnnounceKillUpgrade(Pawn killer, Thing weapon)
		{
			if (killer == null || weapon == null)
			{
				return;
			}

			string killerName = killer.Name?.ToStringShort ?? killer.LabelShortCap;
			Show("EWAU_KillUpgrade".Translate(killerName, weapon.LabelShortCap));
		}

		static void Show(TaggedString text)
		{
			string message = text;
			if (message.NullOrEmpty())
			{
				return;
			}

			Messages.Message(message, MessageTypeDefOf.PositiveEvent, false);
			SoundStarter.PlayOneShotOnCamera(SoundDefOf.TinyBell, null);
		}
	}
}
