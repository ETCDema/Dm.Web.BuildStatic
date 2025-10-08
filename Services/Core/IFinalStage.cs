namespace Dm.Web.BuildStatic.Services.Core;

/// <summary>
/// Финальный этап конвейера
/// </summary>
internal interface IFinalStage: IPipelineStage
{
	/// <summary>Тип параметра - результата предыдущего этапа</summary>
	Type ParamType				{ get; }

	/// <summary>
	/// Выполнить действия этапа
	/// </summary>
	/// <param name="param">Результат предыдущего этапа</param>
	void Finalize(object param);
}
