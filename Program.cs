using System.Diagnostics;
using System.Text.Json;

using Dm.Web.BuildStatic.Services;
using Dm.Web.BuildStatic.Services.Core;

namespace Dm.Web.BuildStatic;

internal class Program
{
	private static readonly string _DEFAULT_CFG_NAME	= "dm.web.build.static.json";

	private static int _returnCode		= 0;

	static int Main(string[] args)
	{
		var cfgName             = args.Length>0 ? args[0] : null;
		if (string.IsNullOrEmpty(cfgName)) cfgName = _DEFAULT_CFG_NAME;

		if (!File.Exists(cfgName))
		{
			Console.WriteLine("🛠️ .NET Pre-build tool for static resources");
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
			var config          = JsonDocument.Parse(reader).RootElement;

			Console.WriteLine($"🛠️ Pre-build static resources using config file {cfgName}...");

			var timer           = Stopwatch.StartNew();
			var builders        = new StageProvider();
			var pipelines       = new List<Pipeline>();
			var parallel        = true;

			foreach (var prop in config.EnumerateObject())
			{
				switch (prop.Name)
				{
					case "parallel":	parallel = prop.Value.GetBoolean(); break;
					case "use":			builders.Configure(prop.Value); break;
					case "pipelines":	_readPipelines(prop.Value, builders, pipelines); break;
					default:			throw new NotSupportedException($"Property {prop.Name} at root not supported");
				}
			}

			tryReadConfig		= false;
			
			if (pipelines.Count==0)
			{
				Console.WriteLine($"🚫 No pipelines");
			} else if (parallel)
			{
				// Запускаем конвейеры параллельно
				Parallel.ForEach(pipelines, _safeRun);
				timer.Stop();
				Console.WriteLine($"✔️ Done {timer.Elapsed}");
			} else
			{
				// Запускаем конвейеры последовательно
				foreach (var pipeline in pipelines)
				{
					Console.WriteLine($"⌛ {pipeline.ID}...");
					_safeRun(pipeline);
				}
				timer.Stop();
				Console.WriteLine($"✔️ Done {timer.Elapsed}");
			}

			// Отпускаем файл...
			reader.Close();
		} catch (IOException ex)
		{
			if (tryReadConfig && (ex.HResult==-2147024864 /* == Windows: 0x80070020 Sharing violation */ || ex.HResult==11 /* Linux: The process cannot access the file ... because it is being used by another process. */))
				_waitForCompletion(cfgName);
			else
			{
				Console.WriteLine($"❌ HResult: {ex.HResult}");
				throw;
			}
		}

		return _returnCode;
	}

	private static void _safeRun(Pipeline pipeline)
	{
		try
		{
			pipeline.Run(null!);
		} catch (Exception ex)
		{
			Console.WriteLine($"🛑 Pipeline {pipeline.ID} FAIL: {ex.Message}");
			Interlocked.CompareExchange(ref _returnCode, 2, 0);
		}
	}

	private static void _readPipelines(JsonElement value, StageProvider builders, List<Pipeline> pipelines)
	{
		var i					= 1;
		foreach (var item in value.EnumerateArray())
		{
			pipelines.Add(new Pipeline($"Main[{i++}]", null, item.EnumerateObject(), builders));
		}
	}

	private static void _waitForCompletion(string cfgName)
	{
		Console.WriteLine($"⚠️ Config {cfgName} locked by another process - waiting for completion...");
		// Ожидать окончания блокировки файла т.к. он готовит нужное нам

		while (true)
		{
			Thread.Sleep(100);
			try
			{
				using var reader= new FileStream(cfgName, FileMode.Open, FileAccess.Read, FileShare.None);
				reader.Close();
				Console.WriteLine($"✔️ Done");
				return;
			} catch (IOException ex)
			{
				if (ex.HResult!=-2147024864 /* Windows sharing violation */ && ex.HResult!=11 /* Linux sharing violation */)
					throw;
			}
		}
	}
}
