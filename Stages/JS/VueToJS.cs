using System.Text.Encodings.Web;
using System.Text.Json;

using Dm.Web.BuildStatic.Models;
using Dm.Web.BuildStatic.Services;
using Dm.Web.BuildStatic.Stages.Core;

namespace Dm.Web.BuildStatic.Stages.JS
{
	internal class VueToJS : IFinalStage
	{
		private readonly IStage? _js;

		/// <inheritdoc/>
		public Type ParamType	=> typeof(InOutStreams);

		public VueToJS(JsonElement cfg, StageProvider builders)
		{
			foreach (var prop in cfg.EnumerateObject())
			{
				if (prop.Name=="js")
				{
					_js         = (IStage)builders.BuildStage(prop.Value.GetString()!, new JsonElement());
					if (!_js.ParamType.IsAssignableFrom(typeof(string)) || !typeof(string).IsAssignableFrom(_js.ResultType))
						throw new NotSupportedException($"JS stage {_js.GetType().FullName} get {_js.ParamType.FullName} and return {_js.ResultType.FullName}, but required string and string");
				}
			}
		}

		/// <param name="param">Потоки для чтения/записи <see cref="InOutStreams"/></param>
		/// <inheritdoc/>
		public void Finalize(object param)
		{
			var streams             = (InOutStreams)param;
			_buildJS(streams.In, streams.Out);
			Console.WriteLine($"✅ JS module with {streams.InName} created -> {streams.OutName}");
		}

		private void _buildJS(Stream src, Stream dst)
		{
			using var reader    = new StreamReader(src);
			using var html		= new HTMLSource(reader);

			var vue				= _buildVue(html);

			using var writer    = new StreamWriter(dst, leaveOpen: true);
			_writeJS(vue, writer);
			writer.Flush();
		}

		private _vue _buildVue(HTMLSource html)
		{
			var vue             = new _vue();

			while (html.GetNext())
			{
				if (html.Token==HTMLToken.Tag)
				{
					if (html.TagName=="template" || html.TagName=="script" && _isTemplateType(html.TryGetAttribyte("type")))
					{
						var id  = html.TryGetAttribyte("id");
						if (string.IsNullOrEmpty(id)) throw new FormatException($"No id attribute for template at {html.GetInfo()}");
						if (vue.Templates.ContainsKey(id)) throw new FormatException($"Template {id} duplicate at {html.GetInfo()}");

						vue.Templates.Add(id, html.GetContent());
					} else if (html.TagName=="style")
					{
						vue.Styles.Add(html.GetContent());
					} else if (html.TagName=="script" && _isJSType(html.TryGetAttribyte("type")))
					{
						if (vue.JS!=null) throw new FormatException($"Code duplicate at {html.GetInfo()}");
						vue.JS  = html.GetContent();
					}
				}
			}

			if (vue.JS!=null && _js!=null)
			{
				_js.Run(vue.JS, result => { vue.JS = (string)result; });
			}

			if (vue.IsEmpty()) throw new Exception("Vue is empty");

			return vue;
		}

		private bool _isTemplateType(string? type)
		{
			return type=="text/html" || type=="text/plain";
		}

		private bool _isJSType(string? type)
		{
			return type=="text/javascript" || type==null;
		}

		private static void _writeJS(_vue vue, StreamWriter writer)
		{
			var hasT            = 0<vue.Templates.Count;
			var hasS            = 0<vue.Styles.Count;
			if (hasT || hasS)
			{
				if (hasT) writer.WriteLine("function _t(id, src) { const tag = document.createElement('script'); tag.id=id; tag.type='text/plain'; tag.text=src; return tag; }");
				if (hasS) writer.WriteLine("function _s(src) { const tag = document.createElement('style'); tag.innerText=src; return tag; }");

				writer.WriteLine("document.head.append(");
				foreach (var template in vue.Templates)
				{
					writer.WriteLine($"  _t(\"{JsonEncodedText.Encode(template.Key, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}\", \"{JsonEncodedText.Encode(template.Value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}\"),");
				}
				foreach (var style in vue.Styles)
				{
					writer.WriteLine($"  _s(\"{JsonEncodedText.Encode(style, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}\"),");
				}
				writer.WriteLine(");");
			}
			writer.WriteLine(vue.JS);
		}

		private class _vue
		{
			public readonly Dictionary<string, string> Templates = [];

			public readonly List<string> Styles = [];

			public string? JS;

			internal bool IsEmpty()
			{
				return string.IsNullOrWhiteSpace(JS) && Templates.Count==0 && Styles.Count==0;
			}
		}
	}
}
