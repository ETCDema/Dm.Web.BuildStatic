namespace Dm.Web.BuildStatic.Services.Stages
{
	internal class EachFileCopy(string nameTemplate, Action<object>? fx)
		: FileCopy(nameTemplate, fx)
	{
		public new class Builder: FileCopy.Builder
		{
			protected override FileCopy Build(string nameTemplate, Action<object>? fx)
			{
				return new EachFileCopy(nameTemplate, fx);
			}
		}

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
