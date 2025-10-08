namespace Dm.Web.BuildStatic.Services.Core;

/// <summary>
/// Начальный этап конвейера
/// </summary>
internal interface IStartStage: IPipelineStage
{
	/// <summary>Тип возвращаемого результата</summary>
	Type ResultType				{ get; }

	/// <summary>
	/// Выполнить действия этапа
	/// </summary>
	/// <param name="next">Метод выполнения следующего этапа конвейера</param>
	void Run(Action<object> next);
}
