using System.Text.Json;

using Dm.Web.BuildStatic.Models;
using Dm.Web.BuildStatic.Services.Core;

using NUglify;
using NUglify.JavaScript;

namespace Dm.Web.BuildStatic.Services.Stages;

/// <summary>
/// Минимизация JS
/// </summary>
/// <remarks>
/// Создать этап.
/// </remarks>
/// <param name="cfg">Игнорируется.</param>
/// <param name="builders">Игнорируется.</param>
#pragma warning disable CS9113 // Параметр не прочитан.
internal class MinifyJS(JsonElement cfg, StageProvider builders) : IFinalStage, IStage
#pragma warning restore CS9113 // Параметр не прочитан.
{
	private readonly CodeSettings _opt	= new(){ OutputMode = OutputMode.MultipleLines, Indent = "" };

	/// <inheritdoc/>
	public Type ParamType		=> typeof(InOutStreams);

	/// <inheritdoc/>
	public Type ResultType		=> typeof(InOutStreams);

	/// <param name="param">Потоки для чтения/записи <see cref="InOutStreams"/></param>
	/// <inheritdoc/>
	public void Run(object param, Action<object> next)
	{
		var streams             = (InOutStreams)param;

		if (_isMinified(streams.InName))
		{
			next(param);
			Console.WriteLine($"⏭️ {streams.InName} already minified");
		} else
		{
			using var dst       = new MemoryStream();
			_minify(streams.InName, streams.In, dst);
			dst.Position        = 0;
			next(new InOutStreams
			{
				In              = dst,
				InName          = streams.InName!,
				Out             = streams.Out,
				OutName         = streams.OutName
			});
			Console.WriteLine($"✅ {streams.InName} minified");
		}
	}

	/// <param name="param">Потоки для чтения/записи <see cref="InOutStreams"/></param>
	/// <inheritdoc/>
	void IFinalStage.Finalize(object param)
	{
		var streams             = (InOutStreams)param;
		if (_isMinified(streams.InName))
		{
			streams.In.CopyTo(streams.Out);
			Console.WriteLine($"⏭️ {streams.InName} already minified -> {streams.OutName}");
		} else
		{
			_minify(streams.InName, streams.In, streams.Out);
			Console.WriteLine($"✅ {streams.InName} minified -> {streams.OutName}");
		}
	}

	private static bool _isMinified(string name)
	{
		if (string.IsNullOrEmpty(name)) return false;

		var last                = name.LastIndexOf('.');
		var minExt              = 5<last ? string.Concat(".min", name.AsSpan(last)) : null;
		return minExt!=null && name.EndsWith(minExt, StringComparison.OrdinalIgnoreCase);
	}

	private void _minify(string inName, Stream src, Stream dst)
	{
		using var reader		= new StreamReader(src);
		var js					= reader.ReadToEnd();
		var minJS               = Uglify.Js(js, _opt);

		if (minJS.HasErrors)
			throw new Exception($"❌ Minify {inName} FAIL:\n\t{string.Join(";\n\t", minJS.Errors)}");

		using var writer		= new StreamWriter(dst, leaveOpen: true);
		writer.Write(minJS);
		writer.Flush();
	}
}
