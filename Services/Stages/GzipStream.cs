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
	internal class GzipStream: IFinalStage, IStage
	{
		private readonly CompressionLevel _level;

		/// <summary>
		/// Создать этап. Параметр конфигурации - уровень сжатия <see cref="CompressionLevel"/>.
		/// </summary>
		/// <param name="cfg">Строка - уровень сжатия, возможные значения см. тут <see cref="CompressionLevel"/>. Если не указан, то используется <see cref="CompressionLevel.Optimal"/>.</param>
		/// <param name="dst">Создаваемый конвейер</param>
		public GzipStream(JsonElement cfg, StageProvider builders)
		{
			var level			= cfg.GetString();
			_level				= string.IsNullOrEmpty(level) ? CompressionLevel.Optimal : Enum.Parse<CompressionLevel>(level);
		}

		/// <inheritdoc/>
		public Type ParamType	=> typeof(InOutStreams);

		/// <param name="param">Потоки для чтения/записи <see cref="InOutStreams"/></param>
		/// <inheritdoc/>
		public void Finalize(object param)
		{
			var streams			= (InOutStreams)param;
			using var gzip		= new GZipStream(streams.Out, _level);
			streams.In.CopyTo(gzip);

			Console.WriteLine($"✅ Gzip {streams.InName} -> {streams.OutName}");
		}

		/// <inheritdoc/>
		Type IStage.ResultType	=> ParamType;

		/// <param name="param">Потоки для чтения/записи <see cref="InOutStreams"/></param>
		/// <inheritdoc/>
		void IStage.Run(object param, Action<object> next)
		{
			var streams         = (InOutStreams)param;
			using var gzip      = new GZipStream(streams.Out, _level);

			Console.WriteLine($"✅ Out {streams.OutName} is GZIP");
			next(new InOutStreams
			{
				In				= streams.In,
				InName			= streams.InName,
				Out				= gzip,
				OutName			= streams.OutName,
			});
		}
	}
}
