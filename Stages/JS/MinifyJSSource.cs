using System.Text.Json;
using System.Xml.Linq;

using Dm.Web.BuildStatic.Services;

using Dm.Web.BuildStatic.Stages.Core;

using NUglify;

using NUglify.JavaScript;

namespace Dm.Web.BuildStatic.Stages.JS
{
	internal class MinifyJSSource(JsonElement cfg, StageProvider builders) : IStage
	{
		private readonly CodeSettings _opt  = new(){ OutputMode = OutputMode.MultipleLines, Indent = "" };

		/// <inheritdoc/>
		public Type ParamType	=> typeof(string);

		/// <inheritdoc/>
		public Type ResultType	=> typeof(string);

		public void Run(object param, Action<object> next)
		{
			var js              = (string)param;
			var minJS           = Uglify.Js(js, _opt);
			if (minJS.HasErrors)
				throw new Exception($"❌ Minify js FAIL:\n\t{string.Join(";\n\t", minJS.Errors)}");

			next(minJS.Code);
		}
	}
}
