#if !NET9_0_OR_GREATER
// Polyfill: Activity.AddException(Exception) is a .NET 9+ API. On netstandard2.0 AND net8 it does not exist,
// so we provide an extension that records the exception as a standard OpenTelemetry "exception" activity event.
// Public because downstream projects in this repo (e.g. FoundationDB.Client) also call activity.AddException(...).
namespace System.Diagnostics
{

	/// <summary>Netstandard2.0 / net8 stand-in for the .NET 9+ <c>Activity.AddException</c> method.</summary>
	public static class ActivityExceptionCompat
	{

		/// <summary>Records <paramref name="exception"/> on the activity as an OTel "exception" event.</summary>
		public static Activity AddException(this Activity activity, Exception exception)
		{
			var tags = new ActivityTagsCollection
			{
				{ "exception.type", exception.GetType().ToString() },
				{ "exception.message", exception.Message },
				{ "exception.stacktrace", exception.ToString() },
			};
			activity.AddEvent(new ActivityEvent("exception", tags: tags));
			return activity;
		}

	}

}
#endif
