using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Hoxise_AutoUpgrade;
using Verse;

namespace EWAutoUpgradeCompat
{
	/// <summary>
	/// 读取历久弥新（AutoUpgrade）的状态。原则：能走 public API 的一律走 public API，
	/// 只有确实私有的字段才反射，而且每个都有兜底值——兜底时功能降级但不报错、不刷屏。
	///
	/// public：UsageRecord.GetRecordDic / GetEquipmentKey、ProbabilityUtils.GetUpgradeProbability、
	///         CommonUtils.GetNextQuality、KillUpgradeUtils.NeedKillCount、AutoUpgradeMod.GetSettings。
	/// 私有（反射）：UsageRecord.RecordInterval / LastRecordTick、CompKillTracker.killCount。
	/// </summary>
	internal static class AutoUpgradeBridge
	{
		// 与上游 RecordInterval 的默认值一致；反射失败时用它，行为差别只是记录节奏。
		const int FallbackRecordInterval = 30000;

		static bool fieldsResolved;
		static FieldInfo recordIntervalField;
		static FieldInfo lastRecordTickField;
		static FieldInfo killCountField;
		static bool killCountWarned;

		static void ResolveFields()
		{
			if (fieldsResolved)
			{
				return;
			}

			fieldsResolved = true;
			recordIntervalField = AccessTools.Field(typeof(UsageRecord), "RecordInterval");
			lastRecordTickField = AccessTools.Field(typeof(UsageRecord), "LastRecordTick");
			killCountField = AccessTools.Field(typeof(CompKillTracker), "killCount");
		}

		/// <summary>上游设置页里的数值；拿不到返回 null（调用方当"未启用"处理）。</summary>
		internal static AutoUpgradeModSettings Settings()
		{
			try
			{
				AutoUpgradeMod mod = AutoUpgradeMod.Instance;
				return mod?.GetSettings<AutoUpgradeModSettings>();
			}
			catch (Exception ex)
			{
				LogOnce.Warn("读取 AutoUpgrade 设置失败：", ex);
				return null;
			}
		}

		/// <summary>
		/// 上游的"佩戴时长"字典（键为 Pawn.ThingID + "_" + Thing.ThingID，值为累计 tick）。
		/// 它由上游自己存档，因此往这里写条目等于免费获得持久化，不需要本 mod 另存一份状态。
		/// </summary>
		internal static Dictionary<string, int> UsageRecords()
		{
			try
			{
				// UsageRecord.Instance 在载入地图前为 null，GetRecordDic 会抛空引用。
				return UsageRecord.Instance == null ? null : UsageRecord.GetRecordDic();
			}
			catch (Exception ex)
			{
				LogOnce.Warn("读取 AutoUpgrade 佩戴记录失败：", ex);
				return null;
			}
		}

		internal static int RecordInterval()
		{
			ResolveFields();
			if (recordIntervalField != null && recordIntervalField.GetValue(null) is int value && value > 0)
			{
				return value;
			}

			return FallbackRecordInterval;
		}

		internal static int LastRecordTick()
		{
			ResolveFields();
			if (lastRecordTickField != null && lastRecordTickField.GetValue(null) is int value)
			{
				return value;
			}

			return 0;
		}

		/// <summary>武器累计击杀数；读不到返回 -1（调用方据此跳过击杀升级，时间升级不受影响）。</summary>
		internal static int KillCount(CompKillTracker tracker)
		{
			ResolveFields();
			if (killCountField == null)
			{
				if (!killCountWarned)
				{
					killCountWarned = true;
					Log.Warning(EWAutoUpgradeCompatMod.LogTag + "读不到 CompKillTracker.killCount，额外武器槽的击杀升级关闭。");
				}

				return -1;
			}

			try
			{
				return killCountField.GetValue(tracker) is int count ? count : -1;
			}
			catch (Exception)
			{
				return -1;
			}
		}
	}

	/// <summary>同一条问题只报一次，避免 60 秒一轮的检查把日志刷满。</summary>
	internal static class LogOnce
	{
		static readonly HashSet<string> warneds = new HashSet<string>();

		internal static void Warn(string message, Exception ex)
		{
			string key = string.Concat(message, ex?.GetType().Name);
			if (!warneds.Add(key))
			{
				return;
			}

			Log.Warning(string.Concat(EWAutoUpgradeCompatMod.LogTag, message, ex?.ToString()));
		}
	}
}
