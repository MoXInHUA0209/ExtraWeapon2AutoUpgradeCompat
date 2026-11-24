using System;
using Verse;

namespace EWAutoUpgradeCompat
{
	/// <summary>
	/// 入口。本类只负责"什么时候装补丁"，不含任何业务规则；
	/// 规则与适配分别落在 Patch_* 与 *_Bridge 里。
	/// </summary>
	public class EWAutoUpgradeCompatMod : Mod
	{
		internal const string HarmonyId = "yg.extraweapon2.autoupgradecompat";
		internal const string LogTag = "[EW2xAutoUpgrade] ";

		public EWAutoUpgradeCompatMod(ModContentPack content) : base(content)
		{
			try
			{
				AutoUpgradeCompatPatcher.Apply();
			}
			catch (Exception ex)
			{
				// 补丁装不上时本 mod 静默失效即可，不能把启动流程带崩。
				Log.Error(LogTag + "补丁安装失败，本兼容模组不生效：" + ex);
			}
		}
	}
}
