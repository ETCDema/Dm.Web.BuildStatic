using System.Text.Json;

using Dm.Web.BuildStatic.Models;
using Dm.Web.BuildStatic.Services.Core;

using NUglify;
using NUglify.JavaScript;

namespace Dm.Web.BuildStatic.Services.Stages;

/// <summary>
/// Минимизация JS
/// </summary>
internal class MinifyJS : IFinalStage, IStage
{
	public class Builder : IStageBuilder
	{
		/// <summary>
		/// Создать этап.
		/// </summary>
		/// <param name="cfg">Игнорируется.</param>
		/// <param name="dst">Создаваемый конвейер</param>
		public void Build(JsonElement cfg, Pipeline dst)
		{
			dst.Add(new MinifyJS());
		}
	}

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
		} else
		{
			using var dst       = new MemoryStream();
			_minify(streams.In, dst);
			dst.Position        = 0;
			next(new InOutStreams
			{
				In              = dst,
				InName          = streams.InName!,
				Out             = streams.Out,
				OutName         = streams.OutName
			});
		}
	}

	/// <param name="param">Потоки для чтения/записи <see cref="InOutStreams"/></param>
	/// <inheritdoc/>
	void IFinalStage.Finalize(object param)
	{
		var streams             = (InOutStreams)param;
		if (_isMinified(streams.InName))
			streams.In.CopyTo(streams.Out);
		else
			_minify(streams.In, streams.Out);
	}

	private static bool _isMinified(string name)
	{
		return name!=null && name.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase);
	}

	private void _minify(Stream src, Stream dst)
	{
		using var reader		= new StreamReader(src);
		var js					= reader.ReadToEnd();
		var minJS               = Uglify.Js(js, _opt);

		using var writer		= new StreamWriter(dst, leaveOpen: true);
		writer.Write(minJS);
		writer.Flush();
	}
}
