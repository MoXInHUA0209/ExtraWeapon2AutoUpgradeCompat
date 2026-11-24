using System;
using System.Collections.Generic;
using Hoxise_AutoUpgrade;
using RimWorld;
using Verse;

namespace EWAutoUpgradeCompat
{
	/// <summary>
	/// 通路二：时间概率升级。
	///
	/// 上游 ExecuteQualityUpgradeLogic 只对 equipment.Primary 与 WornApparel 掷骰，
	/// 额外武器槽的武器即使记录里有佩戴时长也永远不会被检查到。
	/// 这里在同一方法的后置补丁里补一轮同样的掷骰：
	///   - 概率完全调用上游 ProbabilityUtils.GetUpgradeProbability（受设置页 BaseProbability 影响），
	///   - 品质档位完全调用上游 CommonUtils.GetNextQuality，
	///   - 门槛与上游相同：Find.CurrentMap 必须是本土地图、只算自由殖民者，
	///     这样额外武器槽的手感与普通装备一致，而不是另开一套规则。
	/// </summary>
	internal static class Patch_QualityUpgrade_ExtraWeapons
	{
		// 上游 TryUpgradeEquipmentQuality 用的换算：60000 tick = 1 天。
		const float TicksPerDay = 60000f;

		internal static void Postfix()
		{
			try
			{
				Map map = Find.CurrentMap;
				if (map == null || !map.IsPlayerHome)
				{
					return;
				}

				List<Pawn> colonists = map.mapPawns?.FreeColonists;
				if (colonists == null || colonists.Count == 0)
				{
					return;
				}

				Dictionary<string, int> records = AutoUpgradeBridge.UsageRecords();
				if (records == null)
				{
					return;
				}

				List<string> upgraded = null;
				for (int i = 0; i < colonists.Count; i++)
				{
					Pawn pawn = colonists[i];
					if (!TryUpgradePawnWeapons(pawn, records))
					{
						continue;
					}

					if (upgraded == null)
					{
						upgraded = new List<string>();
					}

					upgraded.Add(pawn.Name.ToStringShort);
				}

				if (upgraded != null)
				{
					CompatMessages.AnnounceSlotUpgrade(upgraded);
				}
			}
			catch (Exception ex)
			{
				LogOnce.Warn("额外武器槽品质检查失败：", ex);
			}
		}

		static bool TryUpgradePawnWeapons(Pawn pawn, Dictionary<string, int> records)
		{
			List<ExtraWeaponData> slots = ExtraWeaponBridge.GetSlotWeapons(pawn);
			if (slots == null)
			{
				return false;
			}

			bool upgradedAny = false;
			for (int i = 0; i < slots.Count; i++)
			{
				ThingWithComps weapon = ExtraWeaponBridge.GetWeapon(slots[i]);
				if (weapon == null)
				{
					continue;
				}

				CompQuality quality = weapon.TryGetComp<CompQuality>();
				if (quality == null)
				{
					continue;
				}

				QualityCategory current = quality.Quality;
				QualityCategory next = CommonUtils.GetNextQuality(current);
				if (next == current)
				{
					continue;
				}

				if (!records.TryGetValue(UsageRecord.GetEquipmentKey(pawn, weapon), out int ticks) || ticks <= 0)
				{
					continue;
				}

				float usageDays = ticks / TicksPerDay;
				float chance = ProbabilityUtils.GetUpgradeProbability(usageDays, current);
				if (Rand.Value >= chance)
				{
					continue;
				}

				// 与上游相同的调用：ArtGenerationContext.Colony。
				quality.SetQuality(next, ArtGenerationContext.Colony);
				upgradedAny = true;
			}

			return upgradedAny;
		}
	}
}
