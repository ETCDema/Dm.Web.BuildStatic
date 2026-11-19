using System.Text.Json;

using Dm.Web.BuildStatic.Stages.Core;

namespace Dm.Web.BuildStatic.Services
{
	internal class StageProvider
	{
		private readonly Dictionary<string, Func<JsonElement, IPipelineStage>> _builders = [];

		internal void Configure(JsonElement cfg)
		{
			foreach (var prop in cfg.EnumerateObject())
			{
				var type            = Type.GetType(prop.Value.GetString()!, true)!;
				_builders.Add(prop.Name, (cfg) => (IPipelineStage)Activator.CreateInstance(type, cfg, this)!);
			}
		}

		public IPipelineStage? TryBuildStage(string name, JsonElement cfg)
		{
			return name!=null && _builders.TryGetValue(name, out var fx) ? fx(cfg) : null;
		}

		public IPipelineStage BuildStage(string name, JsonElement cfg)
		{
			return TryBuildStage(name, cfg)
				?? throw new NotSupportedException($"Stage {name} not defined in \"use {{ ... }}\" section.");
		}
	}
}
