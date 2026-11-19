using System.Security.Cryptography;
using System.Text.Json;

using Dm.Web.BuildStatic.Services;
using Dm.Web.BuildStatic.Stages.Core;

namespace Dm.Web.BuildStatic.Stages.Files;

/// <summary>
/// Получить коллекцию файлов из указанной директории.
/// </summary>
/// <remarks>Если этап не первый, то он дополнит коллекцию с предыдущего этапа.</remarks>
internal class FilesSource : IStartStage, IStage
{
	private readonly string _directory;
	private readonly string _pattern;
	private readonly bool _recursive;

	/// <summary>
	/// Создать этап. Параметр конфигурации - относительный/абсолютный путь к директории с файлами + паттерн фильтрации файлов. Если перет паттерном содержит сегмент <c>**</c>, то будут просмотрены все вложенные папки.
	/// </summary>
	public FilesSource(JsonElement cfg, StageProvider builders)
	{
		var segments			= cfg.GetString()?.Split(Path.PathSeparator, Path.AltDirectorySeparatorChar);
		if (segments==null || segments.Length==0) throw new ArgumentException("Empty path+pattern for FilesSource");

		_pattern				= segments[segments.Length-1];
		_recursive				= 2<=segments.Length && segments[segments.Length-2]=="**";
		_directory				= string.Join(Path.DirectorySeparatorChar, segments, 0, segments.Length-(_recursive ? 2 : 1));
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
		return Directory.Exists(_directory)
			 ? Directory.EnumerateFiles(_directory, _pattern!, new EnumerationOptions { RecurseSubdirectories = _recursive })
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
