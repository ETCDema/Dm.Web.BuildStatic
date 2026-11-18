using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dm.Web.BuildStatic.Services;

namespace Dm.Web.BuildStatic;

internal class Program
{
	private static readonly string _DEFAULT_CFG_NAME	= "dm.web.build.static.json";

	private static readonly int _NO_CONFIG				= 1;
	private static readonly int _ME_FAIL				= 2;
	private static readonly int _OTHER_FAIL				= 3;
	private static readonly int _CFG_SHARING_VIOLATION	= 4;

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
			return _NO_CONFIG;
		}

		// Один именованный Mutex на каждый уникальный путь конфига.
		using var mutex			= new Mutex(initiallyOwned: false, name: _buildMutexName(cfgName));
		bool doWork             = false;
		bool acquired			= false;

		try
		{
			// Сначала пробуем без ожидания — вдруг мы первые
			try
			{
				acquired		= mutex.WaitOne(TimeSpan.Zero);
				doWork          = acquired;
			} catch (AbandonedMutexException)
			{
				// прошлый владелец умер — мьютекс теперь наш
				acquired        = true;
				Console.WriteLine("❌ Another process FAILED.");
				return _OTHER_FAIL;
			}

			if (!acquired)
			{
				Console.WriteLine($"⚠️ Config {cfgName} processed by another process - waiting for completion...");
				try
				{
					// блокируемся до освобождения
					mutex.WaitOne();
					acquired	= true;
				} catch (AbandonedMutexException)
				{
					acquired	= true;
					Console.WriteLine("❌ Another process FAILED.");
					return _OTHER_FAIL;
				}
			}

			return doWork ? _run(cfgName) : _returnCode;
		} finally
		{
			if (acquired)
			{
				try { mutex.ReleaseMutex(); } catch { /* уже отпущен — ничего не делаем */ }
			}
		}
	}

	private static string _buildMutexName(string cfgName)
	{
		// Абсолютный путь — чтобы разные относительные пути к одному файлу
		// (./config.json и ../app/config.json) давали одно имя.
		var fullPath			= Path.GetFullPath(cfgName);
		var hash				= Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath)));

		// "Global\" — только Windows (терминальные сессии). На Linux префикс не поддерживается.
		var prefix				= OperatingSystem.IsWindows() ? "Global\\" : "";
		return $"{prefix}DmWeb.BuildStatic.{hash}";
	}

	private static int _run(string cfgName)
	{
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

			//Console.ReadKey();

			// Отпускаем файл...
			reader.Close();
		} catch (IOException ex)
		{
			if (tryReadConfig && (ex.HResult==-2147024864 /* == Windows: 0x80070020 Sharing violation */ || ex.HResult==11 /* Linux: The process cannot access the file ... because it is being used by another process. */))
			{
				Console.WriteLine($"❌ Config {ex.HResult} sharing violation");
				_returnCode     = _CFG_SHARING_VIOLATION;
			} else
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
			Interlocked.CompareExchange(ref _returnCode, _ME_FAIL, 0);
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
}
