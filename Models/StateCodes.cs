namespace Dm.Web.BuildStatic.Models
{
	internal enum StateCodes: int
	{
		Abandoned				= -2,

		Running					= -1,

		Ok						= 0,

		NoConfig				= 1,

		MeFail					= 2,

		OtherFail				= 3,

		SharingViolation		= 4,
	}
}
