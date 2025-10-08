using System.IO.Compression;
using System.Text.Json;

using Dm.Web.BuildStatic.Models;
using Dm.Web.BuildStatic.Services.Core;

namespace Dm.Web.BuildStatic.Services.Stages
{
	/// <summary>
	/// Сжать данные из потока для чтения в поток для записи
	/// </summary>
	/// <param name="level">Уровень сжатия</param>
	internal class GzipStream(CompressionLevel level): IFinalStage, IStage
	{
		public class Builder : IStageBuilder
		{
			/// <summary>
			/// Создать этап. Параметр конфигурации - уровень сжатия <see cref="CompressionLevel"/>.
			/// </summary>
			/// <param name="cfg">Строка - уровень сжатия, возможные значения см. тут <see cref="CompressionLevel"/>. Если не указан, то используется <see cref="CompressionLevel.Optimal"/>.</param>
			/// <param name="dst">Создаваемый конвейер</param>
			public void Build(JsonElement cfg, Pipeline dst)
			{
				var level       = cfg.GetString();
				dst.Add(new GzipStream(string.IsNullOrEmpty(level) ? CompressionLevel.Optimal : Enum.Parse<CompressionLevel>(level)));
			}
		}

		/// <inheritdoc/>
		public Type ParamType	=> typeof(InOutStreams);

		/// <param name="param">Потоки для чтения/записи <see cref="InOutStreams"/></param>
		/// <inheritdoc/>
		public void Finalize(object param)
		{
			var streams			= (InOutStreams)param;
			using var gzip		= new GZipStream(streams.Out, level);
			streams.In.CopyTo(gzip);

			Console.WriteLine($"✅ {streams.InName} -> {streams.OutName}");
		}

		/// <inheritdoc/>
		Type IStage.ResultType	=> ParamType;

		/// <param name="param">Потоки для чтения/записи <see cref="InOutStreams"/></param>
		/// <inheritdoc/>
		void IStage.Run(object param, Action<object> next)
		{
			Finalize(param);
			next(param);
		}
	}
}
