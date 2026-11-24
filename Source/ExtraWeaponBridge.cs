using System.Collections.Generic;
using Verse;

namespace EWAutoUpgradeCompat
{
	/// <summary>
	/// ExtraWeapon2 适配层：对 EW2 类型的直接引用全部收敛在这里。
	///
	/// 事实依据（读 EW2 1.7 程序集得到）：
	///   - EW2 在 PatchMain 里给所有 humanlike ThingDef 注入了 CompProperties_ExtraWeapon，
	///     所以 TryGetComp&lt;ExtraWeaponComponent&gt;() 是取槽位的正规途径；
	///   - 槽位武器被 ThingOwner 持有，不出现在 Pawn_EquipmentTracker.AllEquipmentListForReading 里，
	///     这正是历久弥新漏掉它们的根本原因；
	///   - ExtraWeaponData.Weapon 在武器被销毁时返回 null，可直接当有效性判断用。
	/// </summary>
	internal static class ExtraWeaponBridge
	{
		/// <summary>取某小人六个额外武器槽的数据。取不到返回 null（上游缺失、非人形、或异常）。</summary>
		internal static List<ExtraWeaponData> GetSlotWeapons(Pawn pawn)
		{
			if (pawn == null || pawn.Destroyed)
			{
				return null;
			}

			try
			{
				global::ExtraWeaponComponent component = pawn.TryGetComp<global::ExtraWeaponComponent>();
				return component?.Weapons;
			}
			catch (System.Exception ex)
			{
				LogOnce.Warn("读取 ExtraWeapon2 槽位失败：", ex);
				return null;
			}
		}

		/// <summary>槽位里的武器实例；空槽或已销毁返回 null。</summary>
		internal static ThingWithComps GetWeapon(ExtraWeaponData slot)
		{
			if (slot == null)
			{
				return null;
			}

			try
			{
				return slot.Weapon as ThingWithComps;
			}
			catch (System.Exception ex)
			{
				LogOnce.Warn("读取额外武器实例失败：", ex);
				return null;
			}
		}
	}
}
