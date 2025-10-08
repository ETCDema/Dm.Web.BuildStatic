using Dm.Web.BuildStatic.Services.Core;

namespace Dm.Web.BuildStatic.Services
{
	/// <summary>
	/// Конвейер подготовки статических ресурсов
	/// </summary>
	internal class Pipeline
	{
		private IStartStage _start				= default!;
		private readonly List<IStage> _stages	= [];
		private IFinalStage? _final;
		private Type? _resultType;

		/// <summary>
		/// Добавить этап, в конвейере должен быть один начальный и один конечный этап а так же несколько промежуточных этапов.
		/// </summary>
		/// <param name="stage"></param>
		/// <exception cref="Exception"></exception>
		public void Add(IPipelineStage stage)
		{
			if (_start==null)
			{
				if (stage is not IStartStage start) throw new Exception("Start stage is empty - add start pipeline stage first");

				_start          = start;
				_resultType		= start.ResultType;
			} else if (stage is IFinalStage final)
			{
				_tryFinalAsStage();
				if (_resultType==null) throw new Exception("Pipeline finalized");
				if (!final.ParamType.IsAssignableFrom(_resultType)) throw new Exception($"Prev result type {_resultType.FullName} can't be assign to parameter type {final.ParamType.FullName}");

				_final			= final;
				_resultType     = null;
			} else if (stage is IStage middle)
			{
				_tryFinalAsStage();
				if (_resultType==null) throw new Exception("Pipeline finalized");
				if (!middle.ParamType.IsAssignableFrom(_resultType)) throw new Exception($"Prev result type {_resultType.FullName} can't be assign to parameter type {middle.ParamType.FullName}");

				_stages.Add(middle);
				_resultType		= middle.ResultType;
			} else if (stage is IStartStage)
			{
				throw new Exception("Pipeline has start stage");
			} else
			{
				throw new Exception("Stage mast be IStartStage/IStage/IFinalStage");
			}
		}

		private void _tryFinalAsStage()
		{
			if (_final==null) return;
			if (_final is not IStage stage) return;

			_stages.Add(stage);
			_resultType         = stage.ResultType;
			_final              = null!;
		}


		/// <summary>
		/// Выполнить этапы конвейера
		/// </summary>
		/// <exception cref="Exception"></exception>
		public void Run()
		{
			var start			= _start
								?? this as IStartStage 
								?? throw new Exception("No start stage at pipeline");

			// Строим методы вызовов следующих этапов с конца
			var last            = _stages.Count-1;
			Action<object> next	= _final!=null						 ? _final.Finalize
								: last<0 && start is IFinalStage fin ? fin.Finalize
								: last<0 && this  is IFinalStage plf ? plf.Finalize
								: throw new Exception("No final stage at pipeline");
			
			// Промежуточные этапы
			for (int i = last; 0<=i; i--)
			{
				next            = _buildNextFx(_stages[i], next);
			}

			// Запускаем с начального этапа
			start.Run(next);
		}

		private static Action<object> _buildNextFx(IStage stage, Action<object> next)
		{
			return (p) => stage.Run(p, next);
		}
	}
}
