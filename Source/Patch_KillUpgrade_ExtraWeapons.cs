using System;
using System.Collections.Generic;
using Hoxise_AutoUpgrade;
using RimWorld;
using Verse;

namespace EWAutoUpgradeCompat
{
	/// <summary>
	/// 通路三：击杀计数升级。
	///
	/// 上游 KillRecord.Postfix 挂了 Pawn.Kill，但只认 dinfo.Instigator.equipment.Primary，
	/// 所以用额外武器槽里的枪打死人时，那把枪的 CompKillTracker 一动不动（物品信息里一直显示 0）。
	///
	/// 这里补一条同样的后置补丁，把击杀归到"实际开火的那把槽位武器"上：
	///   - 伤害里只有武器 ThingDef，没有实例引用，所以候选是"def 相同的槽位武器"；
	///   - 同型号多把时取 LastAttackTargetTick 最大的那把（即刚刚开火的那把）；
	///   - 计数写进上游自己的 CompKillTracker.IncrementKillCount（公开方法），
	///     于是武器信息里的击杀数、阈值都可正常显示与推进；
	///   - 阈值与升级判定沿用上游 KillUpgradeUtils.NeedKillCount + CommonUtils.GetNextQuality，
	///     顺序也与上游一致：先计数，再看是否够格。
	///
	/// 已知取舍：同型号武器并存时按"最近开火"归属，极端情况下可能算到同型号的另一把上；
	/// 只影响哪一把升品质，不影响总收益。
	/// </summary>
	internal static class Patch_KillUpgrade_ExtraWeapons
	{
		internal static void Postfix(Pawn __instance, DamageInfo? dinfo)
		{
			try
			{
				if (!dinfo.HasValue)
				{
					return;
				}

				DamageInfo info = dinfo.Value;
				Pawn killer = info.Instigator as Pawn;
				if (killer == null || killer == __instance)
				{
					return;
				}

				ThingDef weaponDef = info.Weapon;
				if (weaponDef == null)
				{
					return;
				}

				AutoUpgradeModSettings settings = AutoUpgradeBridge.Settings();
				if (settings == null || !settings.SwitchKillUpgrade)
				{
					return;
				}

				ExtraWeaponData slot = FindFiringSlotWeapon(killer, weaponDef);
				ThingWithComps weapon = ExtraWeaponBridge.GetWeapon(slot);
				if (weapon == null)
				{
					return;
				}

				CompKillTracker tracker = weapon.TryGetComp<CompKillTracker>();
				CompQuality quality = weapon.TryGetComp<CompQuality>();
				if (tracker == null || quality == null)
				{
					return;
				}

				// 上游顺序：先计数，再判断（传说品质也会继续计数，只是不再升级）。
				tracker.IncrementKillCount();

				QualityCategory current = quality.Quality;
				QualityCategory next = CommonUtils.GetNextQuality(current);
				if (next == current)
				{
					return;
				}

				int kills = AutoUpgradeBridge.KillCount(tracker);
				if (kills < 0 || kills < KillUpgradeUtils.NeedKillCount(current))
				{
					return;
				}

				quality.SetQuality(next, ArtGenerationContext.Colony);
				CompatMessages.AnnounceKillUpgrade(killer, weapon);
			}
			catch (Exception ex)
			{
				LogOnce.Warn("额外武器槽击杀升级失败：", ex);
			}
		}

		static ExtraWeaponData FindFiringSlotWeapon(Pawn killer, ThingDef weaponDef)
		{
			List<ExtraWeaponData> slots = ExtraWeaponBridge.GetSlotWeapons(killer);
			if (slots == null)
			{
				return null;
			}

			ExtraWeaponData best = null;
			int bestTick = int.MinValue;
			for (int i = 0; i < slots.Count; i++)
			{
				ExtraWeaponData slot = slots[i];
				ThingWithComps weapon = ExtraWeaponBridge.GetWeapon(slot);
				if (weapon == null || weapon.def != weaponDef)
				{
					continue;
				}

				int lastAttackTick = slot.LastAttackTargetTick;
				if (best == null || lastAttackTick >= bestTick)
				{
					best = slot;
					bestTick = lastAttackTick;
				}
			}

			return best;
		}
	}
}
