using System.Text.Json;

namespace Dm.Web.BuildStatic.Services.Core;

/// <summary>
/// Генератор этапов
/// </summary>
internal interface IStageBuilder
{
	/// <summary>
	/// Создать этап с использованием переданных настроек и добавить в конвейер
	/// </summary>
	/// <param name="cfg">Настройки этапа</param>
	/// <param name="dst">Создаваемый конвейер</param>
	void Build(JsonElement cfg, Pipeline dst);
}
