using System.Text.Json;

using Dm.Web.BuildStatic.Services.Core;

namespace Dm.Web.BuildStatic.Services.Stages;

/// <summary>
/// Получить коллекцию файлов из указанной директории.
/// </summary>
/// <remarks>Если этап не первый, то он дополнит коллекцию с предыдущего этапа.</remarks>
internal class FilesSource(string directory, string pattern, bool recursive) : IStartStage, IStage
{
	/// <summary>
	/// Создать этап. Параметр конфигурации - относительный/абсолютный путь к директории с файлами + паттерн фильтрации файлов. Если перет паттерном содержит сегмент <c>**</c>, то будут просмотрены все вложенные папки.
	/// </summary>
	public class Builder : IStageBuilder
	{
		/// <inheritdoc cref="Builder"/>
		/// <param name="cfg">Строка - относительный/абсолютный путь к директории с файлами + паттерн фильтрации файлов</param>
		/// <param name="dst">Создаваемый конвейер</param>
		public void Build(JsonElement cfg, Pipeline dst)
		{
			var segments		= cfg.GetString()?.Split(Path.PathSeparator, Path.AltDirectorySeparatorChar);
			if (segments==null || segments.Length==0) throw new ArgumentException("Empty path+pattern for FilesSource");

			var pattern         = segments[segments.Length-1];
			var recursive       = 2<=segments.Length && segments[segments.Length-2]=="**";
			var directory		= string.Join(Path.DirectorySeparatorChar, segments, 0, segments.Length-(recursive ? 2 : 1));

			dst.Add(new FilesSource(directory, pattern, recursive));
		}
	}

	/// <inheritdoc/>
	public Type ResultType		=> typeof(IEnumerable<string>);

	/// <inheritdoc/>
	public void Run(Action<object> next)
	{
		next(_getFiles());
	}

	private IEnumerable<string> _getFiles()
	{
		return Directory.Exists(directory)
			 ? Directory.EnumerateFiles(directory, pattern, new EnumerationOptions { RecurseSubdirectories = recursive })
			 : [];
	}

	/// <inheritdoc/>
	Type IStage.ParamType		=> ResultType;

	/// <param name="param">Коллекция строк <see cref="IEnumerable{string}"/> с предыдущего этапа</param>
	/// <inheritdoc/>
	void IStage.Run(object param, Action<object> next)
	{
		var files               = ((IEnumerable<string>)param).Union(_getFiles());
		next(files);
	}
}
