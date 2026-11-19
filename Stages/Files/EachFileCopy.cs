using System.Text.Json;

using Dm.Web.BuildStatic.Services;

namespace Dm.Web.BuildStatic.Stages.Files
{
	internal class EachFileCopy(JsonElement cfg, StageProvider builders)
		: FileCopy(cfg, builders)
	{
		public override Type ParamType	=> typeof(IEnumerable<string>);

		public override Type ResultType => typeof(IEnumerable<string>);

		public override void Run(object param, Action<object> next)
		{
			var srcNames        = (IEnumerable<string>)param;
			var dstNames        = new List<string>();

			foreach (var srcName in srcNames)
			{
				var dstName     = DstName(srcName);
				if (CopyFile(srcName, dstName)) dstNames.Add(dstName);
			}
			next(dstNames);
		}

		public override void Finalize(object param)
		{
			var srcNames        = (IEnumerable<string>)param;
			foreach (var srcName in srcNames)
			{
				var dstName     = DstName(srcName);
				CopyFile(srcName, dstName);
			}
		}
	}
}
