using System;
using System.Collections.Generic;
using Hoxise_AutoUpgrade;
using Verse;

namespace EWAutoUpgradeCompat
{
	/// <summary>
	/// 通路一：让额外武器槽里的武器也进入历久弥新的"携带时长"记录。
	///
	/// 上游 UsageRecord.UpdateEquipmentUsage 只遍历 Find.Maps 中本土地图上自由殖民者的
	/// equipment.AllEquipmentListForReading 与 apparel.WornApparel；
	/// 额外武器被 ThingOwner 持有，既不在前者也不在后者，于是它们的键永远不存在，
	/// 上游 TryUpgradeEquipmentQuality 一开始的 ContainsKey 就会失败返回。
	///
	/// 这里在同一个节奏（同一个方法、同一份 delta）里补上这些条目：
	///   - 键用上游自己的 UsageRecord.GetEquipmentKey，格式不会走样；
	///   - 写进上游自己的字典，持久化由它负责，本 mod 不另存状态；
	///   - delta 复刻上游 Math.Max(RecordInterval, TicksGame - LastRecordTick)，
	///     首轮"一次补满"的行为与上游对普通装备完全一致，不会凭空多算。
	/// </summary>
	internal static class Patch_UsageRecord_ExtraWeapons
	{
		static bool loggedFirstSync;

		internal static void Prefix(out int __state)
		{
			__state = AutoUpgradeBridge.LastRecordTick();
		}

		internal static void Postfix(int __state)
		{
			try
			{
				Dictionary<string, int> records = AutoUpgradeBridge.UsageRecords();
				if (records == null)
				{
					return;
				}

				int delta = Math.Max(AutoUpgradeBridge.RecordInterval(), Find.TickManager.TicksGame - __state);
				if (delta <= 0)
				{
					return;
				}

				List<Map> maps = Find.Maps;
				if (maps == null)
				{
					return;
				}

				int syncedWeapons = 0;
				for (int i = 0; i < maps.Count; i++)
				{
					Map map = maps[i];
					// 与上游 <>c.<UpdateEquipmentUsage>b__9_0 一致：只看本土地图。
					if (map == null || !map.IsPlayerHome)
					{
						continue;
					}

					syncedWeapons += AccrueOnMap(map, records, delta);
				}

				// 一次性运行期自检：真的记录到槽位武器才报，避免把"没装 EW2"和
				// "这一刻还没人挂额外武器"混为一谈（Def 扫描在 StaticConstructorOnStartup
				// 阶段与 EW2 的注入顺序无关，反倒可能误判）。
				if (!loggedFirstSync && syncedWeapons > 0)
				{
					loggedFirstSync = true;
					Log.Message(string.Concat(
						EWAutoUpgradeCompatMod.LogTag, "额外武器槽已接入携带时长记录（本次同步 ",
						syncedWeapons.ToString(), " 把武器）。"));
				}
			}
			catch (Exception ex)
			{
				LogOnce.Warn("记录额外武器携带时长失败：", ex);
			}
		}

		static int AccrueOnMap(Map map, Dictionary<string, int> records, int delta)
		{
			List<Pawn> colonists = map.mapPawns?.FreeColonists;
			if (colonists == null)
			{
				return 0;
			}

			int synced = 0;
			for (int i = 0; i < colonists.Count; i++)
			{
				Pawn pawn = colonists[i];
				List<ExtraWeaponData> slots = ExtraWeaponBridge.GetSlotWeapons(pawn);
				if (slots == null)
				{
					continue;
				}

				for (int s = 0; s < slots.Count; s++)
				{
					ThingWithComps weapon = ExtraWeaponBridge.GetWeapon(slots[s]);
					if (weapon == null)
					{
						continue;
					}

					string key = UsageRecord.GetEquipmentKey(pawn, weapon);
					records.TryGetValue(key, out int ticks);
					records[key] = ticks + delta;
					synced++;
				}
			}

			return synced;
		}
	}
}
