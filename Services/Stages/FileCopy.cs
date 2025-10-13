using System.Text.Json;

using Dm.Web.BuildStatic.Models;
using Dm.Web.BuildStatic.Services.Core;

namespace Dm.Web.BuildStatic.Services.Stages;

/// <summary>
/// Копирование из существующего файла в новый. Если новый файл существует и исходный файл старше нового копирование выполняться не будет.
/// </summary>
/// <remarks>Если этап последний, то он просто копирует содержимое, если нет - открывает и передает потоки следующему этапу в <see cref="InOutStreams"/>.</remarks>
/// <param name="dstNameFx">Генератор имени нового файла</param>
internal class FileCopy(Func<string, string> dstNameFx) : IStage, IFinalStage
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
		public void Build(JsonElement cfg, Pipeline dst)
		{
			var ext				= cfg.GetString() ?? throw new Exception("Destination file name not specified");
			string nameFx(string srcName)
			{
				return ext.Replace("{path}",		srcName)
						  .Replace("{path-no-ext}", _pathNoExt(srcName))
						  .Replace("{file}",		Path.GetFileName(srcName))
						  .Replace("{file-no-ext}", Path.GetFileNameWithoutExtension(srcName));
			}

			dst.Add(new FileCopy(nameFx));
		}

		private string _pathNoExt(string srcName)
		{
			var ext				= Path.GetExtension(srcName);
			return string.IsNullOrEmpty(ext) ? srcName : srcName.Substring(0, srcName.Length-ext.Length);
		}
	}

	/// <inheritdoc/>
	public Type ParamType	=> typeof(string);

	/// <inheritdoc/>
	public Type ResultType	=> typeof(InOutStreams);

	/// <remarks>Открывает потоки и передает следующему этапу</remarks>
	/// <param name="param">Строка - имя исходного файла</param>
	/// <inheritdoc/>
	public void Run(object param, Action<object> next)
	{
		var srcName				= (string)param;
		var dstName				= dstNameFx(srcName);

		if (dstName==srcName) throw new Exception($"Cant't replace file {srcName}, diffirent file name required");

		if (string.IsNullOrEmpty(dstName) || _maySkip(srcName, dstName))
		{
			Console.WriteLine($"⏭️ {srcName} SKIPPED");
			return;
		}

		using var input			= File.OpenRead(srcName);
		using var output		= File.Create(dstName);
		next(new InOutStreams
		{
			In					= input,
			InName				= input.Name,
			Out					= output,
			OutName				= output.Name
		});
	}

	/// <remarks>Открывает потоки и копирует содержимое без изменений</remarks>
	/// <param name="param">Строка - имя исходного файла</param>
	/// <inheritdoc/>
	public void Finalize(object param)
	{
		Run(param, _copyStream);
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

	private void _copyStream(object param)
	{
		var streams         = (InOutStreams)param;
		streams.In.CopyTo(streams.Out);
	}
}
