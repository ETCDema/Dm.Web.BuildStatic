using System.Text.Json;

using Dm.Web.BuildStatic.Models;
using Dm.Web.BuildStatic.Services.Core;

namespace Dm.Web.BuildStatic.Services.Stages;

/// <summary>
/// Копирование из существующего файла в новый. Если новый файл существует и исходный файл старше нового копирование выполняться не будет.
/// </summary>
/// <remarks>Если этап последний, то он просто копирует содержимое, если нет - открывает и передает потоки следующему этапу в <see cref="InOutStreams"/>.</remarks>
/// <param name="nameTemplate">Шаблон имени нового файла</param>
internal class FileCopy(string nameTemplate, Action<object>? fx) : IStage, IFinalStage
{
	/// <summary>
	/// Создать этап копирования. Параметр конфигурации - шаблон имени нового файла, может содержать:
	/// <list type="bullet">
	/// <item><c>{path}</c> - подстановка исходного пути с именем и расширением файла</item>
	/// <item><c>{file}</c> - подстановка исходного имени с расширением файла</item>
	/// </list>
	/// </summary>
	public class Builder : IStageBuilder
	{
		/// <inheritdoc cref="Builder"/>
		/// <param name="cfg">Строка - шаблон имени нового файла</param>
		/// <param name="dst">Создаваемый конвейер</param>
		public void Build(JsonElement cfg, PipelineBuilder dst)
		{
			var nameTemplate    = default(string);
			var nestedBuilder   = default(PipelineBuilder);

			foreach (var prop in cfg.EnumerateObject())
			{
				var propName    = prop.Name;

				if (propName=="@dst")
				{
					nameTemplate= prop.Value.GetString();
				} else
				{
					var builder = dst.TryGetBuilder(propName)
								?? throw new Exception($"Builder {propName} not found");

					nestedBuilder       ??= new PipelineBuilder(typeof(InOutStreams), dst.TryGetBuilder);
					builder.Build(prop.Value, nestedBuilder);
				}
			}

			if (string.IsNullOrEmpty(nameTemplate)) throw new Exception("Destination file name not specified");

			dst.Add(Build(nameTemplate, nestedBuilder?.Build()));
		}

		protected virtual FileCopy Build(string nameTemplate, Action<object>? fx)
		{
			return new FileCopy(nameTemplate, fx);
		}
	}

	/// <inheritdoc/>
	public virtual Type ParamType		=> typeof(string);

	/// <inheritdoc/>
	public virtual Type ResultType		=> typeof(string);

	/// <remarks>Открывает потоки и передает следующему этапу</remarks>
	/// <param name="param">Строка - имя исходного файла</param>
	/// <inheritdoc/>
	public virtual void Run(object param, Action<object> next)
	{
		var srcName             = (string)param;
		var dstName             = DstName(srcName);
		if (CopyFile(srcName, dstName)) next(dstName);
	}

	protected string DstName(string srcName)
	{
		return nameTemplate.Replace("{path}", srcName)
						   .Replace("{path-no-ext}", _pathNoExt(srcName))
						   .Replace("{file}", Path.GetFileName(srcName))
						   .Replace("{file-no-ext}", Path.GetFileNameWithoutExtension(srcName));
	}

	protected bool CopyFile(string srcName, string dstName)
	{
		if (dstName==srcName) throw new Exception($"Cant't replace file {srcName}, diffirent file name required");

		if (string.IsNullOrEmpty(dstName) || _maySkip(srcName, dstName))
		{
			Console.WriteLine($"⏭️ {srcName} SKIPPED");
			return false;
		}
		using var input         = File.OpenRead(srcName);
		using var output		= File.Create(dstName);
		if (fx!=null)
		{
			fx(new InOutStreams
			{
				In              = input,
				InName          = input.Name,
				Out             = output,
				OutName         = output.Name
			});
		} else
		{
			input.CopyTo(output);
		}

		output.Close();
		input.Close();
		return true;
	}

	/// <remarks>Открывает потоки и копирует содержимое без изменений</remarks>
	/// <param name="param">Строка - имя исходного файла</param>
	/// <inheritdoc/>
	public virtual void Finalize(object param)
	{
		var srcName             = (string)param;
		var dstName             = DstName(srcName);
		CopyFile(srcName, dstName);
	}

	private static string _pathNoExt(string srcName)
	{
		var ext					= Path.GetExtension(srcName);
		return string.IsNullOrEmpty(ext) ? srcName : srcName[..^ext.Length];
	}

	/// <summary>
	/// Проверить наличие нового файла и сравнить даты изменения файлов
	/// </summary>
	/// <param name="srcName"></param>
	/// <param name="dstName"></param>
	/// <returns></returns>
	private static bool _maySkip(string srcName, string dstName)
	{
		if (!File.Exists(dstName)) return false;

		var srcMod          	= File.GetLastWriteTimeUtc(srcName);
		var dstMod          	= File.GetLastWriteTimeUtc(dstName);

		return srcMod<dstMod;
	}
}
