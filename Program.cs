using System.Text.Json;

using Dm.Web.BuildStatic.Services;
using Dm.Web.BuildStatic.Services.Core;

namespace Dm.Web.BuildStatic;

internal class Program
{
	private static readonly string _DEFAULT_CFG_NAME	= "dm.web.build.static.json";

	static int Main(string[] args)
	{
		Console.WriteLine("🛠️ .NET Pre-build tool for static resources");

		var cfgName             = args.Length>0 ? args[0] : null;
		if (string.IsNullOrEmpty(cfgName)) cfgName = _DEFAULT_CFG_NAME;

		if (!File.Exists(cfgName))
		{
			Console.WriteLine($"❌ Config file {cfgName} not found.");
			Console.WriteLine("Usage: Dm.Web.BuildStatic [config.json]");
			Console.WriteLine($"By default try using {_DEFAULT_CFG_NAME} config from current folder");
			return 1;
		}

		// Читаем конфигурацию: нам нужны сначало генераторы этапов а потом конвейеры, при построении конвейеров используются генераторы этапов.
		// Блокируем файл для исключения конкурирующих обработок
		var tryReadConfig       = true;
		try
		{
			using var reader    = new FileStream(cfgName, FileMode.Open, FileAccess.Read, FileShare.None);
			tryReadConfig       = false;
			var config          = JsonDocument.Parse(reader).RootElement;

			var builders        = new Dictionary<string, IStageBuilder>();
			var pipelines       = new List<Pipeline>();

			foreach (var prop in config.EnumerateObject())
			{
				switch (prop.Name)
				{
					case "use": _readBuilders(prop.Value, builders); break;
					case "pipelines": _readPipelines(prop.Value, builders, pipelines); break;
					default: throw new NotSupportedException($"Property {prop.Name} at root not supported");
				}
			}

			// Запускаем конвейеры последовательно
			foreach (var pipeline in pipelines)
			{
				pipeline.Run();
			}

			// Отпускаем файл...
			reader.Close();
		} catch (IOException ex)
		{
			if (tryReadConfig && ex.HResult==-2147024864 /* == 0x80070020 Sharing violation */)
				Console.WriteLine("⚠️ Config locked by another process - exit");
			else
				throw;
		}

		return 0;
	}

	private static void _readBuilders(JsonElement cfg, Dictionary<string, IStageBuilder> builders)
	{
		foreach (var prop in cfg.EnumerateObject())
		{
			var type			= Type.GetType(prop.Value.GetString()!, true)!;
			builders.Add(prop.Name, (IStageBuilder)Activator.CreateInstance(type)!);
		}
	}

	private static void _readPipelines(JsonElement value, Dictionary<string, IStageBuilder> builders, List<Pipeline> pipelines)
	{
		foreach (var item in value.EnumerateArray())
		{
			var pipeline        = new Pipeline();
			_readPipeline(item, builders, pipeline);
			pipelines.Add(pipeline);
		}
	}

	private static void _readPipeline(JsonElement cfg, Dictionary<string, IStageBuilder> builders, Pipeline pipeline)
	{
		foreach (var prop in cfg.EnumerateObject())
		{
			var builder         = builders[prop.Name];
			builder.Build(prop.Value, pipeline);
		}
	}
}
