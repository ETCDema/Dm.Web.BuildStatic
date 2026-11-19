using System.Text.Encodings.Web;
using System.Text.Json;

using Dm.Web.BuildStatic.Models;
using Dm.Web.BuildStatic.Services;
using Dm.Web.BuildStatic.Stages.Core;

namespace Dm.Web.BuildStatic.Stages.SVG;

internal class SVGtoJS: IStage, IFinalStage
{
	/// <summary>
	/// Создать этап импорта SVG в document.head в виде js module.
	/// </summary>
	public SVGtoJS(JsonElement cfg, StageProvider builders)
	{
	}

	/// <inheritdoc/>
	public Type ParamType		=> typeof(InOutStreams);

	/// <inheritdoc/>
	public Type ResultType		=> typeof(InOutStreams);

	/// <param name="param">Замена потока <see cref="InOutStreams.In"/> с SVG на поток JS</param>
	/// <inheritdoc/>
	public void Run(object param, Action<object> next)
	{
		var src                 = (InOutStreams)param;
		
		using var dst			= new MemoryStream();
		_buildJS(src.In, dst);
		dst.Position			= 0;
		Console.WriteLine($"✅ JS module with {src.InName} created");

		next(new InOutStreams
		{
			In					= dst,
			InName				= src.InName+".mjs",
			Out					= src.Out,
			OutName				= src.OutName
		});
	}

	public void Finalize(object param)
	{
		var streams             = (InOutStreams)param;
		_buildJS(streams.In, streams.Out);
		Console.WriteLine($"✅ JS module with {streams.InName} created -> {streams.OutName}");
	}

	private void _buildJS(Stream src, Stream dst)
	{
		using var reader        = new StreamReader(src);
		var svg                 = reader.ReadToEnd();

		using var writer        = new StreamWriter(dst, leaveOpen: true);
		writer.Write("((svg) => { const el = document.createElement(\"section\"); el.innerHTML=svg; document.head.append(...el.childNodes);})(\"");
		writer.Write(JsonEncodedText.Encode(svg, JavaScriptEncoder.UnsafeRelaxedJsonEscaping));
		writer.Write("\")");
		writer.Flush();
	}
}
