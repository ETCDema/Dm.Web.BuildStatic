using System.Text.Json;
using System.Xml;

using Dm.Web.BuildStatic.Models;
using Dm.Web.BuildStatic.Services.Core;

namespace Dm.Web.BuildStatic.Services.Stages;

/// <summary>
/// Упаковка SVG файлов в один SVG Sprite файл.
/// </summary>
internal class SVGSprite(string dstName) : IFinalStage, IStage
{
	public class Builder : IStageBuilder
	{
		/// <summary>
		/// Создать этап.
		/// </summary>
		/// <param name="cfg">Имя SVG Sprite файла.</param>
		/// <param name="dst">Создаваемый конвейер</param>
		public void Build(JsonElement cfg, Pipeline dst)
		{
			dst.Add(new SVGSprite(cfg.GetString()!));
		}
	}

	/// <inheritdoc/>
	public Type ParamType 		=> typeof(IEnumerable<string>);

	/// <inheritdoc/>
	public Type ResultType 		=> typeof(InOutStreams);

	/// <param name="param">Коллекция исходных SVG файлов <see cref="IEnumerable{string}"/></param>
	/// <inheritdoc/>
	public void Run(object param, Action<object> next)
	{
		var src 				= new List<string>((IEnumerable<string>)param);
		if (_maySkip(src)) return;

		using var input 		= new MemoryStream();
		using var output 		= File.Create(dstName);
		_buildSVG(src, input);
		input.Position 			= 0;
		next(new InOutStreams
		{
			In 					= input,
			InName 				= $"Symbols[{src.Count}] -> {Path.GetFileNameWithoutExtension(output.Name)}",
			Out 				= output,
			OutName 			= output.Name
		});
	}

	/// <param name="param">Потоки для чтения/записи <see cref="InOutStreams"/></param>
	/// <inheritdoc/>
	void IFinalStage.Finalize(object param)
	{
		var src 				= new List<string>((IEnumerable<string>)param);
		if (_maySkip(src)) return;

		using var output 		= File.Create(dstName);
		_buildSVG(src, output);
		Console.WriteLine($"✅ Symbols[{src.Count}] -> {output.Name}");
	}

	private bool _maySkip(List<string> src)
	{
		if (src.Count == 0)
		{
			Console.WriteLine($"⚠️ The collection of SVG symbol files is empty - cannot build SVG sprite {dstName}");
			return true;
		}

		if (!File.Exists(dstName)) return false;

		var dstMod 				= File.GetLastWriteTimeUtc(dstName);
		foreach (var svg in src)
		{
			var srcMod 			= File.GetLastWriteTimeUtc(svg);
			if (dstMod < srcMod) return false;
		}

		Console.WriteLine($"⏭️ Symbols[{src.Count}] -> {dstName} SKIPPED");

		return true;
	}

	private void _buildSVG(List<string> src, Stream output)
	{
		var sprite				= new XmlDocument();
		var defs				= sprite.CreateElement("defs", "http://www.w3.org/2000/svg");
		var svg 				= sprite.CreateElement("svg", "http://www.w3.org/2000/svg");

		sprite.AppendChild(svg);
		svg.AppendChild(defs);

		foreach (var symbol in src)
		{
			_addSymbol(symbol, defs);
		}

		sprite.Save(output);
	}

	private void _addSymbol(string symbol, XmlElement defs)
	{
		var src					= new XmlDocument();
		src.Load(symbol);
		var svg 				= src.DocumentElement;
		
		if (svg==null || svg.Name!="svg") throw new Exception($"File {symbol} is not SVG file");

		var doc					= defs.OwnerDocument;
		var sprite 				= doc.CreateElement("symbol", "http://www.w3.org/2000/svg");

		sprite.SetAttribute("id", Path.GetFileNameWithoutExtension(symbol));
		sprite.SetAttribute("viewBox", svg.GetAttribute("viewBox"));

		var srcSvg				= doc.ImportNode(svg, true);
		foreach (XmlNode node in srcSvg.ChildNodes)
		{
			sprite.AppendChild(node);
		}

		defs.AppendChild(sprite);
	}
}