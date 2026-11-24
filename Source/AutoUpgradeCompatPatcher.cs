using System;
using System.Collections.Generic;
using System.Reflection;
using Hoxise_AutoUpgrade;
using HarmonyLib;
using Verse;

namespace EWAutoUpgradeCompat
{
	/// <summary>
	/// 三个补丁，分别对应历久弥新的三条既有通路：
	///   1) 携带时长累计  —— UsageRecord.UpdateEquipmentUsage（私有）
	///   2) 时间概率升级  —— AutoUpgradeComp.ExecuteQualityUpgradeLogic（私有）
	///   3) 击杀计数升级  —— Pawn.Kill 后置（上游同样只认 equipment.Primary，这里补额外武器槽）
	///
	/// 两点刻意设计：
	///   - 手动安装而不是 PatchAll：挂点用名字解析，目标缺失只丢一条警告，不是整类失效；
	///   - 每条通路各自 try/catch：某一条在运行时安装失败（例如自定义运行时对补丁目标有限制）
	///     不会把另外两条带下水。
	/// </summary>
	internal static class AutoUpgradeCompatPatcher
	{
		internal static readonly List<string> Problems = new List<string>();

		internal static void Apply()
		{
			Harmony harmony = new Harmony(EWAutoUpgradeCompatMod.HarmonyId);
			Problems.Clear();
			int applied = 0;

			// 1) 携带时长累计
			if (Patch(harmony, "UsageRecord.UpdateEquipmentUsage",
				() => AccessTools.Method(typeof(UsageRecord), "UpdateEquipmentUsage"),
				prefixType: typeof(Patch_UsageRecord_ExtraWeapons), prefixName: nameof(Patch_UsageRecord_ExtraWeapons.Prefix),
				postfixType: typeof(Patch_UsageRecord_ExtraWeapons), postfixName: nameof(Patch_UsageRecord_ExtraWeapons.Postfix)))
			{
				applied++;
			}

			// 2) 时间概率升级
			if (Patch(harmony, "AutoUpgradeComp.ExecuteQualityUpgradeLogic",
				() => AccessTools.Method(typeof(AutoUpgradeComp), "ExecuteQualityUpgradeLogic"),
				postfixType: typeof(Patch_QualityUpgrade_ExtraWeapons), postfixName: nameof(Patch_QualityUpgrade_ExtraWeapons.Postfix)))
			{
				applied++;
			}

			// 3) 击杀计数升级
			if (Patch(harmony, "Verse.Pawn.Kill",
				() => AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill), new[] { typeof(DamageInfo?), typeof(Hediff) }),
				postfixType: typeof(Patch_KillUpgrade_ExtraWeapons), postfixName: nameof(Patch_KillUpgrade_ExtraWeapons.Postfix)))
			{
				applied++;
			}

			for (int i = 0; i < Problems.Count; i++)
			{
				Log.Warning(string.Concat(EWAutoUpgradeCompatMod.LogTag, Problems[i]));
			}

			Log.Message(string.Concat(EWAutoUpgradeCompatMod.LogTag, "补丁已安装 ", applied.ToString(), "/3。"));
		}

		static bool Patch(
			Harmony harmony,
			string label,
			Func<MethodInfo> resolve,
			Type prefixType = null,
			string prefixName = null,
			Type postfixType = null,
			string postfixName = null)
		{
			MethodInfo target;
			try
			{
				target = resolve();
			}
			catch (Exception ex)
			{
				Problems.Add(string.Concat("解析挂点 ", label, " 失败（", ex.GetType().Name, "），该通路关闭。"));
				return false;
			}

			if (target == null)
			{
				Problems.Add(string.Concat("找不到上游方法 ", label, "，该通路关闭（多半是上游更新改了成员名）。"));
				return false;
			}

			try
			{
				harmony.Patch(
					target,
					prefix: prefixType == null ? null : new HarmonyMethod(AccessTools.Method(prefixType, prefixName)),
					postfix: postfixType == null ? null : new HarmonyMethod(AccessTools.Method(postfixType, postfixName)));
				return true;
			}
			catch (Exception ex)
			{
				Problems.Add(string.Concat("给 ", label, " 打补丁失败（", ex.GetType().Name, "：", ex.Message, "），该通路关闭。"));
				return false;
			}
		}
	}
}
