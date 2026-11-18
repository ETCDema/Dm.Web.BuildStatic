using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dm.Web.BuildStatic.Models;
using Dm.Web.BuildStatic.Services;

namespace Dm.Web.BuildStatic;

internal class Program
{
	private static readonly string _DEFAULT_CFG_NAME	= "dm.web.build.static.json";

	private static StateCodes _returnCode		= StateCodes.Ok;

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
			return (int)StateCodes.NoConfig;
		}

		var cfgFullPath			= Path.GetFullPath(cfgName);
		var cfgHash             = _buildCfgHash(cfgFullPath);
		var statusPath          = _statusPath(cfgHash);

		// Один именованный Mutex на каждый уникальный путь конфига.
		using var mutex			= new Mutex(initiallyOwned: false, name: _buildMutexName(cfgHash));
		var doWork				= false;
		var acquired			= false;
		var abandoned			= false;

		try
		{
			// Сначала пробуем без ожидания — вдруг мы первые
			try
			{
				acquired        = mutex.WaitOne(TimeSpan.Zero);
				doWork          = acquired;
			} catch (AbandonedMutexException)
			{
				// прошлый владелец умер — мьютекс теперь наш
				acquired        = true;
				abandoned		= true;
			}

			if (!acquired)
			{
				Console.WriteLine($"⚠️ Config {cfgName} processed by another process - waiting for completion...");
				try
				{
					// блокируемся до освобождения
					mutex.WaitOne();
					acquired    = true;
				} catch (AbandonedMutexException)
				{
					acquired    = true;
					abandoned   = true;
				}
			}

			if (doWork)
			{
				try
				{
					_writeStatus(statusPath, StateCodes.Running);
					var code	= StateCodes.MeFail;
					try
					{
						code	= _run(cfgName);
					} finally
					{
						_writeStatus(statusPath, code);
					}
					return (int)code;
				} catch
				{
					// _run бросил необработанное — статус StateCodes.MeFail уже записан,
					// мьютекс будет отпущен в finally.
					throw;
				}
			}
			
			var state			= abandoned ? StateCodes.Abandoned : _readStatus(statusPath);

			if (state==StateCodes.Ok)
			{
				Console.WriteLine($"✔️ Other process done.");
				return (int)StateCodes.Ok;
			} else
			{
				Console.WriteLine($"❌ Another process FAIL: {state?.ToString() ?? "-NO RESULT-"}.");
				return (int)StateCodes.OtherFail;
			}

		} finally
		{
			if (acquired)
			{
				try { mutex.ReleaseMutex(); } catch { /* уже отпущен — ничего не делаем */ }
			}
		}
	}

	private static string _buildCfgHash(string cfgFullPath)
	{
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cfgFullPath)));
	}

	private static string _buildMutexName(string hash)
	{
		// "Global\" — только Windows (терминальные сессии). На Linux префикс не поддерживается.
		var prefix				= OperatingSystem.IsWindows() ? "Global\\" : "";
		return $"{prefix}Dm.Web.BuildStatic.{hash}";
	}

	private static string _statusPath(string hash)
	{
		var dir					= Path.Combine(Path.GetTempPath(), "Dm.Web.BuildStatic");
		Directory.CreateDirectory(dir);
		return Path.Combine(dir, hash + ".status");
	}

	private static void _writeStatus(string path, StateCodes state)
	{
		var tmp					= $"{path}.tmp.{Environment.ProcessId}";
		File.WriteAllText(tmp, state.ToString());
		File.Move(tmp, path, overwrite: true);					// атомарная замена
	}

	private static StateCodes? _readStatus(string path)
	{
		try { return Enum.Parse<StateCodes>(File.ReadAllText(path)); } catch { return null; }
	}

	private static StateCodes _run(string cfgName)
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
			//throw new Exception("Test");

			// Отпускаем файл...
			reader.Close();
		} catch (IOException ex)
		{
			if (tryReadConfig && (ex.HResult==-2147024864 /* == Windows: 0x80070020 Sharing violation */ || ex.HResult==11 /* Linux: The process cannot access the file ... because it is being used by another process. */))
			{
				Console.WriteLine($"❌ Config {ex.HResult} sharing violation");
				_returnCode     = StateCodes.SharingViolation;
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
			Interlocked.CompareExchange(ref _returnCode, StateCodes.MeFail, StateCodes.Ok);
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
